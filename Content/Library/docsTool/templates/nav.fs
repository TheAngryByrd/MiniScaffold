module Nav

open System

open Fable.React
open Fable.React.Props

type NameOfArticle = string
type UrlPath = string

type TopLevelNav = {
    Tutorials : list<NameOfArticle * UrlPath>
    HowToGuides : list<NameOfArticle * UrlPath>
    Explanations : list<NameOfArticle * UrlPath>
}

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

let dropDownNavMenu text items =
            li [ Class "nav-item dropdown" ][
                a [
                    Id (sprintf "navbarDropdown-%s"  text)
                    Href "#"
                    DataToggle "dropdown"
                    AriaHasPopup true
                    AriaExpanded false
                    Class "nav-link dropdown-toggle" ]
                    [ str text ]
                ul [    HTMLAttr.Custom ("aria-labelledby", "dropdownMenu1")
                        Class "dropdown-menu border-0 shadow" ] items ]

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

type NavTree =
| File of title:string * link:string
| Folder of title: string * NavTree list

let navTreeFromPaths (rootPath : IO.DirectoryInfo) (files : IO.FileInfo list) =
    let rec addPath subFilePath parts nodes =
        // printfn "parts -> %A ... nodes -> %A" parts nodes
        match parts with
        | [] -> nodes
        | hp :: tp ->
            addHeadPath subFilePath hp tp nodes
    and addHeadPath subFilePath (hp : string) tp (nodes : NavTree list)=
        // printfn "hp -> %A ... tp -> %A ... nodes -> %A" hp tp nodes
        match nodes with
        | [] -> [
            if IO.Path.HasExtension hp then
                File(IO.Path.GetFileNameWithoutExtension hp, subFilePath)
            else
                Folder(hp, addPath subFilePath tp [])
            ]
        | Folder(title, subnodes) :: nodes when title = hp -> Folder(title, addPath subFilePath tp subnodes ) :: nodes
        | hn :: tn -> hn :: addHeadPath subFilePath hp tp tn

    ([], files)
    ||> List.fold(fun state file ->
        let subFilePath = file.FullName.Replace(rootPath.FullName, "")
        let pathParts = subFilePath.Split(IO.Path.DirectorySeparatorChar) |> Array.toList
        addPath subFilePath pathParts state
    )


let rootpath = IO.DirectoryInfo("/rootthing/another/docs/")

let files = [
    IO.FileInfo(rootpath.FullName + "Tutorials/Getting_Started.html")
    IO.FileInfo(rootpath.FullName + "Tutorials/Another_Tutorial.html")
    IO.FileInfo(rootpath.FullName + "Tutorials/OtherThings/Another_Tutorial3.html")
]

let foo = navTreeFromPaths rootpath files

let generateNavMenus (navTree : NavTree list) =
    let rec innerDo depth (navTree : NavTree list) =
        navTree
        |> List.map(fun nav ->
            match nav with
            | File (title, link) when depth = 0 -> navItem title link
            | File (title, link) -> dropDownNavItem title link
            | Folder (title, subtree) when depth = 0 ->
                innerDo (depth + 1) subtree
                |> dropDownNavMenu title
            | Folder (title, subtree) ->
                innerDo (depth + 1) subtree
                |> dropdownSubMenu title
        )
    innerDo 0 navTree


let fakeMenu = [
    Folder("Hover for action", [
        File("Level 1 - Item 1" , "#")
        Folder("Level 1 - Item 2", [
            File("Level 2 - Item 1" , "#")
            File("Level 2 - Item 2" , "#")
            Folder("Level 2 - Item 3", [
                File("Level 3 - Item 1" , "#")
                File("Level 3 - Item 2" , "#")
            ])
        ])
        File("Level 1 - Item 3" , "#")
        File("Level 1 - Item 3" , "#")

    ])
    File("About" , "#")
    File("Contact" , "#")
    File("Services" , "#")
]

let generateNav (gitRepoName : string) (topLevelNav : TopLevelNav) =

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
                yield! navTreeFromPaths rootpath files |> generateNavMenus
                // yield! generateNavMenus fakeMenu
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


