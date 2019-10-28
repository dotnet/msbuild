# EventSource

[EventSource](https://docs.microsoft.com/en-us/dotnet/api/system.diagnostics.tracing.eventsource?view=netframework-4.8) is the tool that allows Event Tracing for Windows (ETW) used in MSBuild. Among its useful features, functions with names ending in "start" and "stop" correlate between calls such that it can automatically record how long the event between the two calls took. It also provides an easy way to cheaply opt in or out, log auxiliary messages in addition to time, and add progress updates in the middle of an event as needed.

## EventSource in MSBuild
EventSource is primarily used to profile code. For MSBuild specifically, a major goal is to reduce the time it takes to run, as measured (among other metrics) by the Regression Prevention System (RPS), i.e., running specific scenarios. To find which code segments were likely candidates for improvement, EventSources were added around a mix of code segments. Larger segments that encompass several steps within a build occur nearly every time MSBuild is run and take a long time. They generally run relatively few times. Smaller methods with well-defined purposes may occur numerous times. Profiling both types of events provides both broad strokes to identify large code segments that underperform and, more specifically, which parts of them. Profiled functions include:

* MSBuildExe: Executes MSBuild from the command line.
* Build: Sets up a BuildManager to receive build requests.
* BuildProject: Builds a project file.
* RequestThreadProc: A function to requesting a new builder thread.
* LoadDocument: Loads an XMLDocumentWithLocation from a path.
* RarRemoveReferencesMarkedForExclusion: Removes blacklisted references from the reference table, putting primary and dependency references in invalid file lists.
* RarComputeClosure: Resolves references from, for example, properties to explicit values. Used in resolving assembly references (RAR).
* EvaluateCondition: Checks whether a condition is true and removes false conditionals.
* Parse: Parses an XML document into a ProjectRootElement.
* Evaluate: Evaluates a project, running several other parts of MSBuild in the process.
* GenerateResourceOverall: Uses resource APIs to transform resource files into strongly-typed resource classes.
* ExpandGlob: Identifies a list of files that correspond to an item, potentially with a wildcard.
* ApplyLazyItemOperations: Collects a set of items, mutates them in a specified way, and saves the results in a lazy way.
* RarOverall: Initiates the process of resolving assembly references (RAR).
* Save: Saves a project to the file system if dirty, creating directories as necessary.
* Target: Executes a target.
* RarLogResults: Logs the results from having resolved assembly references (RAR).

One can run MSBuild with eventing using the following command:

`PerfView /OnlyProviders=*Microsoft-Build run MSBuild.exe <project to build>`

For example, if PerfView is one level up from my current directory (which has MSBuild.exe), and I want to build MSBuild.sln on Windows, I would use the following command:

`..\PerfView /OnlyProviders=*Microsoft-Build run .\MSBuild.exe .\MSBuild.sln`
