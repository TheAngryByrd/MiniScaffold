#load "../../.fake/build.fsx/intellisense.fsx"
#if !FAKE
#r "Facades/netstandard"
#r "netstandard"
#endif
open System
open Fable.React
open Fable.React.Props
open FSharp.MetadataFormat


let partNested (types : Type array) (modules : Module array) =
    [
        if types.Length > 0 then
            yield table [ Class "table" ] [
                thead [] [
                    tr [] [
                        th [] [
                            str "Type"
                        ]
                        th [] [
                            str "Description"
                        ]
                    ]
                ]
                tbody [] [
                    for t in types do
                        yield tr [] [
                            td [Class "type-name"] [
                                a [Href (sprintf "%s.html" t.UrlName)] [
                                    str t.Name
                                ]
                            ]
                            td [Class "xmdoc"] [
                                if t.IsObsolete then
                                    yield div [Class "alert alert-warning"] [
                                        strong [] [
                                            str "WARNING:"
                                        ]
                                        str "This API is obsolete"
                                        p [] [
                                            str t.ObsoleteMessage
                                        ]
                                    ]
                                yield RawText t.Comment.Blurb
                            ]
                        ]
                ]
            ]
        if modules.Length > 0 then
            yield table [ Class "table" ] [
                thead [] [
                    tr [] [
                        th [] [
                            str "Module"
                        ]
                        th [] [
                            str "Description"
                        ]
                    ]
                ]
                tbody [] [
                    for t in modules do
                        yield tr [] [
                            td [Class "Modules-name"] [
                                a [Href (sprintf "%s.html" t.UrlName)] [
                                    str t.Name
                                ]
                            ]
                            td [Class "xmdoc"] [
                                if t.IsObsolete then
                                    yield div [Class "alert alert-warning"] [
                                        strong [] [
                                            str "WARNING:"
                                        ]
                                        str "This API is obsolete"
                                        p [] [
                                            str t.ObsoleteMessage
                                        ]
                                    ]
                                yield RawText t.Comment.Blurb
                            ]
                        ]
                ]
            ]
    ]
