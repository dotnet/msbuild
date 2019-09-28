// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO;
using System.Linq;
using FluentAssertions;
using Microsoft.NET.Build.Tasks;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Xunit.Abstractions;

namespace Microsoft.NET.Build.Tests
{
    public class GivenThatWeWantToPackACppCliProject : SdkTest
    {
        public GivenThatWeWantToPackACppCliProject(ITestOutputHelper log) : base(log)
        {
        }

        [FullMSBuildOnlyFact]
        public void It_cannot_pack_the_cppcliproject()
        {
            var testAsset = _testAssetsManager
                .CopyTestAsset("NetCoreCsharpAppReferenceCppCliLib")
                .WithSource();

            new PackCommand(Log, Path.Combine(testAsset.TestRoot, "NETCoreCppCliTest", "NETCoreCppCliTest.vcxproj"))
                .Execute("-p:Platform=x64")
                .Should()
                .Fail()
                .And
                .HaveStdOutContaining(Strings.NoSupportCppPackDotnetCore);
        }
    }
}
