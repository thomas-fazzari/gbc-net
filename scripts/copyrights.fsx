open System
open System.Collections.Generic
open System.Diagnostics
open System.Globalization
open System.IO
open System.Text
open System.Text.RegularExpressions

type GitStatus =
    | Renamed of source: string * destination: string
    | Copied of source: string * destination: string
    | Modified of path: string
    | Deleted
    | Ignored

type UpdateResult =
    | Unchanged
    | Changed
    | Failed of file: string * error: string

let normalizeGitPath (file: string) = file.Replace('\\', '/')

let copyrightHeader =
    Regex(
        @"^// Copyright \(C\) [0-9]{4}(?: |$)",
        RegexOptions.Compiled
        ||| RegexOptions.CultureInvariant
        ||| RegexOptions.ExplicitCapture,
        TimeSpan.FromSeconds 1.0
    )

let isSpdxLine (line: string) =
    line.StartsWith("// SPDX-License-Identifier: ", StringComparison.Ordinal)

let isHeaderLine line =
    isSpdxLine line || copyrightHeader.IsMatch line

let usage (writer: TextWriter) =
    writer.WriteLine(
        """Usage: dotnet fsi scripts/copyrights.fsx -- [--check]
Adds GPL-3.0-only headers to tracked C# files using per-file git author names."""
    )

let runGit (arguments: string array) =
    let startInfo = ProcessStartInfo("git")
    startInfo.RedirectStandardError <- true
    startInfo.RedirectStandardOutput <- true
    startInfo.UseShellExecute <- false

    for argument in arguments do
        startInfo.ArgumentList.Add argument

    use proc =
        Process.Start startInfo
        |> Option.ofObj
        |> Option.defaultWith (fun () -> invalidOp "git failed to start.")

    let output = proc.StandardOutput.ReadToEnd()
    let error = proc.StandardError.ReadToEnd()
    proc.WaitForExit()

    if proc.ExitCode <> 0 then
        invalidOp (error.Trim())

    output

let parseStatus (fields: string array) =
    match fields with
    | [| status; source; destination |] when status.StartsWith("R", StringComparison.Ordinal) ->
        Renamed(source, destination)
    | [| status; source; destination |] when status.StartsWith("C", StringComparison.Ordinal) ->
        Copied(source, destination)
    | [| status; _ |] when status.StartsWith("D", StringComparison.Ordinal) -> Deleted
    | [| _; path |] -> Modified path
    | _ -> Ignored

let readContributorsByFile () =
    let contributors = Dictionary<string, ResizeArray<string>>(StringComparer.Ordinal)

    let addContributor file contributor =
        let file = normalizeGitPath file
        let exists, fileContributors = contributors.TryGetValue file

        let fileContributors =
            if exists then
                fileContributors
            else
                let created = ResizeArray<string>()
                contributors.Add(file, created)
                created

        if
            not (
                fileContributors
                |> Seq.exists (fun existing -> StringComparer.Ordinal.Equals(existing, contributor))
            )
        then
            fileContributors.Add contributor

    let copyContributors source destination removeSource =
        let source = normalizeGitPath source
        let destination = normalizeGitPath destination

        match contributors.TryGetValue source with
        | false, _ -> ()
        | true, sourceContributors ->
            for contributor in Seq.toArray sourceContributors do
                addContributor destination contributor

            if removeSource then
                contributors.Remove source |> ignore

    let mutable author = String.Empty

    let logOutput =
        runGit
            [| "log"
               "--reverse"
               "--format=%x1e%aN"
               "--name-status"
               "-M"
               "--"
               "*.cs" |]

    let logLines =
        logOutput.Split([| '\r'; '\n' |], StringSplitOptions.RemoveEmptyEntries)

    logLines
    |> Array.iter (fun line ->
        if line[0] = '\u001e' then
            author <- line[1..]
        else if author.Length > 0 then
            match parseStatus (line.Split '\t') with
            | Renamed(source, destination) ->
                copyContributors source destination true
                addContributor destination author
            | Copied(source, destination) ->
                copyContributors source destination false
                addContributor destination author
            | Modified path -> addContributor path author
            | Deleted
            | Ignored -> ())

    contributors

