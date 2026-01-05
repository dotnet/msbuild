// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.CommandLine;
using Microsoft.Build.App;

var msbuildLocation = @"D:\code\msbuild\artifacts\bin\bootstrap\core\sdk\10.0.100\MSBuild.dll";
string[] msbuildArgs = [
    // MSbuild command line parsing is really basic and expecting a usage like `dotnet msbuild.dll ...stuff`
    // and so when submitting even _normal_ jobs you need to adhere to this
    msbuildLocation,
    // this is the project we actually want to work with
    @"D:\code\scratch\cs\cs.csproj",
    "/bl:server-build.binlog", 
    "/m"];
// need this to be set so that the buildenvironmenthelper can find the right MSBuild location,
// because the inferred tools path is used in the salts for the server node handshake.
System.Environment.SetEnvironmentVariable("MSBUILD_EXE_PATH", msbuildLocation);

// other implicit state:
// * the fileversion data in the ServerNodeHandshake. this works in my example here because 
//   we're picking up the versioning info from the resolved msbuild dll, but 
//   it would be really great to have the be less _explicit_
// * using MSBuildClientApp.Execute today means that we need to have implicitly set things up
//   so that BuildEnvironmentHelper resolves the right stuff. This should be more explicit.
// * the arg parsing overall sucks here. you have to preload the msbuild dll on that command line.
//   this is because msbuild's arg parsing gets the whole environment command line and has to be
//   able to handle both `msbuild.exe stuff` and `dotnet msbuild.dll stuff` usages.
//   if we move to apphosts all the time, this goes away and we get more consistent.

using var cts = new CancellationTokenSource();
var result = MSBuildClientApp.Execute(msbuildArgs, msbuildLocation, cts.Token);

switch (result)
{
    case ExitType.Success:
        Console.WriteLine("Build succeeded.");
        return 0;
    case ExitType.SwitchError:
        Console.WriteLine("There was a syntax error in a command line argument.");
        return 1;
    case ExitType.InitializationError:
        Console.WriteLine("A command line argument was not valid.");
        return 2;
    case ExitType.BuildError:
        Console.WriteLine("The build failed.");
        return 3;
    case ExitType.LoggerAbort:
        Console.WriteLine("A logger aborted the build.");
        return 4;
    case ExitType.LoggerFailure:
        Console.WriteLine("A logger failed unexpectedly.");
        return 5;
    case ExitType.Unexpected:
        Console.WriteLine("The build stopped unexpectedly, for example, because a child died or hung.");
        return 6;
    case ExitType.ProjectCacheFailure:
        Console.WriteLine("A project cache failed unexpectedly.");
        return 7;
    case ExitType.MSBuildClientFailure:
        Console.WriteLine("The client for MSBuild server failed unexpectedly, for example, because the server process died or hung.");
        return 8;
    default:
        return 100;
}


