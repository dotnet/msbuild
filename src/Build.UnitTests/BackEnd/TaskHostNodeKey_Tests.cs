// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.BackEnd;
using Microsoft.Build.Internal;
using Shouldly;
using Xunit;

namespace Microsoft.Build.Engine.UnitTests.BackEnd
{
    /// <summary>
    /// Tests for TaskHostNodeKey record struct functionality.
    /// </summary>
    public class TaskHostNodeKey_Tests
    {
        private static readonly NetTaskHostHandshakeConfiguration s_netConfiguration = new(TaskHostHandshakeOptions.None);
        private static readonly FrameworkTaskHostHandshakeConfiguration s_frameworkConfiguration = new(new(TaskHostArchitecture.X64));

        [Fact]
        public void TaskHostNodeKey_Equality_SameValues_AreEqual()
        {
            TaskHostLaunchIdentity launchIdentity = new("msbuild", "arguments", "dotnet");
            var key1 = new TaskHostNodeKey(s_netConfiguration, 1, LaunchIdentity: launchIdentity);
            var key2 = new TaskHostNodeKey(s_netConfiguration, 1, LaunchIdentity: launchIdentity);

            key1.ShouldBe(key2);
            (key1 == key2).ShouldBeTrue();
            key1.GetHashCode().ShouldBe(key2.GetHashCode());
        }

        [Fact]
        public void TaskHostNodeKey_Equality_DifferentNodeId_AreNotEqual()
        {
            var key1 = new TaskHostNodeKey(s_netConfiguration, 1);
            var key2 = new TaskHostNodeKey(s_netConfiguration, 2);

            key1.ShouldNotBe(key2);
            (key1 != key2).ShouldBeTrue();
        }

        [Fact]
        public void TaskHostNodeKey_Equality_DifferentHandshakeOptions_AreNotEqual()
        {
            var key1 = new TaskHostNodeKey(s_netConfiguration, 1);
            var key2 = new TaskHostNodeKey(s_frameworkConfiguration, 1);

            key1.ShouldNotBe(key2);
            (key1 != key2).ShouldBeTrue();
        }

        [Fact]
        public void TaskHostNodeKey_Equality_DifferentConsoleForwarding_AreNotEqual()
        {
            var key1 = new TaskHostNodeKey(s_netConfiguration, 1, ForwardConsoleOutput: false);
            var key2 = new TaskHostNodeKey(s_netConfiguration, 1, ForwardConsoleOutput: true);

            key1.ShouldNotBe(key2);
            (key1 != key2).ShouldBeTrue();
        }

        [Theory]
        [InlineData("other-msbuild", "arguments", "dotnet")]
        [InlineData("msbuild", "other-arguments", "dotnet")]
        [InlineData("msbuild", "arguments", "other-dotnet")]
        public void TaskHostNodeKey_Equality_DifferentLaunchIdentityComponents_AreNotEqual(
            string executablePath,
            string commandLineArgs,
            string dotnetHostPath)
        {
            var key1 = new TaskHostNodeKey(
                s_netConfiguration,
                1,
                LaunchIdentity: new TaskHostLaunchIdentity("msbuild", "arguments", "dotnet"));
            var key2 = new TaskHostNodeKey(
                s_netConfiguration,
                1,
                LaunchIdentity: new TaskHostLaunchIdentity(executablePath, commandLineArgs, dotnetHostPath));

            key1.ShouldNotBe(key2);
            (key1 != key2).ShouldBeTrue();
        }

        [Fact]
        public void TaskHostNodeKey_Equality_NullAndPopulatedLaunchIdentity_AreNotEqual()
        {
            var keyWithoutIdentity = new TaskHostNodeKey(s_netConfiguration, 1);
            var keyWithIdentity = new TaskHostNodeKey(
                s_netConfiguration,
                1,
                LaunchIdentity: new TaskHostLaunchIdentity(string.Empty, string.Empty, string.Empty));

            keyWithoutIdentity.ShouldNotBe(keyWithIdentity);
        }

