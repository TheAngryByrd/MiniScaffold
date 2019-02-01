open FSharp.Literate
open System.Collections.Generic
open System.Reflection
open Fable.Import.React
#load ".fake/build.fsx/intellisense.fsx"
#if !FAKE
#r "Facades/netstandard"
#r "netstandard"
#endif

open System
open System.IO
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
let docsApiDir = docsDir @@ "api"
let docsSrcDir = __SOURCE_DIRECTORY__ @@ "docsSrc"
let docsSrcGlob = docsSrcDir @@ "**/*.fsx"

type FProps = (string * IDictionary<string, string>) list


let render html =
    fragment [] [
        RawText "<!doctype html>"
        RawText "\n"
        html ]
    |> Fable.Helpers.ReactServer.renderToString



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

let masterTemplate gitRepoName navBar titletext bodyText =
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
            yield navBar
            yield! bodyText
            yield script [
                Src "https://code.jquery.com/jquery-3.2.1.slim.min.js"
                Integrity "sha384-KJ3o2DKtIkvYIK3UENzmM7KCkRr/rE9/Qpg6aAZGJwFDMVNA/GpGFF93hXpG5KkN"
                CrossOrigin "anonymous"
                ] []
            yield script [
                Src "https://cdnjs.cloudflare.com/ajax/libs/popper.js/1.12.9/umd/popper.min.js"
                Integrity "sha384-ApNbgh9B+Y1QKtv3Rn7W3mgPxhU9K/ScQsAP7hUibX39j7fakFPskvXusvfa0b4Q"
                CrossOrigin "anonymous"
                ] []
            yield script [
                Src "https://maxcdn.bootstrapcdn.com/bootstrap/4.0.0/js/bootstrap.min.js"
                Integrity "sha384-JZR6Spejh4U02d8jOt6vLEHfe/JQGiRRSQQxSfFWpi1MquVdAyjUar5+76PVCmYl"
                CrossOrigin "anonymous"
                ] []
            yield script [Src "/content/tips.js" ] []
        ]
    ]


let renderWithMasterTemplate navBar titletext bodytext =
    masterTemplate "gitRepoName" navBar titletext bodytext
    |> render

let renderWithMasterAndWrite (outPath : FileInfo) navBar titletext bodytext   =
    let contents = renderWithMasterTemplate navBar titletext bodytext
    IO.Directory.CreateDirectory(outPath.DirectoryName) |> ignore

    IO.File.WriteAllText(outPath.FullName, contents)
    Fake.Core.Trace.tracefn "Rendered to %s" outPath.FullName

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

let baseDir = Path.GetFullPath "."
let dllsAndLibDirs (dllPattern:IGlobbingPattern) =
    let dlls =
        dllPattern
        |> GlobbingPattern.setBaseDir baseDir
        |> Seq.distinctBy Path.GetFileName
        |> List.ofSeq
    let libDirs =
        dlls
        |> Seq.map Path.GetDirectoryName
        |> Seq.distinct
        |> List.ofSeq
    (dlls, libDirs)

type ByCategory = {
    Name : string
    Index : string
    Types : Type array
    Modules : Module array
}

let partNested (types : Type array) (modules : Module array) =
    [
        if types.Length > 0 then
            yield table [] [
                thead [Class "table table-bordered type-list"] [
                    tr [] [
                        td [] [
                            str "Type"
                        ]
                        td [] [
                            str "Description"
                        ]
                    ]
                ]
                tbody [] [
                    for t in types do
                        yield tr [] [
                            td [Class "type-name"] [
                                a [Href (sprintf "%s.html" t.UrlName)] [
                                    str t.Name
                                ]
                            ]
                            td [Class "xmdoc"] [
                                if t.IsObsolete then
                                    yield div [Class "alert alert-warning"] [
                                        strong [] [
                                            str "WARNING:"
                                        ]
                                        str "This API is obsolete"
                                        p [] [
                                            str t.ObsoleteMessage
                                        ]
                                    ]
                                yield RawText t.Comment.Blurb
                            ]
                        ]
                ]
            ]
        if modules.Length > 0 then
            yield table [] [
                thead [Class "table table-bordered type-list"] [
                    tr [] [
                        td [] [
                            str "Module"
                        ]
                        td [] [
                            str "Description"
                        ]
                    ]
                ]
                tbody [] [
                    for t in modules do
                        yield tr [] [
                            td [Class "Modules-name"] [
                                a [Href (sprintf "%s.html" t.UrlName)] [
                                    str t.Name
                                ]
                            ]
                            td [Class "xmdoc"] [
                                if t.IsObsolete then
                                    yield div [Class "alert alert-warning"] [
                                        strong [] [
                                            str "WARNING:"
                                        ]
                                        str "This API is obsolete"
                                        p [] [
                                            str t.ObsoleteMessage
                                        ]
                                    ]
                                yield RawText t.Comment.Blurb
                            ]
                        ]
                ]
            ]
    ]

