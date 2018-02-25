module ExpectoTemplate
open Expecto
open System.Reflection


module AssemblyInfo =

    let metaDataValue  (mda : AssemblyMetadataAttribute) = mda.Value
    let getMetaDataAttribute (assembly : Assembly) key =
        assembly.GetCustomAttributes(typedefof<AssemblyMetadataAttribute>)
                              |> Seq.cast<AssemblyMetadataAttribute>
                              |> Seq.find(fun x -> x.Key = key)

    let getReleaseDate assembly =
        "ReleaseDate"
        |> getMetaDataAttribute assembly
        |> metaDataValue

    let getGitHash assembly =
        "GitHash"
        |> getMetaDataAttribute assembly
        |> metaDataValue

[<EntryPoint>]
let main argv =
    if argv |> Seq.contains ("--version") then
        let assembly =  Assembly.GetEntryAssembly()
        let name = assembly.GetName()
        let version = assembly.GetName().Version
        let releaseDate = AssemblyInfo.getReleaseDate assembly
        let githash  = AssemblyInfo.getGitHash assembly
        printfn "%s - %A - %s - %s" name.Name version releaseDate githash
    Tests.runTestsInAssembly defaultConfig argv
