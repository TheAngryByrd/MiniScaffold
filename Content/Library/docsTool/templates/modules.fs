module Modules
open System
open Fable.React
open Fable.React.Props
open FSharp.Formatting.ApiDocs
open PartNested
open PartMembers
open Helpers


let generateModuleDocs (moduleInfo : ApiDocEntityInfo) =
    let members = moduleInfo.Entity.AllMembers
    let comment = moduleInfo.Entity.Comment

    let byCategory =
        members
        |> List.groupBy(fun m -> m.Category)
        |> List.sortBy(fun (g,v)  -> if String.IsNullOrEmpty g then "ZZZ" else g)
        |> List.mapi(fun i (key, value) -> {
            Index = i
            GroupKey = key
            Members = value |> List.sortBy(fun m -> m.Name)
            Name = if String.IsNullOrEmpty key then "Other module members" else key
        })
    let nestModules = moduleInfo.Entity.NestedEntities
    let nestTypes = moduleInfo.Entity.NestedEntities
    [
        yield div [ Class "container-fluid py-3" ] [
            yield div [ Class "row" ] [
                yield div [ Class "col-12" ] [
                    yield h1 [] [
                        str moduleInfo.Entity.Name
                    ]
                    yield! renderObsoleteMessage moduleInfo.Entity
                    yield! renderNamespace moduleInfo.Namespace
                    yield dl [] [
                        if moduleInfo.ParentModule.IsSome then
                            yield dt [] [
                                str "Parent Module"
                            ]
                            yield dd [] [
                                a [
                                    Href (sprintf "%s.html" moduleInfo.ParentModule.Value.UrlBaseName)
                                ] [
                                    str moduleInfo.ParentModule.Value.Name
                                ]
                            ]
                        if moduleInfo.Entity.Attributes |> Seq.isEmpty |> not then
                            yield dt [] [
                                str "Attributes"
                            ]
                            yield dd [] [
                                for attr in moduleInfo.Entity.Attributes do
                                    yield str (attr.Format())
                                    yield br []
                            ]
                    ]

                    // yield div [
                    //     Class "xmldoc"
                    // ] [
                    //     for sec in comment.Sections do
                    //         if byCategory |> Seq.exists (fun g -> g.GroupKey = sec.Key) |> not then
                    //             if sec.Key <> "<default>" then
                    //                 yield h2 [] [
                    //                     RawText sec.Key
                    //                 ]
                    //             yield RawText sec.Value
                    // ]


                    if byCategory |> Seq.length > 1 then
                        yield h2 [] [
                            str "Table of contents"
                        ]

                        yield ul [] [
                            for g in byCategory do
                                yield li [] [
                                    a [
                                        Href (g.Index.ToString() |> sprintf "#section%s")
                                    ] [
                                        str g.Name
                                    ]
                                ]
                        ]

                    if (nestTypes |> Seq.length) + (nestModules |> Seq.length) > 0 then
                        yield h2 [] [
                            str "Nested types and modules"
                        ]

                        yield! (partNested (nestTypes |> Seq.toArray) (nestModules |> Seq.toArray))

                    for g in byCategory do
                        if byCategory |> Seq.length > 1 then
                            yield h2 [] [
                                str g.Name
                                a [
                                    Name (sprintf "section%d" g.Index)
                                ] [
                                    str "&#160;"
                                ]
                            ]

                        // let info = comment.Sections |> Seq.tryFind(fun kvp -> kvp.Key = g.GroupKey)

                        // match info with
                        // | Some info ->
                        //     yield div [
                        //         Class "xmldoc"
                        //     ] [
                        //         str info.Value
                        //     ]
                        // | None ->
                        //     yield nothing

                        yield! partMembers "Functions and values" "Function or value" (g.Members |> Seq.filter(fun m -> m.Kind = ApiDocMemberKind.ValueOrFunction))

                        yield! partMembers "Type extensions" "Type extension" (g.Members |> Seq.filter(fun m -> m.Kind = ApiDocMemberKind.TypeExtension))

                        yield! partMembers "Active patterns" "Active pattern" (g.Members |> Seq.filter(fun m -> m.Kind = ApiDocMemberKind.ActivePattern))
                ]
            ]
        ]
    ]
