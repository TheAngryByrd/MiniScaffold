#load "../../.fake/build.fsx/intellisense.fsx"
#if !FAKE
#r "Facades/netstandard"
#r "netstandard"
#endif

open Fable.Helpers.React
open Fable.Helpers.React.Props


let masterTemplate gitRepoName navBar titletext bodyText =
    html [Lang "en"] [
        head [] [
            title [] [ str (sprintf "%s docs / %s" gitRepoName titletext) ]
            link [
                Href "https://stackpath.bootstrapcdn.com/bootstrap/4.2.1/css/bootstrap.min.css"
                Rel "stylesheet"
                Integrity "sha384-GJzZqFGwb1QTTN6wy59ffF1BuGJpLSa9DkKMp0DgiMDm4iYMj70gZWKYbI706tWS"
                CrossOrigin "anonymous"
            ]
            link [
                Href "/content/style.css"
                Type "text/css"
                Rel "stylesheet"
            ]

        ]
        body [] [
            yield navBar
            yield! bodyText
            yield script [
                Src "https://code.jquery.com/jquery-3.3.1.slim.min.js"
                Integrity "sha384-q8i/X+965DzO0rT7abK41JStQIAqVgRVzpbzo5smXKp4YfRvH+8abtTE1Pi6jizo"
                CrossOrigin "anonymous"
                ] []
            yield script [
                Src "https://cdnjs.cloudflare.com/ajax/libs/popper.js/1.14.6/umd/popper.min.js"
                Integrity "sha384-wHAiFfRlMFy6i5SRaxvfOCifBUQy1xHdJ/yoi7FRNXMRBu5WHdZYu1hA6ZOblgut"
                CrossOrigin "anonymous"
                ] []
            yield script [
                Src "https://stackpath.bootstrapcdn.com/bootstrap/4.2.1/js/bootstrap.min.js"
                Integrity "sha384-B0UglyR+jN6CkvvICOB2joaf5I4l3gm9GU6Hc1og6Ls7i6U/mkkaduKaBhlAXv9k"
                CrossOrigin "anonymous"
                ] []
            yield script [Src "/content/tips.js" ] []
            yield script [Src "/content/hotload.js" ] []
        ]
    ]
