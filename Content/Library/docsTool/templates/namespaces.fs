module Namespaces

open System
open Fable.React
open Fable.React.Props
open FSharp.Formatting.ApiDocs


type ByCategory = {
    Name : string
    Index : string
    Types : ApiDocEntity array
    Modules : ApiDocEntity array
}

let generateNamespaceDocs (asm : ApiDocCollection)  =
    let parts =
        asm.Namespaces
        |> Seq.mapi(fun nsi ns ->
            let allByCategories =
                ns.Entities
                |> Seq.map(fun t -> t.Category)
                // |> Seq.append (ns.Entities |> Seq.map(fun m -> m.Category))
                |> Seq.distinct
                |> Seq.sortBy(fun s ->
                    if String.IsNullOrEmpty(s) then "ZZZ"
                    else s)
                |> Seq.mapi(fun ci c ->
                {
                    Name = if String.IsNullOrEmpty(c) then "Other namespace members" else c
                    Index = sprintf "%d_%d" nsi ci
                    Types = ns.Entities |> Seq.filter(fun t -> t.Category = c && t.IsTypeDefinition) |> Seq.toArray
                    Modules = ns.Entities |> Seq.filter(fun m -> m.Category = c && not m.IsTypeDefinition) |> Seq.toArray
                })
                |> Seq.filter(fun c -> c.Types.Length + c.Modules.Length > 0)
                |> Seq.toArray
            [
                yield h2 [] [
                    Helpers.createAnchor ns.Name ns.Name
                ]
                if allByCategories.Length > 1 then
                    yield ul [] [
                        for c in allByCategories do
                            yield
                                li [] [
                                    a [Href (sprintf "#section%s" c.Index)] [
                                        str c.Name
                                    ]
                                ]
                    ]


                for c in allByCategories do
                    if allByCategories.Length > 1 then
                        yield h3 [] [
                            a  [Class "anchor"; Name (sprintf "section%s" c.Index); Href (sprintf "#section%s" c.Index)] [
                                str c.Name
                            ]
                        ]
                    yield! PartNested.partNested c.Types c.Modules
            ]
        )
        |> Seq.collect id
    div [ Class "container-fluid py-3" ] [
        div [ Class "row" ] [
            div [ Class "col-12" ] [
                yield h1 [] [
                    Helpers.createAnchor asm.CollectionName asm.CollectionName
                ]
                yield! parts
            ]
        ]
    ]
