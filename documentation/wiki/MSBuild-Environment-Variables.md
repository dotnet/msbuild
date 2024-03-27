# MSBuild environment variables list

This document describes the environment variables that are respected in MSBuild, its purpose and usage. 

Some of the env variables listed here are unsupported, meaning there is no guarantee that variable or a specific combination of multiple variables will be respected in upcoming release, so please use at your own risk.

* `MSBuildDebugEngine=1` & `MSBUILDDEBUGPATH=<DIRECTORY>`
  * Set this to cause any MSBuild invocation launched within this environment to emit binary logs and additional debugging information to `<DIRECTORY>`. Useful when debugging build or evaluation issues when you can't directly influence the MSBuild invocation, such as in Visual Studio. More details on [capturing binary logs](./Providing-Binary-Logs.md)
* `MSBUILDTARGETOUTPUTLOGGING=1`
   * Set this to enable [printing all target outputs to the log](https://learn.microsoft.com/archive/blogs/msbuild/displaying-target-output-items-using-the-console-logger).
* `MSBUILDLOGTASKINPUTS=1`
   * Log task inputs (not needed if there are any diagnostic loggers already).
 * `MSBUILDEMITSOLUTION=1`
   * Save the generated .proj file for the .sln that is used to build the solution. The generated files are emitted into a binary log by default and their presence on disk can break subsequent builds.
* `MSBUILDENABLEALLPROPERTYFUNCTIONS=1`
   * Enable [additional property functions](https://devblogs.microsoft.com/visualstudio/msbuild-property-functions/). If you need this level of detail you are generally served better with a binary log than the text log.
* `MSBUILDLOGVERBOSERARSEARCHRESULTS=1`
   * In ResolveAssemblyReference task, log verbose search results.
* `MSBUILDLOGCODETASKFACTORYOUTPUT=1`
   * Dump generated code for task to a <GUID>.txt file in the TEMP directory
* `MSBUILDDISABLENODEREUSE=1`
   * Set this to not leave MSBuild processes behind (see `/nr:false`, but the environment variable is useful to also set this for Visual Studio for example).
* `MSBUILDLOGASYNC=1`
   * Enable asynchronous logging.
* `MSBUILDDEBUGONSTART=1`
   * Launches debugger on build start. Works on Windows operating systems only.  
   * Setting the value of 2 allows for manually attaching a debugger to a process ID. This works on Windows and non-Windows operating systems.
* `MSBUILDDEBUGSCHEDULER=1` & `MSBUILDDEBUGPATH=<DIRECTORY>`
   * Dumps scheduler state at specified directory (`MSBUILDDEBUGSCHEDULER` is implied by `MSBuildDebugEngine`).

* `MsBuildSkipEagerWildCardEvaluationRegexes`
  *  If specified, overrides the default behavior of glob expansion. During glob expansion, if the path with wildcards that is being processed matches one of the regular expressions provided in the [environment variable](#msbuildskipeagerwildcardevaluationregexes), the path is not processed (expanded). 
  * The value of the environment  variable is a list of regular expressions, separated by semicolon (;).