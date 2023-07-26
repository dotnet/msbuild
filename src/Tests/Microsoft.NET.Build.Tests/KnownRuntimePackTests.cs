// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.NET.Build.Tests
{
    public class KnownRuntimePackTests : SdkTest
    {
        public KnownRuntimePackTests(ITestOutputHelper log) : base(log)
        {
        }

        [RequiresMSBuildVersionFact("16.8.0")]
        public void BuildSucceedsWithRuntimePackWithDifferentLabel()
        {
            var testProject = new TestProject()
            {
                TargetFrameworks = "net5.0",
                IsExe = true,
                RuntimeIdentifier = EnvironmentInfo.GetCompatibleRid()
            };

            var testAsset = _testAssetsManager.CreateTestProject(testProject);

            var knownRuntimePack = CreateTestKnownRuntimePack();

            AddItem(testAsset, knownRuntimePack);

            var buildCommand = new BuildCommand(testAsset);

            buildCommand
                .Execute()
                .Should()
                .Pass();
        }

        [Fact]
        public void DuplicateRuntimePackCausesFailure()
        {
            var testProject = new TestProject()
            {
                TargetFrameworks = "net5.0",
                IsExe = true,
                RuntimeIdentifier = EnvironmentInfo.GetCompatibleRid()
            };

            var testAsset = _testAssetsManager.CreateTestProject(testProject);

            var knownRuntimePack = CreateTestKnownRuntimePack();
            knownRuntimePack.Attribute("RuntimePackLabels").Value = "";

            AddItem(testAsset, knownRuntimePack);

            var buildCommand = new BuildCommand(testAsset);

            buildCommand
                .Execute()
                .Should()
                .Fail()
                .And
                .HaveStdOutContaining("NETSDK1133");
        }

        [Fact]
        public void RuntimePackWithLabelIsSelected()
        {
            var testProject = new TestProject()
            {
                TargetFrameworks = "net5.0",
                IsExe = true,
                RuntimeIdentifier = EnvironmentInfo.GetCompatibleRid()
            };

            var testAsset = _testAssetsManager.CreateTestProject(testProject);

            var knownRuntimePack = CreateTestKnownRuntimePack();

            AddItem(testAsset, knownRuntimePack);

            var frameworkReferenceUpdate = new XElement("FrameworkReference",
                new XAttribute("Update", "Microsoft.NETCore.App"),
                new XAttribute("RuntimePackLabels", "Mono"));

            AddItem(testAsset, frameworkReferenceUpdate);

            var getValuesCommand = new GetValuesCommand(testAsset, "RuntimePack", GetValuesCommand.ValueType.Item)
            {
                DependsOnTargets = "ProcessFrameworkReferences",
                ShouldRestore = false
            };

            getValuesCommand
                .Execute()
                .Should()
                .Pass();

            //  StartsWith instead of exact match because current RID is likely to be more specific than the runtime pack RID
            getValuesCommand.GetValues().Should().Contain(rp => rp.StartsWith("Microsoft.NETCore.App.Runtime.Mono."));

        }

        [Fact]
        public void AspNetRuntimePackIsNotRestoredForAndroid()
        {
            var testProject = new TestProject()
            {
                TargetFrameworks = ToolsetInfo.CurrentTargetFramework,
                IsExe = true
            };
            testProject.AdditionalProperties["RuntimeIdentifiers"] = "android-arm;android-arm64;android-x86;android-x64";

            var testAsset = _testAssetsManager.CreateTestProject(testProject);

            var knownFrameworkReferenceUpdate = new XElement("KnownFrameworkReference",
                new XAttribute("Update", "Microsoft.AspNetCore.App"),
                new XAttribute("RuntimePackExcludedRuntimeIdentifiers", "android"));

            AddItem(testAsset, knownFrameworkReferenceUpdate);

            var getValuesCommand = new GetValuesCommand(testAsset, "PackageDownload", GetValuesCommand.ValueType.Item)
            {
                DependsOnTargets = "ProcessFrameworkReferences",
                ShouldRestore = false
            };

            getValuesCommand
                .Execute()
                .Should()
                .Pass();

            var packageDownloads = getValuesCommand.GetValues();

            packageDownloads.Should().NotContain(packageDownload => packageDownload.StartsWith("Microsoft.AspNetCore.App.Runtime."));
        }

        private XElement CreateTestKnownRuntimePack()
        {
            var knownRuntimePack = new XElement("KnownRuntimePack",
                        new XAttribute("Include", "Microsoft.NETCore.App"),
                        new XAttribute("TargetFramework", "net5.0"),
                        new XAttribute("RuntimeFrameworkName", "Microsoft.NETCore.App"),
                        new XAttribute("DefaultRuntimeFrameworkVersion", "5.0.0-preview1"),
                        new XAttribute("LatestRuntimeFrameworkVersion", "5.0.0-preview1.1"),
                        new XAttribute("RuntimePackNamePatterns", "Microsoft.NETCore.App.Runtime.Mono.**RID**"),
                        new XAttribute("RuntimePackRuntimeIdentifiers", "linux-arm;linux-arm64;linux-musl-arm64;linux-musl-x64;linux-x64;osx-x64;rhel.6-x64;tizen.4.0.0-armel;tizen.5.0.0-armel;win-arm;win-arm64;win-x64;win-x86;ios-arm64;ios-arm;iossimulator-x64;iossimulator-arm64;iossimulator-x86;tvos-arm64;tvossimulator-x64;tvossimulator-arm64;android-arm64;android-arm;android-x64;android-x86;browser-wasm;maccatalyst-x64;maccatalyst-arm64"),
                        new XAttribute("IsTrimmable", "true"),
                        new XAttribute("RuntimePackLabels", "Mono"));

            return knownRuntimePack;
        }

        private void AddItem(TestAsset testAsset, XElement item)
        {
            testAsset.WithProjectChanges(project =>
            {
                var ns = project.Root.Name.Namespace;

                var itemGroup = new XElement(ns + "ItemGroup");
                project.Root.Add(itemGroup);
                itemGroup.Add(item);
            });
        }
    }
}
