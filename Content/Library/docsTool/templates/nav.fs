module Nav


open Fable.React
open Fable.React.Props

type NameOfArticle = string
type UrlPath = string

type TopLevelNav = {
    Tutorials : list<NameOfArticle * UrlPath>
    HowToGuides : list<NameOfArticle * UrlPath>
    Explanations : list<NameOfArticle * UrlPath>
}

let generateNav (gitRepoName : string) (topLevelNav : TopLevelNav) =

    let navItem text link =
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
    let dropDownNavItem text link =
        li [
            Class "nav-item"
        ] [
            a [
                Class "dropdown-item"
                Href link
            ] [
                span [] [str text]
            ]
        ]
    let dropdownSubMenu text items =
        li [ Class "dropdown-submenu" ] [
            a [ Id (sprintf "navbarDropdown-%s"  text)
                Href "#"
                Role "button"
                DataToggle "dropdown"
                AriaHasPopup true
                AriaExpanded false
                Class "dropdown-item dropdown-toggle" ] [
                    str text ]
            ul [
                HTMLAttr.Custom ("aria-labelledby", "dropdownMenu2")
                Class "dropdown-menu border-0 shadow" ] items
        ]



    // let navDropDownItem text href =
    //     a [
    //         Class "dropdown-item"
    //         Href href
    //     ] [
    //         str text
    //     ]
    // let navDropDown text items =
    //     li [
    //         Class "nav-item dropdown"
    //     ] [
    //         a [
    //             Class "nav-link dropdown-toggle"
    //             Id (sprintf "navbarDropdown-%s"  text)
    //             Role "button"
    //             DataToggle "dropdown"
    //             HTMLAttr.Custom ("aria-haspopup", "true")
    //             HTMLAttr.Custom ("aria-expanded", "false")
    //         ] [str text]
    //         div [
    //             Class "dropdown-menu"
    //             HTMLAttr.Custom ("aria-labelledby", (sprintf "navbarDropdown-%s"  text))
    //         ] items

    //     ]
    // let subNavDropDown text items =
    //     li [
    //         Class "dropdown-submenu"
    //     ] [
    //         a [
    //             Class "dropdown-item dropdown-toggle"
    //             Id (sprintf "navbarDropdown-%s"  text)
    //             Role "button"
    //             DataToggle "dropdown"
    //             HTMLAttr.Custom ("aria-haspopup", "true")
    //             HTMLAttr.Custom ("aria-expanded", "false")
    //         ] [str text]
    //         div [
    //             Class "dropdown-menu"
    //             HTMLAttr.Custom ("aria-labelledby", (sprintf "navbarDropdown-%s"  text))
    //         ] [
    //             ul [ Class "dropdown-menu border-0 shadow" ] items
    //         ]
    //     ]

    // nav [ Class "navbar navbar-expand-lg sticky-top navbar-dark bg-dark" ] [
    //     div [ Class "container" ][
    //             a [ Href "#"
    //                 Class "navbar-brand font-weight-bold" ]
    //                 [ str (gitRepoName) ]
    //             button [
    //                 Type "button"
    //                 DataToggle "collapse"
    //                 HTMLAttr.Data ("target", "#navbarContent")
    //                 HTMLAttr.Custom ("aria-controls", "navbars")
    //                 AriaExpanded false
    //                 HTMLAttr.Custom ("aria-label", "Toggle navigation")
    //                 Class "navbar-toggler"
    //             ] [
    //                 span [Class "navbar-toggler-icon"] []
    //             ]
    //             div [
    //                 Class "collapse navbar-collapse"
    //                 Id "navbarContent"
    //             ] [
    //                 ul [ Class "navbar-nav mr-auto" ] [
    //                     li [ Class "nav-item dropdown" ] [
    //                         a [ Id "dropdownMenu1"
    //                             Href "#"
    //                             DataToggle "dropdown"
    //                             AriaHasPopup true
    //                             AriaExpanded false
    //                             Class "nav-link dropdown-toggle" ]
    //                                 [ str "Dropdown" ]
    //                     ]
    //                 ]
                    //       ul [ HTMLAttr.Custom ("aria-labelledby", "dropdownMenu1")
                    //            Class "dropdown-menu border-0 shadow" ]
                    //         [ li [ ]
                    //             [ a [ Href "#"
                    //                   Class "dropdown-item" ]
                    //                 [ str "Some action" ] ]
                    //           li [ ]
                    //             [ a [ Href "#"
                    //                   Class "dropdown-item" ]
                    //                 [ str "Some other action" ] ]
                    //           li [ Class "dropdown-divider" ]
                    //             [ ]
                    //           li [ Class "dropdown-submenu" ]
                    //             [ a [ Id "dropdownMenu2"
                    //                   Href "#"
                    //                   Role "button"
                    //                   DataToggle "dropdown"
                    //                   AriaHasPopup true
                    //                   AriaExpanded false
                    //                   Class "dropdown-item dropdown-toggle" ]
                    //                 [ str "Hover for action" ]
                    //               ul [ HTMLAttr.Custom ("aria-labelledby", "dropdownMenu2")
                    //                    Class "dropdown-menu border-0 shadow" ]
                    //                 [ li [ ]
                    //                     [ a [ TabIndex -1
                    //                           Href "#"
                    //                           Class "dropdown-item" ]
                    //                         [ str "level 2" ] ]
                    //                   li [ Class "dropdown-submenu" ]
                    //                     [ a [ Id "dropdownMenu3"
                    //                           Href "#"
                    //                           Role "button"
                    //                           DataToggle "dropdown"
                    //                           AriaHasPopup true
                    //                           AriaExpanded false
                    //                           Class "dropdown-item dropdown-toggle" ]
                    //                         [ str "level 2" ]
                    //                       ul [ HTMLAttr.Custom ("aria-labelledby", "dropdownMenu3")
                    //                            Class "dropdown-menu border-0 shadow" ]
                    //                         [ li [ ]
                    //                             [ a [ Href "#"
                    //                                   Class "dropdown-item" ]
                    //                                 [ str "3rd level" ] ]
                    //                           li [ ]
                    //                             [ a [ Href "#"
                    //                                   Class "dropdown-item" ]
                    //                                 [ str "3rd level" ] ] ] ]
                    //                   li [ ]
                    //                     [ a [ Href "#"
                    //                           Class "dropdown-item" ]
                    //                         [ str "level 2" ] ]
                    //                   li [ ]
                    //                     [ a [ Href "#"
                    //                           Class "dropdown-item" ]
                    //                         [ str "level 2" ] ] ] ] ] ]
                    //   li [ Class "nav-item" ]
                    //     [ a [ Href "#"
                    //           Class "nav-link" ]
                    //         [ str "About" ] ]
                    //   li [ Class "nav-item" ]
                    //     [ a [ Href "#"
                    //           Class "nav-link" ]
                    //         [ str "Services" ] ]
                    //   li [ Class "nav-item" ]
                    //     [ a [ Href "#"
                    //           Class "nav-link" ]
                    //         [ str "Contact" ] ]
                    // ]


    //         ]
    //     ]
    // ]


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
        div [   Class "collapse navbar-collapse"
                Id "navbarNav" ] [
            ul [ Class "navbar-nav mr-auto" ] [
                li [ Class "nav-item dropdown" ][
                    a [
                        Id "dropdownMenu1"
                        Href "#"
                        DataToggle "dropdown"
                        AriaHasPopup true
                        AriaExpanded false
                        Class "nav-link dropdown-toggle" ]
                        [ str "Dropdown" ]
                    ul [    HTMLAttr.Custom ("aria-labelledby", "dropdownMenu1")
                            Class "dropdown-menu border-0 shadow" ][
                        dropDownNavItem "Level 1 - Item 1" "#"
                        dropDownNavItem "Level 1 - Item 2" "#"
                        li [ Class "dropdown-divider" ] [ ]
                        dropdownSubMenu "Hover for action" [
                                    dropDownNavItem "Level 2 - Item 1" "#"
                                    dropdownSubMenu "Level 2 - Item 2" [
                                        dropDownNavItem "Level 3 - Item 1" "#"
                                        dropDownNavItem "Level 3 - Item 2" "#"
                                    ]
                                    dropDownNavItem "Level 2 - Item 3" "#"
                                    dropDownNavItem "Level 2 - Item 4" "#"
                        ]
                    ]
                ]


                navItem "About" "#"
                navItem "Services" "#"
                navItem "Contact" "#"
            ]

        ]
    ]
            // ul [
            //     Class "navbar-nav"
            // ] [
            //     navDropDown "Tutorials" [
            //         yield! topLevelNav.Tutorials
            //                |> List.map(fun (name, link) -> navDropDownItem name link )
            //     ]
            //     navDropDown "How-To Guides" [
            //         yield! topLevelNav.HowToGuides
            //                |> List.map(fun (name, link) -> navDropDownItem name link )
            //     ]
            //     navDropDown "Explanationss" [
            //         yield! topLevelNav.Explanations
            //                |> List.map(fun (name, link) -> navDropDownItem name link )
            //     ]
            //     navItem "Reference/API" "/api/index.html"

            //     navDropDown "Top Level" [
            //         subNavDropDown "Second Level" [

            //             navItem "hello" ""
            //             navItem "world" ""
            //         ]

            //     ]
            // ]
        // ]


