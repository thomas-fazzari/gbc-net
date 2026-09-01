open System
open System.Diagnostics
open System.Globalization
open System.IO
open System.Text
open System.Text.RegularExpressions

type UpdateResult =
    | Unchanged
    | Changed
    | Failed of error: string

let copyrightHeader =
    Regex(
        @"^// Copyright \(C\) [0-9]{4}(?: |$)",
        RegexOptions.Compiled
        ||| RegexOptions.CultureInvariant
        ||| RegexOptions.ExplicitCapture,
        TimeSpan.FromSeconds 1.0
    )

let license = "GPL-3.0-only"

let isSpdxLine (line: string) =
    line.StartsWith("// SPDX-License-Identifier: ", StringComparison.Ordinal)

let isHeaderLine line =
    isSpdxLine line || copyrightHeader.IsMatch line

let skipExistingHeader (lines: ResizeArray<string>) =
    let mutable index = 0

    while index < lines.Count && isHeaderLine lines[index] do
        index <- index + 1

    if index < lines.Count && lines[index].Length = 0 then
        index + 1
    else
        index

let usage (writer: TextWriter) =
    writer.WriteLine(
        """Usage: dotnet fsi scripts/copyrights.fsx -- [--check]
Adds GPL-3.0-only headers to tracked C# files."""
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

let readFileWithEncoding (path: string) =
    use reader =
        new StreamReader(
            path,
            UTF8Encoding(encoderShouldEmitUTF8Identifier = false),
            detectEncodingFromByteOrderMarks = true
        )

    let text = reader.ReadToEnd()
    text, reader.CurrentEncoding

let updateFile check year file : UpdateResult =
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

        let contentStart = skipExistingHeader lines

        let header =
            [| $"// Copyright (C) {year} GBC.Net Contributors"
               $"// SPDX-License-Identifier: {license}"
               String.Empty |]

        let newText =
            String.Join(newline, Seq.append header (lines |> Seq.skip contentStart))
            + newline

        if String.Equals(newText, text, StringComparison.Ordinal) then
            Unchanged
        else
            if not check then
                File.WriteAllText(file, newText, encoding)

            Changed
    with ex ->
        Failed ex.Message

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

let readYear () =
    let value = Environment.GetEnvironmentVariable "COPYRIGHT_YEAR"

    if String.IsNullOrWhiteSpace value then
        DateTimeOffset.Now.Year.ToString CultureInfo.InvariantCulture
    elif value.Length <> 4 || not (value |> Seq.forall Char.IsAsciiDigit) then
        invalidOp "COPYRIGHT_YEAR must contain exactly four digits."
    else
        value

let run () =
    let year = readYear ()
    let lsFilesOutput = runGit [| "ls-files"; "-z"; "--"; "*.cs" |]

    let files =
        lsFilesOutput.Split('\u0000', StringSplitOptions.RemoveEmptyEntries)
        |> Array.filter File.Exists

    let results =
        files |> Array.Parallel.map (fun file -> file, updateFile check year file)

    let changedFiles =
        results
        |> Array.choose (function
            | file, Changed -> Some file
            | _ -> None)
        |> Array.sortWith (fun left right -> StringComparer.Ordinal.Compare(left, right))

    let failures =
        results
        |> Array.choose (function
            | file, Failed message -> Some(file, message)
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
