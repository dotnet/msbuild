# Repro Helix Failure

## General

Most test failures on helix should repro locally. Try following the [developer-guide](developer-guide.md) _Run tests from the command line_ section first.

## Repro test as tool layout

Running tests on Helix reuses the "test as tool" infrastructure. All tests projects are published  as tools that accept xunit command line arguments. The SDK under test, the published test project, and other dependencies are copied to a helix machine. This step changes the repo structure. Most of the helix-only failures are caused by tests depending on the repo structure or having dependencies that are not carried by the test project publishing step. The following steps help you to repro the test layout that is used on the helix machine. And you can run the individual test to discover the cause.

### repro the test layout as in the helix machine

```cmd
REM fully build first
.\build.cmd

REM use build env to have stage 0 dotnet on the PATH
.\artifacts\sdk-build-env.bat

REM run the special test target CreateLocalHelixTestLayout. To have the test layout created on disk.
dotnet msbuild /restore /t:CreateLocalHelixTestLayout .\src\Tests\UnitTests.proj /p:creator=dotnetsdkdev  /p:_CustomHelixTargetQueue=Windows.Server.Amd64.VS2019.Pre.Open /bl
```

Copy the result of `artifacts\bin\localHelixTestLayout` to another directory or VM. For example to `C:\helix\localHelixTestLayout`. See "Folders under localHelixTestLayout" for the content. This is _correlation_ payload in helix term.

Publish the test project you want to repro. For example Microsoft.NET.Build.Tests.

```cmd
dotnet publish src\Tests\Microsoft.NET.Build.Tests\
```

The output is in `artifacts\bin\Tests\Microsoft.NET.Build.Tests\Debug\publish`. Copy this folder to another directory or VM. For example `C:\helix\payload-dir`. This is the "workitem payload" in helix term.

On the other machine or directory

```cmd
cd C:\helix\payload-dir

REM HELIX_CORRELATION_PAYLOAD would be set to correlation payload by real helix machine
set HELIX_CORRELATION_PAYLOAD=C:\helix\localHelixTestLayout

REM "true" is full framework test. Without "true", it is dotnet core tests. RunTestsOnHelix.cmd is the same script will setup the helix environnement.
C:\helix\localHelixTestLayout\t\RunTestsOnHelix.cmd true
```

Example of running a specific method. "Microsoft.NET.Build.Tests.dll" need to match the test project target output dll.

```cmd
dotnet Microsoft.NET.Build.Tests.dll -testExecutionDirectory %TestExecutionDirectory% -msbuildAdditionalSdkResolverFolder %HELIX_CORRELATION_PAYLOAD%\r -html testResults.html -method "Microsoft.NET.Build.Tests.GivenThatWeWantToBuildADesktopExeWithFSharp.It_builds_a_simple_net50_app"
```

## Folders under localHelixTestLayout

Due to Helix long path problem. The folders under localHelixTestLayout all have one letter name.

- d - stage 2 dotnet
- ex - msbuildExtensions
- r - Microsoft.DotNet.MSBuildSdkResolver
- t - other loose file needed to recreate the environment like global.json and eng folder (used to find VS or xcopy msbuild), NuGet.config, RunTestsOnHelix.cmd (used to set up environment variable on helix).

## Other possible cause of Helix-only failure

- Helix machine does not have the same pre-installed dependencies as the host machine. Ideally the dependencies should be declared by the SDK repo itself. At the time the document is written. The build queue and the helix queue should have the same Visual Studio version. If they are diverged, we should contact arcade team.

- New test environment variable introduced has not been reflected in RunTestsOnHelix.cmd.

- To test out changes to the Helix tasks, you can run the unit test project directly: `dotnet -t:Test .\src\Tests\UnitTests.proj /p:_CustomHelixTargetQueue=foo /bl` It won't be able to push the files into helix but it will let you view what helix work items are getting created.
