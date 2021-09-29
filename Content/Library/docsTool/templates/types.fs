module Types

open System
open Fable.React
open Fable.React.Props
open FSharp.Formatting.ApiDocs
open PartMembers
open Helpers

let generateTypeDocs (model : ApiDocEntityInfo)=
    let members = model.Entity.AllMembers
    let comment = model.Entity.Comment
    let ``type`` = model.Entity
    let byCategory =
        members
        |> List.groupBy (fun m -> m.Category)
        |> List.sortBy (fun (k,v) -> if String.IsNullOrEmpty(k) then "ZZZ" else k )
        |> List.mapi (fun i (k,v) -> {
            Index = i
            GroupKey = k
            Members = v |> List.sortBy (fun m -> if m.Kind = ApiDocMemberKind.StaticParameter then "" else m.Name)
            Name = if String.IsNullOrEmpty(k) then "Other type members" else k
        })
    [
        yield h1 [] [
            str model.Entity.Name
        ]

        yield p [] [
            yield! renderObsoleteMessage model.Entity
            yield! renderNamespace model.Namespace

            if model.ParentModule.IsSome then
                yield br []
                yield span [] [
                    str "Parent Module: "

                    a [
                        Href (sprintf "%s.html" model.ParentModule.Value.UrlBaseName)
                    ] [
                        str model.ParentModule.Value.Name
                    ]
                ]


            if ``type``.Attributes |> Seq.isEmpty |> not then
                yield br []
                yield span [] [
                    yield str "Attributes: "

                    yield br []

                    for attr in ``type``.Attributes do
                        yield str (attr.Format())
                        yield br []
                ]
        ]

        // yield div [
        //     Class "xmldoc"
        // ] [
        //     for sec in comment.Sections do
        //         if byCategory |> Seq.exists (fun m -> m.GroupKey = sec.Key) |> not then
        //             if sec.Key <> "<default>" then
        //                 yield h2 [] [
        //                     str sec.Key
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
                            Href (sprintf "#section%d" g.Index)
                        ] [
                            str g.Name
                        ]
                    ]
            ]

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

                // match comment.Sections |> Seq.tryFind (fun kvp -> kvp.Key = g.GroupKey) with
                // | Some info ->
                //     yield div [
                //         Class "xmldoc"
                //     ] [
                //         str info.Value
                //     ]
                // | None -> yield nothing

            yield! partMembers "Union Cases" "Union Case" (g.Members |> Seq.filter(fun m -> m.Kind = ApiDocMemberKind.UnionCase))
            yield! partMembers "Record Fields" "Record Field" (g.Members |> Seq.filter(fun m -> m.Kind = ApiDocMemberKind.RecordField))
            yield! partMembers "Static parameters" "Static parameters" (g.Members |> Seq.filter(fun m -> m.Kind = ApiDocMemberKind.StaticParameter))
            yield! partMembers "Contructors" "Constructor" (g.Members |> Seq.filter(fun m -> m.Kind = ApiDocMemberKind.Constructor))
            yield! partMembers "Instance members" "Instance member" (g.Members |> Seq.filter(fun m -> m.Kind = ApiDocMemberKind.InstanceMember))
            yield! partMembers "Static members" "Static member" (g.Members |> Seq.filter(fun m -> m.Kind = ApiDocMemberKind.StaticMember))
    ]
