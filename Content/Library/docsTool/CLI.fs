namespace DocsTool

module CLIArgs =
    open Argu
    open Fake.IO.Globbing.Operators

    type WatchArgs =
        | ProjectGlob of string
    with
        interface IArgParserTemplate with
            member this.Usage =
                match this with
                | ProjectGlob _  -> "The glob for the dlls to generate API documentation"

    type BuildArgs =
        | ProjectGlob of string
    with
        interface IArgParserTemplate with
            member this.Usage =
                match this with
                | ProjectGlob _  -> "The glob for the dlls to generate API documentation"

    type CLIArguments =
        | [<CustomCommandLine("watch")>]  Watch of ParseResults<WatchArgs>
        | [<CustomCommandLine("build")>]  Build of ParseResults<BuildArgs>
    with
        interface IArgParserTemplate with
            member this.Usage =
                match this with
                | Watch _ -> "Builds the docs, serves the content, and watches for changes to the content."
                | Build _ -> "Builds the docs"
