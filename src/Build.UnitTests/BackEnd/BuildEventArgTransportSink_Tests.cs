// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.Build.BackEnd;
using Microsoft.Build.BackEnd.Logging;
using Microsoft.Build.Framework;

#nullable disable

namespace Microsoft.Build.UnitTests.Logging
{
    /// <summary>
    /// Test the central forwarding logger by initializing a new one and sending events through it.
    /// </summary>
    [TestClass]
    public class BuildEventArgTransportSink_Tests
    {
        /// <summary>
        /// Verify the properties on BuildEventArgTransportSink properly work
        /// </summary>
        [MSBuildTestMethod]
        public void PropertyTests()
        {
            BuildEventArgTransportSink sink = new BuildEventArgTransportSink(PacketProcessor);
            Assert.IsNull(sink.Name);

            const string name = "Test Name";
            sink.Name = name;
            Assert.AreEqual(0, string.Compare(sink.Name, name, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Make sure we throw an exception if the transport delegate is null
        /// </summary>
        [MSBuildTestMethod]
        public void TestConstructorNullSendDataDelegate()
        {
            Assert.ThrowsExactly<InternalErrorException>(() =>
            {
                var transportSink = new BuildEventArgTransportSink(null);
            });
        }
        /// <summary>
        /// Verify consume throws the correct exception when a null build event is passed in
        /// </summary>
        [MSBuildTestMethod]
        public void TestConsumeNullBuildEvent()
        {
            Assert.ThrowsExactly<InternalErrorException>(() =>
            {
                BuildEventArgTransportSink transportSink = new BuildEventArgTransportSink(PacketProcessor);
                transportSink.Consume(null, 0);
            });
        }
        /// <summary>
        /// Verify consume properly packages up the message event into a packet and send it to the
        /// transport delegate
        /// </summary>
        [MSBuildTestMethod]
        public void TestConsumeMessageBuildEvent()
        {
            bool wentInHandler = false;
            BuildMessageEventArgs messageEvent = new BuildMessageEventArgs("My message", "Help me keyword", "Sender", MessageImportance.High);

            void TransportDelegate(INodePacket packet)
            {
                wentInHandler = true;
                LogMessagePacket loggingPacket = packet as LogMessagePacket;
                Assert.IsNotNull(loggingPacket);
                BuildMessageEventArgs messageEventFromPacket = loggingPacket.NodeBuildEvent.Value.Value as BuildMessageEventArgs;
                Assert.AreEqual(messageEventFromPacket, messageEvent);
            }

            BuildEventArgTransportSink transportSink = new BuildEventArgTransportSink(TransportDelegate);
            transportSink.Consume(messageEvent, 0);
            Assert.IsTrue(wentInHandler); // "Expected to go into transport delegate"
        }

        /// <summary>
        /// Verify consume ignores BuildStarted events
        /// </summary>
        [MSBuildTestMethod]
        public void TestConsumeBuildStartedEvent()
        {
            bool wentInHandler = false;
            BuildStartedEventArgs buildStarted = new BuildStartedEventArgs("Start", "Help");

            void TransportDelegate(INodePacket packet)
            {
                wentInHandler = true;
            }

            BuildEventArgTransportSink transportSink = new BuildEventArgTransportSink(TransportDelegate);
            transportSink.Consume(buildStarted, 0);
            Assert.IsTrue(transportSink.HaveLoggedBuildStartedEvent);
            Assert.IsFalse(transportSink.HaveLoggedBuildFinishedEvent);
            Assert.IsFalse(wentInHandler); // "Expected not to go into transport delegate"
        }

        /// <summary>
        /// Verify consume ignores BuildFinished events
        /// </summary>
        [MSBuildTestMethod]
        public void TestConsumeBuildFinishedEvent()
        {
            bool wentInHandler = false;
            BuildFinishedEventArgs buildFinished = new BuildFinishedEventArgs("Finished", "Help", true);

            void TransportDelegate(INodePacket packet)
            {
                wentInHandler = true;
            }

            BuildEventArgTransportSink transportSink = new BuildEventArgTransportSink(TransportDelegate);
            transportSink.Consume(buildFinished, 0);
            Assert.IsFalse(transportSink.HaveLoggedBuildStartedEvent);
            Assert.IsTrue(transportSink.HaveLoggedBuildFinishedEvent);
            Assert.IsFalse(wentInHandler); // "Expected not to go into transport delegate"
        }

        /// <summary>
        /// Make sure shutdown will correctly null out the send data delegate
        /// </summary>
        [WindowsFullFrameworkOnlyFact("https://github.com/dotnet/msbuild/issues/282")]
        public void TestShutDown()
        {
            SendDataDelegate transportDelegate = new(PacketProcessor);
            var weakTransportDelegateReference = new WeakReference(transportDelegate);
            var transportSink = new BuildEventArgTransportSink(transportDelegate);

            transportSink.ShutDown();

            Assert.IsNotNull(weakTransportDelegateReference.Target);
            transportDelegate = null;
            GC.Collect();
            GC.WaitForPendingFinalizers();

            // Expected shutdown to null out the sendData delegate, the two garbage collections
            // should have collected the sendDataDelegate causing the weak reference to die.
            Assert.IsNull(weakTransportDelegateReference.Target);  // " Expected delegate to be dead"
        }

        /// <summary>
        /// Create a method which will be a fake method to process a packet.
        /// This needs to be done because using an anonymous method does not work.
        /// Using an anonymous method does not work because when the delegate is created
        /// it seems that a field is created which creates a strong reference
        /// between the delegate and the class it was created in. This means the delegate is not
        /// garbage collected until the class it was instantiated in is collected itself.
        /// </summary>
        /// <param name="packet">Packet to process</param>
        private static void PacketProcessor(INodePacket packet)
        {
        }
    }
}
