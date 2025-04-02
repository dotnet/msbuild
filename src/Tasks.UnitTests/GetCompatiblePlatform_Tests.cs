// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.UnitTests;
using Microsoft.Build.Utilities;
using Shouldly;
using Xunit;
using Xunit.Abstractions;

#nullable disable

namespace Microsoft.Build.Tasks.UnitTests
{
    public sealed class GetCompatiblePlatform_Tests
    {
        private readonly ITestOutputHelper _output;

        public GetCompatiblePlatform_Tests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public void ResolvesViaPlatformLookupTable()
        {
            // PlatformLookupTable always takes priority. It is typically user-defined.
            TaskItem projectReference = new TaskItem("foo.bar");
            projectReference.SetMetadata("Platforms", "x64;x86;AnyCPU");

            GetCompatiblePlatform task = new GetCompatiblePlatform()
            {
                BuildEngine = new MockEngine(_output),
                CurrentProjectPlatform = "win32",
                PlatformLookupTable = "win32=x64",
                AnnotatedProjects = new TaskItem[] { projectReference }
            };

            task.Execute().ShouldBeTrue();

            task.AssignedProjectsWithPlatform[0].GetMetadata("NearestPlatform").ShouldBe("x64");
        }


        [Fact]
        public void ResolvesViaOverride()
        {
            // OverridePlatformNegotiationValue always takes priority over everything. It is typically user-defined.
            TaskItem projectReference = new TaskItem("foo.bar");
            projectReference.SetMetadata("Platforms", "x64;x86;AnyCPU");
            projectReference.SetMetadata("platform", "x86");
            projectReference.SetMetadata("OverridePlatformNegotiationValue", "x86");

            GetCompatiblePlatform task = new GetCompatiblePlatform()
            {
                BuildEngine = new MockEngine(_output),
                CurrentProjectPlatform = "x64",
                PlatformLookupTable = "win32=x64",
                AnnotatedProjects = new TaskItem[] { projectReference }
            };

            task.Execute().ShouldBeTrue();

            task.AssignedProjectsWithPlatform[0].GetMetadata("NearestPlatform").ShouldBe("");
        }

        [Fact]
        public void ResolvesViaProjectReferencesPlatformLookupTable()
        {
            // A ProjectReference's PlatformLookupTable takes priority over the current project's table.
            // This allows overrides on a per-ProjectItem basis.
            TaskItem projectReference = new TaskItem("foo.bar");
            projectReference.SetMetadata("Platforms", "x64;x86;AnyCPU");

            // ProjectReference will be assigned x86 because its table takes priority
            projectReference.SetMetadata("PlatformLookupTable", "win32=x86");

            GetCompatiblePlatform task = new GetCompatiblePlatform()
            {
                BuildEngine = new MockEngine(_output),
                CurrentProjectPlatform = "win32",
                PlatformLookupTable = "win32=x64",
                AnnotatedProjects = new TaskItem[] { projectReference }
            };

            task.Execute().ShouldBeTrue();

            task.AssignedProjectsWithPlatform[0].GetMetadata("NearestPlatform").ShouldBe("x86");
        }

        [Fact]
        public void ResolvesViaAnyCPUDefault()
        {
            // No valid mapping via the lookup table, should default to AnyCPU when the current project
            // and ProjectReference platforms don't match.
            TaskItem projectReference = new TaskItem("foo.bar");
            projectReference.SetMetadata("Platforms", "x64;AnyCPU");

            GetCompatiblePlatform task = new GetCompatiblePlatform()
            {
                BuildEngine = new MockEngine(_output),
                CurrentProjectPlatform = "x86",
                PlatformLookupTable = "AnyCPU=x64",
                AnnotatedProjects = new TaskItem[] { projectReference }
            };

            task.Execute().ShouldBeTrue();

            task.AssignedProjectsWithPlatform[0].GetMetadata("NearestPlatform").ShouldBe("AnyCPU");
        }

