namespace MiniScaffold.Tests

module DependabotConfig =
    open System.Collections.Generic
    open System.IO
    open Expecto

    type IgnoreRule = {
        DependencyName: string
        Keys: string Set
    }

    type Group = {
        Name: string
        Keys: string Set
        Patterns: string list
        GroupBy: string option
    }

    type Update = {
        Ecosystem: string
        Directories: string list
        Interval: string
        Ignore: IgnoreRule list
        Groups: Group list
    }

    type Config = {
        Version: string
        Updates: Update list
    }

    let private mapping path (value: obj) =
        match value with
        | :? IDictionary<obj, obj> as map -> map
        | other -> failtestf "%s should hold a YAML mapping, not %A" path other

    let private sequence path (value: obj) =
        match value with
        | :? IList<obj> as items -> List.ofSeq items
        | other -> failtestf "%s should hold a YAML sequence, not %A" path other

    let private keys (map: IDictionary<obj, obj>) =
        map.Keys
        |> Seq.map string
        |> Set.ofSeq

    let private required (key: string) path (map: IDictionary<obj, obj>) =
        match map.TryGetValue key with
        | true, value -> value
        | _ -> failtestf "%s should have the key %s" path key

    let private optional key path parse (map: IDictionary<obj, obj>) =
        match map.TryGetValue key with
        | true, value -> parse $"{path}.{key}" value
        | _ -> []

    let private ignoreRule path value =
        let rule = mapping path value

        {
            DependencyName = string (required "dependency-name" path rule)
            Keys = keys rule
        }

    let private group path (name: obj, value: obj) =
        let settings = mapping $"{path}.{name}" value

        {
            Name = string name
            Keys = keys settings
            Patterns =
                settings
                |> optional
                    "patterns"
                    path
                    (fun p v ->
                        sequence p v
                        |> List.map string
                    )
            GroupBy =
                match settings.TryGetValue "group-by" with
                | true, value -> Some(string value)
                | _ -> None
        }

    let private update path value =
        let block = mapping path value

        {
            Ecosystem = string (required "package-ecosystem" path block)
            Directories =
                match block.TryGetValue "directory", block.TryGetValue "directories" with
                | (true, directory), _ -> [ string directory ]
                | _, (true, directories) ->
                    sequence $"{path}.directories" directories
                    |> List.map string
                | _ -> []
            Interval =
                let schedule =
                    required "schedule" path block
                    |> mapping $"{path}.schedule"

                string (required "interval" $"{path}.schedule" schedule)
            Ignore =
                block
                |> optional
                    "ignore"
                    path
                    (fun p v ->
                        sequence p v
                        |> List.map (ignoreRule p)
                    )
            Groups =
                block
                |> optional
                    "groups"
                    path
                    (fun p v ->
                        mapping p v
                        |> Seq.map (fun pair -> group p (pair.Key, pair.Value))
                        |> List.ofSeq
                    )
        }

    let read path =
        Expect.isTrue (File.Exists path) $"{path} should exist"

        let root =
            YamlDotNet.Serialization.Deserializer().Deserialize<obj>(File.ReadAllText path)
            |> mapping path

        {
            Version = string (required "version" path root)
            Updates =
                required "updates" path root
                |> sequence $"{path}.updates"
                |> List.map (update $"{path}.updates")
        }
