# MSBuild Server

MSBuild Server nodes accept build requests from clients and use worker nodes in the current fashion to build projects. The main purpose of the server node is to preserve caches between builds and avoid expensive MSBuild process start operations during build from tools like the .NET SDK.

## Usage

The primary ways to use MSBuild are via Visual Studio and via the CLI using the `dotnet build`/`dotnet msbuild` commands. MSBuild Server is not supported in Visual Studio because Visual Studio itself works like MSBuild Server. For the CLI, the server functionality is enabled by default and can be disabled by setting the `DOTNET_CLI_DO_NOT_USE_MSBUILD_SERVER` environment variable to value `1`.
To re-enable MSBuild Server, remove the variable or set its value to `0`.

## Garbage collection

When a build is multithreaded (`/mt`), the server node is launched with [Server GC](https://learn.microsoft.com/dotnet/standard/garbage-collection/workstation-server-gc) enabled. Under `/mt` the server runs all project work on threads in this single process, so Server GC's higher throughput is beneficial; without `/mt` the server only orchestrates and delegates project work to separate worker nodes, so it keeps the default Workstation GC. GC mode is fixed at CLR startup, so it is set via the `DOTNET_gcServer` environment variable in the server's launch environment (decided from the launching invocation's command line). An explicit user-set `DOTNET_gcServer` is honored (e.g. set `DOTNET_gcServer=0` to keep Workstation GC in a memory-constrained environment). This is scoped to the server process only: sidecar TaskHosts and worker nodes keep the default Workstation GC.

## Node reuse and server lifetime

MSBuild Server is a form of node reuse: the whole point of the server is to stay resident between builds so later builds reuse its warmed-up process and caches. Consequently:

- **Node reuse on (the default).** The server is eligible and, after a build, returns to listening so the next compatible client reuses it.
- **Node reuse off (`-nodeReuse:false` / `-nr:false`) without `/mt`.** Keeping a process resident contradicts the no-reuse intent, so the build does not use the server at all (it runs entirely in the launching process). See `ServerShouldNotRunWhenNodeReuseEqualsFalse`.
- **Node reuse off *with* `/mt`.** A `/mt` build needs the server for a different reason: multithreaded project execution runs inside the server process, which is where Server GC is applied (see [Garbage collection](#garbage-collection)). So a `/mt` build still engages the server even when node reuse is off - but it must honor the no-reuse request by **not** leaving the server resident afterwards. This is a *short-lived* server: a fresh process that tears itself down after the build.

The client makes a single, response-file-aware determination and sets the `ShutdownAfterBuild` flag on the `ServerNodeBuildCommand` packet if server needs shutdown.

## Diagnostics: server lifecycle messages

Every build where MSBuild Server is *requested* now records what happened to the server — whether it started a new one, reused a running one, or ran the build in-process instead (and why). This is emitted as a dedicated, structured `MSBuildServerLifecycleEventArgs` (its own binary-log record kind), so it appears in binary logs and at diagnostic verbosity and server behavior is easy to see when troubleshooting. Ordinary builds that never request the server record nothing.

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
