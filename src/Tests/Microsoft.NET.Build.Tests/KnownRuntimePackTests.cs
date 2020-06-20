using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using FluentAssertions;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Microsoft.NET.TestFramework.ProjectConstruction;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.NET.Build.Tests
{
    public class KnownRuntimePackTests : SdkTest
    {
        public KnownRuntimePackTests(ITestOutputHelper log) : base(log)
        {
        }

        [Fact]
        public void BuildSucceedsWithRuntimePackWithDifferentLabel()
        {
            var testProject = new TestProject()
            {
                TargetFrameworks = "net5.0",
                IsSdkProject = true,
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
                IsSdkProject = true,
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
                IsSdkProject = true,
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

        private XElement CreateTestKnownRuntimePack()
        {
            var knownRuntimePack = new XElement("KnownRuntimePack",
                        new XAttribute("Include", "Microsoft.NETCore.App"),
                        new XAttribute("TargetFramework", "net5.0"),
                        new XAttribute("RuntimeFrameworkName", "Microsoft.NETCore.App"),
                        new XAttribute("DefaultRuntimeFrameworkVersion", "5.0.0-preview1"),
                        new XAttribute("LatestRuntimeFrameworkVersion", "5.0.0-preview1.1"),
                        new XAttribute("RuntimePackNamePatterns", "Microsoft.NETCore.App.Runtime.Mono.**RID**"),
                        new XAttribute("RuntimePackRuntimeIdentifiers", "linux-arm;linux-arm64;linux-musl-arm64;linux-musl-x64;linux-x64;osx-x64;rhel.6-x64;tizen.4.0.0-armel;tizen.5.0.0-armel;win-arm;win-arm64;win-x64;win-x86;ios-arm64;ios-arm;ios-x64;ios-x86;tvos-arm64;tvos-x64;android-arm64;android-arm;android-x64;android-x86;browser-wasm"),
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
