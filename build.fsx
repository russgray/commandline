// include Fake lib
#r "buildpackages/FAKE/tools/FakeLib.dll"
#r "buildpackages/FSharp.Data/lib/net40/FSharp.Data.dll"

open System
open Fake
open Fake.AssemblyInfoFile
open FSharp.Data
open FSharp.Data.JsonExtensions


// Project meta
let authors = ["Giacomo Stelluti Scala <gsscoder@gmail.com>"; "Russell Gray <russgray@gmail.com>"]
let projectName = "CommandLineParser"
let projectDescription = "The Command Line Parser Library offers to CLR applications a clean and concise API for manipulating command line arguments and related tasks."
let projectSummary = projectDescription // TODO: write a summary
let copyright = "Copyright (c) 2005 - 2013 Giacomo Stelluti Scala"


// Directories
let sourcePackagesDir = "./packages"
let buildDir = "./build"
let packagingRoot = "./packaging"
let buildPackagesDir = "./buildpackages"
let packagingDir = packagingRoot @@ "commandline"


// Properties
let gitVersion = buildPackagesDir @@ "GitVersion.CommandLine/Tools/GitVersion.exe"
let buildMode = getBuildParamOrDefault "buildMode" "Debug"


let getVersionInfo =
    // Execute GitVersion
    let (result, messages) =
        ExecProcessRedirected
            (fun info -> info.FileName <- gitVersion)
            TimeSpan.MaxValue

    // Check response
    if not result then failwithf "Error executing GitVersion: %A" messages
    // Concat the messages together (one per line of output)
    String.Concat (query { for m in messages do select m.Message })

let versionJson = getVersionInfo |> JsonValue.Parse

// for MyGet
logfn "##myget[buildNumber '%s']" (versionJson?NuGetVersion.AsString())


// Targets
Target "Clean" (fun _ ->
    CleanDir buildDir
    CleanDir packagingRoot
)

Target "RestoreSrcPackages" (fun _ ->
    let restore =
        RestorePackage (fun p ->
            { p with
                OutputPath = sourcePackagesDir
            })

    !! "./src/**/packages.config"
    |> Seq.iter (restore)
)

Target "GenerateAssemblyInfo" (fun _ ->
    CreateCSharpAssemblyInfo "./src/libcmdline/Properties/AssemblyInfo.cs"
        [Attribute.Title projectName
         Attribute.Description projectDescription
         Attribute.Product projectName
         Attribute.Copyright copyright
         Attribute.InternalsVisibleTo ("CommandLine.Tests, PublicKey=" +
            "002400000480000094000000060200000024000052534131000400000100010015eb7571d696c0" +
            "75627830f9468969103bc35764467bdbccfc0850f2fbe6913ee233d5d7cf3bbcb870fd42e6a8cc" +
            "846d706b5cef35389e5b90051991ee8b6ed73ee1e19f108e409be69af6219b2e31862405f4b8ba" +
            "101662fbbb54ba92a35d97664fe65c90c2bebd07aef530b01b709be5ed01b7e4d67a6b01c8643e" +
            "42a20fb4")
         Attribute.Version (versionJson?ClassicVersion.AsString())
         Attribute.FileVersion (versionJson?ClassicVersion.AsString())
         Attribute.InformationalVersion (versionJson?InformationalVersion.AsString())]
)

Target "BuildApp" (fun _ ->
    MSBuild buildDir "Build" ["Configuration", buildMode] ["./CommandLine.sln"]
      |> Log "AppBuild-Output: "
)

Target "Test" (fun _ ->
    !! (buildDir @@ "*.Tests.dll") 
      |> xUnit (fun p -> {p with OutputDir = buildDir })
)

Target "CreatePackage" (fun _ ->
    let net40Dir = packagingDir @@ "lib/net40/"
    CleanDirs [net40Dir]

    CopyFile net40Dir (buildDir @@ "CommandLine.dll")
    CopyFile net40Dir (buildDir @@ "CommandLine.pdb")
    CopyFile net40Dir (buildDir @@ "CommandLine.xml")
    CopyFiles packagingDir ["./doc/LICENSE"; "./nuget/readme.txt"]

    NuGet (fun p ->
        {p with
            Authors = authors
            Project = projectName
            Description = projectDescription
            OutputPath = packagingRoot
            Summary = projectSummary
            Tags = "command line argument option parser parsing library syntax shell"
            Files = [
                    (@"LICENSE", None, None)
                    (@"readme.txt", None, None)
                    (@"lib\net40\*.dll", Some @"lib\net40", None)
                    (@"lib\net40\*.pdb", Some @"lib\net40", None)
                    (@"lib\net40\*.xml", Some @"lib\net40", None)
                    (@"..\..\src\**\*.cs", Some "src", Some "..\..\src\**\TemporaryGeneratedFile*.cs")
            ]
            WorkingDir = packagingDir
            Version = (versionJson?NuGetVersion.AsString())
            SymbolPackage = NugetSymbolPackage.Nuspec })
            "./nuget/CommandLine.nuspectemplate"
)

Target "Default" <| DoNothing

// Dependencies
"Clean"
  ==> "RestoreSrcPackages"
  ==> "GenerateAssemblyInfo"
  ==> "BuildApp"
  ==> "Test"
  ==> "CreatePackage"
  ==> "Default"

// start build
RunTargetOrDefault "Default"
