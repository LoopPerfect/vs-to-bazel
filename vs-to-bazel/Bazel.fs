module VsToBazel.Bazel

open System.IO
open VsToBazel.VisualStudio

let private unixify (x : string) =
  x.Replace("\\", "/")

let render (sln : Solution) (proj : VcxProj) =
  seq {
    let projType =
      proj.ConfigurationGroups
      |> Seq.map (fun x -> x.ConfigurationType)
      |> Seq.tryHead
      |> Option.defaultValue "StaticLibrary"

    let includes =
      proj.ItemDefinitionGroups
      |> Seq.choose (fun x -> x.Compile)
      |> Seq.collect (fun x ->
        x.AdditionalIncludeDirs
      )
      |> Seq.collect (fun x -> x.Split [| ';' |])
      |> Seq.filter (fun x ->
        (x.StartsWith "%(" |> not) &&
        (x.Contains ".." |> not) // Assume these will come from deps
      )
      |> Seq.distinct

    let headers =
      proj.ItemGroups
      |> Seq.collect (fun x -> x.Includes)
      |> Seq.distinct
      |> Seq.map unixify
      |> Seq.toList

    let sources =
      proj.ItemGroups
      |> Seq.collect (fun x -> x.Compiles)
      |> Seq.distinct
      |> Seq.map unixify
      |> Seq.toList

    let deps =
      proj.ItemDefinitionGroups
      |> Seq.choose (fun x -> x.Link)
      |> Seq.choose (fun x -> x.AdditionalDependencies)
      |> Seq.collect (fun x -> x.Split [| ';' |])
      |> Seq.filter (fun x -> x.StartsWith "%(" |> not)
      |> Seq.map (fun x -> x.Replace (".lib", ""))
      |> Seq.choose (fun x ->
        match sln.Projects |> Seq.tryFind (fun y -> y.ProjectName = x) with
        | Some p ->
          p.RelativePath
          |> unixify
          |> Path.GetDirectoryName
          |> (fun x -> "//" + x + ":" + p.ProjectName)
          |> Some
        | None -> None
      )
      |> Seq.distinct

    if projType = "Application"
    then
      yield "cc_binary("
    else
      yield "cc_library("

    yield "  name = \"" + proj.Name + "\","

    if Seq.isEmpty includes |> not
    then
      yield "  includes = ["

      for inc in includes do
        yield "    \"" + inc + "\","

      yield "  ],"

    if Seq.isEmpty headers |> not
    then
      yield "  hdrs = ["

      for x in headers do
        yield "    \"" + x + "\","

      yield "  ],"

    yield "  srcs = ["

    for x in sources do
      yield "    \"" + x + "\","

    yield "  ],"

    if Seq.isEmpty deps |> not
    then
      yield "  deps = ["

      for d in deps do
        yield "    \"" + d + "\","

      yield "  ],"

    yield ")"
  }
  |> String.concat "\n"
