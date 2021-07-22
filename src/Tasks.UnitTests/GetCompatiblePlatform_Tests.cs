// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.Shared;
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
            TaskItem childProj = new TaskItem("foo.bar");
            childProj.SetMetadata("PlatformOptions", "x64;x86;AnyCPU");

            GetCompatiblePlatform task = new GetCompatiblePlatform()
            {
                BuildEngine = new MockEngine(_output),
                CurrentProjectPlatform = "win32",
                PlatformLookupTable = "win32=x64",
                AnnotatedProjects = new TaskItem[] { childProj }
            };

            task.Execute();

            task.AssignedProjectsWithPlatform[0].GetMetadata("NearestPlatform").ShouldBe("x64");
        }

        [Fact]
        public void ResolvesViaChildsPlatformLookupTable()
        {
            // A child's PlatformLookupTable takes priority over the current project's table.
            // This allows overrides on a per-ProjectItem basis.
            TaskItem childProj = new TaskItem("foo.bar");
            childProj.SetMetadata("PlatformOptions", "x64;x86;AnyCPU");

            // childproj will be assigned x86 because its table takes priority
            childProj.SetMetadata("PlatformLookupTable", "win32=x86");

            GetCompatiblePlatform task = new GetCompatiblePlatform()
            {
                BuildEngine = new MockEngine(_output),
                CurrentProjectPlatform = "win32",
                PlatformLookupTable = "win32=x64",
                AnnotatedProjects = new TaskItem[] { childProj }
            };

            task.Execute();

            task.AssignedProjectsWithPlatform[0].GetMetadata("NearestPlatform").ShouldBe("x86");
        }

        [Fact]
        public void ResolvesViaAnyCPUDefault()
        {
            // No valid mapping via the lookup table, should default to AnyCPU when possible because
            // it is inherently compatible with any platform.

            TaskItem childProj = new TaskItem("foo.bar");
            childProj.SetMetadata("PlatformOptions", "x86;AnyCPU");

            GetCompatiblePlatform task = new GetCompatiblePlatform()
            {
                BuildEngine = new MockEngine(_output),
                CurrentProjectPlatform = "x86",
                PlatformLookupTable = "AnyCPU=x64", 
                AnnotatedProjects = new TaskItem[] { childProj }
            };

            task.Execute();

            task.AssignedProjectsWithPlatform[0].GetMetadata("NearestPlatform").ShouldBe("AnyCPU");
        }

        [Fact]
        public void ResolvesViaSamePlatform()
        {
            // No valid mapping via the lookup table, child project can't default to AnyCPU,
            // child project can match with parent project so match them.
            TaskItem childProj = new TaskItem("foo.bar");
            childProj.SetMetadata("PlatformOptions", "x86;x64");

            GetCompatiblePlatform task = new GetCompatiblePlatform()
            {
                BuildEngine = new MockEngine(_output),
                CurrentProjectPlatform = "x86",
                PlatformLookupTable = "AnyCPU=x64",
                AnnotatedProjects = new TaskItem[] { childProj }
            };

            task.Execute();

            task.AssignedProjectsWithPlatform[0].GetMetadata("NearestPlatform").ShouldBe("x86");
        }

        [Fact]
        public void FailsToResolve()
        {
            MockLogger log = new MockLogger(_output);
            // No valid mapping via the lookup table, child project can't default to AnyCPU,
            // child can't match with parent, log a warning.
            TaskItem childProj = new TaskItem("foo.bar");
            childProj.SetMetadata("PlatformOptions", "x64");

            GetCompatiblePlatform task = new GetCompatiblePlatform()
            {
                BuildEngine = new MockEngine(_output),
                CurrentProjectPlatform = "x86",
                PlatformLookupTable = "AnyCPU=x64",
                AnnotatedProjects = new TaskItem[] { childProj },
            };
            
            task.Execute();
            // When the task logs a warning, it does not set NearestPlatform
            task.AssignedProjectsWithPlatform[0].GetMetadata("NearestPlatform").ShouldBe("");
        }

        /// <summary>
        /// Invalid format on PlatformLookupTable results in an exception being thrown.
        /// </summary>
        [Fact]
        public void FailsOnInvalidFormatLookupTable()
        {
            MockLogger log = new MockLogger(_output);
            TaskItem childProj = new TaskItem("foo.bar");
            childProj.SetMetadata("PlatformOptions", "x64");

            GetCompatiblePlatform task = new GetCompatiblePlatform()
            {
                BuildEngine = new MockEngine(_output),
                CurrentProjectPlatform = "x86",
                PlatformLookupTable = "AnyCPU=;A=B", // invalid format
                AnnotatedProjects = new TaskItem[] { childProj },
            };

            Should.Throw<InternalErrorException>(() => task.Execute());
        }
    }
}
