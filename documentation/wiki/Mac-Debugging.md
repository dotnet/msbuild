#Debugging with MacOS
* Open terminal 
  * Hit command-space, type terminal, hit enter. Alternatively, hit magnifying glass (spotlight) in upper-right corner and search for terminal.)
* Build and run tests
  * Navigation in terminal is similar to command prompt (cd), although you type `ls` in place of `dir`.
  * **Use `./build.sh -test` instead of `.\build.cmd -test`.**
  * If tests fail, they will appear twice in red: once when the test fails and once after all tests have run. As an example, it might say `XUnit : error : Tests failed: /Users/forgind/Desktop/code/msbuild/artifacts/TestResults/Debug/Microsoft.Build.Utilities.UnitTests_netcoreapp2.1_x64.html [netcoreapp2.1|x64] [/Users/forgind/Desktop/code/msbuild/src/Utilities.UnitTests/Microsoft.Build.Utilities.UnitTests.csproj]` near the end.
  * Successful tests appear in white and only once like this: `Tests succeeded: /Users/forgind/Desktop/code/msbuild/artifacts/bin/Microsoft.Build.CommandLine.UnitTests/Debug/netcoreapp2.1/Microsoft.Build.CommandLine.UnitTests.dll [netcoreapp2.1|x64]`
* Choose a set of tests to analyze.
  * From the previous example, one sample would be `Microsoft.Build.UnitTests_netcoreapp2.1_x64`, that is, the part immediately preceding `.html`.
* Run `find . -name Microsoft.Build.Engine.UnitTests_netcoreapp2.1_x64.log`
  * Note the addition of the extension `.log` in place of `.html`.
  * This should print out a path (from your current working directory) to the relevant log file.
* Type `tail -n 1 <path from previous step>`
  * This prints out the last line of that file.
  * You can also just open it normally from a finder window.
  * This file contains the standard output from the last run.
  * You may notice that the line printed by this command is a single (long) command line statement.
* Copy the command line statement from the previous step. Remove the portion after the redirection (`>` character not preceded by 2) including that character. You may notice that part redirects output to the file you’re viewing.
  * The last part (`2>&1`) redirects standard error (using `2>`) to the same place as where standard out is going (`&1`), in this case this log file.
  * If you would like to rerun all tests from a given class (rather than just a specific method), you can append `-class` and the class's fully qualified name. To run all tests from `TaskBuilder_Tests`, for instance, you would add `-class Microsoft.Build.UnitTests.BackEnd.TaskBuilder_Tests` and run the statement without the following steps. Note that in the example below, standard output is redirected to `/dev/null`, thus only printing the errors.
* Prepend `./build.sh &&` to the truncated command line statement.
* Append `-method ` and the name of the method you want to test to the end.
  * You can find the failing method by opening the html file  (`/Users/forgind/Desktop/code/msbuild/artifacts/TestResults/Debug/Microsoft.Build.Utilities.UnitTests_netcoreapp2.1_x64.html` from the above case) in a web browser of choice. You will need to prepend `file://` if you use Safari.
  * This will show a list of failing methods including why they failed.
*	Running this statement will run just the one test (after building) and print out both the error and the Console.WriteLine() statements you added to the test/what it calls.


Sample statements with outputs below and changes to the output of the second command **bolded** (note that additionally, the last two lines and one character of the second output were deleted):

<pre><code>
$ find . -name Microsoft.Build.Engine.UnitTests_netcoreapp2.1_x64.log
./artifacts/log/Debug/Microsoft.Build.Engine.UnitTests_netcoreapp2.1_x64.log

