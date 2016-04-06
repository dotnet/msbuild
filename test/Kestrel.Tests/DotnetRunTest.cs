// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using Microsoft.DotNet.TestFramework;
using Microsoft.DotNet.Tools.Test.Utilities;
using Xunit;
using System.Net.Http;
using System.Threading.Tasks;
using FluentAssertions;

namespace Microsoft.DotNet.Kestrel.Tests
{
    public class DotnetRunTest : TestBase
    {
        private const string KestrelSampleBase = "KestrelSample";
        private const string KestrelPortable = "KestrelPortable";
        private const string KestrelStandalone = "KestrelStandalone";

        [Fact]
        public void ItRunsKestrelPortableApp()
        {
            TestInstance instance = TestAssetsManager.CreateTestInstance(KestrelSampleBase)
                                                     .WithLockFiles();

            var url = NetworkHelper.GetLocalhostUrlWithFreePort();
            var args = $"{url} {Guid.NewGuid().ToString()}";
            var runCommand = new RunCommand(Path.Combine(instance.TestRoot, KestrelPortable));

            try
            {
                runCommand.ExecuteAsync(args);
                NetworkHelper.IsServerUp(url).Should().BeTrue($"Unable to connect to kestrel server - {KestrelPortable} @ {url}");
                NetworkHelper.TestGetRequest(url, args);
            }
            finally
            {
                runCommand.KillTree();
            }
        }

        [Fact]
        public void ItRunsKestrelStandaloneApp()
        {
            TestInstance instance = TestAssetsManager.CreateTestInstance(KestrelSampleBase)
                                                     .WithLockFiles();

            var url = NetworkHelper.GetLocalhostUrlWithFreePort();
            var args = $"{url} {Guid.NewGuid().ToString()}";
            var runCommand = new RunCommand(Path.Combine(instance.TestRoot, KestrelStandalone));

            try
            {
                runCommand.ExecuteAsync(args);
                NetworkHelper.IsServerUp(url).Should().BeTrue($"Unable to connect to kestrel server - {KestrelStandalone} @ {url}");
                NetworkHelper.TestGetRequest(url, args);
            }
            finally
            {
                runCommand.KillTree();
            }
        }
    }
}
