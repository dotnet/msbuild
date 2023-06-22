// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FluentAssertions;
using Microsoft.DotNet.Cli.NuGetPackageDownloader.WorkloadUnixFilePermissions;
using Microsoft.DotNet.ToolPackage;
using Microsoft.DotNet.Tools;
using NuGet.Protocol.Core.Types;
using Xunit;

namespace Microsoft.DotNet.PackageInstall.Tests
{
    public class UnixFilePermissionsTests
    {
        [Fact]
        public void GivenXmlPathItShouldDeserialize()
        {
            var fileList = FileList.Deserialize("UnixFilePermissionsSample.xml");

            fileList.File.Length.Should().Be(4);
            fileList.File[0].Path.Should().Be("tools/bin/bgen");
            fileList.File[0].Permission.Should().Be("755");
        }
    }
}
