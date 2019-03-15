module VsToBazel.VisualStudio

open System

type ProjectRef =
  {
    ProjectHostGuid : Guid
    ProjectName : string
    RelativePath : string
    ProjectGuid : Guid
  }

type Solution =
  {
    SlnVersion : string
    Projects : ProjectRef list
  }

type ItemGroup =
  {
    Includes : string list
    Compiles : string list
  }

type ClCompile =
  {
    Optimization : string
    AdditionalIncludeDirs : string list
  }

type ItemDefinitionGroup =
  {
    Condition : Map<string, string> // Mapping from variable name to required value
    Compile : ClCompile
  }

type VcxProj =
  {
    Name : string
    ItemDefinitionGroups : ItemDefinitionGroup list
    ItemGroups : ItemGroup list
  }
