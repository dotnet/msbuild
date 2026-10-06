# Experimental command-line parsing API

`Microsoft.Build.CommandLine.Experimental.CommandLineParser` is an internal API shared with the .NET SDK.
`Parse(IReadOnlyList<string>, CommandLineParsingOptions)` accepts tokens without an executable path.
The existing `Parse(IEnumerable<string>)` overload remains available.

All options default to `true`, which preserves existing parsing behavior:

| Option | Behavior when `false` |
| --- | --- |
| `ThrowOnUnknownSwitches` | Collect unknown switch tokens in `UnrecognizedArguments` and continue parsing known switches. |
| `ReadResponseFiles` | Do not read explicit or automatic response files. Return explicit `@file` tokens in `UnexpandedResponseFileArguments`. |
| `ReadLoggingArgumentsFromEnvironment` | Do not include switches from `MSBUILD_LOGGING_ARGS`. |

For token-only classification, set all three options to `false`:

```csharp
CommandLineSwitchesAccessor result = parser.Parse(tokens, new CommandLineParsingOptions
{
    ThrowOnUnknownSwitches = false,
    ReadResponseFiles = false,
    ReadLoggingArgumentsFromEnvironment = false
});
```

Both returned token collections preserve original quoting, input order, and duplicates.
When response-file reading is enabled, `UnrecognizedArguments` also includes unknown tokens from those files.
Explicit response files expand at their position in the input.
Automatic response-file switches have lower precedence than command-line switches.
The API preserves existing automatic response-file discovery. It does not add the CLI's separate project-directory discovery step.

Unknown-switch collection does not suppress other errors.
Malformed known switches, duplicate project arguments, and response-file errors still cause exceptions.
Non-switch tokens still follow MSBuild's project-argument rules.
When response-file reading is disabled, even missing or repeated `@file` tokens remain unexpanded without errors.
`-noautoresponse` retains its existing meaning: it disables automatic response files, but not explicit response files.

Options apply to one parse call and are not retained by the parser.
Later calls do not change earlier results.
The MSBuild CLI retains strict parsing and its existing response-file behavior.
