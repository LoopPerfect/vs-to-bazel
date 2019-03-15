module VsToBazel.Bazel

open VsToBazel.VisualStudio

let private unixify (x : string) =
  x.Replace("\\", "/")

let render (proj : VcxProj) =
  seq {
    let includes =
      proj.ItemDefinitionGroups
      |> Seq.collect (fun x ->
        x.Compile.AdditionalIncludeDirs
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

    yield "cc_library("
    yield "  name = \"" + proj.Name + "\","

    if Seq.isEmpty includes |> not
    then
      yield "  includes = ["

      for inc in includes do
        yield "    \"" + inc + "\","

      yield "  ],"

    yield "  hdrs = ["

    for x in headers do
      yield "    \"" + x + "\","

    yield "  ],"

    yield "  srcs = ["

    for x in sources do
      yield "    \"" + x + "\","

    yield "  ],"

    yield ")"
  }
  |> String.concat "\n"
