# MSBuild Server

MSBuild Server is basically an another type of node which can accept build request from clients and utilize worker nodes in current fashion to build projects. Main purpose of the server node is to avoid expensive MSBuild process start during build from tools like .NET SDK.

## Communication protocol

The server node uses same IPC approach as current worker nodes - named pipes. This solution allows to reuse existing code. When process starts, pipe with deterministic name is opened and waiting for commands. Client has following worfklow:

1. Try to connect to server
   - If server is not running, start new instance
   - If server is busy, fallback to classic build 
2. Initiate handshake
2. Issue build command with `EntryNodeCommand` packet
3. Read packets from pipe
   - When `EntryNodeConsoleWrite` packet type is recieved, write content to appropriate output stream with respected coloring
   - When `EntryNodeResponse` packet type is recieved, build is done and client writes trace message with exit code

### Pipe name convention & handshake

There might be multiple server processes started with different architecture, associated user, MSBuild version and another options. To quickly identify the appropriate one, server uses convention that includes these options in the name of the pipe. Name has format `MSBuildServer-{hash}` where `{hash}` is a SHA256 hashed value identifying these options.

Handshake is a procedure ensuring that client is connecting to a compatible server instance. It uses same logic and security guarantees as current connection between entry node and worker nodes. Hash in the pipe name is basically hash of the handshake object.

### Packets for client-server communication

Server requires to introduce new packet types for IPC.

`EntryNodeCommand` contains all of the information necessary for a server to run a build.

| Property name            | Type                         | Description |
|---|---|---|
| CommandLine              | String                       | The MSBuild command line with arguments for build |
| StartupDirectory         | String                       | The startup directory path |
| BuildProcessEnvironment  | IDictionary<String, String>  | Environment variables for current build |
| Culture                  | CultureInfo                  | The culture value for current build |
| UICulture                | CultureInfo                  | The UI culture value for current build |

`EntryNodeConsoleWrite` contains information for console output.

| Property name            | Type          | Description |
|---|---|---|
| Text                     | String        | The text that is written to the output stream. It includes ANSI escape codes for formatting. |
| OutputType               | Byte          | Identification of the output stream (1 = standard output, 2 = error output) |

`EntryNodeResponse` informs about finished build.

| Property name            | Type          | Description |
|---|---|---|
| ExitCode                 | Int32         | The exit code of the build |
| ExitType                 | String        | The exit type of the build |

`EntryNodeCancel` cancels the current build.

This type is intentionally empty and properties for build cancelation could be added in future.