let contributorsFor (contributorsByFile: Dictionary<string, ResizeArray<string>>) file =
    match contributorsByFile.TryGetValue(normalizeGitPath file) with
    | true, contributors when contributors.Count > 0 -> " " + String.Join(", ", contributors)
    | _ -> String.Empty

let readFileWithEncoding (path: string) =
    let defaultEncoding =
        UTF8Encoding(encoderShouldEmitUTF8Identifier = false) :> Encoding

    use reader =
        new StreamReader(path, defaultEncoding, detectEncodingFromByteOrderMarks = true)

    let text = reader.ReadToEnd()
    text, reader.CurrentEncoding

let updateFile check year license contributorsByFile file : UpdateResult =
    try
        let text, encoding = readFileWithEncoding file

        let newline =
            if text.Contains("\r\n", StringComparison.Ordinal) then
                "\r\n"
            else
                "\n"

        let lines = ResizeArray(text.Split([| "\r\n"; "\n" |], StringSplitOptions.None))

        if lines.Count > 0 && lines[lines.Count - 1].Length = 0 then
            lines.RemoveAt(lines.Count - 1)

        let mutable index = 0

        while index < lines.Count && isHeaderLine lines[index] do
            index <- index + 1

            if index < lines.Count && isSpdxLine lines[index] then
                index <- index + 1

            if index < lines.Count && lines[index].Length = 0 then
                index <- index + 1

        let header =
            [| $"// Copyright (C) {year}{contributorsFor contributorsByFile file}"
               $"// SPDX-License-Identifier: {license}"
               String.Empty |]

        let newText =
            String.Join(newline, Seq.append header (lines |> Seq.skip index)) + newline

        if String.Equals(newText, text, StringComparison.Ordinal) then
            Unchanged
        else
            if not check then
                File.WriteAllText(file, newText, encoding)

            Changed
    with ex ->
        Failed(file, ex.Message)

let args = fsi.CommandLineArgs |> Array.skip 1

let check =
    match args with
    | [||] -> false
    | [| "--check" |] -> true
    | [| "-h" |]
    | [| "--help" |] ->
        usage Console.Out
        exit 0
    | _ ->
        usage Console.Error
        exit 2

let year =
    let value = Environment.GetEnvironmentVariable "COPYRIGHT_YEAR"

    if String.IsNullOrWhiteSpace value then
        DateTimeOffset.Now.Year.ToString CultureInfo.InvariantCulture
    else
        value

let license = "GPL-3.0-only"

let run () =
    let lsFilesOutput = runGit [| "ls-files"; "-z"; "--"; "*.cs" |]

    let files =
        lsFilesOutput.Split('\u0000', StringSplitOptions.RemoveEmptyEntries)
        |> Array.filter File.Exists

    let contributorsByFile = readContributorsByFile ()

    let results =
        files
        |> Array.Parallel.map (fun file -> file, updateFile check year license contributorsByFile file)

    let changedFiles =
        results
        |> Array.choose (function
            | file, Changed -> Some file
            | _ -> None)
        |> Array.sortWith (fun left right -> StringComparer.Ordinal.Compare(left, right))

    let failures =
        results
        |> Array.choose (function
            | _, Failed(file, message) -> Some(file, message)
            | _ -> None)
        |> Array.sortWith (fun (left, _) (right, _) -> StringComparer.Ordinal.Compare(left, right))

    for file, message in failures do
        Console.Error.WriteLine $"warning: skipped {file}: {message}"

    if check then
        if changedFiles.Length > 0 then
            changedFiles |> Array.iter Console.WriteLine
            Console.Error.WriteLine $"{changedFiles.Length} C# files need license header updates."
            1
        elif failures.Length > 0 then
            1
        else
            Console.WriteLine "All C# files have current license headers."
            0
    else
        Console.WriteLine $"Updated {changedFiles.Length} C# files."
        if failures.Length > 0 then 1 else 0

try
    exit (run ())
with ex ->
    Console.Error.WriteLine $"error: {ex.Message}"
    exit 1
