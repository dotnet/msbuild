
# Question the build (Is Up-To-Date?)

MSBuild can skip Target or Task from running again by implementing some checks. Targets uses the Inputs and Outputs parameters to compare the timestamp of input and output files (see ['Build incrementally'](https://learn.microsoft.com/en-us/visualstudio/msbuild/how-to-build-incrementally) for details). Tasks have different behavior and thus have different rules. See below for details of each task shipped with MSBuild. Custom tasks can implement `IIncrementalTask` interface.

Question switch ask if the next build is up-to-date. It will start a build, but will error out as soon as a target or task is not up-to-date. This error stops the build to allow quick investigation at the point of failure. It is recommended to use binlog logging to gather all the information. Targets or tasks that don't have an up-to-date check will build normally. Question is a check to help ensure minimal builds with zeros changes, and thus, faster build with small changes.

[Fast Up-To-Date Check](https://github.com/dotnet/project-system/blob/cd275918ef9f181f6efab96715a91db7aabec832/docs/up-to-date-check.md) is a system that is implemented by the Project System, that decides, if it needs to run MSBuild.  MSBuild takes a non-trival amount of time to load, evaluate, and run through each target and task.  Fast Up-To-Date is faster, but can be less accurate, suitable for an IDE and a human interface. Question is not and does not replace Fast Up-To-Date Check.

## Usage
Question mode is designed to be used on the command line.  Run your normal build, then run again with /question.
```
msbuild /p:Configuration=Debug Project1.csproj /bl:build.binlog
msbuild /p:Configuration=Debug Project1.csproj /bl:incremental.binlog /question
```
If there are no errors, then your build is up-to-date.  
If there are errors, then investigate the error.  See common errors below.  Use both logs to help with your investigation.

## Custom Tasks
Task author can implement the optional `IIncrementalTask` interface that will explose `FailIfNotIncremental`. `FailIfNotIncremental` is true when `/question` switch is used. The custom task will need to decide how it want to handle its behavior.  For example - if there is already a message describing why the task cannot be skipped, then simply convert the message to an error. Remember to return `false` to stop the build.  For the best reproducibility, try to preserve the input state and exit early.

```C#
if (FailIfNotIncremental)
{
  TaskLoggingHelper.LogErrorWithCodeFromResources("ToolTask.NotUpToDate");
  return false;
}
else
{
  TaskLoggingHelper.LogMessageWithCodeFromResources("ToolTask.NotUpToDate");
}
```

### `ToolTask`
If inheriting from ToolTask, your custom task can override `SkipTaskExecution()`.  When it returns `false`, TookTask will exit with error.

## Shipping Tasks
When `/question` switch is used, it will modify the shipping task behavior as follows.  Note: this is still experimental and can change.

`Exec`
Doesn't have an up-to-date check.  It will always run.  Use Target Inputs and Outputs to skip the exec task.

`Touch`
Warns when a file is touched.  It is unclear if the file touched will participate in the build as it is a common practice to touch a file to signal external tool to run.  Use Target Inputs and Outputs to skip this task.

`Copy`
Errors out when any copy action occurs.

`WriteLinesToFile`
Error out when WriteOnlyWhenDifferent is true.  This task could be used to append to a log file that isn't participating in the build itself.

`Delete`
Warns that a file still exists and is to be deleted.  It is unclear if the file is part of the build or it is deleting an accessary file outside of the build.

`Move`
No warnings nor errors are logged.  This task is effectively a no-op as files could not exist anymore.

`DownloadFile`
Errors out if SkipUnchangedFiles is true.

`GenerateResource` 
Errors out if any files would be generated.

`MakeDir`
Errors out if folder doesn't exist.

`RemoveDir`
Errors out if folder still exist.

`Unzip`
Errors out if SkipUnchangedFiles is true.

`ZipDirectory`
Errors out if the destination zip file doesn't exists.

## Common Up-To-Date Errors
- **Typographical error**. Spelling or incorrect path.  Check if the target inputs and outputs real files.
- **Casing**. While MSBuild on Windows is case insenstive, some command line comparision and hashing tool are case senstive.  Paths and Project Refernces are a common source of casing issues.
- **Batching**. Inputs and Outputs are sometimes used for [Cross Product](https://learn.microsoft.com/en-us/visualstudio/msbuild/msbuild-batching). Try to move all operations to the `Outputs` parameter or to the `Returns` parameter to avoid using the `Inputs` parameter.
- **Exec Task** are not Skipable, thus they should be wrapped with Target Inputs and Outputs or other systems.  For backwards compatibility, Question will not error out.
- **FileWritten**.  The common clean system will remove files that aren't in the FileWritten itemgroup. Verify that the task always adds to the FileWritten itemgroup even when the task is skipped.
- **Build, Build, Build**.  Incremental issue can appear beyond the 2nd build. Run a second build, then use `/question` to validate the next build.
- **Rebuild**.  Rebuild will clean previous results and build again. Since it executes in a single instance, it would execute in different order with a chance of unintended behavior compared to a straigh up build. Use `/question` after rebuild (`/t:rebuild`) to validate.
- **Double Incremental Checks**.  Since target and task could both be incremental, if both employ incrementality checks, then it can lead to task skipping without vindicating the target.  For example, a Target has inputs A and outputs B.  If A is newer, than B, then the target will start.  If the task called by that target compares the content of A and B and deems nothing has changed, then B is not updated.  In such case, it will lead to the target rerunning.
