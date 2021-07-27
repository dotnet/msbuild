// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.UnitTests;
using Microsoft.Build.Utilities;
using Shouldly;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Build.Tasks.UnitTests
{
    sealed public class GetCompatiblePlatform_Tests
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

            task.Execute();

            task.AssignedProjectsWithPlatform[0].GetMetadata("NearestPlatform").ShouldBe("x64");
        }

        [Fact]
        public void ResolvesViaChildsPlatformLookupTable()
        {
            // A child's PlatformLookupTable takes priority over the current project's table.
            // This allows overrides on a per-ProjectItem basis.
            TaskItem projectReference = new TaskItem("foo.bar");
            projectReference.SetMetadata("Platforms", "x64;x86;AnyCPU");

            // childproj will be assigned x86 because its table takes priority
            projectReference.SetMetadata("PlatformLookupTable", "win32=x86");

            GetCompatiblePlatform task = new GetCompatiblePlatform()
            {
                BuildEngine = new MockEngine(_output),
                CurrentProjectPlatform = "win32",
                PlatformLookupTable = "win32=x64",
                AnnotatedProjects = new TaskItem[] { projectReference }
            };

            task.Execute();

            task.AssignedProjectsWithPlatform[0].GetMetadata("NearestPlatform").ShouldBe("x86");
        }

        [Fact]
        public void ResolvesViaAnyCPUDefault()
        {
            // No valid mapping via the lookup table, should default to AnyCPU when the parent
            // and child's platforms don't match.
            TaskItem projectReference = new TaskItem("foo.bar");
            projectReference.SetMetadata("Platforms", "x64;AnyCPU");

            GetCompatiblePlatform task = new GetCompatiblePlatform()
            {
                BuildEngine = new MockEngine(_output),
                CurrentProjectPlatform = "x86",
                PlatformLookupTable = "AnyCPU=x64", 
                AnnotatedProjects = new TaskItem[] { projectReference }
            };

            task.Execute();

            task.AssignedProjectsWithPlatform[0].GetMetadata("NearestPlatform").ShouldBe("AnyCPU");
        }

        [Fact]
        public void ResolvesViaSamePlatform()
        {
            // No valid mapping via the lookup table. If the child's platform
            // matches the parent's platform, it takes priority over AnyCPU default.
            TaskItem projectReference = new TaskItem("foo.bar");
            projectReference.SetMetadata("Platforms", "x86;x64;AnyCPU");

            GetCompatiblePlatform task = new GetCompatiblePlatform()
            {
                BuildEngine = new MockEngine(_output),
                CurrentProjectPlatform = "x86",
                PlatformLookupTable = "AnyCPU=x64",
                AnnotatedProjects = new TaskItem[] { projectReference }
            };

            task.Execute();

            task.AssignedProjectsWithPlatform[0].GetMetadata("NearestPlatform").ShouldBe("x86");
        }

        [Fact]
        public void FailsToResolve()
        {
            // No valid mapping via the lookup table, child project can't default to AnyCPU,
            // child can't match with parent, log a warning.
            TaskItem projectReference = new TaskItem("foo.bar");
            projectReference.SetMetadata("Platforms", "x64");

            GetCompatiblePlatform task = new GetCompatiblePlatform()
            {
                BuildEngine = new MockEngine(_output),
                CurrentProjectPlatform = "x86",
                PlatformLookupTable = "AnyCPU=x64",
                AnnotatedProjects = new TaskItem[] { projectReference },
            };

            task.Execute();
            // When the task logs a warning, it does not set NearestPlatform
            task.AssignedProjectsWithPlatform[0].GetMetadata("NearestPlatform").ShouldBe("");
            ((MockEngine)task.BuildEngine).AssertLogContains("MSB3981");
        }

        [Fact]
        public void WarnsWhenProjectReferenceHasNoPlatformOptions()
        {
            // Task should log a warning when a ProjectReference has no options to build as.
            // It will continue and have no NearestPlatform metadata.
            TaskItem projectReference = new TaskItem("foo.bar");
            projectReference.SetMetadata("Platforms", "");

            GetCompatiblePlatform task = new GetCompatiblePlatform()
            {
                BuildEngine = new MockEngine(_output),
                CurrentProjectPlatform = "x86",
                PlatformLookupTable = "AnyCPU=x64",
                AnnotatedProjects = new TaskItem[] { projectReference },
            };

            task.Execute();
            // When the task logs a warning, it does not set NearestPlatform
            task.AssignedProjectsWithPlatform[0].GetMetadata("NearestPlatform").ShouldBe("");
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

            task.Execute();
            // When the platformlookuptable is in an invalid format, it is discarded.
            // There shouldn't have been a translation found from AnyCPU to anything.
            // Meaning the projectreference would not have NearestPlatform set.
            task.AssignedProjectsWithPlatform[0].GetMetadata("NearestPlatform").ShouldBe("");
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

            task.Execute();

            // A ProjectReference PlatformLookupTable should take priority, but is thrown away when
            // it has an invalid format. The current project's PLT should be the next priority.
            task.AssignedProjectsWithPlatform[0].GetMetadata("NearestPlatform").ShouldBe("x86");
            ((MockEngine)task.BuildEngine).AssertLogContains("MSB3983");
        }
    }
}
