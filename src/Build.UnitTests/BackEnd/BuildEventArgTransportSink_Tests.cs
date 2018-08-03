// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using Microsoft.Build.Framework;
using Microsoft.Build.BackEnd;
using Microsoft.Build.BackEnd.Logging;
using Microsoft.Build.Shared;
using Xunit;

namespace Microsoft.Build.UnitTests.Logging
{
    /// <summary>
    /// Test the central forwarding logger by initializing a new one and sending events through it.
    /// </summary>
    public class BuildEventArgTransportSink_Tests
    {
        /// <summary>
        /// Verify the properties on BuildEventArgTransportSink properly work
        /// </summary>
        [Fact]
        public void PropertyTests()
        {
            BuildEventArgTransportSink sink = new BuildEventArgTransportSink(PacketProcessor);
            Assert.Null(sink.Name);

            const string name = "Test Name";
            sink.Name = name;
            Assert.Equal(0, string.Compare(sink.Name, name, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Make sure we throw an exception if the transport delegate is null
        /// </summary>
        [Fact]
        public void TestConstructorNullSendDataDelegate()
        {
            Assert.Throws<InternalErrorException>(() =>
            {
                var transportSink = new BuildEventArgTransportSink(null);
            });
        }
        /// <summary>
        /// Verify consume throws the correct exception when a null build event is passed in
        /// </summary>
        [Fact]
        public void TestConsumeNullBuildEvent()
        {
            Assert.Throws<InternalErrorException>(() =>
            {
                BuildEventArgTransportSink transportSink = new BuildEventArgTransportSink(PacketProcessor);
                transportSink.Consume(null, 0);
            }
           );
        }
        /// <summary>
        /// Verify consume properly packages up the message event into a packet and send it to the 
        /// transport delegate
        /// </summary>
        [Fact]
        public void TestConsumeMessageBuildEvent()
        {
            bool wentInHandler = false;
            BuildMessageEventArgs messageEvent = new BuildMessageEventArgs("My message", "Help me keyword", "Sender", MessageImportance.High);

            void TransportDelegate(INodePacket packet)
            {
                wentInHandler = true;
                LogMessagePacket loggingPacket = packet as LogMessagePacket;
                Assert.NotNull(loggingPacket);
                BuildMessageEventArgs messageEventFromPacket = loggingPacket.NodeBuildEvent.Value.Value as BuildMessageEventArgs;
                Assert.Equal(messageEventFromPacket, messageEvent);
            }

            BuildEventArgTransportSink transportSink = new BuildEventArgTransportSink(TransportDelegate);
            transportSink.Consume(messageEvent, 0);
            Assert.True(wentInHandler); // "Expected to go into transport delegate"
        }

        /// <summary>
        /// Verify consume ignores BuildStarted events
        /// </summary>
        [Fact]
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
            Assert.True(transportSink.HaveLoggedBuildStartedEvent);
            Assert.False(transportSink.HaveLoggedBuildFinishedEvent);
            Assert.False(wentInHandler); // "Expected not to go into transport delegate"
        }

        /// <summary>
        /// Verify consume ignores BuildFinished events
        /// </summary>
        [Fact]
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
            Assert.False(transportSink.HaveLoggedBuildStartedEvent);
            Assert.True(transportSink.HaveLoggedBuildFinishedEvent);
            Assert.False(wentInHandler); // "Expected not to go into transport delegate"
        }

        /// <summary>
        /// Make sure shutdown will correctly null out the send data delegate
        /// </summary>
        [Fact]
        [Trait("Category", "mono-osx-failing")]
        [SkipOnTargetFramework(TargetFrameworkMonikers.Netcoreapp, "https://github.com/Microsoft/msbuild/issues/282")]
        public void TestShutDown()
        {
            SendDataDelegate transportDelegate = PacketProcessor;
            var weakTransportDelegateReference = new WeakReference(transportDelegate);
            var transportSink = new BuildEventArgTransportSink(transportDelegate);

            transportSink.ShutDown();

            Assert.NotNull(weakTransportDelegateReference.Target);
            transportDelegate = null;
            GC.Collect();
            GC.WaitForPendingFinalizers();

            // Expected shutdown to null out the sendData delegate, the two garbage collections
            // should have collected the sendDataDelegate causing the weak reference to die.
            Assert.Null(weakTransportDelegateReference.Target);  // " Expected delegate to be dead"
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
