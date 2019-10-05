module Nav


open Fable.React
open Fable.React.Props

let generateNav (gitRepoName) =

    let  navItem text link =
        li [
            Class "nav-item"
        ] [
            a [
                Class "nav-link"
                Href link
            ] [
                span [] [str text]
            ]

        ]

    let navDropDownItem text href =
        a [
            Class "dropdown-item"
            Href href
        ] [
            str text
        ]
    let navDropDown text items =
        li [
            Class "nav-item dropdown"
        ] [
            a [
                Class "nav-link dropdown-toggle"
                Id (sprintf "navbarDropdown-%s"  text)
                Role "button"
                DataToggle "dropdown"
                HTMLAttr.Custom ("aria-haspopup", "true")
                HTMLAttr.Custom ("aria-expanded", "false")
            ] [str text]
            div [
                Class "dropdown-menu"
                HTMLAttr.Custom ("aria-labelledby", (sprintf "navbarDropdown-%s"  text))
            ] items

        ]

    nav [
        Class "navbar navbar-expand-lg sticky-top navbar-dark bg-dark"
    ] [
        a [
            Class "navbar-brand"
            Href "/index.html"
        ] [str (gitRepoName)]
        button [
            Class "navbar-toggler"
            Type "button"
            DataToggle "collapse"
            HTMLAttr.Custom("data-target","#navbarNav" )
            HTMLAttr.Custom("aria-controls","navbarNav" )
            HTMLAttr.Custom("aria-expanded","false" )
            HTMLAttr.Custom("aria-label","Toggle navigation" )
        ] [
            span [Class "navbar-toggler-icon"] []
        ]
        div [
            Class "collapse navbar-collapse"
            Id "navbarNav"
        ] [
            ul [
                Class "navbar-nav"
            ] [
                navItem "Getting Started" "/Getting_Started.html"
                navDropDown "Docs" [
                    navDropDownItem "Docs" "/docs/Docs.html"
                ]
                navItem "Api" "/api/index.html"
            ]
        ]

    ]
