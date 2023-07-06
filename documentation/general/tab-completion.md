# .NET CLI Tab Completion

In version 2.0 of the .NET Core CLI, we have added support for providing suggestions when you press `tab`. While it's not yet enabled by default, you can try it out in PowerShell, bash, or zsh by following the instructions below. 

Here are some examples of what it provides:

Input                                | becomes                                                                     | because
:------------------------------------|:----------------------------------------------------------------------------|:--------------------------------
`dotnet a⇥`                          | `dotnet add`                                                                 | `add` is the first subcommand, alphabetically.
`dotnet add p⇥`                      | `dotnet add --help`                                                          | it matches substrings and `--help` comes first alphabetically.
`dotnet add p⇥⇥`                    | `dotnet add package`                                                          | pressing tab a second time brings up the next suggestion.      
`dotnet add package Microsoft⇥`      | `dotnet add package Microsoft.ApplicationInsights.Web`                      | results are returned alphabetically.
`dotnet remove reference ⇥`          | `dotnet remove reference ..\..\src\OmniSharp.DotNet\OmniSharp.DotNet.csproj` | it is project file aware.

## How to enable it

Tab completion is currently supported in three shells: PowerShell, bash, and zsh. These scripts assume that `dotnet` v2.0 is on your path. You can verify that you have the correct version of `dotnet` on your path by trying out the new `complete` command directly:

```
[04/26/2017 11:38:20] C:\
> dotnet complete "dotnet add p"
```

If you just installed `dotnet` you may see the first-run output:

```
Welcome to .NET Core!
---------------------
Learn more about .NET Core: https://aka.ms/dotnet-docs
...
...
```

You should see the following output (perhaps coming at the end of the first-run output). This indicates that `dotnet` is resolving to a v2.0 installation that supports completion.

```
--help
package
```

### PowerShell

To enable tab completion in PowerShell, edit your PowerShell profile:

```
notepad $PROFILE
```

Add the contents of [register-completions.ps1](https://github.com/dotnet/cli/blob/master/scripts/register-completions.ps1) to this file and save. 

### bash

To enable tab completion in bash, edit your `.bashrc` file to add the contents of [register-completions.bash](https://github.com/dotnet/cli/blob/master/scripts/register-completions.bash).

### zsh 

To enable tab completion in zsh, edit your `.zshrc` file to add the contents of [register-completions.zsh](https://github.com/dotnet/cli/blob/master/scripts/register-completions.zsh).

## How it works

Each of these scripts provides a hook for completions for its respective shell. The logic that determines which suggestions to provide is in the CLI itself, configured using [our new parser](https://github.com/dotnet/CliCommandLineParser). You can see a code example [here](https://github.com/dotnet/cli/blob/master/src/dotnet/commands/dotnet-add/dotnet-add-package/AddPackageParser.cs). 
