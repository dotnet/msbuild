# MSBuild Server

MSBuild Server nodes accept build requests from clients and use worker nodes in the current fashion to build projects. The main purpose of the server node is to preserve caches between builds and avoid expensive MSBuild process start operations during build from tools like the .NET SDK.

## Usage

The primary ways to use MSBuild are via Visual Studio and via the CLI using the `dotnet build`/`dotnet msbuild` commands. MSBuild Server is not supported in Visual Studio because Visual Studio itself works like MSBuild Server. For the CLI, the server functionality is enabled by default and can be disabled by setting the `DOTNET_CLI_DO_NOT_USE_MSBUILD_SERVER` environment variable to value `1`.
To re-enable MSBuild Server, remove the variable or set its value to `0`.

## Garbage collection

When a build is multithreaded (`/mt`), the server node is launched with [Server GC](https://learn.microsoft.com/dotnet/standard/garbage-collection/workstation-server-gc) enabled. Under `/mt` the server runs all project work on threads in this single process, so Server GC's higher throughput is beneficial; without `/mt` the server only orchestrates and delegates project work to separate worker nodes, so it keeps the default Workstation GC. GC mode is fixed at CLR startup, so it is set via the `DOTNET_gcServer` environment variable in the server's launch environment (decided from the launching invocation's command line). An explicit user-set `DOTNET_gcServer` is honored (e.g. set `DOTNET_gcServer=0` to keep Workstation GC in a memory-constrained environment). This is scoped to the server process only: sidecar TaskHosts and worker nodes keep the default Workstation GC.

## Diagnostics: server lifecycle messages

At the start of each build that involves MSBuild Server, a single low-importance message records how the build relates to the server. These are only visible in a binary log (`-bl`) and at diagnostic verbosity (`-v:diag`); default console output is unchanged, and ordinary builds that never request the server log nothing.

| Situation | Message (English) |
|---|---|
| A freshly spawned server node serves its first build | `MSBuild Server node started for this build (process ID N).` |
| An already-running server node is reused | `Reusing the running MSBuild Server node for this build (process ID N).` |
| The server was requested but the build ran in-process | `MSBuild Server was requested but not used for this build: {reason}.` |

### Forward-compatible, structured payload

To let tooling recognize and render these without parsing localized text, each message is logged as an [`ExtendedBuildMessageEventArgs`](https://learn.microsoft.com/dotnet/api/microsoft.build.framework.extendedbuildmessageeventargs) rather than a plain message. This keeps the data forward-compatible in both directions:

- On the wire (in a binary log) the event is an ordinary `Message` record; the structured payload rides in an optional field. Binary-log readers and viewers that predate the payload — including older versions of https://msbuildlog.com — still surface the human-readable text as a build message instead of skipping an unknown record kind.
- Because the event still derives from `BuildMessageEventArgs`, the console at `-v:diag` prints the same human-readable text as before.
- Current readers reconstruct the structured data, which newer tooling can render specially.

The contract is stable and versionable (new metadata keys can be added over time):

| Field | Value |
|---|---|
| `ExtendedType` | `msbuild-server` |
| `ExtendedMetadata["kind"]` | `spawned`, `reused`, or `not-used` (stable, non-localized) |
| `ExtendedMetadata["processId"]` | The server node's process ID (for `spawned`/`reused`) |
| `ExtendedMetadata["reason"]` | The localized fall-back reason (for `not-used`) |
| `ExtendedMetadata["reasonCode"]` | A stable, non-localized code for the fall-back cause (for `not-used`): `node-reuse-disabled`, `stdout-redirected`, `incompatible-invocation`, `command-line-parse-error`, `server-busy`, `server-crashed`, `server-state-unknown`, or `server-unreachable` |
| `Message` | The localized, human-readable sentence shown above |


## Communication protocol

The server node uses same IPC approach as current worker nodes - named pipes. This solution allows to reuse existing code. When process starts, pipe with deterministic name is opened and waiting for commands. Client has following worfklow:

1. Try to connect to server
   - If server is not running, start new instance
   - If server is busy or the connection is broken, fall back to previous build behavior
2. Initiate handshake
2. Issue build command with `ServerNodeBuildCommand` packet
3. Read packets from pipe
   - Write content to the appropriate output stream (respecting coloring) with the `ServerNodeConsoleWrite` packet
   - After the build completes, the `ServerNodeBuildResult` packet indicates the exit code

### Pipe name convention & handshake

There might be multiple server processes started with different architecture, associated user, MSBuild version and another options. To quickly identify the appropriate one, server uses convention that includes these options in the name of the pipe. Name has format `MSBuildServer-{hash}` where `{hash}` is a SHA256 hashed value identifying these options.

Handshake is a procedure ensuring that client is connecting to a compatible server instance. It uses same logic and security guarantees as current connection between entry node and worker nodes. Hash in the pipe name is basically hash of the handshake object.

### Packets for client-server communication

Server requires to introduce new packet types for IPC.

`ServerNodeBuildCommand` contains all of the information necessary for a server to run a build.

| Property name            | Type                         | Description |
|---|---|---|
| CommandLine              | String                       | The MSBuild command line with arguments for build |
| StartupDirectory         | String                       | The startup directory path |
| BuildProcessEnvironment  | IDictionary<String, String>  | Environment variables for current build |
| Culture                  | CultureInfo                  | The culture value for current build |
| UICulture                | CultureInfo                  | The UI culture value for current build |
| ConsoleConfiguration     | TargetConsoleConfiguration   | Console configuration of target Console at which the output will be rendered |

`ServerNodeConsoleWrite` contains information for console output.

| Property name            | Type          | Description |
|---|---|---|
| Text                     | String        | The text that is written to the output stream. It includes ANSI escape codes for formatting. |
| OutputType               | Byte          | Identification of the output stream (1 = standard output, 2 = error output) |

`ServerNodeBuildResult` indicates how the build finished.

| Property name            | Type          | Description |
|---|---|---|
| ExitCode                 | Int32         | The exit code of the build |
| ExitType                 | String        | The exit type of the build |

`ServerNodeBuildCancel` cancels the current build.

This type is intentionally empty and properties for build cancelation could be added in future.
