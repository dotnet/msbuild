// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using Shouldly;
using Xunit;

namespace Microsoft.Build.Framework.UnitTests
{
    public class MSBuildServerLifecycleEventArgs_Tests
    {
        /// <summary>
        /// Covers the node-packet serialization path (WriteToStream / CreateFromStream) which carries the event
        /// from the out-of-proc server node to the client. Complements the binary-log round-trip in
        /// BuildEventArgsSerialization_Tests; the two serializers must stay in field-order sync. Exercises a
        /// spawned short-lived event (null reason), a reused event (null reason), and a not-used event (non-null
        /// reason/reasonCode — the WriteOptionalString branch).
        /// </summary>
        [Fact]
        public void SerializationDeserializationTest()
        {
            RoundtripAndAssert(new MSBuildServerLifecycleEventArgs(
                MSBuildServerLifecycleKind.Spawned, 4321, reason: null, reasonCode: null, "spawned short-lived", MessageImportance.Low, shortLived: true));

            RoundtripAndAssert(new MSBuildServerLifecycleEventArgs(
                MSBuildServerLifecycleKind.Reused, 4321, reason: null, reasonCode: null, "reused", MessageImportance.Low));

            RoundtripAndAssert(new MSBuildServerLifecycleEventArgs(
                MSBuildServerLifecycleKind.NotUsed, 0, reason: "node reuse is disabled", reasonCode: "node-reuse-disabled", "not used", MessageImportance.Low));

            static void RoundtripAndAssert(MSBuildServerLifecycleEventArgs arg)
            {
                using MemoryStream stream = new MemoryStream();
                using BinaryWriter bw = new BinaryWriter(stream);
                arg.WriteToStream(bw);

                stream.Position = 0;
                using BinaryReader br = new BinaryReader(stream);
                MSBuildServerLifecycleEventArgs argDeserialized = new();
                int packetVersion = (Environment.Version.Major * 10) + Environment.Version.Minor;
                argDeserialized.CreateFromStream(br, packetVersion);

                argDeserialized.Kind.ShouldBe(arg.Kind);
                argDeserialized.ProcessId.ShouldBe(arg.ProcessId);
                argDeserialized.Reason.ShouldBe(arg.Reason);
                argDeserialized.ReasonCode.ShouldBe(arg.ReasonCode);
                argDeserialized.ShortLived.ShouldBe(arg.ShortLived);
                argDeserialized.Message.ShouldBe(arg.Message);
                argDeserialized.Importance.ShouldBe(arg.Importance);
            }
        }
    }
}