        [Fact]
        public void ResolvesViaAnyCPUDefaultWithDefaultPlatformEnabled()
        {
            // No valid mapping via the lookup table, should default to AnyCPU when the current project
            // and ProjectReference platforms don't match.
            TaskItem projectReference = new TaskItem("foo.bar");
            projectReference.SetMetadata("Platforms", "x64;AnyCPU");
            projectReference.SetMetadata("Platform", "AnyCPU");

            GetCompatiblePlatform task = new GetCompatiblePlatform()
            {
                BuildEngine = new MockEngine(_output),
                CurrentProjectPlatform = "x86",
                PlatformLookupTable = "AnyCPU=x64",
                AnnotatedProjects = new TaskItem[] { projectReference }
            };

            task.Execute().ShouldBeTrue();

            task.AssignedProjectsWithPlatform[0].GetMetadata("NearestPlatform").ShouldBe(string.Empty);
        }

        [Fact]
        public void ResolvesViaSamePlatform()
        {
            // No valid mapping via the lookup table. If the ProjectReference's platform
            // matches the current project's platform, it takes priority over AnyCPU default.
            TaskItem projectReference = new TaskItem("foo.bar");
            projectReference.SetMetadata("Platforms", "x86;x64;AnyCPU");
            projectReference.SetMetadata("PlatformLookupTable", "x86=AnyCPU"); // matching platform takes priority over lookup tables

            GetCompatiblePlatform task = new GetCompatiblePlatform()
            {
                BuildEngine = new MockEngine(_output),
                CurrentProjectPlatform = "x86",
                PlatformLookupTable = "x86=AnyCPU",
                AnnotatedProjects = new TaskItem[] { projectReference }
            };

            task.Execute().ShouldBeTrue();

            task.AssignedProjectsWithPlatform[0].GetMetadata("NearestPlatform").ShouldBe("x86");
        }

        [Fact]
        public void FailsToResolve()
        {
            // No valid mapping via the lookup table, ProjectReference can't default to AnyCPU,
            // it also can't match with current project, log a warning.
            TaskItem projectReference = new TaskItem("foo.bar");
            projectReference.SetMetadata("Platforms", "x64");

            GetCompatiblePlatform task = new GetCompatiblePlatform()
            {
                BuildEngine = new MockEngine(_output),
                CurrentProjectPlatform = "x86",
                PlatformLookupTable = "AnyCPU=x64",
                AnnotatedProjects = new TaskItem[] { projectReference },
            };

            task.Execute().ShouldBeTrue();
            // When the task logs a warning, it does not set NearestPlatform
            task.AssignedProjectsWithPlatform[0].GetMetadata("NearestPlatform").ShouldBe(string.Empty);
            ((MockEngine)task.BuildEngine).AssertLogContains("MSB3981");
        }

        [Fact]
        public void WarnsWhenProjectReferenceHasNoPlatformOptions()
        {
            // Task should log a warning when a ProjectReference has no options to build as.
            // It will continue and have no NearestPlatform metadata.
            TaskItem projectReference = new TaskItem("foo.bar");
            projectReference.SetMetadata("Platforms", string.Empty);
            projectReference.SetMetadata("Platform", string.Empty);

            GetCompatiblePlatform task = new GetCompatiblePlatform()
            {
                BuildEngine = new MockEngine(_output),
                CurrentProjectPlatform = "x86",
                PlatformLookupTable = "AnyCPU=x64",
                AnnotatedProjects = new TaskItem[] { projectReference },
            };

            task.Execute().ShouldBeTrue();
            // When the task logs a warning, it does not set NearestPlatform
            task.AssignedProjectsWithPlatform[0].GetMetadata("NearestPlatform").ShouldBe(string.Empty);
            ((MockEngine)task.BuildEngine).AssertLogContains("MSB3982");
        }

