# Benchmarks

Measures frames and save states on DMG, CGB, and SGB.

```sh
make benchmark # Full run, Release
make benchmark BENCHMARK_ARGS="--filter '*' --job short" # Quick measurements
make benchmark BENCHMARK_ARGS="--filter '*FrameBenchmarks*'"
make benchmark BENCHMARK_ARGS="--filter '*CaptureSaveState*'"
make benchmark BENCHMARK_ARGS="--filter '*' --job Dry" # Validate all cases
```

Reports go to `artifacts/benchmarks/` (ignored by Git). The workspace also has a
`benchmark` task. Run without a debugger or other CPU-heavy work. Compare results
on the same machine and runtime, and record the commit and arguments.

Frames use the tracked acid2 ROMs, with 60 startup frames and a restored snapshot
before each 600-frame batch. Time and managed allocations are reported **per
frame**. Save states measure capture, encoding, decoding, restoration, compression,
and decompression separately. Setup checks repeatability and state round trips.

These fixed scenes exclude UI, disk I/O, pacing, and audio devices. The SGB scene
has no border commands. Batch averages do not reveal stutter. Use `dotnet-trace`
during gameplay to investigate actual pauses. Dry runs only validate execution,
and short runs are exploratory.
