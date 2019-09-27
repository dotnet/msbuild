# EventSource

[EventSource](https://docs.microsoft.com/en-us/dotnet/api/system.diagnostics.tracing.eventsource?view=netframework-4.8) is the tool that allows Event Tracing for Windows (ETW) used in MSBuild. Among its useful features, functions with names ending in "start" and "stop" correlate between calls such that it can automatically record how long the event between the two calls took. It also provides an easy way to cheaply opt in or out, log auxiliary messages in addition to time, and add progress updates in the middle of an event as needed.

## EventSource in MSBuild
EventSource is primarily used to profile code. For MSBuild specifically, a major goal is to reduce the time it takes to run, as measured by the Regression Prevention System (RPS), i.e., running specific scenarios. To find which code segments were likely candidates for improvement, EventSources were added around a mix of processes. Large, high-level processes occur nearly every run of MSBuild andtake a long time. They generally only relatively few times. Smaller methods with well-defined purposes may occur numerous times. Profiling both types of events provides both broad strokes to identify large code segments that underperform and, more specifically, which parts ot them. Profiled functions include:

* XMake: executes MSBuild from the command line.
* RequestThreadProc: a function to requesting a new builder thread.
* LoadDocument: loads an XMLDocumentWithLocation from a path.
* RemoveReferencesMarkedForExclusion: removes blacklisted references from the reference table, putting primary and dependency references in invalid file lists.
* ComputeClosure: resolves references to, for example, properties to explicit values.
* EvaluateCondition: checks whether a condition is true and removes false conditionals.
* Parse: parses an XML document into a ProjectRootElement.
* Evaluate: Evaluates a project, running several other parts of MSBuild in the process.
* ExecuteGenerateResource: uses resource APIs to transform resource files into strongly-typed resource classes.
* SelectItems: identifies a list of files that correspond to an item, potentially with a wildcard.
* Apply: collects a set of items, mutates them in a specified way, and saves the results.
* ExecuteTask: executes a task.
* Save: saves a project to the file system if dirty, creating directories as necessary.
* LogResults: logs the results from having executed a task.

## Larger context

MSBuild is the build system underlying Visual Studio. As millions of developers use Visual Studio, improvements in MSBuild's efficiency affect millions of developers and their customers.