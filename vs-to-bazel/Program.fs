open System
open System.IO
open System.Text.RegularExpressions
open System.Xml
open VsToBazel
open VsToBazel.VisualStudio

let private unixify (x : string) =
  x.Replace("\\", "/")

let task path = async {

  let! sln = Files.read path

  let slnVersion =
    (
      Regex.Match
        (sln, "[\r\n]?Microsoft Visual Studio Solution File, Format Version ([0-9.]+)", RegexOptions.Multiline)
    ).Groups.[1].Value

  let projectsRegex =
    Regex
      (
        "Project\\(\"(?<ProjectHostGuid>{[A-F0-9-]+})\"\\) = \"(?<ProjectName>.*?)\", \"(?<RelativePath>.*?)\", \"(?<ProjectGuid>{[A-F0-9-]+})\"[\r\n]*(?<dependencies>.*?)EndProject[\r\n]+",
        RegexOptions.Singleline
      )

  let projects =
    seq {
      let mutable m = projectsRegex.Match sln

      while m.Success do
        let projectHostGuid = Guid.Parse (m.Groups.[1].Value)
        let projectName = m.Groups.[2].Value
        let relativePath = m.Groups.[3].Value
        let projectGuid = Guid.Parse (m.Groups.[4].Value)

        yield
          {
            ProjectHostGuid = projectHostGuid
            ProjectName = projectName
            RelativePath = relativePath
            ProjectGuid = projectGuid
          }

        m <- m.NextMatch()
    }
    |> Seq.toList

  let solution =
    {
      SlnVersion = slnVersion
      Projects = projects
    }

  printfn "%O" solution

  for project in solution.Projects do
    let path = unixify project.RelativePath

    printfn "%s" ("Processing " + path)

    let! xml = Files.read path

    // The process should work regardless of the namespace
    let xml = xml.Replace (" xmlns=\"", " notxmlns=\"")

    let doc = XmlDocument ()

    doc.LoadXml xml

    let projectName =
      try
        (doc.SelectSingleNode "/Project/PropertyGroup/ProjectName").InnerText
      with _ ->
        Path.GetFileNameWithoutExtension path

    let propertyGroupNodes =
      doc.SelectNodes "/Project/PropertyGroup"
      |> Seq.cast<XmlNode>

    let configurationGroups =
      propertyGroupNodes
      |> Seq.filter (fun node ->
        let label = node.Attributes.["Label"]
        isNull label |> not && label.InnerText = "Configuration"
      )
      |> Seq.map (fun node ->
        let configurationType =
          (node.SelectSingleNode "./ConfigurationType").InnerText

        {
          ConfigurationType = configurationType
        }
      )
      |> Seq.toList

    let itemGroupNodes =
      doc.SelectNodes "/Project/ItemGroup"
      |> Seq.cast<XmlNode>

    let itemGroups =
      itemGroupNodes
      |> Seq.map (fun node ->
        let includes =
          node.SelectNodes "./ClInclude/@Include"
          |> Seq.cast<XmlNode>
          |> Seq.map (fun node -> node.InnerText)
          |> Seq.toList

        let compiles =
          node.SelectNodes "./ClCompile/@Include"
          |> Seq.cast<XmlNode>
          |> Seq.map (fun node -> node.InnerText)
          |> Seq.toList

        {
          Includes = includes
          Compiles = compiles
        }
      )

    let itemDefinitionGroupNodes =
      doc.SelectNodes "/Project/ItemDefinitionGroup"
      |> Seq.cast<XmlNode>

    let itemDefinitionGroups =
      itemDefinitionGroupNodes
      |> Seq.map (fun x ->
        let compileNode =
          x.SelectSingleNode "./ClCompile"

        let optimization =
          (compileNode.SelectSingleNode "./Optimization").InnerText

        let additionalIncludeDirs =
          match compileNode.SelectSingleNode "./AdditionalIncludeDirectories" with
          | null -> []
          | x -> x.InnerText |> List.singleton

        let linkNode =
          match x.SelectSingleNode "./Link" with
          | null -> None
          | x ->
            Some
              {
                TargetMachine =
                  match x.SelectSingleNode "./TargetMachine" with
                  | null -> None
                  | x -> Some x.InnerText
                SubSystem =
                  match x.SelectSingleNode "./SubSystem" with
                  | null -> None
                  | x -> Some x.InnerText
                AdditionalDependencies =
                  match x.SelectSingleNode "./AdditionalDependencies" with
                  | null -> None
                  | x -> Some x.InnerText
              }

        {
          Condition = Map.empty
          Compile =
            Some
              {
                Optimization = optimization
                AdditionalIncludeDirs = additionalIncludeDirs
              }
          Link = linkNode
        }
      )
      |> Seq.toList

    let proj =
      {
        Name = projectName
        ConfigurationGroups = configurationGroups
        ItemGroups =
          itemGroups
          |> Seq.toList
        ItemDefinitionGroups = itemDefinitionGroups
      }

    printfn "%O" proj

    let bazel =
      Bazel.render solution proj

    let buildFilePath =
      Path.Combine (Path.GetDirectoryName path, "BUILD.bazel")

    do! Files.write buildFilePath bazel

    printfn "%s" ("Wrote " + buildFilePath)

  Files.touch "WORKSPACE"

  return ()
}

[<EntryPoint>]
let main argv =

  if Array.length argv = 1
  then
    task (argv.[0])
    |> Async.RunSynchronously
  else
    let executableName = 
      System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName

    printfn "%s" ("Usage: " + executableName + " <path/to/Solution.sln>")

  0
