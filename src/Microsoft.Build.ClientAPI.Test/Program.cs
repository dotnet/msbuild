// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.CommandLine;
using Microsoft.Build.App;

string[] msbuildArgs= [@"D:\code\scratch\cs\cs.csproj"];
var msbuildLocation = @"D:\code\msbuild\artifacts\bin\bootstrap\core\sdk\10.0.100\MSBuild.dll";
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


