// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Shouldly;
using Xunit;

using Microsoft.Build.Shared;
using System.Diagnostics;
using System.Threading.Tasks;
using Xunit.Abstractions;

namespace Microsoft.Build.UnitTests
{
    public class ProcessExtensions_Tests
    {
        private readonly ITestOutputHelper output;

        public ProcessExtensions_Tests(ITestOutputHelper output)
        {
            this.output = output;
        }

        [Fact]
        public async Task KillTree()
        {
            Process p = Process.Start("sleep", "600"); // sleep 10m.

            output.WriteLine(p.MainModule.FileName);

            // Verify the process is running.
            await Task.Delay(500);
            p.HasExited.ShouldBe(false);

            // Kill the process.
            p.KillTree(timeout: 5000);
            p.HasExited.ShouldBe(true);
            p.ExitCode.ShouldNotBe(0);
        }
    }
}
