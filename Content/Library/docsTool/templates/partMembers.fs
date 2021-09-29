module PartMembers

open System
open Fable.React
open Fable.React.Props
open FSharp.Formatting.ApiDocs
open System.Collections.Generic
open Helpers

module Debugging =
  let mutable shouldBreak = true
  let waitForDebuggerAttached (programName) =
#if DEBUG
    if not(System.Diagnostics.Debugger.IsAttached) then
      Fake.Core.Trace.tracefn "Please attach a debugger for %s, PID: %d" programName (System.Diagnostics.Process.GetCurrentProcess().Id)
    while not(System.Diagnostics.Debugger.IsAttached) do
      System.Threading.Thread.Sleep(100)
    if shouldBreak then // Break only once
      System.Diagnostics.Debugger.Break()
      shouldBreak <- false
#else
    ()
#endif

type ModuleByCategory = {
    Index : int
    GroupKey : string
    Members : list<ApiDocMember>
    Name : string
}

type ApiDocMember with
    member this.Signature = ()
        // let params =
        //     this.Parameters
        //     |> List.map(fun p ->
        //         match p.ParameterSymbol with
        //         | Choice1Of2 p -> p.Type.TypeDefinition.DisplayName
        //         | Choice1Of2 f -> f.DisplayName )
        // let returns =
        //     this.ReturnInfo.ReturnType



let signature (m : ApiDocMember) = seq {
    Debugging.waitForDebuggerAttached "docsTool"
    printfn "--> %A" m
    match m.FormatTypeArguments with
    | Some args ->
        yield
            code [ Class "function-or-value"] [
                str args
            ]
    | None ->
        yield code [ Class "function-or-value"] [
                str (sprintf "--> %A" m.CompiledName)
            ]


}

let repoSourceLink (m: ApiDocMember) = seq {
    match m.SourceLocation with
    | Some location ->
        yield a [
            Href location
            Class "float-right"
            HTMLAttr.Custom("aria-label", "View source on GitHub")
        ] [
            yield i [
                Class "fab fa-github text-dark"
            ] []
        ]
    | None -> ()
}

let replaceh2withh5 (content : string) =
    content.Replace("<h2>", "<h2 class=\"h5\">")


let normalize (content : string) =
    content
    |> replaceh2withh5



// let commentBlock (c: ApiDocComment) =
//     let (|EmptyDefaultBlock|NonEmptyDefaultBlock|Section|) (KeyValue(section, content)) =
//         match section, content with
//         | "<default>", c when String.IsNullOrEmpty c -> EmptyDefaultBlock
//         | "<default>", c -> NonEmptyDefaultBlock c
//         | section, content -> Section (section, content)

//     let renderSection (s : KeyValuePair<string,string>): Fable.React.ReactElement list =
//         match s with
//         | EmptyDefaultBlock -> []
//         | NonEmptyDefaultBlock content -> [ div [ Class "comment-block" ] [ RawText (normalize content)  ] ]
//         | Section(name, content) -> [ h5 [] [ str name ] // h2 is obnoxiously large for this context, go with the smaller h5
//                                       RawText (normalize content) ]
//     c.Sections
//     |> List.collect renderSection

// let compiledName (m: ApiDocMember) = seq {
//     if m.Details.FormatCompiledName |> String.IsNullOrEmpty |> not then
//         yield p [] [
//             strong [] [ str "CompiledName:" ]
//             code [] [ str m.Details.FormatCompiledName ]
//         ]
// }

let partMembers (header : string) (tableHeader : string) (members : #seq<ApiDocMember>) = [
    if members |> Seq.length > 0 then
        yield h3 [] [
            str header
        ]

        yield table [
            Class "table"
        ] [
            thead [] [

                tr [] [
                    th [Class "fit"] [

                    ]
                    th [] [
                        str tableHeader
                    ]

                    th [] [
                        str "Signature"
                    ]

                    th [] [
                        str "Description"
                    ]
                ]
            ]
            tbody [] [
                for it in members do
                    let id = Guid.NewGuid().ToString()
                    yield tr [] [
                        td [] [
                                Helpers.createAnchorIcon (it.Name)
                            ]
                        td [
                            Class "member-name"
                        ] [
                            code [
                                Class "function-or-value"
                                HTMLAttr.Custom("data-guid", id)
                            ] [
                                str (it.Name)
                            ]
                        ]
                        td [
                            Class "member-name"
                        ] [
                            yield! signature it
                        ]

                        td [
                            Class "xmldoc"
                        ] [
                            yield! renderObsoleteMessage it
                            yield! repoSourceLink it
                            // yield! commentBlock it.Comment
                            // yield! compiledName it
                        ]
                    ]
            ]
        ]
]
