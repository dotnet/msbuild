// Copyright (c) .NET Foundation and contributors. All rights reserved. 
// Licensed under the MIT license. See LICENSE file in the project root for full license information. 

using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Tools.Test.Utilities;
using Microsoft.Extensions.PlatformAbstractions;
using System;
using System.IO;
using System.Runtime.InteropServices;
using Xunit;
using Xunit.Abstractions;
using FluentAssertions;

namespace Microsoft.DotNet.Tests
{
    public class GivenThatIWantToManageMulticoreJIT : TestBase
    {
        ITestOutputHelper _output;
        private const string OptimizationProfileFileName = "dotnet.";
        private readonly string _optimizationProfileFilePath;
        
        public GivenThatIWantToManageMulticoreJIT(ITestOutputHelper output)
        {
            _output = output;
            _optimizationProfileFilePath = GetOptimizationProfileFilePath();
        }

        [Fact]
        public void When_invoked_it_writes_optimization_data_to_the_profile_root()
        {
            var testStartTime = DateTime.UtcNow;

            new TestCommand("dotnet")
                .Execute("--version");

            File.Exists(_optimizationProfileFilePath)
                .Should().BeTrue("Because dotnet CLI creates it after each run");

            File.GetLastWriteTimeUtc(_optimizationProfileFilePath)
                .Should().BeOnOrAfter(testStartTime, "Because dotnet CLI was executed after that time.");
        }

        private static string GetOptimizationProfileFilePath()
        {
            Console.WriteLine(GetOptimizationRootPath(GetDotnetVersion()));
            return Path.Combine(GetOptimizationRootPath(GetDotnetVersion()),
                OptimizationProfileFileName);
        }

        private static string GetOptimizationRootPath(string version)
        {
            return RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? GetWindowsProfileRoot(version)
                : GetNonWindowsProfileRoot(version);
        }

        private static string GetWindowsProfileRoot(string version)
        {
            return $@"{(Environment.GetEnvironmentVariable("LocalAppData"))}\Microsoft\dotnet\sdk\{version}\optimizationdata";
        }

        private static string GetNonWindowsProfileRoot(string version)
        {
            return $"{(Environment.GetEnvironmentVariable("HOME"))}/.dotnet/sdk/{version}/optimizationdata";
        }

        private static string GetDotnetVersion()
        {
            return Command.Create("dotnet", new[] { "--version" })
                .CaptureStdOut()
                .Execute()
                .StdOut
                .Trim();
        }
    }
}
