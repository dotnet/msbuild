// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Microsoft.DotNet.Tools.New;
using Microsoft.DotNet.Tools.Test.Utilities;
using Microsoft.DotNet.Workloads.Workload.Install.InstallRecord;
using Microsoft.DotNet.Workloads.Workload.List;
using Microsoft.NET.Sdk.WorkloadManifestReader;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Commands;
using Xunit;
using Xunit.Abstractions;
using Moq;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.TemplateEngine.Abstractions.Components;

namespace Microsoft.DotNet.New.Tests
{
    public class SdkInfoProviderTests
    {
        [Fact]
        public void GetInstalledVersionsAsync_ShouldContainCurrentVersion()
        {
            ISdkInfoProvider sp = new SdkInfoProvider();

            string currentVersion = sp.GetCurrentVersionAsync(default).Result;
            List<string> allVersions = sp.GetInstalledVersionsAsync(default).Result?.ToList();

            currentVersion.Should().NotBeNullOrEmpty("Current Sdk version should be populated");
            allVersions.Should().NotBeNull();
            allVersions.Should().Contain(currentVersion, "All installed versions should contain current version");
        }
    }
}
