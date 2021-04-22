// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Shouldly;
using Xunit;

using Microsoft.Build.Shared;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Microsoft.Build.UnitTests
{
    public class ProcessExtensions_Tests
    {
        [Fact]
        public async Task KillTree()
        {
            var psi =
                NativeMethodsShared.IsWindows ?
                    new ProcessStartInfo("powershell", "-NoLogo -NoProfile -command \"Start-Sleep -Seconds 600\"") :
                    new ProcessStartInfo("sleep", "600");

            Process p = Process.Start(psi); // sleep 10m.

            // Verify the process is running.
            await Task.Delay(500);
            p.HasExited.ShouldBe(false);

            // Kill the process.
            p.KillTree(timeoutMilliseconds: 5000);
            p.HasExited.ShouldBe(true);
            p.ExitCode.ShouldNotBe(0);
        }
    }
}
