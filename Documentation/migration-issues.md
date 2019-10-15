## Getting help on migration issues
You're using the new .NET Core tools that are MSBuild-based. You took your project.json project and ran `dotnet migrate` or migrated from Visual Studio 2017...and you maybe ran into problems. 

The best way to get help is to [file an issue](https://github.com/dotnet/cli/issues/new) on this repo and we will investigate and provide help and/or fixes as part of new CLI builds. Make sure to read the instructions below and to **add the [MIGRATION] prefix to the issue title**.

### Filing an migration issue 
CLI is a very high-traffic repository in terms of issues. In order to be able to respond fast to migration issues, we need the issue to be formatted in a certain way:

* Add `[MIGRATION]:` prefix to the title of the issue.
* Make sure that we can see your project.json
   * If you have a GH repo or this is an OSS project, share the URL to the repo.
   * Otherwise attach or paste the project.json contents into the issue.
 * Add all of the errors that any operation like `dotnet restore`, `dotnet build` or others reported. This will help us speedily triage where the potential problem will be. 
* Add output of `dotnet --info` to the issue so we know what build you are running. 
* Mention @blackdwarf and @livarcocc in the issue body. 

From there on, we will start investigating the issue and respond. 
