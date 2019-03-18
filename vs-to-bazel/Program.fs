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
        printfn "%O" x.Attributes.["Condition"].InnerText

        let compileNode =
          x.SelectSingleNode "./ClCompile"

        let optimization =
          (compileNode.SelectSingleNode "./Optimization").InnerText

        let additionalIncludeDirs =
          (compileNode.SelectSingleNode "./AdditionalIncludeDirectories").InnerText
          |> List.singleton

        {
          Condition = Map.empty
          Compile =
            {
              Optimization = optimization
              AdditionalIncludeDirs = additionalIncludeDirs
            }
        }
      )
      |> Seq.toList

    let proj =
      {
        Name = projectName
        ItemGroups =
          itemGroups
          |> Seq.toList
        ItemDefinitionGroups = itemDefinitionGroups
      }

    let bazel =
      Bazel.render proj

    let buildFilePath =
      Path.Combine (Path.GetDirectoryName path, "BUILD.bazel")

    do! Files.write buildFilePath bazel

    printfn "%s" ("Wrote " + buildFilePath)

  Files.touch "WORKSPACE"

  return ()
}

[<EntryPoint>]
let main argv =
  task (argv.[0])
  |> Async.RunSynchronously

  0