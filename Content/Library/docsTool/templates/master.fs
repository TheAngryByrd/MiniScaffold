module Master

open System
open Fable.React
open Fable.React.Props
open DocsTool

type MasterTemplateConfig = {
    SiteBaseUrl : Uri
    GitHubRepoUrl : Uri
    ProjectName : string
    ReleaseVersion : string
    ReleaseDate : DateTimeOffset
}

let renderFooter (cfg : MasterTemplateConfig) =
    footer [Class "footer font-small m-0 pt-1 bg-dark"] [
        div [Class "container"] [
            div [Class "row py-4"] [
                div [Class "col"] [
                    div [Class "text-light"] [
                        h5 [] [ str "Project Resources"]
                        ul [Class "list-group list-group-flush"] [
                            li [Class "list-group-item list-group-item-dark ml-0 pl-0"] [
                                a [Href (cfg.GitHubRepoUrl |> Uri.simpleCombine "blob/master/README.md"); Target "_blank"; Class "text-white"] [
                                    i [ Class "fab fa-readme pr-1"] []
                                    str "README"
                                ]
                            ]
                            li [Class "list-group-item list-group-item-dark ml-0 pl-0"] [
                                a [Href (cfg.GitHubRepoUrl |> Uri.simpleCombine "blob/master/RELEASE_NOTES.md"); Target "_blank"; Class "text-white"] [
                                    i [ Class "far fa-sticky-note pr-1"] []
                                    str "Release Notes / Changelog"
                                ]
                            ]
                            li [Class "list-group-item list-group-item-dark ml-0 pl-0"] [
                                a [Href (cfg.GitHubRepoUrl |> Uri.simpleCombine "blob/master/LICENSE.md"); Target "_blank"; Class "text-white"] [
                                    i [ Class "far fa-id-card pr-1"] []
                                    str "License"
                                ]
                            ]
                            li [Class "list-group-item list-group-item-dark ml-0 pl-0"] [
                                a [Href (cfg.GitHubRepoUrl |> Uri.simpleCombine "blob/master/CONTRIBUTING.md"); Target "_blank"; Class "text-white"] [
                                    i [ Class "fas fa-directions pr-1"] []
                                    str "Contributing"
                                ]
                            ]
                            li [Class "list-group-item list-group-item-dark ml-0 pl-0"] [
                                a [Href (cfg.GitHubRepoUrl |> Uri.simpleCombine "blob/master/CODE_OF_CONDUCT.md"); Target "_blank"; Class "text-white"] [
                                    i [ Class "fas fa-users pr-1"] []
                                    str "Code of Conduct"
                                ]
                            ]

                        ]

                    ]
                ]
                div [Class "col"] [
                    div [Class "text-light"] [
                        h5 [] [ str "Other links"]
                        ul [Class "list-group list-group-flush"] [
                            li [Class "list-group-item list-group-item-dark ml-0 pl-0"] [
                                a [Href "https://docs.microsoft.com/en-us/dotnet/fsharp/"; Target "_blank"; Class "text-white"] [
                                    i [Class "fab fa-microsoft pr-1"] []
                                    str "F# Documentation"
                                ]
                            ]
                            li [Class "list-group-item list-group-item-dark ml-0 pl-0"] [
                                a [Href "https://fsharp.slack.com/"; Target "_blank"; Class "text-white"] [
                                    i [Class "fab fa-slack pr-1"] []
                                    str "F# Slack"
                                ]
                            ]
                            li [Class "list-group-item list-group-item-dark ml-0 pl-0"] [
                                a [Href "http://foundation.fsharp.org/"; Target "_blank"; Class "text-white"] [
                                    img [Class "fsharp-footer-logo pr-1"; Src "https://fsharp.org/img/logo/fsharp.svg"; Alt "FSharp Logo"]
                                    str "F# Software Foundation"
                                ]
                            ]
                        ]
                    ]


                ]
                div [Class "col"] [
                    div [Class "text-light"] [
                        h5 [] [str "Metadata"]
                        p [] [
                            str "Generated for version "
                            a [Class "text-white"; Target "_blank"; Href (cfg.GitHubRepoUrl |> Uri.simpleCombine (sprintf "releases/tag/%s" cfg.ReleaseVersion))] [str cfg.ReleaseVersion]
                            str (sprintf " on %s" (cfg.ReleaseDate.ToString("yyyy/MM/dd")))
                        ]
                    ]
                ]
            ]
            div [Class "row py-4"] [
                div [Class "col"] [
                    div [Class "text-light text-center"] [
                        i [Class "fas fa-copyright pr-1"] []
                        str "MyLib.1, All rights reserved"
                    ]
                ]
            ]
        ]
    ]

let masterTemplate (cfg : MasterTemplateConfig) navBar titletext bodyText =
    html [Lang "en"] [
        head [] [
            title [] [ str (sprintf "%s docs / %s" cfg.ProjectName titletext) ]
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
                Href (cfg.SiteBaseUrl |> Uri.simpleCombine (sprintf "/content/style.css?version=%i" cfg.ReleaseDate.Ticks) )
                Type "text/css"
                Rel "stylesheet"
            ]

        ]
        body [] [
            yield navBar
            yield div [Class "wrapper d-flex flex-column justify-content-between min-vh-100"] [
                main [Class "container main mb-4"] bodyText
                renderFooter cfg
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
            yield script [Src (cfg.SiteBaseUrl |> Uri.simpleCombine (sprintf "/content/tips.js?version=%i" cfg.ReleaseDate.Ticks)) ] []
            yield script [Src (cfg.SiteBaseUrl |> Uri.simpleCombine (sprintf "/content/hotload.js?version=%i" cfg.ReleaseDate.Ticks)) ] []
            yield script [Src (cfg.SiteBaseUrl |> Uri.simpleCombine (sprintf "/content/submenu.js?version=%i" cfg.ReleaseDate.Ticks)) ] []
        ]
    ]
