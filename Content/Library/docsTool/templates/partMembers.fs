module PartMembers

open System
open Fable.React
open Fable.React.Props
open FSharp.MetadataFormat

type ModuleByCategory = {
    Index : int
    GroupKey : string
    Members : list<Member>
    Name : string
}

let tooltip (m: Member) (dataId: string) =
    div [
        Class "tip"
        Id dataId
    ] [
        yield strong [] [
            str "Signature:"
        ]

        yield str m.Details.Signature

        yield br []

        if m.Details.Modifiers |> Seq.isEmpty then
            yield strong [] [
                str "Modifiers:"
            ]

            yield str m.Details.FormatModifiers

            yield br []

        if m.Details.TypeArguments |> Seq.isEmpty then
            yield strong [] [
                str "Type parameters:"
            ]

            yield str m.Details.FormatTypeArguments

        if m.Attributes |> Seq.isEmpty |> not then
            yield span [] [
                yield strong [] [
                    str "Attributes:"
                ]

                yield br []

                for attr in m.Attributes do
                    yield str (attr.Format())

                    yield br []
            ]
    ]

let obsoleteMessage (m: Member) = seq {
    if m.IsObsolete then
        yield div [
            Class "alert alert-warning"
        ] [
            strong [] [
                str "WARNING:"
            ]

            str "This API is obsolete"

            p [] [
                str m.ObsoleteMessage
            ]
        ]
    }

let repoSourceLink (m: Member) = seq {
    if m.Details.FormatSourceLocation |> String.IsNullOrEmpty |> not then
        yield a [
            Href m.Details.FormatSourceLocation
            Class "float-right"
        ] [
            yield i [
                Class "fab fa-github text-dark"
            ] []
        ]
}


let commentBlock (c: Comment) =
    let (|EmptyDefaultBlock|NonEmptyDefaultBlock|Section|) (KeyValue(section, content)) =
        match section, content with
        | "<default>", c when String.IsNullOrEmpty c -> EmptyDefaultBlock
        | "<default>", c -> NonEmptyDefaultBlock c
        | section, content -> Section (section, content)

    let renderSection s: Fable.React.ReactElement list =
        match s with
        | EmptyDefaultBlock -> []
        | NonEmptyDefaultBlock content -> [ div [ Class "comment-block" ] [ RawText content ] ]
        | Section(name, content) -> [ h5 [] [ str name ] // h2 is obnoxiously large for this context, go with the smaller h5
                                      RawText content ]

    c.Sections
    |> List.collect renderSection

let compiledName (m: Member) = seq {
    if m.Details.FormatCompiledName |> String.IsNullOrEmpty |> not then
        yield p [] [
            strong [] [ str "CompiledName:" ]
            code [] [ str m.Details.FormatCompiledName ]
        ]
}

let partMembers (header : string) (tableHeader : string) (members : #seq<Member>) = [
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
                        str "Description"
                    ]
                ]
            ]
            tbody [] [
                for it in members do
                    let id = Guid.NewGuid().ToString()
                    yield tr [] [
                        td [] [
                                Helpers.createAnchorIcon (it.Details.FormatUsage(40))
                            ]
                        td [
                            Class "member-name"
                        ] [
                            code [
                                Class "function-or-value"
                                HTMLAttr.Custom("data-guid", id)
                            ] [
                                str (it.Details.FormatUsage(40))
                            ]
                            tooltip it id
                        ]

                        td [
                            Class "xmldoc"
                        ] [
                            yield! obsoleteMessage it
                            yield! repoSourceLink it
                            // printfn "%s:\n%A" it.Name it.Comment
                            yield! commentBlock it.Comment
                            yield! compiledName it
                        ]
                    ]
            ]
        ]
]
