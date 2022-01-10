# Solution Parser

## Expander

`Expander` class is used to expand value from an expression, which can
be function invocation. These expression (or embedded properties)
have form - "$(property)".

`Expander` handles intrinsic functions, which is a list of built-in functions
(bunch of ifs).

MSBuild defines initial set of properties, like `MSBuildFrameworkToolsPath32`,
`MSBuildFrameworkToolsPath`, etc.

Tools configuration can come from configuration file, registry and local.

## Building

Function `BeginBuild` prepares `BuildManager` to receive build requests, which:
• Attaches debugger;
• Checks that current build manager's state is idle;
• Initializes logging service;
• Initializes caches;
• Registers packet handlers.

After this setup, `MSBuild` creates build request data from project
files or project instances. Data for build requests is stored in `BuildRequestData` class.

After build data for request has been prepared, `MSBuild` executes build (notice that
it can also execute restore and graphbuild depending on configuration). It executes
build in `ExecuteBuild` function. This function pends build request, which creates
an instance of `BuildSubmission` that represent build submission.

All build submissions are stored in dictionary in `BuildManager` class. Then
they are added to the work queue.

## Execution

Work queue dispatches build submissions. Function `IssueBuildSubmissionToSchedulerImpl`
creates `BuildRequestBlocker` (blocker) that’s handled in `HandleNewRequest` function
that handles new requests coming from nodes.
This function iterates over all build requests in blocker and, if
request is building a solution then it loads this solution to configuration.

### Solution file parser

Later, it parses solution file to generate solution wrapper using `SolutionFile` class.
Parser function first parser file header, which should only contain solution
file format version.

After that, it parses all remaining lines. Each such line should start with
one of the following strings:
• `Project(` - calls `ParseProject`
• `GlobalSection(NestedProject)` - calls `ParseNestedProjects`
• `GlobalSection(SolutionConfigurationPlatforms)` - calls `ParseSolutionConfiguration`
• `GlobalSection(ProjectConfigurationPlatforms)` - calls `ProjectConfigurationPlatforms`
• `VisualStudioVersion` - calls `ParseVisualStudioVersion`

`ParseProject` parses first line which contains Project Type GUID,
Project name, Relative Path, Project GUID. Each project type is hardcoded (defined
in `SolutionFile`). Then it parses all project dependencies. After project has
been parsed, it’s added to project list.

`GlobalSection(SolutionConfigurationPlatforms)` parses global configuration,
such as lines `Debug|Any CPU = Debug|Any CPU`. It adds these configurations
to solution configuration list.

`GlobalSection(ProjectConfigurationPlatforms)` works pretty much the same as
previous, but it’s global configuration for projects. It adds these
configurations to raw project configuration list.

`GlobalSection(NestedProjects)` finds parent projects to each given project.
It stores parent GUID in nested projects.

After solution file has been parsed, it processes project configuration section.
It updates project configuration from earlier parsed solution file.

### Solution project generation

Then `MSBuild` generates MSBuild project file from a list of projects
and dependencies collected from solution file.

While generating MSBuild wrapper project for solution file, `MSBuild` first
collects an ordered list of actual projects. Then it creates traversal
project instance and adds some XML to this instance. Then it emits
metaproject (if it was specified) from traversal project. Then it builds
project instance.

`MSBuild` initializes that project instance by setting a bunch of
fields and then evaluates project data.

