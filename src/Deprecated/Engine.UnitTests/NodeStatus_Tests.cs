// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Text;
using System.IO;

using NUnit.Framework;

using Microsoft.Build.BuildEngine;

namespace Microsoft.Build.UnitTests
{
    [TestFixture]
    public class NodeStatus_Tests
    {
        [Test]
        public void NodeStatusCustomSerialization()
        {
            // Stream, writer and reader where the events will be serialized and deserialized from
            MemoryStream stream = new MemoryStream();
            BinaryWriter writer = new BinaryWriter(stream);
            BinaryReader reader = new BinaryReader(stream);
            try
            {
                NodeStatus nodeStatus1 = new NodeStatus(1, true, 4, 1928374, 384923834, true);

                Exception except = new Exception("I am bad");
                NodeStatus nodeStatus2 = new NodeStatus(except);

                stream.Position = 0;
                // Serialize
                nodeStatus1.WriteToStream(writer);
                // Get position of stream after write so it can be compared to the position after read
                long streamWriteEndPosition = stream.Position;

                // Deserialize and Verify
                stream.Position = 0;
                NodeStatus newNodeStatus = NodeStatus.CreateFromStream(reader);
                long streamReadEndPosition = stream.Position;
                Assert.IsTrue(streamWriteEndPosition == streamReadEndPosition, "Stream End Positions Should Match");
                Assert.IsTrue(newNodeStatus.IsActive == newNodeStatus.IsActive);
                Assert.IsTrue(newNodeStatus.IsLaunchInProgress == nodeStatus1.IsLaunchInProgress);
                Assert.IsTrue(newNodeStatus.TimeSinceLastLoopActivity == nodeStatus1.TimeSinceLastLoopActivity);
                Assert.IsTrue(newNodeStatus.TimeSinceLastTaskActivity == nodeStatus1.TimeSinceLastTaskActivity);
                Assert.IsTrue(newNodeStatus.QueueDepth == nodeStatus1.QueueDepth);
                Assert.IsTrue(newNodeStatus.RequestId == nodeStatus1.RequestId);
                Assert.IsTrue(newNodeStatus.UnhandledException == null);

                stream.Position = 0;
                // Serialize
                nodeStatus2.WriteToStream(writer);
                // Get position of stream after write so it can be compared to the position after read
                streamWriteEndPosition = stream.Position;

                // Deserialize and Verify
                stream.Position = 0;
                newNodeStatus = NodeStatus.CreateFromStream(reader);
                streamReadEndPosition = stream.Position;
                Assert.IsTrue(streamWriteEndPosition == streamReadEndPosition, "Stream End Positions Should Match");
                Assert.IsTrue(newNodeStatus.IsActive == nodeStatus2.IsActive);
                Assert.IsTrue(newNodeStatus.IsLaunchInProgress == nodeStatus2.IsLaunchInProgress);
                Assert.IsTrue(newNodeStatus.TimeSinceLastLoopActivity == nodeStatus2.TimeSinceLastLoopActivity);
                Assert.IsTrue(newNodeStatus.TimeSinceLastTaskActivity == nodeStatus2.TimeSinceLastTaskActivity);
                Assert.IsTrue(newNodeStatus.QueueDepth == nodeStatus2.QueueDepth);
                Assert.IsTrue(newNodeStatus.RequestId == nodeStatus2.RequestId);
                Assert.IsTrue(newNodeStatus.UnhandledException.Message == nodeStatus2.UnhandledException.Message);
            }
            finally
            {
                // Close will close the writer/reader and the underlying stream
                writer.Close();
                reader.Close();
                reader = null;
                stream = null;
                writer = null;
            }
        }
    }
}
