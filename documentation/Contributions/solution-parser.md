# Solution Parser

## Expander

The `Expander` class is used to expand a value from an expression, which can be a function invocation. These expressions (or embedded properties) have the form "$(Property)".

`Expander` handles intrinsic functions, which is a list of built-in functions.

MSBuild defines an initial set of properties like `MSBuildFrameworkToolsPath32`, `MSBuildFrameworkToolsPath`, etc.

Tools configuration can come from configuration file, registry and current exe path.

## Building

Function `BeginBuild` prepares `BuildManager` to receive build requests, which:
- Attaches debugger;
- Checks that the current build manager's state is idle;
- Initializes the logging service;
- Initializes caches;
- Registers packet handlers.

After this setup, `MSBuild` creates build request data from project files or project instances. Data for build requests are stored in `BuildRequestData` objects.

After build data for a request have been prepared, `MSBuild` executes the build. It may execute restore and/or a graph build instead depending on the configuration. It executes the build in the `ExecuteBuild` function, which pends the build request, creating an instance of `BuildSubmission` that represents the build submission.

All build submissions are stored in a dictionary in the `BuildManager` class. Then they are added to the work queue.

## Execution

The work queue dispatches build submissions. Function `IssueBuildSubmissionToSchedulerImpl`
creates `BuildRequestBlocker` (blocker) that’s handled in the `HandleNewRequest` function that handles new requests coming from nodes. This function iterates over all build requests in blocker and, for a solution build, loads the solution to the configuration.

### Solution file parser

Later, it parses the solution file to generate a solution wrapper using methods from the `SolutionFile` class. First, the parser parses the file header, which should only contain solution file format version.

After that, it parses all remaining lines. Each such line should start with one of the following strings:
- `Project(` - calls `ParseProject`
- `GlobalSection(NestedProject)` - calls `ParseNestedProjects`
- `GlobalSection(SolutionConfigurationPlatforms)` - calls `ParseSolutionConfiguration`
- `GlobalSection(ProjectConfigurationPlatforms)` - calls `ProjectConfigurationPlatforms`
- `VisualStudioVersion` - calls `ParseVisualStudioVersion`

`ParseProject` parses the first line containing the Project Type GUID, Project name, Relative Path, and Project GUID. Each project type is hardcoded (as defined in `SolutionFile`). Then it parses all project dependencies. After a project has been parsed, it’s added to the list of parsed projects.

`GlobalSection(SolutionConfigurationPlatforms)` parses the global configuration, such as `Debug|Any CPU = Debug|Any CPU`. It adds these configurations to the solution configuration list.

`GlobalSection(ProjectConfigurationPlatforms)` works pretty much the same as `GlobalSection(ProjectConfigurationPlatforms)`, but it's the global configuration for projects. It adds these configurations to the raw project configuration list.

`GlobalSection(NestedProjects)` finds parent projects for each given project. It stores the parent GUID in nested projects.

After parsing the solution file, MSBuild processes the project configuration section, updating the project configuration section parsed from a previous solution file.

### Solution project generation

Then `MSBuild` generates an MSBuild project file from the list of projects and dependencies collected from the solution file.

While generating the MSBuild wrapper project for a solution file, `MSBuild` first collects an ordered list of actual projects then creates a traversal project instance and adds some XML to it. Then it emits a solution metaproject (if it was specified) from that traversal project. It does not write this project to disk unless specified. Finally, it builds the metaproject.

