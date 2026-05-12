// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

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
        [Fact]
        public void TaskHostNodeKey_Equality_SameValues_AreEqual()
        {
            var key1 = new TaskHostNodeKey(HandshakeOptions.TaskHost | HandshakeOptions.NET, 1);
            var key2 = new TaskHostNodeKey(HandshakeOptions.TaskHost | HandshakeOptions.NET, 1);

            key1.ShouldBe(key2);
            (key1 == key2).ShouldBeTrue();
            key1.GetHashCode().ShouldBe(key2.GetHashCode());
        }

        [Fact]
        public void TaskHostNodeKey_Equality_DifferentNodeId_AreNotEqual()
        {
            var key1 = new TaskHostNodeKey(HandshakeOptions.TaskHost | HandshakeOptions.NET, 1);
            var key2 = new TaskHostNodeKey(HandshakeOptions.TaskHost | HandshakeOptions.NET, 2);

            key1.ShouldNotBe(key2);
            (key1 != key2).ShouldBeTrue();
        }

        [Fact]
        public void TaskHostNodeKey_Equality_DifferentHandshakeOptions_AreNotEqual()
        {
            var key1 = new TaskHostNodeKey(HandshakeOptions.TaskHost | HandshakeOptions.NET, 1);
            var key2 = new TaskHostNodeKey(HandshakeOptions.TaskHost | HandshakeOptions.X64, 1);

            key1.ShouldNotBe(key2);
            (key1 != key2).ShouldBeTrue();
        }

        [Fact]
        public void TaskHostNodeKey_CanBeUsedAsDictionaryKey()
        {
            var dict = new System.Collections.Generic.Dictionary<TaskHostNodeKey, string>();
            var key1 = new TaskHostNodeKey(HandshakeOptions.TaskHost | HandshakeOptions.NET, 1);
            var key2 = new TaskHostNodeKey(HandshakeOptions.TaskHost | HandshakeOptions.X64, 2);

            dict[key1] = "value1";
            dict[key2] = "value2";

            dict[key1].ShouldBe("value1");
            dict[key2].ShouldBe("value2");

            // Create a new key with same values as key1
            var key1Copy = new TaskHostNodeKey(HandshakeOptions.TaskHost | HandshakeOptions.NET, 1);
            dict[key1Copy].ShouldBe("value1");
        }

        [Fact]
        public void TaskHostNodeKey_LargeNodeId_Works()
        {
            // Test that we can use node IDs greater than 255 (the previous limit)
            var key1 = new TaskHostNodeKey(HandshakeOptions.TaskHost | HandshakeOptions.NET, 256);
            var key2 = new TaskHostNodeKey(HandshakeOptions.TaskHost | HandshakeOptions.NET, 1000);
            var key3 = new TaskHostNodeKey(HandshakeOptions.TaskHost | HandshakeOptions.NET, int.MaxValue);

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
            var key = new TaskHostNodeKey(HandshakeOptions.TaskHost | HandshakeOptions.NET, -1);

            key.NodeId.ShouldBe(-1);
            key.HandshakeOptions.ShouldBe(HandshakeOptions.TaskHost | HandshakeOptions.NET);
        }

        [Fact]
        public void TaskHostNodeKey_AllHandshakeOptions_Work()
        {
            // Test various HandshakeOptions combinations
            HandshakeOptions[] optionsList =
            [
                HandshakeOptions.None,
                HandshakeOptions.TaskHost,
                HandshakeOptions.TaskHost | HandshakeOptions.NET,
                HandshakeOptions.TaskHost | HandshakeOptions.X64,
                HandshakeOptions.TaskHost | HandshakeOptions.NET | HandshakeOptions.NodeReuse,
                HandshakeOptions.TaskHost | HandshakeOptions.CLR2,
                HandshakeOptions.TaskHost | HandshakeOptions.Arm64
            ];

            foreach (var options in optionsList)
            {
                var key = new TaskHostNodeKey(options, 42);

                key.HandshakeOptions.ShouldBe(options);
                key.NodeId.ShouldBe(42);
            }
        }
    }
}
