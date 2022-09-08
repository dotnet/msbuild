// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.IO;
using Xunit;

namespace Microsoft.Extensions.DotNetDeltaApplier
{
    public class StartupHookTests
    {
        [Fact]
        public void ClearHotReloadEnvironmentVariables_ClearsStartupHook()
        {
            // Arrange
            var environmentVariables = new Dictionary<string, string?>
            {
                ["DOTNET_MODIFIABLE_ASSEMBLIES"] = "debug",
                ["DOTNET_STARTUP_HOOKS"] = typeof(StartupHook).Assembly.Location
            };

            // Act
            StartupHook.ClearHotReloadEnvironmentVariables(
                (name) => environmentVariables[name],
                (name, value) => environmentVariables[name] = value);

            // Assert
            Assert.Empty(environmentVariables["DOTNET_STARTUP_HOOKS"]!);
        }

        [Fact]
        public void ClearHotReloadEnvironmentVariables_PreservedOtherStartupHooks()
        {
            // Arrange
            var customStartupHook = "/path/mycoolstartup.dll";
            var environmentVariables = new Dictionary<string, string?>
            {
                ["DOTNET_MODIFIABLE_ASSEMBLIES"] = "debug",
                ["DOTNET_STARTUP_HOOKS"] = typeof(StartupHook).Assembly.Location + Path.PathSeparator + customStartupHook,
            };

            // Act
            StartupHook.ClearHotReloadEnvironmentVariables(
                (name) => environmentVariables[name],
                (name, value) => environmentVariables[name] = value);

            // Assert
            Assert.Equal(customStartupHook, environmentVariables["DOTNET_STARTUP_HOOKS"]);
        }

        [Fact]
        public void ClearHotReloadEnvironmentVariables_RemovesHotReloadStartup_InCaseInvariantManner()
        {
            // Arrange
            var customStartupHook = "/path/mycoolstartup.dll";
            var environmentVariables = new Dictionary<string, string?>
            {
                ["DOTNET_MODIFIABLE_ASSEMBLIES"] = "debug",
                ["DOTNET_STARTUP_HOOKS"] = customStartupHook + Path.PathSeparator + typeof(StartupHook).Assembly.Location.ToUpperInvariant(),
            };

            // Act
            StartupHook.ClearHotReloadEnvironmentVariables(
                (name) => environmentVariables[name],
                (name, value) => environmentVariables[name] = value);

            // Assert
            Assert.Equal(customStartupHook, environmentVariables["DOTNET_STARTUP_HOOKS"]);
        }
    }
}
