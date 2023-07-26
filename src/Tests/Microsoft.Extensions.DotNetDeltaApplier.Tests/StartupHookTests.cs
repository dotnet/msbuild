// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

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
            Assert.True(string.IsNullOrEmpty(environmentVariables["DOTNET_STARTUP_HOOKS"]));
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
