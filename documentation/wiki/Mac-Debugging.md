#Debugging with MacOS
Say unit tests are failing from Microsoft.Build.Engine.UnitTests_netcoreapp2.1_x64
* Open terminal 
  * Hit command-space, type terminal, hit enter. Alternatively, hit magnifying glass (spotlight) in upper-right corner and search for terminal.)
* Build something
  * Navigation in terminal is similar to command prompt (cd), although you type `ls` in place of `dir`.
  * **Use ./build.sh instead of .\build.cmd.**
* Type `find . -name Microsoft.Build.Engine.UnitTests_netcoreapp2.1_x64.log`
  * This should print out a path (from your current working directory) to the relevant log file.
* Type `tail <path from previous step>`
  * This prints out the last part of that file.
  * You can also just open it normally from a finder window.
  * This file contains the standard output from the last run.
  * You may notice that the file ends with `=== COMMAND LINE ===` followed by a single (long) command line statement.
* Copy the command line statement from the previous step. Remove the portion after the redirection (`>` character). You may notice that part redirects output to the file you’re viewing.
  * The last part (`2>&1`) redirects standard error (using `2>`) to the same place as where standard out is going (`&1`), in this case this log file.
* Append `./build.sh &&` to the beginning of the truncated command line statement.
* Append `-method ` and the name of the method you want to test to the end.
*	Running this statement will run just the one test (after building) and print out both the error and the Console.WriteLine() statements you added to the test/what it calls.


Sample statements with outputs below (*italicized*) and changes to the output of the second command **bolded** (note that additionally, the last two lines and one character of the second output were deleted):
find . -name Microsoft.Build.Engine.UnitTests_netcoreapp2.1_x64.log
*./artifacts/log/Debug/Microsoft.Build.Engine.UnitTests_netcoreapp2.1_x64.log*

*tail ./artifacts/log/Debug/Microsoft.Build.Engine.UnitTests_netcoreapp2.1_x64.log
AnyEvent:message
CustomWarning:Event type "MyCustomBuildWarningEventArgsNotSerializable" was expected to be serializable using the .NET serializer. The event was not serializable and has been ignored.
AnyEvent:Event type "MyCustomBuildWarningEventArgsNotSerializable" was expected to be serializable using the .NET serializer. The event was not serializable and has been ignored.
CustomWarning:Event type "MyCustomBuildErrorEventArgsNotSerializable" was expected to be serializable using the .NET serializer. The event was not serializable and has been ignored.
AnyEvent:Event type "MyCustomBuildErrorEventArgsNotSerializable" was expected to be serializable using the .NET serializer. The event was not serializable and has been ignored.
  Finished:    Microsoft.Build.Engine.UnitTests
=== TEST EXECUTION SUMMARY ===
   Microsoft.Build.Engine.UnitTests  Total: 2928, Errors: 0, Failed: 0, Skipped: 47, Time: 112.394s
=== COMMAND LINE ===
"/Users/nathanmytelka/Desktop/code/msbuild/.dotnet/dotnet" exec --depsfile "/Users/nathanmytelka/Desktop/code/msbuild/artifacts/bin/Microsoft.Build.Engine.UnitTests/Debug/netcoreapp2.1/Microsoft.Build.Engine.UnitTests.deps.json" --runtimeconfig "/Users/nathanmytelka/Desktop/code/msbuild/artifacts/bin/Microsoft.Build.Engine.UnitTests/Debug/netcoreapp2.1/Microsoft.Build.Engine.UnitTests.runtimeconfig.json"  "/Users/nathanmytelka/.nuget/packages/xunit.runner.console/2.4.1/tools/netcoreapp2.0/xunit.console.dll" "/Users/nathanmytelka/Desktop/code/msbuild/artifacts/bin/Microsoft.Build.Engine.UnitTests/Debug/netcoreapp2.1/Microsoft.Build.Engine.UnitTests.dll" -noautoreporters -xml "/Users/nathanmytelka/Desktop/code/msbuild/artifacts/TestResults/Debug/Microsoft.Build.Engine.UnitTests_netcoreapp2.1_x64.xml" -html "/Users/nathanmytelka/Desktop/code/msbuild/artifacts/TestResults/Debug/Microsoft.Build.Engine.UnitTests_netcoreapp2.1_x64.html" -notrait category=nonosxtests -notrait category=netcore-osx-failing -notrait category=nonnetcoreapptests -notrait category=failing > "/Users/nathanmytelka/Desktop/code/msbuild/artifacts/log/Debug/Microsoft.Build.Engine.UnitTests_netcoreapp2.1_x64.log" 2>&1*

**./build.sh &&** "/Users/nathanmytelka/Desktop/code/msbuild/.dotnet/dotnet" exec --depsfile "/Users/nathanmytelka/Desktop/code/msbuild/artifacts/bin/Microsoft.Build.Engine.UnitTests/Debug/netcoreapp2.1/Microsoft.Build.Engine.UnitTests.deps.json" --runtimeconfig "/Users/nathanmytelka/Desktop/code/msbuild/artifacts/bin/Microsoft.Build.Engine.UnitTests/Debug/netcoreapp2.1/Microsoft.Build.Engine.UnitTests.runtimeconfig.json"  "/Users/nathanmytelka/.nuget/packages/xunit.runner.console/2.4.1/tools/netcoreapp2.0/xunit.console.dll" "/Users/nathanmytelka/Desktop/code/msbuild/artifacts/bin/Microsoft.Build.Engine.UnitTests/Debug/netcoreapp2.1/Microsoft.Build.Engine.UnitTests.dll" -noautoreporters -xml "/Users/nathanmytelka/Desktop/code/msbuild/artifacts/TestResults/Debug/Microsoft.Build.Engine.UnitTests_netcoreapp2.1_x64.xml" -html "/Users/nathanmytelka/Desktop/code/msbuild/artifacts/TestResults/Debug/Microsoft.Build.Engine.UnitTests_netcoreapp2.1_x64.html" -notrait category=nonosxtests -notrait category=netcore-osx-failing -notrait category=nonnetcoreapptests -notrait category=failing **-method Microsoft.Build.UnitTests.BackEnd.TaskBuilder_Tests.NullMetadataOnLegacyOutputItems**
