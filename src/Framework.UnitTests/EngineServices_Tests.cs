// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;

using Microsoft.Build.Framework;

using Shouldly;
using Xunit;

namespace Microsoft.Build.UnitTests
{
    public sealed class EngineServices_Tests
    {
        [Fact]
        public void BaseProgressReporterIsNoOp()
        {
            EngineServices services = new TestEngineServices();

            using ITaskProgressReporter reporter = services.CreateTaskProgressReporter(
                "Downloading package",
                TaskProgressUnit.Bytes);

            reporter.Report(new TaskProgressUpdate(10, 100, "Downloading"));
            reporter.Complete("Downloaded");

            services.Version.ShouldBe(EngineServices.Version3);
        }

        [Fact]
        public void ProgressReporterRequiresTitle()
        {
            EngineServices services = new TestEngineServices();

            Should.Throw<ArgumentNullException>(() => services.CreateTaskProgressReporter(null!));
            Should.Throw<ArgumentException>(() => services.CreateTaskProgressReporter(string.Empty));
            Should.Throw<ArgumentException>(() => services.CreateTaskProgressReporter(" "));
        }

        [Fact]
        public void ProgressUpdatePreservesValues()
        {
            var update = new TaskProgressUpdate(10, 100, "Downloading");

            update.Completed.ShouldBe(10);
            update.Total.ShouldBe(100);
            update.Status.ShouldBe("Downloading");
        }

        private sealed class TestEngineServices : EngineServices
        {
        }
    }
}