$ tail -n 1 ./artifacts/log/Debug/Microsoft.Build.Engine.UnitTests_netcoreapp2.1_x64.log
"/Users/forgind/Desktop/code/msbuild/.dotnet/dotnet" exec --depsfile "/Users/forgind/Desktop/code/msbuild/artifacts/bin/Microsoft.Build.Engine.UnitTests/Debug/netcoreapp2.1/Microsoft.Build.Engine.UnitTests.deps.json" --runtimeconfig "/Users/forgind/Desktop/code/msbuild/artifacts/bin/Microsoft.Build.Engine.UnitTests/Debug/netcoreapp2.1/Microsoft.Build.Engine.UnitTests.runtimeconfig.json"  "/Users/forgind/.nuget/packages/xunit.runner.console/2.4.1/tools/netcoreapp2.0/xunit.console.dll" "/Users/forgind/Desktop/code/msbuild/artifacts/bin/Microsoft.Build.Engine.UnitTests/Debug/netcoreapp2.1/Microsoft.Build.Engine.UnitTests.dll" -noautoreporters -xml "/Users/forgind/Desktop/code/msbuild/artifacts/TestResults/Debug/Microsoft.Build.Engine.UnitTests_netcoreapp2.1_x64.xml" -html "/Users/forgind/Desktop/code/msbuild/artifacts/TestResults/Debug/Microsoft.Build.Engine.UnitTests_netcoreapp2.1_x64.html" -notrait category=nonosxtests -notrait category=netcore-osx-failing -notrait category=nonnetcoreapptests -notrait category=failing > "/Users/forgind/Desktop/code/msbuild/artifacts/log/Debug/Microsoft.Build.Engine.UnitTests_netcoreapp2.1_x64.log" 2>&1

$ "/Users/forgind/Desktop/code/msbuild/.dotnet/dotnet" exec --depsfile "/Users/forgind/Desktop/code/msbuild/artifacts/bin/Microsoft.Build.Engine.UnitTests/Debug/netcoreapp2.1/Microsoft.Build.Engine.UnitTests.deps.json" --runtimeconfig "/Users/forgind/Desktop/code/msbuild/artifacts/bin/Microsoft.Build.Engine.UnitTests/Debug/netcoreapp2.1/Microsoft.Build.Engine.UnitTests.runtimeconfig.json"  "/Users/forgind/.nuget/packages/xunit.runner.console/2.4.1/tools/netcoreapp2.0/xunit.console.dll" "/Users/forgind/Desktop/code/msbuild/artifacts/bin/Microsoft.Build.Engine.UnitTests/Debug/netcoreapp2.1/Microsoft.Build.Engine.UnitTests.dll" -noautoreporters -xml "/Users/forgind/Desktop/code/msbuild/artifacts/TestResults/Debug/Microsoft.Build.Engine.UnitTests_netcoreapp2.1_x64.xml" -html "/Users/forgind/Desktop/code/msbuild/artifacts/TestResults/Debug/Microsoft.Build.Engine.UnitTests_netcoreapp2.1_x64.html" -notrait category=nonosxtests -notrait category=netcore-osx-failing -notrait category=nonnetcoreapptests -notrait category=failing -class Microsoft.Build.UnitTests.BackEnd.TaskBuilder_Tests > /dev/null
Microsoft.Build.UnitTests.BackEnd.TaskBuilder_Tests.NullMetadataOnLegacyOutputItems [FAIL]

$ <b>./build.sh &&</b> "/Users/forgind/Desktop/code/msbuild/.dotnet/dotnet" exec --depsfile "/Users/forgind/Desktop/code/msbuild/artifacts/bin/Microsoft.Build.Engine.UnitTests/Debug/netcoreapp2.1/Microsoft.Build.Engine.UnitTests.deps.json" --runtimeconfig "/Users/forgind/Desktop/code/msbuild/artifacts/bin/Microsoft.Build.Engine.UnitTests/Debug/netcoreapp2.1/Microsoft.Build.Engine.UnitTests.runtimeconfig.json"  "/Users/forgind/.nuget/packages/xunit.runner.console/2.4.1/tools/netcoreapp2.0/xunit.console.dll" "/Users/forgind/Desktop/code/msbuild/artifacts/bin/Microsoft.Build.Engine.UnitTests/Debug/netcoreapp2.1/Microsoft.Build.Engine.UnitTests.dll" -noautoreporters -xml "/Users/forgind/Desktop/code/msbuild/artifacts/TestResults/Debug/Microsoft.Build.Engine.UnitTests_netcoreapp2.1_x64.xml" -html "/Users/forgind/Desktop/code/msbuild/artifacts/TestResults/Debug/Microsoft.Build.Engine.UnitTests_netcoreapp2.1_x64.html" -notrait category=nonosxtests -notrait category=netcore-osx-failing -notrait category=nonnetcoreapptests -notrait category=failing <b>-method Microsoft.Build.UnitTests.BackEnd.TaskBuilder_Tests.NullMetadataOnLegacyOutputItems</b>
</code></pre>
