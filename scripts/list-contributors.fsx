open System
open System.Collections.Generic
open System.Diagnostics

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

let run () =
    let logOutput = runGit [| "log"; "--all"; "--format=%aN" |]
    let lines = logOutput.Split([| '\r'; '\n' |], StringSplitOptions.RemoveEmptyEntries)
    let uniqueContributors = HashSet<string> StringComparer.OrdinalIgnoreCase

    for line in lines do
        let name = line.Trim()

        if name.Length > 0 then
            uniqueContributors.Add(name) |> ignore

    let sortedContributors = uniqueContributors |> Seq.toArray |> Array.sort

    Console.WriteLine $"Total : {sortedContributors.Length} contributors(s)"
    Console.WriteLine(String.replicate 40 "-")

    for contributor in sortedContributors do
        Console.WriteLine contributor

    0

try
    exit (run ())
with ex ->
    Console.Error.WriteLine $"error: {ex.Message}"
    exit 1
