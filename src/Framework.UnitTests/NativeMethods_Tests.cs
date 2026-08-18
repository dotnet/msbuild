// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Shouldly;
using Xunit;
using NativeMethods = Microsoft.Build.Framework.NativeMethods;

namespace Microsoft.Build.UnitTests
{
    /// <summary>
    /// Tests for <see cref="NativeMethods.QueryIsScreenAndTryEnableAnsiColorCodes"/>, specifically the
    /// <see cref="NativeMethods.ConsoleConfigurationOverride"/> seam used by the MSBuild Server node to
    /// report the client's terminal capabilities instead of the node's own redirected stdout
    /// (see dotnet/msbuild#13940).
    /// </summary>
    public class NativeMethods_Tests
    {
        [Theory]
        [InlineData(true, true)]
        [InlineData(true, false)]
        [InlineData(false, true)]
        [InlineData(false, false)]
        public void QueryIsScreenAndTryEnableAnsiColorCodes_HonorsOverride(bool acceptAnsi, bool outputIsScreen)
        {
            try
            {
                NativeMethods.ConsoleConfigurationOverride = (acceptAnsi, outputIsScreen);

                (bool actualAnsi, bool actualScreen, uint? originalConsoleMode) =
                    NativeMethods.QueryIsScreenAndTryEnableAnsiColorCodes();

                actualAnsi.ShouldBe(acceptAnsi);
                actualScreen.ShouldBe(outputIsScreen);

                // The node must not touch its own console mode when an override is present, so there is
                // nothing to restore.
                originalConsoleMode.ShouldBeNull();
            }
            finally
            {
                NativeMethods.ConsoleConfigurationOverride = null;
            }
        }

        [Fact]
        public void QueryIsScreenAndTryEnableAnsiColorCodes_OverrideAppliesToStandardError()
        {
            try
            {
                NativeMethods.ConsoleConfigurationOverride = (acceptAnsiColorCodes: true, outputIsScreen: true);

                (bool actualAnsi, bool actualScreen, uint? originalConsoleMode) =
                    NativeMethods.QueryIsScreenAndTryEnableAnsiColorCodes(useStandardError: true);

                actualAnsi.ShouldBeTrue();
                actualScreen.ShouldBeTrue();
                originalConsoleMode.ShouldBeNull();
            }
            finally
            {
                NativeMethods.ConsoleConfigurationOverride = null;
            }
        }
    }
}
