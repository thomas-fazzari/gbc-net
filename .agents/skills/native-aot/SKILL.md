---
name: native-aot
description: Native AOT compilation reference for .NET 8+ desktop apps (Avalonia, WPF, console) with native library dependencies (SQLite, audio). Use when configuring PublishAot, resolving IL2xxx/IL3xxx trim/AOT warnings, writing ILLink descriptors, choosing DllImport vs LibraryImport, optimizing binary size, or packaging AOT binaries into app bundles.
---

# .NET Native AOT — Desktop

Use this skill as a compact reference for Native AOT publishing decisions on desktop apps. Version baseline: .NET 8.0+.

## Rules

- Apps and libraries use different MSBuild switches — never mix them.
- Never use legacy RD.xml; it is silently ignored by modern AOT.
- Prefer `[LibraryImport]` over `[DllImport]` for all new P/Invoke code.
- State model scope explicitly when giving compatibility answers: app vs library, .NET version.
- If a feature's AOT support is partial, keep that uncertainty visible rather than declaring full support.
- Native dependencies (SQLite, audio backends) are the primary AOT risk on desktop — check their AOT compatibility before adding them.

## Required Workflow

1. Classify the task:
   - enabling/configuring PublishAot
   - trim/AOT analyzer warnings (IL2xxx/IL3xxx)
   - reflection removal / type preservation
   - P/Invoke marshalling (SQLite, audio, native interop)
   - size optimization
   - app bundle / packaging
2. Apply the matching pattern below.
3. Flag any deviation from the "Agent Gotchas" list before finalizing code.

## Configuration

**Apps:**

```xml
<PublishAot>true</PublishAot>
<EnableAotAnalyzer>true</EnableAotAnalyzer>
<EnableTrimAnalyzer>true</EnableTrimAnalyzer>
```

**Libraries** (never set `PublishAot` here):

```xml
<IsAotCompatible>true</IsAotCompatible>
<IsTrimmable>true</IsTrimmable>
```

**Check compatibility without publishing:**

```bash
dotnet build /p:EnableAotAnalyzer=true /p:EnableTrimAnalyzer=true
```

## Type Preservation

| Scenario                       | Approach                                        |
| ------------------------------ | ----------------------------------------------- |
| One or two members             | `[DynamicDependency]`                           |
| Whole assembly / many types    | ILLink descriptor XML (`TrimmerRootDescriptor`) |
| Your own reflection-heavy code | Refactor to source generators                   |

Never use RD.xml. Watch for Avalonia binding/reflection: prefer compiled bindings (`AvaloniaUseCompiledBindingsByDefault=true`) to avoid runtime reflection on `XAML` bindings.

## P/Invoke

Use `[LibraryImport]` (compile-time marshalling) instead of `[DllImport]`.

## Size Optimization

```xml
<StripSymbols>true</StripSymbols>
<InvariantGlobalization>true</InvariantGlobalization>
<OptimizationPreference>Size</OptimizationPreference>
```

Note: `InvariantGlobalization` can affect Avalonia text rendering/culture-specific formatting — verify UI behavior after enabling.

## Agent Gotchas

1. `PublishAot` in a library project — wrong, use `IsAotCompatible`.
2. Setting both `IsAotCompatible` and `PublishAot` on the same app project — redundant; `PublishAot` already enables the analyzers.
3. RD.xml for type preservation — ignored, use ILLink XML or `[DynamicDependency]`.
4. `[DllImport]` in new code — use `[LibraryImport]`.
5. Assuming a native dependency (SQLite, audio) is AOT-safe without checking upstream docs/issues.
6. `dotnet publish --no-actual-publish` — flag doesn't exist; use `dotnet build /p:EnableAotAnalyzer=true /p:EnableTrimAnalyzer=true`.

## References

- [Native AOT deployment](https://learn.microsoft.com/en-us/dotnet/core/deploying/native-aot/)
- [ILLink descriptor format](https://learn.microsoft.com/en-us/dotnet/core/deploying/trimming/trimming-options#descriptor-format)
- [LibraryImport source generation](https://learn.microsoft.com/en-us/dotnet/standard/native-interop/pinvoke-source-generation)
-
