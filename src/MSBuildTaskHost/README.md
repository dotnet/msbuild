# MSBuildTaskHost

`MSBuildTaskHost.exe` is required for some project types to maintain Visual Studio asset compatibility, especially around targeting .NET Framework 3.5. It targets .NET Framework 3.5 itself and must remain able to communicate over a named pipe with the rest of the code in this repository.

This area is not under active development. Avoid editing it unless a compatibility or servicing need requires a change, and avoid changes made only for style, modernization, cleanup, or other minor considerations.
