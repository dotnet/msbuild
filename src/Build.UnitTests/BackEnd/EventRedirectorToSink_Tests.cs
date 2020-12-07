// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.Framework;
using Microsoft.Build.BackEnd.Logging;
using Microsoft.Build.Shared;
using Xunit;

namespace Microsoft.Build.UnitTests.Logging
{
    /// <summary>
    /// Test the central forwarding logger by initializing a new one and sending events through it.
    /// </summary>
    public class EventRedirectorToSink_Tests
    {
        /// <summary>
        /// Tests the basic getting and setting of the logger parameters
        /// </summary>
        [Fact]
        public void TestConstructorNegativeLoggerId()
        {
            Assert.Throws<InternalErrorException>(() =>
            {
                EventSourceSink testSink = new EventSourceSink();
                EventRedirectorToSink eventRedirector = new EventRedirectorToSink(-10, testSink);
            }
           );
        }
        /// <summary>
        /// Verify the correct exception is thrown when the logger is initialized with a null 
        /// event source.
        /// </summary>
        [Fact]
        public void TestConstructorNullSink()
        {
            Assert.Throws<InternalErrorException>(() =>
            {
                EventRedirectorToSink eventRedirector = new EventRedirectorToSink(0, null);
            }
           );
        }
        /// <summary>
        /// Verify an valid inputs work and do not produce an exception
        /// </summary>
        [Fact]
        public void TestConstructorValidInputs()
        {
            EventSourceSink testSink = new EventSourceSink();
            EventRedirectorToSink eventRedirector = new EventRedirectorToSink(5, testSink);
            Assert.NotNull(eventRedirector); // "eventRedirector was not supposed to be null"
        }

        /// <summary>
        /// Verify when an event is forwarded, the event that was put in is the same event that was received on the event source
        /// also make sure the sinkId has been updated by the event redirector.
        /// </summary>
        [Fact]
        public void TestForwardingNotNullEvent()
        {
            EventSourceSink testSink = new EventSourceSink();
            EventRedirectorToSink eventRedirector = new EventRedirectorToSink(5, testSink);
            BuildMessageEventArgs messageEvent = new BuildMessageEventArgs("My message", "Help me keyword", "Sender", MessageImportance.High);
            bool wentInHandler = false;
            testSink.AnyEventRaised += new AnyEventHandler
                (
                  delegate
                  (
                    object sender,
                    BuildEventArgs buildEvent
                  )
                  {
                      wentInHandler = true;
                      BuildMessageEventArgs messageEventFromPacket = buildEvent as BuildMessageEventArgs;
                      Assert.Equal(messageEvent, messageEventFromPacket); // "Expected messageEvent to be forwarded to match actually forwarded event"
                  }

                );

            ((IEventRedirector)eventRedirector).ForwardEvent(messageEvent);
            Assert.True(wentInHandler); // "Expected to go into event handler"
        }

        /// <summary>
        /// Verify when a null event is forwarded we get a null argument exception
        /// </summary>
        [Fact]
        public void TestForwardingNullEvent()
        {
            Assert.Throws<InternalErrorException>(() =>
            {
                EventSourceSink testSink = new EventSourceSink();
                EventRedirectorToSink eventRedirector = new EventRedirectorToSink(5, testSink);
                ((IEventRedirector)eventRedirector).ForwardEvent(null);
            }
           );
        }
    }
}