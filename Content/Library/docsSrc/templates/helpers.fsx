#load "../../.fake/build.fsx/intellisense.fsx"
#if !FAKE
#r "Facades/netstandard"
#r "netstandard"
#endif
open System
open Fable.React
open Fable.React.Props
open FSharp.MetadataFormat


let createAnchor name =
    let normalized = name
    let href = sprintf "#%s" normalized
    a [Href href] [
        i [ Class "fas fa-anchor"] []
    ]

