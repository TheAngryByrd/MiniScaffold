module Master

open System
open Fable.React
open Fable.React.Props
open DocsTool

let masterTemplate (siteBaseUrl : Uri) gitRepoName navBar titletext bodyText =
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
                Href "https://cdnjs.cloudflare.com/ajax/libs/font-awesome/5.11.2/css/all.min.css"
                Rel "stylesheet"
                Integrity "sha384-KA6wR/X5RY4zFAHpv/CnoG2UW1uogYfdnP67Uv7eULvTveboZJg0qUpmJZb5VqzN"
                CrossOrigin "anonymous"
            ]
            link [
                Href (siteBaseUrl |> Uri.simpleCombine "/content/style.css" )
                Type "text/css"
                Rel "stylesheet"
            ]

        ]
        body [] [
            yield navBar
            yield div [Class "container main"] bodyText
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
            yield script [Src (siteBaseUrl |> Uri.simpleCombine "/content/tips.js") ] []
            yield script [Src (siteBaseUrl |> Uri.simpleCombine "/content/hotload.js") ] []
            yield script [Src (siteBaseUrl |> Uri.simpleCombine "/content/submenu.js") ] []
        ]
        // footer [ Class "footer font-small bg-dark navbar fixed-bottom" ] [
        //     div [Class "container"] [
        //         p [] [str "hello"]
        //     ]
        // ]
    ]
