#load ".fake/build.fsx/intellisense.fsx"
#if !FAKE
#r "Facades/netstandard"
#r "netstandard"
#endif

open System
open Paket
open Fake.SystemHelper
open Fake.Core
open Fake.DotNet
open Fake.Tools
open Fake.IO
open Fake.IO.FileSystemOperators
open Fake.IO.Globbing.Operators
open Fake.Core.TargetOperators
open Fake.Api
open FSharp.Literate
open FSharp.MetadataFormat
open Fable.Helpers.React
open Fable.Helpers.React.Props

let docsDir = __SOURCE_DIRECTORY__ @@ "docs"
let docsSrcDir = __SOURCE_DIRECTORY__ @@ "docsSrc"
let docsSrcGlob = docsSrcDir @@ "**/*.fsx"



let template gitRepoName navBar titletext bodytext =
    html [Lang "en"] [
        head [] [
            title [] [ str (sprintf "%s docs / %s" gitRepoName titletext) ]
            link [
                Href "https://maxcdn.bootstrapcdn.com/bootstrap/4.0.0/css/bootstrap.min.css"
                Type "text/css"
                Rel "stylesheet"
                Integrity "sha384-Gn5384xqQ1aoWXA+058RXPxPg6fy4IWvTNh0E263XmFcJlSAwiGgFAW/dAiS6JXm"
                CrossOrigin "anonymous"
            ]
            link [
                Href "/content/style.css"
                Type "text/css"
                Rel "stylesheet"
            ]

        ]
        body [] [
            navBar
            RawText bodytext
            script [
                Src "https://code.jquery.com/jquery-3.2.1.slim.min.js"
                Integrity "sha384-KJ3o2DKtIkvYIK3UENzmM7KCkRr/rE9/Qpg6aAZGJwFDMVNA/GpGFF93hXpG5KkN"
                CrossOrigin "anonymous"
                ] []
            script [
                Src "https://cdnjs.cloudflare.com/ajax/libs/popper.js/1.12.9/umd/popper.min.js"
                Integrity "sha384-ApNbgh9B+Y1QKtv3Rn7W3mgPxhU9K/ScQsAP7hUibX39j7fakFPskvXusvfa0b4Q"
                CrossOrigin "anonymous"
                ] []
            script [
                Src "https://maxcdn.bootstrapcdn.com/bootstrap/4.0.0/js/bootstrap.min.js"
                Integrity "sha384-JZR6Spejh4U02d8jOt6vLEHfe/JQGiRRSQQxSfFWpi1MquVdAyjUar5+76PVCmYl"
                CrossOrigin "anonymous"
                ] []
            script [Src "/content/tips.js" ] []
        ]
    ]



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
                navDropDown "Apis" []
            ]
        ]

    ]


let render html =
  fragment [] [
    RawText "<!doctype html>"
    RawText "\n"
    html ]
  |> Fable.Helpers.ReactServer.renderToString


let copyAssets () =
    Shell.copyDir (docsDir </> "content")   ( docsSrcDir </> "content") (fun _ -> true)
    Shell.copyDir (docsDir </> "files")   ( docsSrcDir </> "files") (fun _ -> true)


let locateDLL name rid =
    let lockFile = Paket.LockFile.LoadFrom Paket.Constants.LockFileName
    let packageName = Paket.Domain.PackageName name
    let (_,package,version) =
        lockFile.InstalledPackages
        |> Seq.filter(fun (_,p,_) ->
            p =  packageName
        )
        |> Seq.maxBy(fun (_,_,semver) -> semver)
    Paket.NuGetCache.GetTargetUserFolder package version </> "lib" </> rid

let generateDocs githubRepoName =
    // This finds the current fsharp.core version of your solution to use for fsharp.literate
    let fsharpCoreDir = locateDLL "FSharp.Core" "netstandard1.6"

    let parse fileName source =
        let doc =
          let fsharpCoreDir = sprintf "-I:%s" fsharpCoreDir
          let systemRuntime = "-r:System.Runtime"
          Literate.ParseScriptString(
                      source,
                      path = fileName,
                      compilerOptions = systemRuntime + " " + fsharpCoreDir,
                      fsiEvaluator = FSharp.Literate.FsiEvaluator([|fsharpCoreDir|]))
        FSharp.Literate.Literate.FormatLiterateNodes(doc, OutputKind.Html, "", true, true)

    let format (doc: LiterateDocument) =
        if not <| Seq.isEmpty doc.Errors
        then
            failwithf "error while formatting file %s. Errors are:\n%A" doc.SourceFile doc.Errors
        else
            Formatting.format doc.MarkdownDocument true OutputKind.Html
            + doc.FormattedTips

    let relativePaths = generateNav githubRepoName


    !! docsSrcGlob
    |> Seq.iter(fun filePath ->
        Fake.Core.Trace.tracefn "Rendering %s" filePath
        let file = IO.File.ReadAllText filePath
        let outPath =
            filePath.Replace(docsSrcDir, docsDir).Replace(".fsx", ".html")
            |> FileInfo
        let fs =
            file
            |> parse filePath
            |> format
        let contents =
            fs
            |> template "gitRepoName" relativePaths outPath.Name
            |> render
        IO.Directory.CreateDirectory(outPath.DirectoryName) |> ignore

        IO.File.WriteAllText(outPath.FullName, contents)
        Fake.Core.Trace.tracefn "Rendered %s to %s" filePath outPath.FullName

    )

    copyAssets()


open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Hosting
open Microsoft.Extensions.FileProviders

let openBrowser url =
    //https://github.com/dotnet/corefx/issues/10361
    let result =
        Process.execSimple (fun info ->
                { info with
                    FileName = url
                    UseShellExecute = true })
                TimeSpan.MaxValue
    if result <> 0 then failwithf "opening browser failed"


let waitForPortInUse (hostname : string) port =
    let mutable portInUse = false
    while not portInUse do
        Async.Sleep(10) |> Async.RunSynchronously
        use client = new Net.Sockets.TcpClient()
        try
            client.Connect(hostname,port)
            portInUse <- client.Connected
            client.Close()
        with e ->
            client.Close()

let startWebserver (url : string) =
    WebHostBuilder()
        .UseKestrel()
        .UseUrls(url)
        .Configure(fun app ->
            let opts =
                StaticFileOptions(
                    FileProvider =  new PhysicalFileProvider(docsDir)
                )
            app.UseStaticFiles(opts) |> ignore
        )
        .Build()
        .Run()

let serveDocs () =
    let hostname = "localhost"
    let port = 5000
    async {
        waitForPortInUse hostname port
        sprintf "http://%s:%d/index.html" hostname port |> openBrowser
    } |> Async.Start
    startWebserver (sprintf "http://%s:%d" hostname port)