let generateNamespaceDocs (asm : AssemblyGroup) (props : FProps) =
    let parts =
        asm.Namespaces
        |> Seq.mapi(fun nsi ns ->
            let allByCategories =
                ns.Types
                |> Seq.map(fun t -> t.Category)
                |> Seq.append (ns.Modules |> Seq.map(fun m -> m.Category))
                |> Seq.distinct
                |> Seq.sortBy(fun s ->
                    if String.IsNullOrEmpty(s) then "ZZZ"
                    else s)
                |> Seq.mapi(fun ci c ->
                {
                    Name = if String.IsNullOrEmpty(c) then "Other namespace members" else c
                    Index = sprintf "%d_%d" nsi ci
                    Types = ns.Types |> Seq.filter(fun t -> t.Category = c) |> Seq.toArray
                    Modules = ns.Modules |> Seq.filter(fun m -> m.Category = c) |> Seq.toArray
                })
                |> Seq.filter(fun c -> c.Types.Length + c.Modules.Length > 0)
                |> Seq.toArray
            [
                yield h2 [] [
                    str ns.Name
                ]
                if allByCategories.Length > 1 then
                    yield ul [] [
                        for c in allByCategories do
                            yield li [] [
                                a [Href (sprintf "#section%s" c.Index)] [
                                    str c.Name
                                ]
                                ]
                    ]


                for c in allByCategories do
                    if allByCategories.Length > 1 then
                        yield h3 [] [
                            a  [Class "anchor"; Name (sprintf "section%s" c.Index); Href (sprintf "#section%s" c.Index)] [
                                str c.Name
                            ]
                        ]
                    yield! partNested c.Types c.Modules
            ]
        )
        |> Seq.collect id
    div [] [
        yield h1 [] [
            str asm.Name
        ]
        yield! parts
    ]

type ModuleByCategory = {
    Index : int
    GroupKey : string
    Members : seq<Member>
    Name : string
}

let partMembers (header : string) (tableHeader : string) (members : #seq<Member>) = [
    if members |> Seq.length > 0 then
        yield h3 [] [
            str header
        ]

        yield table [
            Class "table table-bordered member-list"
        ] [
            thead [] [
                tr [] [
                    td [] [
                        str tableHeader
                    ]

                    td [] [
                        str "Description"
                    ]
                ]
            ]

            tbody [] [
                for it in members do
                    let id = Guid.NewGuid().ToString()
                    yield tr [] [
                        td [
                            Class "member-name"
                        ] [
                            code [
                                Class "function-or-value"
                                HTMLAttr.Custom("data-guid", id)
                            ] [
                                str (it.Details.FormatUsage(40))
                            ]

                            div [
                                Class "tip"
                                Id id
                            ] [
                                yield strong [] [
                                    str "Signature:"
                                ]

                                yield str it.Details.Signature

                                yield br []

                                if it.Details.Modifiers |> Seq.isEmpty then
                                    yield strong [] [
                                        str "Modifiers:"
                                    ]

                                    yield str it.Details.FormatModifiers

                                    yield br []

                                if it.Details.TypeArguments |> Seq.isEmpty then
                                    yield strong [] [
                                        str "Type parameters:"
                                    ]

                                    yield str it.Details.FormatTypeArguments

                                if it.Attributes |> Seq.isEmpty |> not then
                                    yield span [] [
                                        yield strong [] [
                                            str "Attributes:"
                                        ]

                                        yield br []

                                        for attr in it.Attributes do
                                            yield str (attr.Format())

                                            yield br []
                                    ]
                            ]

                        ]

                        td [
                            Class "xmldoc"
                        ] [
                            if it.IsObsolete then
                                yield div [
                                    Class "alert alert-warning"
                                ] [
                                    strong [] [
                                        str "WARNING:"
                                    ]

                                    str "This API is obsolete"

                                    p [] [
                                        str it.ObsoleteMessage
                                    ]
                                ]

                            if it.Details.FormatSourceLocation |> String.IsNullOrEmpty |> not then
                                yield a [
                                    Href it.Details.FormatSourceLocation
                                    Class "github-link"
                                ] [
                                    yield img [
                                        Src "../content/img/github.png"
                                        Class "github-link"
                                    ]

                                    yield img [
                                        Src "../content/img/github-blue.png"
                                        Class "normal"
                                    ]

                                    yield RawText it.Comment.FullText

                                    if it.Details.FormatCompiledName |> String.isNotNullOrEmpty |> not then
                                        yield p [] [
                                            str "CompiledName: "

                                            code [] [
                                                str it.Details.FormatCompiledName
                                            ]
                                        ]
                                ]
                        ]
                    ]
            ]
        ]
]


