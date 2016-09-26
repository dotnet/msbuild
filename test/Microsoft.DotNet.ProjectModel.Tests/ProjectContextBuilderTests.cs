using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using FluentAssertions;
using Microsoft.DotNet.Tools.Test.Utilities;
using NuGet.Frameworks;
using NuGet.ProjectModel;
using Xunit;

namespace Microsoft.DotNet.ProjectModel.Tests
{
    public class ProjectContextBuilderTests : TestBase
    {
        private static readonly HashSet<string> KnownProperties = new HashSet<string>(StringComparer.Ordinal) {
            "Project",
            "LockFile",
            "TargetFramework",
            "RuntimeIdentifiers",
            "RootDirectory",
            "ProjectDirectory",
            "PackagesDirectory",
            "ReferenceAssembliesPath",
            "IsDesignTime",
            "ProjectResolver",
            "LockFileResolver",
            "ProjectReaderSettings"
        };

        // This test ensures that Clone is always kept up-to-date to avoid hard-to-debug errors
        // because someone added a property but didn't put it in Clone.
        [Fact]
        public void CloneTest()
        {
            // Initialize a test instance that we're going to clone. Make sure all properties are initialized here.
            var initialBuilder = new ProjectContextBuilder()
                .WithProject(new Project())
                .WithLockFile(new LockFile())
                .WithTargetFramework(FrameworkConstants.CommonFrameworks.NetStandard10)
                .WithRuntimeIdentifiers(new[] { "win7-x64", "osx.10.10-x64" })
                .WithRootDirectory("C:\\The\\Root")
                .WithProjectDirectory("/where/the/project/at")
                .WithPackagesDirectory("D:\\My\\Awesome\\NuGet\\Packages")
                .WithReferenceAssembliesPath("/these/are/the/reference/assemblies")
                .WithProjectResolver(_ => new Project())
                .WithLockFileResolver(_ => new LockFile())
                .WithProjectReaderSettings(new ProjectReaderSettings());

            // Clone the builder
            var clonedBuilder = initialBuilder.Clone();

            // Compare all the properties. This is a shallow clone, so they should all be exactly ReferenceEqual
            foreach (var prop in typeof(ProjectContextBuilder).GetTypeInfo().DeclaredProperties)
            {
                KnownProperties.Remove(prop.Name).Should().BeTrue(because: $"{prop.Name} should be listed in the known properties to ensure it is properly tested.");

                if (prop.PropertyType.GetTypeInfo().IsValueType)
                {
                    // Can't use reference equality on value types
                    prop.GetValue(clonedBuilder).Should().Be(prop.GetValue(initialBuilder), because: $"clone should have duplicated the {prop.Name} property");
                }
                else
                {
                    prop.GetValue(clonedBuilder).Should().BeSameAs(prop.GetValue(initialBuilder), because: $"clone should have duplicated the {prop.Name} property");
                }
            }

            KnownProperties.Should().BeEmpty(because: "all properties should have been checked by the CloneTest");
        }

        [Fact]
        public void BuildAllTargetsProperlyDeduplicatesTargets()
        {
            // Load all project contexts for the test project
            var contexts = new ProjectContextBuilder()
                .WithProjectDirectory(Path.Combine(TestAssetsManager.AssetsRoot, "TestProjectContextBuildAllDedupe"))
                .BuildAllTargets()
                .ToList();

            // This is a portable app, so even though RIDs are specified, BuildAllTargets should only produce one output.
            Assert.Equal(1, contexts.Count);
        }
    }
}
