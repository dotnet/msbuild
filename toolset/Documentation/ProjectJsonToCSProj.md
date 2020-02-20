.NET CLI preview 3 is here, and with it comes the .csproj project file format and msbuild engine. The team has put a lot of focus on making the transition as seamless as possible, but as with any change of this magnitude there are some gotcha's to keep in mind. This issue explains how to use the .NET CLI during this transitional period.

# Overview
Starting with @coolcsh's great post [Changes to Project.json](https://blogs.msdn.microsoft.com/dotnet/2016/05/23/changes-to-project-json/) the team has been hard at work moving the .NET command line story from project.json to msbuild. Our plan was fairly straightforward:
- Create a preview3 fork of the CLI
- Temporarily support both project.json and msbuild in the preview3 fork
  - This allowed the team to use the CLI to build the new CLI
  - msbuild support lit up through `3` verbs such as `build3`, `publish3`, `restore3`
- Once the msbuild support matures enough to build working applications, remove project.json support

In addition to lighting up the msbuild scenario, the team also provided a migration tool called `dotnet migrate` that converts project.json files to csproj files.

# Where are we now?
The team is feature complete to the plan above. The transition to msbuild is done and the migration tool is already part of the preview3 CLI. Of course there are still many issues to address before we can claim that we're ready to drop the `preview` suffix, but the core functionality is in place.

# How do I...
Until we are ready to call the `preview3+` CLI ready for prime-time we find ourselves in a world where it is interesting to work on csproj, project.json, or both. How can we make that work?

## How do I keep working with project.json?
Developers who are waiting for the new CLI to ship will continue using the `preview2` CLI. As long as they don't install any `preview3` or later bits on their machine they will be able to continue using Visual Studio 2015 preview tooling and maintain their existing developer experience. 

The `preview3` CLI does not offer any benefits for developers that want to keep using `project.json` for the time being.

## How do I work with csproj only?
Developers who want to try out the new csproj / msbuild CLI and don't have project.json-based code to worry about can just install the `preview3` tooling. Any build starting at `1.0.0-preview3-004056` is sufficient to get started. Once installed, you can open a command prompt and:
- `dotnet new` to get a new csproj console application
- `dotnet restore` to get required nuget packages
- `dotnet build` to build the project
- `dotnet run` to run the project

## How do I work with project.json while trying out csproj?
This scenario presents a few options. First, you can use the `dotnet migrate` tool to migrate your existing `project.json` assets to csproj. Alternatively, you can maintain both workflows side-by-side with some care, described below.

### How do I migrate project.json to csproj?
I'll start with a warning. Migration is fairly complex and `preview3` is the first preview of this capability. The tool will be better in future releases, and it will need your project.json files in order to redo the migration in the future. `dotnet migrate` moves your `project.json` assets into a `backup` folder whose structure matches the layout of your solution. If you intend to migrate with newer tooling in the future then keep this folder in source control so you can put the files back when needed.

To migrate a project open a command prompt and go to the project's root directory. From here, type `dotnet migrate` and let the tool do its work. For most projects, that's it! There are some special cases where the tool will complain and ask for some help, however:
- Projects are expected to have been updated to the `preview2` format for `project.json`.  If you had noticed the previous CLI complaining about `deprecated` properties, these warnings will need to be cleaned up prior to migration.
- Some advanced capabilities, particularly some script variables, do not have an equivalent in the csproj world. The migration tool will ask you to remove these before migration can complete.

Once migration is done you can use the same verbs as before to build, run, publish, etc. your project.

### How do I work with project.json and csproj on the same machine?
This is an important scenario, as many folks will keep working with the existing tooling while experimenting with the preview3 implementations.

To work with both project.json and csproj a developer will install two CLIs: one `preview2` for project.json and one `preview3` for csproj. There will only be one `dotnet` on the PATH, of course, so the CLI will need some help knowing which version to choose. This is accomplished with the help of the `global.json` file.

When `preview2` shipped the Visual Studio new project templates included both a `project.json` file and a `global.json` file. The latter was fairly empty, including project locations and a `version` property. That `version` specified the `preview2` CLI that shipped with the tools.


```
{
  "projects": [ "src", "test" ],
  "sdk": {
    "version": "1.0.0-preview2-003121"
  }
}
```

We included this file by default as a future-proofing tactic. When the CLI launches it looks for this file in the current directory, or the nearest parent directory, and tries to find a matching version of itself. If an exact match is found then it is used. Otherwise, `dotnet.exe` picks the latest installed CLI. When there is no exact match AND preview3 is installed then we get into trouble because preview3 cannot reason about project.json files.

When working with `preview2` and `preview3` on the same machine we need to be sure that `preview2` projects have a global.json present and that the `version` property is set to an installed preview2 version. This will typically be `1.0.0-preview2-003121` or `1.0.0-preview2-003131`. You can check what is installed by looking in `%PROGRAM FILES%\dotnet\sdk` and checking the folder names. 