let generateModuleDocs (moduleInfo : ModuleInfo) (props : FProps) =
    let members = moduleInfo.Module.AllMembers
    let comment = moduleInfo.Module.Comment

    let byCategory =
        members
        |> Seq.groupBy(fun m -> m.Category)
        |> Seq.sortBy(fun (g,v)  -> if String.IsNullOrEmpty g then "ZZZ" else g)
        |> Seq.mapi(fun i (key, value) -> {
            Index = i
            GroupKey = key
            Members = value |> Seq.sortBy(fun m -> m.Name)
            Name = if String.IsNullOrEmpty key then "Other module members" else key
        })
    let nestModules = moduleInfo.Module.NestedModules
    let nestTypes = moduleInfo.Module.NestedTypes
    [
        yield h1 [] [
            str moduleInfo.Module.Name
        ]
        yield p [] [
            if moduleInfo.Module.IsObsolete then
                yield div [
                    Class "alert alert-warning"
                ] [
                    strong [] [
                        str "WARNING: "
                    ]

                    str " This API is obsolete"

                    p [] [
                        str moduleInfo.Module.ObsoleteMessage
                    ]
                ]
            yield span [] [
                str (sprintf "Namespace: %s" moduleInfo.Namespace.Name)
            ]
            yield br []
            if moduleInfo.ParentModule.IsSome then
                yield
                    span [] [
                        str "Parent Module: "

                        a [
                            Href (sprintf "%s.html" moduleInfo.ParentModule.Value.UrlName)
                        ] [
                            str moduleInfo.ParentModule.Value.Name
                        ]
                    ]
            if moduleInfo.Module.Attributes |> Seq.isEmpty |> not then
                yield
                    span [] [
                        yield str "Attributes:"
                        yield br []
                        for attr in moduleInfo.Module.Attributes do
                            yield str (attr.Format())
                            yield br []
                    ]
        ]

        yield div [
            Class "xmldoc"
        ] [
            for sec in comment.Sections do
                if byCategory |> Seq.exists (fun g -> g.GroupKey = sec.Key) |> not then
                    if sec.Key <> "<default>" then
                        yield h2 [] [
                            RawText sec.Key
                        ]
                    yield RawText sec.Value
        ]


        if byCategory |> Seq.length > 1 then
            yield h2 [] [
                str "Table of contents"
            ]

            yield ul [] [
                for g in byCategory do
                    yield li [] [
                        a [
                            Href (g.Index.ToString() |> sprintf "#section%s")
                        ] [
                            str g.Name
                        ]
                    ]
            ]

        if (nestTypes |> Seq.length) + (nestModules |> Seq.length) > 0 then
            yield h2 [] [
                str "Nexted types and modules"
            ]

            yield div [] (partNested (nestTypes |> Seq.toArray) (nestModules |> Seq.toArray))

        for g in byCategory do
            if byCategory |> Seq.length > 1 then
                yield h2 [] [
                    str g.Name
                    a [
                        Name (sprintf "section%d" g.Index)
                    ] [
                        str "&#160;"
                    ]
                ]

            let info = comment.Sections |> Seq.tryFind(fun kvp -> kvp.Key = g.GroupKey)

            match info with
            | Some info ->
                yield div [
                    Class "xmldoc"
                ] [
                    str info.Value
                ]
            | None ->
                yield nothing

            yield! partMembers "Functions and values" "Function or value" (g.Members |> Seq.filter(fun m -> m.Kind = MemberKind.ValueOrFunction))

            yield! partMembers "Type extensions" "Type extension" (g.Members |> Seq.filter(fun m -> m.Kind = MemberKind.TypeExtension))

            yield! partMembers "Active patterns" "Active pattern" (g.Members |> Seq.filter(fun m -> m.Kind = MemberKind.ActivePattern))
    ]



