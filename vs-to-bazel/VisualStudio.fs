module VsToBazel.VisualStudio

open System

    // <Link>
    //   <AdditionalDependencies>MathParseKit.lib;%(AdditionalDependencies)</AdditionalDependencies>
    //   <AdditionalLibraryDirectories>../../MathParseKit/lib/Win32/Debug/;%(AdditionalLibraryDirectories)</AdditionalLibraryDirectories>
    //   <GenerateDebugInformation>true</GenerateDebugInformation>
    //   <SubSystem>Console</SubSystem>
    //   <TargetMachine>MachineX86</TargetMachine>
    // </Link>

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

  // <PropertyGroup Condition="'$(Configuration)|$(Platform)'=='Release|Win32'" Label="Configuration">
  //   <ConfigurationType>Application</ConfigurationType>
  //   <PlatformToolset>v141</PlatformToolset>
  //   <CharacterSet>Unicode</CharacterSet>
  //   <WholeProgramOptimization>true</WholeProgramOptimization>
  // </PropertyGroup>
type ConfigurationGroup =
  {
    ConfigurationType : string
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

type Link =
  {
    TargetMachine : string
    SubSystem : string
    AdditionalDependencies : string
  }

type ItemDefinitionGroup =
  {
    Condition : Map<string, string> // Mapping from variable name to required value
    Compile : ClCompile option
    Link : Link option
  }

type VcxProj =
  {
    Name : string
    ConfigurationGroups : ConfigurationGroup list
    ItemDefinitionGroups : ItemDefinitionGroup list
    ItemGroups : ItemGroup list
  }