        [Fact]
        public void TaskHostNodeKey_Equality_UnspecifiedAndX86ShareWireIdentity()
        {
            FrameworkTaskHostHandshakeConfiguration unspecified = new(new(TaskHostArchitecture.Unspecified));
            FrameworkTaskHostHandshakeConfiguration x86 = new(new(TaskHostArchitecture.X86));
            TaskHostNodeKey unspecifiedKey = new(unspecified, 1);
            TaskHostNodeKey x86Key = new(x86, 1);

            unspecified.Options.Architecture.ShouldNotBe(x86.Options.Architecture);
            unspecifiedKey.ShouldBe(x86Key);
            unspecifiedKey.GetHashCode().ShouldBe(x86Key.GetHashCode());
        }

        [Theory]
        [InlineData(0, false)]
        [InlineData(7, false)]
        [InlineData(8, true)]
        public void ParameterConversionSupportComesFromNegotiatedPacketVersion(byte version, bool expected)
        {
            TaskHostTask.CanConvertTaskParameters(version).ShouldBe(expected);
        }

        [Fact]
        public void TaskHostNodeKey_CanBeUsedAsDictionaryKey()
        {
            var dict = new System.Collections.Generic.Dictionary<TaskHostNodeKey, string>();
            TaskHostLaunchIdentity launchIdentity = new("msbuild", "arguments", "dotnet");
            var key1 = new TaskHostNodeKey(s_netConfiguration, 1, LaunchIdentity: launchIdentity);
            var key2 = new TaskHostNodeKey(s_frameworkConfiguration, 2);

            dict[key1] = "value1";
            dict[key2] = "value2";

            dict[key1].ShouldBe("value1");
            dict[key2].ShouldBe("value2");

            // Create a new key with same values as key1
            var key1Copy = new TaskHostNodeKey(s_netConfiguration, 1, LaunchIdentity: launchIdentity);
            dict[key1Copy].ShouldBe("value1");
        }

        [Fact]
        public void TaskHostNodeKey_LargeNodeId_Works()
        {
            // Test that we can use node IDs greater than 255 (the previous limit)
            var key1 = new TaskHostNodeKey(s_netConfiguration, 256);
            var key2 = new TaskHostNodeKey(s_netConfiguration, 1000);
            var key3 = new TaskHostNodeKey(s_netConfiguration, int.MaxValue);

            key1.NodeId.ShouldBe(256);
            key2.NodeId.ShouldBe(1000);
            key3.NodeId.ShouldBe(int.MaxValue);

            // Ensure they are all different
            key1.ShouldNotBe(key2);
            key2.ShouldNotBe(key3);
            key1.ShouldNotBe(key3);
        }

        [Fact]
        public void TaskHostNodeKey_NegativeNodeId_Works()
        {
            // Traditional multi-proc builds use -1 for node ID
            var key = new TaskHostNodeKey(s_netConfiguration, -1);

            key.NodeId.ShouldBe(-1);
            key.HandshakeOptions.ShouldBe(HandshakeOptions.TaskHost | HandshakeOptions.NET);
        }

        [Fact]
        public void TaskHostNodeKey_PreservesConfigurationWireOptions()
        {
            TaskHostHandshakeConfiguration[] configurations =
            [
                new FrameworkTaskHostHandshakeConfiguration(TaskHostHandshakeOptions.None),
                s_netConfiguration,
                s_frameworkConfiguration,
                new NetTaskHostHandshakeConfiguration(new(NodeReuse: true)),
                new Clr2TaskHostHandshakeConfiguration(TaskHostHandshakeOptions.None),
                new FrameworkTaskHostHandshakeConfiguration(new(TaskHostArchitecture.Arm64))
            ];

            foreach (TaskHostHandshakeConfiguration configuration in configurations)
            {
                var key = new TaskHostNodeKey(configuration, 42);

                key.HandshakeOptions.ShouldBe(configuration.HandshakeOptions);
                key.NodeId.ShouldBe(42);
            }
        }
    }
}
