module Helpers
open System
open Fable.React
open Fable.React.Props
open FSharp.MetadataFormat


let createAnchorIcon name =
    let normalized = name
    let href = sprintf "#%s" normalized
    a [Href href; Id normalized] [
        str "#"
    ]

let createAnchor fullName name =
    let fullNameNormalize = fullName
    a [
        Name fullNameNormalize
        Href (sprintf "#%s" fullNameNormalize)
        Class "anchor"
    ] [
        str name
    ]

let renderNamespace (ns: Namespace) = [
    h3 [] [ str "Namespace" ]
    str ns.Name
]