        /// <summary>
        /// Invalid format on PlatformLookupTable results in an exception being thrown.
        /// </summary>
        [Fact]
        public void WarnsOnInvalidFormatLookupTable()
        {
            TaskItem projectReference = new TaskItem("foo.bar");
            projectReference.SetMetadata("Platforms", "x64");

            GetCompatiblePlatform task = new GetCompatiblePlatform()
            {
                BuildEngine = new MockEngine(_output),
                CurrentProjectPlatform = "AnyCPU",
                PlatformLookupTable = "AnyCPU=;A=B", // invalid format
                AnnotatedProjects = new TaskItem[] { projectReference },
            };

            task.Execute().ShouldBeTrue();
            // When the platformlookuptable is in an invalid format, it is discarded.
            // There shouldn't have been a translation found from AnyCPU to anything.
            // Meaning the projectreference would not have NearestPlatform set.
            task.AssignedProjectsWithPlatform[0].GetMetadata("NearestPlatform").ShouldBe(string.Empty);
            ((MockEngine)task.BuildEngine).AssertLogContains("MSB3983");
        }

        /// <summary>
        /// Invalid format on PlatformLookupTable from the projectreference results in an exception being thrown.
        /// </summary>
        [Fact]
        public void WarnsOnInvalidFormatProjectReferenceLookupTable()
        {
            TaskItem projectReference = new TaskItem("foo.bar");
            projectReference.SetMetadata("Platforms", "x64;x86");
            projectReference.SetMetadata("PlatformLookupTable", "x86=;b=d");

            GetCompatiblePlatform task = new GetCompatiblePlatform()
            {
                BuildEngine = new MockEngine(_output),
                CurrentProjectPlatform = "AnyCPU",
                PlatformLookupTable = "AnyCPU=x86;A=B", // invalid format
                AnnotatedProjects = new TaskItem[] { projectReference },
            };

            task.Execute().ShouldBeTrue();

            // A ProjectReference PlatformLookupTable should take priority, but is thrown away when
            // it has an invalid format. The current project's PLT should be the next priority.
            task.AssignedProjectsWithPlatform[0].GetMetadata("NearestPlatform").ShouldBe("x86");
            ((MockEngine)task.BuildEngine).AssertLogContains("MSB3983");
        }

        // When `Platform` is retrieved in "GetTargetFrameworks" and that platform matches what's currently
        // being built, build that project _without_ a global property for Platform.
        [Theory]
        [InlineData("x86;AnyCPU", "x64", "x64")] // Referenced platform matches current platform, build w/o global property
        [InlineData("x64;x86;AnyCPU", "x64", "x64")] // Referenced platform overrides 'Platforms' being an option
        public void PlatformIsChosenAsDefault(string referencedPlatforms, string referencedPlatform, string currentPlatform)
        {
            TaskItem projectReference = new TaskItem("foo.bar");
            projectReference.SetMetadata("Platforms", referencedPlatforms);
            projectReference.SetMetadata("Platform", referencedPlatform);

            GetCompatiblePlatform task = new GetCompatiblePlatform()
            {
                BuildEngine = new MockEngine(_output),
                CurrentProjectPlatform = currentPlatform,
                AnnotatedProjects = new TaskItem[] { projectReference }
            };

            task.Execute().ShouldBeTrue();

            task.AssignedProjectsWithPlatform[0].GetMetadata("NearestPlatform").ShouldBe(string.Empty);
            task.Log.HasLoggedErrors.ShouldBeFalse();
        }

        // When `Platform` is retrieved in "GetTargetFrameworks" and that platform matches what the task has decided the project should be built as
        // through negotiation. build that project _without_ a global property for Platform.
        [Fact]
        public void ChosenPlatformMatchesDefault()
        {
            TaskItem projectReference = new TaskItem("foo.bar");
            projectReference.SetMetadata("Platforms", "AnyCPU;x64");
            projectReference.SetMetadata("Platform", "AnyCPU");

            GetCompatiblePlatform task = new GetCompatiblePlatform()
            {
                BuildEngine = new MockEngine(_output),
                CurrentProjectPlatform = "x86",
                PlatformLookupTable = "", // invalid format
                AnnotatedProjects = new TaskItem[] { projectReference },
            };

            task.Execute().ShouldBeTrue();

            task.AssignedProjectsWithPlatform[0].GetMetadata("NearestPlatform").ShouldBe(string.Empty);
            task.Log.HasLoggedErrors.ShouldBeFalse();
        }
    }
}
