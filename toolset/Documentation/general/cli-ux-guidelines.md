.NET Core Command-Line Tools UX Guidelines
-------------------------------------------

This document outlines the User Experience (UX) of the .NET Core command line tools (CLI). These guidelines are intended for anyone that wants to add a new command to the CLI. 

The guidelines presented in this document have been adopted to provide a clear and concise 
command line syntax that is easy to learn, remember and work with, and that has an added benefit 
of being familiar to people who have used other command-line interfaces as well as existing 
Visual Studio users. 

## Naming the commands

In the .NET Core CLI, commands should be **verbs**. This rule was adopted
because most of the commands do *something*.

Sub-commands are supported in the CLI, and they are usually nouns. A good
example of this is the “dotnet add reference” command. If there is a need to add
a subcommand, that subcommand should usually specialize what the parent command
does.

## Create/Read/Update/Delete (CRUD) commands
New CRUD commands should be named according to the following logic:

-   Does the command work on data in the project (either properties or
    items)? If yes, then it should be added as a noun to the “dotnet
    add/list/remove/update”, e.g. “dotnet add foo”.

-   Does the command work on the solution (SLN) file? If yes, then it should be
    added as a verb to the “dotnet sln” command.

-   Does the command work on a completely new artifact (e.g. a new metadata file
    that is added in the future)? If yes, it should be created as a top level
    noun with all of the underlying operations defined as sub-commands.

-   If the command adds a new artifact to the project, it should become an item
    template that is dropped with “dotnet new” command.


If none of the above applies, the proposal should clearly outline why none of
the above applies and suggest an alternative naming that will be decided on
during the process described above.

If the command works on the project or solution, it must also accept an optional
argument that specifies which project or solution to work on. The current
convention is for that argument to follow the verb in the CRUD command. For
example:

>   dotnet add \<PROJECT\> reference \<REFERENCE\>

All the existing CRUD commands have this argument defined and it will be passed
to the sub-command as part of the overall options. The sub-command is expected
to consider this optional argument.

## Options 
CLI follows the [GNU convention](https://www.gnu.org/prep/standards/html_node/Command_002dLine-Interfaces.html) for options’ format, which is based on the POSIX
standard. The summary is:

-   Command can have both short and long options.

-   The short format starts with a single dash (“-“), and has **exactly** one
    letter. The letter is usually the start letter of the longer form of the
    option, e.g. “-o” for “--output” or any of its words. This provides a good
    mnemonic.

    -   **Example:** “dotnet build -o path/to/where/to/put/output”

-   The long format starts with double dashes (“--“) and can have one or more
    words. Multiple words are separated by a single dash (“-“).

    -   **Example:** “dotnet test --test-case-filter”

-   The double dashes (“--“) on their own mark the **argument separator**.
    Anything after this option is passed verbatim to whatever the command starts
    as a sub-process.

    -   **Example:** “dotnet run -- --app-arg-1”

-   Windows-style forward slash options (e.g. “/p”) are supported **only for
    MSBuild parameters.**

-   PowerShell-style single dashes with a word (e.g. “-Filter”) are **not
    supported**.

-   Help options are predefined as “-h \| --help” and those should be used.

There are common options in the CLI that are reserved for certain concepts. The
table below outlines those common options. If a command needs to accept the same
options, it should use these and it must not replace their semantics (e.g. use
“--output” to mean something different or redefine what “-f” means for just that
command).

| Long option      | Short option | Description                                                  |
|------------------|--------------|--------------------------------------------------------------|
| \--framework     | \-f          | Specifies which framework (target) to use in the invocation. |
| \--output        | \-o          | Specifies where the output of the command should be placed.  |
| \--runtime       | \-r          | Specifies the runtime ID (RID) to use in the invocation.     |
| \--configuration | \-c          | Specifies the configuration to use in the invocation.        |
| \--verbosity     | \-v          | Specifies the verbosity level of the command.                |
| \--help          | \-h          | Shows the help for the command.                              |

One special area is interacting with MSBuild. If a command has the need to do
that, there are two additional requirements with regards to options:

1.  The commands need to be able to take “/p” parameters in addition to whatever
    other properties they require.

2.  If the command invokes a predefined MSBuild target, it needs to block
    “/t:\<target\>” and “/target:\<target\>” options and throw an error that is
    pre-defined in the CLI.

It is important to note that commands that invoke user-specified targets
should not be submitted to the CLI, since the CLI comes with “dotnet msbuild”
command that does precisely that.

## Arguments
Arguments can have any name that authors of the commands being added need. We do have predefined
argument names for the SLN file and project file. They are defined in the CLI
source code and should be used if the command has the need to use those two
arguments.

## Help messages
Help messages are automatically generated based on the arguments, options and
command name. No other work should be required here apart from setting up the
above mentioned and their descriptions.

## Output 
For commands that are invoking MSBuild, the output will be controlled by MSBuild
itself.

In case of a long running operation, the command needs to provide a feedback
mechanism to the user to help the user reason about whether the command has
crashed or is just waiting for I/O. The feedback mechanism guidelines are below:

1. Feedback should not require fonts with special glyphs for display. 
2. Pure text is acceptable (e.g. `Running background process...`) as a feedback mechanism. 
3. Spinners that conform to rule \#1 above are also acceptable.


### Verbosity
If the command interacts with MSBuild, it is required that it can accept a
“--verbosity \| -v” argument and pass it to MSBuild verbatim.

If the command’s verbosity levels cannot fit naturally in [MSBuild’s verbosity levels](https://docs.microsoft.com/en-us/visualstudio/msbuild/msbuild-command-line-reference) or the command does not interact with MSBuild but still has an option to set the verbosity, it
is the job of the command to map them in the most appropriate way. This way, the
verbosity levels will be uniform across all commands which brings consistency to
the toolset.

#### Example
As an example, let us consider a “dotnet configure” command. It doesn’t interact
with MSBuild. I wish to have verbosity on it, but it really has only two levels:
quiet (just successes or errors) and verbose (every operation) that I've defined for the command. To satisfy the
above-mentioned requirement, in my command I would define the “--verbosity \|
-v” option and would map the arguments in the following way:

-   “Quiet” gets mapped naturally to “quiet”

-   “Minimal”, “Normal” and “Diagnostic” get mapped into “verbose”