let generateTypeDocs (model : TypeInfo) (props : FProps) =
    let members = model.Type.AllMembers
    let comment = model.Type.Comment
    let ``type`` = model.Type
    let byCategory =
        members
        |> Seq.groupBy (fun m -> m.Category)
        |> Seq.sortBy (fun (k,v) -> if String.IsNullOrEmpty(k) then "ZZZ" else k )
        |> Seq.mapi (fun i (k,v) -> {
            Index = i
            GroupKey = k
            Members = v |> Seq.sortBy (fun m -> if m.Kind = MemberKind.StaticParameter then "" else m.Name)
            Name = if String.IsNullOrEmpty(k) then "Other type members" else k
        })
    [
        yield h1 [] [
            str model.Type.Name
        ]

        yield p [] [
            if model.Type.IsObsolete then
                yield div [
                    Class "alert alert-warning"
                ] [
                    strong [] [
                        str "WARNING: "
                    ]

                    str " This API is obsolete"
                ]

            yield span [] [
                str (sprintf "Namespace: %s" model.Namespace.Name)
            ]

            yield br []

            if model.HasParentModule then
                yield span [] [
                    str "Parent Module: "

                    a [
                        Href (sprintf "%s.html" model.ParentModule.Value.UrlName)
                    ] [
                        str model.ParentModule.Value.Name
                    ]
                ]

                yield br []

            if ``type``.Attributes |> Seq.isEmpty |> not then
                yield span [] [
                    yield str "Attributes: "

                    yield br []

                    for attr in ``type``.Attributes do
                        yield str (attr.Format())
                        yield br []
                ]
        ]

        yield div [
            Class "xmldoc"
        ] [
            for sec in comment.Sections do
                if byCategory |> Seq.exists (fun m -> m.GroupKey = sec.Key) |> not then
                    if sec.Key <> "<default>" then
                        yield h2 [] [
                            str sec.Key
                        ]
                    yield RawText sec.Value
        ]

        if byCategory |> Seq.length > 1 then
            yield h2 [] [
                str "Table of contents"
            ]

            yield ul [] [
                for g in byCategory do
                    yield li [] [
                        a [
                            Href (sprintf "#section%d" g.Index)
                        ] [
                            str g.Name
                        ]
                    ]
            ]

        for g in byCategory do
            if byCategory |> Seq.length > 1 then
                yield h2 [] [
                    str g.Name

                    a [
                        Name (sprintf "section%d" g.Index)
                    ] [
                        str "&#160;"
                    ]
                ]

                match comment.Sections |> Seq.tryFind (fun kvp -> kvp.Key = g.GroupKey) with
                | Some info ->
                    yield div [
                        Class "xmldoc"
                    ] [
                        str info.Value
                    ]
                | None -> yield nothing

            yield! partMembers "Union Cases" "Union Case" (g.Members |> Seq.filter(fun m -> m.Kind = MemberKind.UnionCase))
            yield! partMembers "Record Fields" "Record Field" (g.Members |> Seq.filter(fun m -> m.Kind = MemberKind.RecordField))
            yield! partMembers "Static parameters" "Static parameters" (g.Members |> Seq.filter(fun m -> m.Kind = MemberKind.StaticParameter))
            yield! partMembers "Contructors" "Constructor" (g.Members |> Seq.filter(fun m -> m.Kind = MemberKind.Constructor))
            yield! partMembers "Instance members" "Instance member" (g.Members |> Seq.filter(fun m -> m.Kind = MemberKind.InstanceMember))
            yield! partMembers "Static members" "Static member" (g.Members |> Seq.filter(fun m -> m.Kind = MemberKind.StaticMember))
    ]


let generateAPI gitRepoName (dllGlob : IGlobbingPattern) =
    let dlls, libDirs = dllsAndLibDirs dllGlob
    let fsharpCoreDir = locateDLL "FSharp.Core" "netstandard1.6"
    let mscorlibDir =
        (Uri(typedefof<System.Runtime.MemoryFailPoint>.GetType().Assembly.CodeBase)) //Find runtime dll
            .AbsolutePath // removes file protocol from path
            |> Path.GetDirectoryName

    let libDirs = fsharpCoreDir :: mscorlibDir  :: libDirs
    // printfn "%A" dlls
    // printfn "%A" libDirs
    let generatorOutput = MetadataFormat.Generate(dlls, libDirs = libDirs)
    // printfn "%A" generatorOutput
    // generatorOutput.AssemblyGroup.Namespaces
    let fi = FileInfo <| docsApiDir @@ "index.html"
    let nav = (generateNav gitRepoName)
    [generateNamespaceDocs generatorOutput.AssemblyGroup generatorOutput.Properties]
    |> renderWithMasterAndWrite fi nav "apiDocs"
    generatorOutput.ModuleInfos
    |> List.iter (fun m ->
        let fi = FileInfo <| docsApiDir @@ (sprintf "%s.html" m.Module.UrlName)
        generateModuleDocs m generatorOutput.Properties
        |> renderWithMasterAndWrite fi nav (sprintf "%s-%s" m.Module.Name gitRepoName)
    )
    generatorOutput.TypesInfos
    |> List.iter (fun m ->
        let fi = FileInfo <| docsApiDir @@ (sprintf "%s.html" m.Type.UrlName)
        generateTypeDocs m generatorOutput.Properties
        |> renderWithMasterAndWrite fi nav (sprintf "%s-%s" m.Type.Name gitRepoName)
    )








let copyAssets () =
    Shell.copyDir (docsDir </> "content")   ( docsSrcDir </> "content") (fun _ -> true)
    Shell.copyDir (docsDir </> "files")   ( docsSrcDir </> "files") (fun _ -> true)



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
            [div [] [
                fs
                |> RawText
            ]]

            |> renderWithMasterTemplate relativePaths outPath.Name
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

