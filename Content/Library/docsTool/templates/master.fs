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
                Href "https://stackpath.bootstrapcdn.com/bootstrap/4.4.1/css/bootstrap.min.css"
                Rel "stylesheet"
                Integrity "sha384-Vkoo8x4CGsO3+Hhxv8T/Q5PaXtkKtu6ug5TOeNV6gBiFeWPGFN9MuhOf23Q9Ifjh"
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
            yield div [Class "wrapper d-flex flex-column justify-content-between min-vh-100"] [
                main [Class "container main"] bodyText
                footer [Class "navbar font-small bg-dark m-0"] [
                    div [Class "container"] [
                        p [Class "text-light mb-0"] [str "hello"]
                    ]
                ]
            ]
            yield script [
                Src "https://code.jquery.com/jquery-3.4.1.slim.min.js"
                Integrity "sha384-J6qa4849blE2+poT4WnyKhv5vZF5SrPo0iEjwBvKU7imGFAV0wwj1yYfoRSJoZ+n"
                CrossOrigin "anonymous"
                ] []
            yield script [
                Src "https://cdn.jsdelivr.net/npm/popper.js@1.16.0/dist/umd/popper.min.js"
                Integrity "sha384-Q6E9RHvbIyZFJoft+2mJbHaEWldlvI9IOYy5n3zV9zzTtmI3UksdQRVvoxMfooAo"
                CrossOrigin "anonymous"
                ] []
            yield script [
                Src "https://stackpath.bootstrapcdn.com/bootstrap/4.4.1/js/bootstrap.min.js"
                Integrity "sha384-wfSDF2E50Y2D1uUdj0O3uMBJnjuUD4Ih7YwaYd1iqfktj0Uod8GCExl3Og8ifwB6"
                CrossOrigin "anonymous"
                ] []
            yield script [Src (siteBaseUrl |> Uri.simpleCombine "/content/tips.js") ] []
            yield script [Src (siteBaseUrl |> Uri.simpleCombine "/content/hotload.js") ] []
            yield script [Src (siteBaseUrl |> Uri.simpleCombine "/content/submenu.js") ] []
        ]
    ]
