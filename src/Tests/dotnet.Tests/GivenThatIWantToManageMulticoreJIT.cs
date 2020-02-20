// Copyright (c) .NET Foundation and contributors. All rights reserved. 
// Licensed under the MIT license. See LICENSE file in the project root for full license information. 

using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Tools.Test.Utilities;
using System;
using System.IO;
using System.Runtime.InteropServices;
using Xunit;
using Xunit.Abstractions;
using FluentAssertions;
using Microsoft.DotNet.PlatformAbstractions;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;

namespace Microsoft.DotNet.Tests
{
    public class GivenThatIWantToManageMulticoreJIT : SdkTest
    {
        private const string OptimizationProfileFileName = "dotnet";
        
        public GivenThatIWantToManageMulticoreJIT(ITestOutputHelper log) : base(log)
        {
        }

        [Fact]
        public void WhenInvokedThenDotnetWritesOptimizationDataToTheProfileRoot()
        {
            var testDirectory = _testAssetsManager.CreateTestDirectory();
            var testStartTime = GetTruncatedDateTime();
                        
            new DotnetCommand(Log)
                .WithUserProfileRoot(testDirectory.Path)
                .Execute("--help");

            var optimizationProfileFilePath = GetOptimizationProfileFilePath(testDirectory.Path);

            new FileInfo(optimizationProfileFilePath)
                .Should().Exist("Because dotnet CLI creates it after each run")
                     .And.HaveLastWriteTimeUtc()
                         .Which.Should().BeOnOrAfter(testStartTime, "Because dotnet CLI was executed after that time");
        }

        [Fact]
        public void WhenInvokedWithMulticoreJitDisabledThenDotnetDoesNotWriteOptimizationDataToTheProfileRoot()
        {
            var testDirectory = _testAssetsManager.CreateTestDirectory();
            var testStartTime = GetTruncatedDateTime();
                        
            new DotnetCommand(Log)
                .WithUserProfileRoot(testDirectory.Path)
                .WithEnvironmentVariable("DOTNET_DISABLE_MULTICOREJIT", "1")
                .Execute("--help");

            var optimizationProfileFilePath = GetOptimizationProfileFilePath(testDirectory.Path);

            File.Exists(optimizationProfileFilePath)
                .Should().BeFalse("Because multicore JIT is disabled");
        }

        [Fact]
        public void WhenCliRepoBuildsThenDotnetWritesOptimizationDataToTheDefaultProfileRoot()
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
            var rid = PlatformAbstractions.RuntimeEnvironment.GetRuntimeIdentifier();
            
            return RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? $@"Microsoft\dotnet\optimizationdata\{version}\{rid}" 
                : $@".dotnet/optimizationdata/{version}/{rid}";
        }

        private static string GetDotnetVersion()
        {
            return TestContext.Current.ToolsetUnderTest.SdkVersion;
        }
        
        private static DateTime GetTruncatedDateTime()
        {
            var dt = DateTime.UtcNow;
            
            return new DateTime(dt.Year, dt.Month, dt.Day, dt.Hour, dt.Minute, dt.Second, 0, dt.Kind);
        }
    }
}
