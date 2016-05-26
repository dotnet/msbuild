// Copyright (c) .NET Foundation and contributors. All rights reserved. 
// Licensed under the MIT license. See LICENSE file in the project root for full license information. 

using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.TestFramework;
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
        private const string OptimizationProfileFileName = "dotnet";
        
        public GivenThatIWantToManageMulticoreJIT(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public void When_invoked_then_dotnet_writes_optimization_data_to_the_profile_root()
        {
            var testDirectory = TestAssetsManager.CreateTestDirectory();
            var testStartTime = GetTruncatedDateTime();
                        
            new TestCommand("dotnet")
                .WithUserProfileRoot(testDirectory.Path)
                .ExecuteWithCapturedOutput("--help");

            var optimizationProfileFilePath = GetOptimizationProfileFilePath(testDirectory.Path);

            File.Exists(optimizationProfileFilePath)
                .Should().BeTrue("Because dotnet CLI creates it after each run");

            File.GetLastWriteTimeUtc(optimizationProfileFilePath)
                .Should().BeOnOrAfter(testStartTime, "Because dotnet CLI was executed after that time");
        }

        [Fact]
        public void When_invoked_with_MulticoreJit_disabled_then_dotnet_does_not_writes_optimization_data_to_the_profile_root()
        {
            var testDirectory = TestAssetsManager.CreateTestDirectory();
            var testStartTime = GetTruncatedDateTime();
                        
            new TestCommand("dotnet")
                .WithUserProfileRoot(testDirectory.Path)
                .WithEnvironmentVariable("DOTNET_DISABLE_MULTICOREJIT", "1")
                .ExecuteWithCapturedOutput("--help");

            var optimizationProfileFilePath = GetOptimizationProfileFilePath(testDirectory.Path);

            File.Exists(optimizationProfileFilePath)
                .Should().BeFalse("Because multicore JIT is disabled");
        }

        [Fact]
        public void When_the_profile_root_is_undefined_then_dotnet_does_not_crash()
        {
            var testDirectory = TestAssetsManager.CreateTestDirectory();
            var testStartTime = GetTruncatedDateTime();
            
            var optimizationProfileFilePath = GetOptimizationProfileFilePath(testDirectory.Path);

            new TestCommand("dotnet")
                .WithUserProfileRoot("")
                .ExecuteWithCapturedOutput("--help")
                .Should().Pass();
        }

        [Fact]
        public void When_cli_repo_builds_then_dotnet_writes_optimization_data_to_the_default_profile_root()
        {
            var optimizationProfileFilePath = GetOptimizationProfileFilePath();

            File.Exists(optimizationProfileFilePath)
                .Should().BeTrue("Because the dotnet building dotnet writes to the default root");
        }

        private static string GetOptimizationProfileFilePath(string userHomePath = null)
        {
            return Path.Combine(
                GetUserProfileRoot(userHomePath), 
                GetOptimizationRootPath(GetDotnetVersion()),
                OptimizationProfileFileName);
        }
        
        private static string GetUserProfileRoot(string overrideUserProfileRoot = null)
        {
            if (overrideUserProfileRoot != null)
            {
                return overrideUserProfileRoot;
            }
            
            return RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? Environment.GetEnvironmentVariable("LocalAppData")
                : Environment.GetEnvironmentVariable("HOME");
        }

        private static string GetOptimizationRootPath(string version)
        {
            var rid = PlatformServices.Default.Runtime.GetRuntimeIdentifier();
            
            return RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? $@"Microsoft\dotnet\optimizationdata\{version}\{rid}" 
                : $@".dotnet/optimizationdata/{version}/{rid}";
        }

        private static string GetDotnetVersion()
        {
            return Command.Create("dotnet", new[] { "--version" })
                .CaptureStdOut()
                .Execute()
                .StdOut
                .Trim();
        }
        
        private static DateTime GetTruncatedDateTime()
        {
            var dt = DateTime.UtcNow;
            
            return new DateTime(dt.Year, dt.Month, dt.Day, dt.Hour, dt.Minute, dt.Second, 0, dt.Kind);
        }
    }
}
