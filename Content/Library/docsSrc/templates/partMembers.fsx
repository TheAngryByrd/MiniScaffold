
#load "../../.fake/build.fsx/intellisense.fsx"
#if !FAKE
#r "Facades/netstandard"
#r "netstandard"
#endif
open System
open Fable.Helpers.React
open Fable.Helpers.React.Props
open FSharp.MetadataFormat

type ModuleByCategory = {
    Index : int
    GroupKey : string
    Members : seq<Member>
    Name : string
}

let partMembers (header : string) (tableHeader : string) (members : #seq<Member>) = [
    if members |> Seq.length > 0 then
        yield h3 [] [
            str header
        ]

        yield table [
            Class "table table-bordered member-list"
        ] [
            thead [] [
                tr [] [
                    td [] [
                        str tableHeader
                    ]

                    td [] [
                        str "Description"
                    ]
                ]
            ]

            tbody [] [
                for it in members do
                    let id = Guid.NewGuid().ToString()
                    yield tr [] [
                        td [
                            Class "member-name"
                        ] [
                            code [
                                Class "function-or-value"
                                HTMLAttr.Custom("data-guid", id)
                            ] [
                                str (it.Details.FormatUsage(40))
                            ]

                            div [
                                Class "tip"
                                Id id
                            ] [
                                yield strong [] [
                                    str "Signature:"
                                ]

                                yield str it.Details.Signature

                                yield br []

                                if it.Details.Modifiers |> Seq.isEmpty then
                                    yield strong [] [
                                        str "Modifiers:"
                                    ]

                                    yield str it.Details.FormatModifiers

                                    yield br []

                                if it.Details.TypeArguments |> Seq.isEmpty then
                                    yield strong [] [
                                        str "Type parameters:"
                                    ]

                                    yield str it.Details.FormatTypeArguments

                                if it.Attributes |> Seq.isEmpty |> not then
                                    yield span [] [
                                        yield strong [] [
                                            str "Attributes:"
                                        ]

                                        yield br []

                                        for attr in it.Attributes do
                                            yield str (attr.Format())

                                            yield br []
                                    ]
                            ]

                        ]

                        td [
                            Class "xmldoc"
                        ] [
                            if it.IsObsolete then
                                yield div [
                                    Class "alert alert-warning"
                                ] [
                                    strong [] [
                                        str "WARNING:"
                                    ]

                                    str "This API is obsolete"

                                    p [] [
                                        str it.ObsoleteMessage
                                    ]
                                ]

                            if it.Details.FormatSourceLocation |> String.IsNullOrEmpty |> not then
                                yield a [
                                    Href it.Details.FormatSourceLocation
                                    Class "github-link"
                                ] [
                                    yield img [
                                        Src "../content/img/github.png"
                                        Class "github-link"
                                    ]

                                    yield img [
                                        Src "../content/img/github-blue.png"
                                        Class "normal"
                                    ]

                                    yield RawText it.Comment.FullText

                                    if it.Details.FormatCompiledName |> String.IsNullOrEmpty |> not then
                                        yield p [] [
                                            str "CompiledName: "

                                            code [] [
                                                str it.Details.FormatCompiledName
                                            ]
                                        ]
                                ]
                        ]
                    ]
            ]
        ]
]
