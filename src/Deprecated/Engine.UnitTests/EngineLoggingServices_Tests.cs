// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Collections;
using System.Text;
using System.Threading;
using NUnit.Framework;
using Microsoft.Build.BuildEngine;
using Microsoft.Build.Framework;
using Microsoft.Build.BuildEngine.Shared;
using System.Diagnostics;
using System.Reflection;

// All of these tests were created by Chris Mann and Jeff Callahan
namespace Microsoft.Build.UnitTests
{
    #region EngineLoggingHelper
    /// <summary>
    /// This class is a basic implementation of EngineLoggingServices so that we can try and test the abstract EngineLoggingServices
    /// class without a lot of other methods being added
    /// </summary>
    internal class EngineLoggingServicesHelper : EngineLoggingServices
    {
        /// <summary>
        /// Reading queue
        /// </summary>
        DualQueue<BuildEventArgs> currentQueueBuildEvent;
        /// <summary>
        /// Reading queue
        /// </summary>
        DualQueue<NodeLoggingEvent> currentQueueNodeEvent;

        internal EngineLoggingServicesHelper()
        {
            base.Initialize(new ManualResetEvent(false));
        }

        /// <summary>
        /// We dont need to do anything to process events, we just want to get what events are in the queue 
        /// </summary>
        internal override bool ProcessPostedLoggingEvents()
        {
            currentQueueBuildEvent = loggingQueueOfBuildEvents;
            currentQueueNodeEvent  = loggingQueueOfNodeEvents;

            return true;
        }

        /// <summary>
        /// Note that this "get" indirectly calls GetCurrentReadingQueue() which
        /// returns references to distinct queues on sequential calls; in other
        /// words this property has side effects on the internal data structures.
        /// </summary>
        internal DualQueue<BuildEventArgs> GetCurrentQueueBuildEvents()
        {
            ProcessPostedLoggingEvents();
            return currentQueueBuildEvent;
        }

        /// <summary>
        /// Note that this "get" indirectly calls GetCurrentReadingQueue() which
        /// returns references to distinct queues on sequential calls; in other
        /// words this property has side effects on the internal data structures.
        /// </summary>
        internal DualQueue<NodeLoggingEvent> GetCurrentQueueNodeEvents()
        {
            ProcessPostedLoggingEvents();
            return loggingQueueOfNodeEvents;
        }

    }
    #endregion

    [TestFixture]
    public class EngineLoggingServices_Tests
    {
        // A simple implementation of the abstract class EngineLoggingServices
        EngineLoggingServicesHelper engineLoggingServicesHelper;

        /// <summary>
        /// Generate a generic BuildErrorEventArgs
        /// </summary>
        /// <param name="message">message to put in the event</param>
        /// <returns>Event</returns>
        private BuildErrorEventArgs GenerateBuildErrorEventArgs(string message)
        {
            return new BuildErrorEventArgs("SubCategory", "code", null, 0, 1, 2, 3, message, "Help", "EngineLoggingServicesTest");
        }

        /// <summary>
        /// Generate a generic BuildWarningEventArgs
        /// </summary>
        /// <param name="message">message to put in the event</param>
        /// <returns>Event</returns>
        private BuildWarningEventArgs GenerateBuildWarningEventArgs(string message)
        {
            return new BuildWarningEventArgs("SubCategory", "code", null, 0, 1, 2, 3, message, "warning", "EngineLoggingServicesTest");
        }

        /// <summary>
        /// Generate a generic BuildMessageEventArgs
        /// </summary>
        /// <param name="message">message to put in the event</param>
        /// <param name="importance">importance for the message</param>
        /// <returns>Event</returns>
        private BuildMessageEventArgs GenerateBuildMessageEventArgs(string message, MessageImportance importance)
        {
            return new BuildMessageEventArgs(message, "HelpKeyword", "senderName", importance);
        }

        /// <summary>
        /// A basic event derived from the abstract class CustomBuildEventArgs
        /// </summary>
        class MyCustomBuildEventArgs : CustomBuildEventArgs
        {
            public MyCustomBuildEventArgs() : base() { }
            public MyCustomBuildEventArgs(string message) : base(message, "HelpKeyword", "SenderName") { }
        }

        /// <summary>
        /// A custom BuildEventArgs derived from a CustomBuildEventArgs
        /// </summary>
        /// <param name="message">message to put in the event</param>
        /// <returns></returns>
        private MyCustomBuildEventArgs GenerateBuildCustomEventArgs(string message)
        {
            return new MyCustomBuildEventArgs("testCustomBuildEvent");
        }

        [SetUp]
        public void SetUp()
        {
            engineLoggingServicesHelper = new EngineLoggingServicesHelper();
        }

        [TearDown]
        public void TearDown()
        {
            engineLoggingServicesHelper = null;
        }

        #region TestEventBasedLoggingMethods
        /// <summary>
        /// Test the logging of and ErrorEvent
        /// </summary>
        [Test]
        public void LogErrorEvent()
        {
            List<BuildEventArgs> eventList = new List<BuildEventArgs>();
            
            // Log a number of events and then make sure that queue at the end contains all of those events
            for (int i = 0; i < 10; i++)
            {
                BuildErrorEventArgs eventToAdd = GenerateBuildErrorEventArgs("ErrorMessage" + i);
                eventList.Add(eventToAdd);
                engineLoggingServicesHelper.LogErrorEvent(eventToAdd);
            }

            // Get the logging queue after we have logged a number of messages
           DualQueue<BuildEventArgs> currentQueue = engineLoggingServicesHelper.GetCurrentQueueBuildEvents();
            
            // Assert that every event we sent to the logger exists in the queue
            Assert.IsTrue(eventList.TrueForAll(delegate(BuildEventArgs args)
                                               {
                                                   return currentQueue.Contains(args);
                                               }), "Expected to find all events sent to LogErrorEvent");
            
            // Assert that every event in the queue is of the correct type
            AssertForEachEventInQueue(currentQueue, IsInstanceOfType<BuildErrorEventArgs>);
        }
        
        /// <summary>
        /// Test the case where null events are attempted to be logged
        /// </summary>
        [Test]
        [ExpectedException(typeof(ArgumentNullException))]
        public void LogErrorEventNullEvent()
        {
            engineLoggingServicesHelper.LogErrorEvent(null);
        }
        
        /// <summary>
        /// Test warning events
        /// </summary>
        [Test]
        public void LogWarningEvent()
        {
            List<BuildEventArgs> eventList = new List<BuildEventArgs>();
           
            // Log a number of events
            for (int i = 0; i < 10; i++)
            {
                BuildWarningEventArgs eventToAdd = GenerateBuildWarningEventArgs("WarningMessage" + i);
                eventList.Add(eventToAdd);
                engineLoggingServicesHelper.LogWarningEvent(eventToAdd);
            }
            
            // Get the logged event queue from the "logger"
           DualQueue<BuildEventArgs> currentQueue = engineLoggingServicesHelper.GetCurrentQueueBuildEvents();

            // Assert that every event we sent to the logger now exists in the queue
            Assert.IsTrue(eventList.TrueForAll(delegate(BuildEventArgs args)
                                               {
                                                   return currentQueue.Contains(args);
                                               }), "Expected to find all events sent to LogWarningEvent");
            
            // Assert that every event in the queue is of the correct type
            AssertForEachEventInQueue(currentQueue, IsInstanceOfType<BuildWarningEventArgs>);
        }

        /// <summary>
        ///  Test the case where we attempt to log a null warning event
        /// </summary>
        [Test]
        [ExpectedException(typeof(ArgumentNullException))]
        public void LogWarningEventNullEvent()
        {
            engineLoggingServicesHelper.LogWarningEvent(null);
        }

        /// <summary>
        /// Test the case where we try and log a null message event
        /// </summary>
        [Test]
        [ExpectedException(typeof(ArgumentNullException))]
        public void LogMessageEventNullEvent()
        {
            // Try and log a null when we are only going to log critical events
            try
            {
                engineLoggingServicesHelper.OnlyLogCriticalEvents = true;
                engineLoggingServicesHelper.LogMessageEvent(null);
                engineLoggingServicesHelper.OnlyLogCriticalEvents = false;
            }
            catch (Exception e)
            {
                Assert.Fail(e.Message + " Should not throw exception if OnlyLogCriticalEvents is true");
            }

            // Should throw an exception as OnlyLogCriticalEvents is false and the null check is performed
            engineLoggingServicesHelper.LogMessageEvent(null);
        }

        /// <summary>
        /// Test that we can log message events
        /// </summary>
        [Test]
        public void LogMessageEvent()
        {
            List<BuildEventArgs> eventList = new List<BuildEventArgs>();
            
            // Log a number of message events and keep track of the events we tried to log
            for (int i = 0; i < 10; i++)
            {
                BuildMessageEventArgs eventToAdd = GenerateBuildMessageEventArgs("MessageMessage" + i, MessageImportance.Normal);
                eventList.Add(eventToAdd);
                engineLoggingServicesHelper.LogMessageEvent(eventToAdd);
            }

            // Get the queue of the events logged by the logger
           DualQueue<BuildEventArgs> currentQueue = engineLoggingServicesHelper.GetCurrentQueueBuildEvents();

            // Assert that every event we sent to the logger exists in the logging queue
            Assert.IsTrue(eventList.TrueForAll(delegate(BuildEventArgs args)
                                               {
                                                   return currentQueue.Contains(args);
                                               }), "Expected to find all events sent to LogMessageEvent");

            // Assert that every event in the queue is of the correct type
            AssertForEachEventInQueue(currentQueue, IsInstanceOfType<BuildMessageEventArgs>);
        }

        /// <summary>
        ///  Test the case where we try and log a null event
        /// </summary>
        [Test]
        [ExpectedException(typeof(ArgumentNullException))]
        public void PostLoggingEventNullEvent()
        {
            BuildEventArgs nullEvent = null;
            engineLoggingServicesHelper.PostLoggingEvent(nullEvent);
        }

        /// <summary>
        /// Test the case where we try and log a null CustomEvent
        /// </summary>
        [Test]
        [ExpectedException(typeof(ArgumentNullException))]
        public void LogCustomEventNullEvent()
        {
            engineLoggingServicesHelper.LogCustomEvent(null);
        }

        /// <summary>
        /// Test that we can log CustomEvents
        /// </summary>
        [Test]
        public void LogCustomEvent()
        {
            List<BuildEventArgs> eventList = new List<BuildEventArgs>();
            
            // Log a number of events and keep track of which events we sent to the logger
            for (int i = 0; i < 10; i++)
            {
                MyCustomBuildEventArgs eventToAdd = GenerateBuildCustomEventArgs("CustomMessage" + i);
                eventList.Add(eventToAdd);
                engineLoggingServicesHelper.LogCustomEvent(eventToAdd);
            }

            // Get the current queue of the logger
           DualQueue<BuildEventArgs> currentQueue = engineLoggingServicesHelper.GetCurrentQueueBuildEvents();

            // Assert that every event we sent to the logger exists in the logging queue
            Assert.IsTrue(eventList.TrueForAll(delegate(BuildEventArgs args)
                                               {
                                                   return currentQueue.Contains(args);
                                               }), "Expected to find all events sent to logcustomevent");
            
            // Assert that every event in the queue is of the correct type
            AssertForEachEventInQueue(currentQueue, IsInstanceOfType<MyCustomBuildEventArgs>);
        }

        /// <summary>
        /// Test that when we send an event to the logger we should see that same event in the queue
        /// </summary>
        [Test]
        public void PostLoggingEventCustomEvent()
        {
            BuildEventArgs testBuildEventArgs = GenerateBuildCustomEventArgs("CustomMessage");
            engineLoggingServicesHelper.PostLoggingEvent(testBuildEventArgs);
            Assert.IsTrue(engineLoggingServicesHelper.GetCurrentQueueBuildEvents().Contains(testBuildEventArgs), "Expected to find event sent to postloggingevent");
        }

        /// <summary>
        /// Test that when we send an event to the logger we should see that same event in the queue
        /// </summary>
        [Test]
        public void PostLoggingEventErrorEvent()
        {
            BuildEventArgs testBuildEventArgs = GenerateBuildErrorEventArgs("testErrorBuildEvent");
            engineLoggingServicesHelper.PostLoggingEvent(testBuildEventArgs);
            Assert.IsTrue(engineLoggingServicesHelper.GetCurrentQueueBuildEvents().Contains(testBuildEventArgs), "Expected to find event we sent to postloggingevent");

        }

        /// <summary>
        /// Test that when we send an event to the logger we should see that same event in the queue
        /// </summary>
        [Test]
        public void PostLoggingEventWarningEvent()
        {
            BuildEventArgs testBuildEventArgs = GenerateBuildWarningEventArgs("testWarningBuildEvent");
            engineLoggingServicesHelper.PostLoggingEvent(testBuildEventArgs);
            Assert.IsTrue(engineLoggingServicesHelper.GetCurrentQueueBuildEvents().Contains(testBuildEventArgs), "Expected to find event sent to postloggingevent");
        }

        /// <summary>
        /// Test that when we send an event to the logger we should see that same event in the queue
        /// </summary>
        [Test]
        public void PostLoggingEventMessageEvent()
        {
            BuildEventArgs testBuildEventArgs = GenerateBuildMessageEventArgs("testMessageBuildEvent", MessageImportance.Normal);
            engineLoggingServicesHelper.PostLoggingEvent(testBuildEventArgs);
            Assert.IsTrue(engineLoggingServicesHelper.GetCurrentQueueBuildEvents().Contains(testBuildEventArgs), "Expected to find event sent to postloggingevent");
        }

        /// <summary>
        /// Test that when we send an multiple event to the logger  on multiple threads, we should everyone one of those event in the queue
        /// </summary>
        [Test]
        public void PostLoggingEventMultiThreaded()
        {
            List<BuildEventArgs> eventsAdded = new List<BuildEventArgs>();
            
            // Add a number of events on multiple threads
            ManualResetEvent[] waitHandles = new ManualResetEvent[10];
            for (int i = 0; i < waitHandles.Length; i++)
            {
                waitHandles[i] = new ManualResetEvent(false);
                ThreadPool.QueueUserWorkItem(
                              delegate(object state)
                              {
                                  for (int j = 0; j < 4; j++)
                                  {
                                      BuildEventArgs testBuildEventArgs = GenerateBuildMessageEventArgs("testMessageBuildEvent" + i + "_" + j, MessageImportance.Normal);
                                      lock (eventsAdded)
                                      {
                                          eventsAdded.Add(testBuildEventArgs);
                                      }
                                      engineLoggingServicesHelper.PostLoggingEvent(testBuildEventArgs);
                                  }
                                  ((ManualResetEvent)state).Set();
                              }, waitHandles[i]);
            }

            // Wait for the threads to finish
            foreach (ManualResetEvent resetEvent in waitHandles)
            {
                resetEvent.WaitOne();
            }

            // Get the current queue
            DualQueue<BuildEventArgs> currentQueue = engineLoggingServicesHelper.GetCurrentQueueBuildEvents();

            // Assert that every event we sent to the logger on the multiple threads is in the queue at the end
            Assert.IsTrue(eventsAdded.TrueForAll(delegate(BuildEventArgs args)
                                               {
                                                   return currentQueue.Contains(args);
                                               }), "Expected to find all events added to queue on multiple threads to be in the queue at the end");
        }
     #endregion

        #region TestLogCommentMethods
        /// <summary>
        ///  Test logging a null comment to the logger
        /// </summary>
        [Test]
        public void LogCommentFromTextNullMessage()
        {
            // Test the case where we are trying to log a null and we are also trying to log only critical events
            try
            {
                engineLoggingServicesHelper.OnlyLogCriticalEvents = true;
                engineLoggingServicesHelper.LogCommentFromText(null,MessageImportance.Low, null);
                engineLoggingServicesHelper.OnlyLogCriticalEvents = false;
            }
            catch (Exception e)
            {
                Assert.Fail(e.Message + " Should not throw exception if OnlyLogCriticalEvents is true");
            }

            // Would have tested the case where null was passed and critical events is false, but this would cause an assertion window
            // to popup thereby failing the test
        }
       
        /// <summary>
        /// Test logging messages to the logger
        /// </summary>
        [Test]
        public void LogCommentFromTextGoodMessages()
        {
            // Send a message, this message should be posted to the queue
            engineLoggingServicesHelper.LogCommentFromText(null, MessageImportance.Low, "Message");
            engineLoggingServicesHelper.LogCommentFromText(null, MessageImportance.Low, string.Empty);
            
            // Make sure that the one message got posted to the queue
            DualQueue<BuildEventArgs> currentQueue = engineLoggingServicesHelper.GetCurrentQueueBuildEvents();
            Assert.IsTrue(currentQueue.Count == 2, "Expected to find two events on the queue");
            AssertForEachEventInQueue(currentQueue, IsInstanceOfType<BuildMessageEventArgs>);

        }
        
        /// <summary>
        /// Test logging message comments to the logger
        /// </summary>
        [Test]
        public void LogCommentGoodMessages()
        {
            // Send a message while not logging critical events, since comments are not considered critical they should 
            // not show up in the queue
            engineLoggingServicesHelper.OnlyLogCriticalEvents = true;
            engineLoggingServicesHelper.LogComment((BuildEventContext)null, MessageImportance.Normal, "ErrorConvertedIntoWarning");
            engineLoggingServicesHelper.LogComment((BuildEventContext)null, MessageImportance.Normal, "ErrorConvertedIntoWarning", 3);
            engineLoggingServicesHelper.LogComment((BuildEventContext)null, "ErrorConvertedIntoWarning");
            engineLoggingServicesHelper.LogComment((BuildEventContext)null, "ErrorCount", 3);
            Assert.IsTrue(engineLoggingServicesHelper.GetCurrentQueueBuildEvents().Count == 0, "Expected to find no events on the queue");

            // Sent the message while we are logging events, even non critical ones.
            engineLoggingServicesHelper.OnlyLogCriticalEvents = false;
            engineLoggingServicesHelper.LogComment((BuildEventContext)null, MessageImportance.Normal, "ErrorConvertedIntoWarning");
            engineLoggingServicesHelper.LogComment((BuildEventContext)null, MessageImportance.Normal, "ErrorConvertedIntoWarning", 3);
            engineLoggingServicesHelper.LogComment((BuildEventContext)null, "ErrorConvertedIntoWarning");
            engineLoggingServicesHelper.LogComment((BuildEventContext)null, "ErrorCount", 3);
            
            // Get the queue from the logger
           DualQueue<BuildEventArgs> currentQueue = engineLoggingServicesHelper.GetCurrentQueueBuildEvents();
            
            // Make sure we got all the events we sent to the logger
            Assert.IsTrue(currentQueue.Count == 4, "Expected to find four events on the queue");
            
            // Make sure that every event in the queue is of the correct type
            AssertForEachEventInQueue(currentQueue, IsInstanceOfType<BuildMessageEventArgs>);
        }
        #endregion

        #region TestStartedFinished
        /// <summary>
        /// Test logging the build started event
        /// </summary>
        [Test]
        public void LogBuildStarted()
        {
            engineLoggingServicesHelper.LogBuildStarted();
            DualQueue<BuildEventArgs> currentQueue = engineLoggingServicesHelper.GetCurrentQueueBuildEvents();
            Assert.IsTrue(currentQueue.Count == 1, "Expected to find one event in the queue");
            AssertForEachEventInQueue(currentQueue, IsInstanceOfType<BuildStartedEventArgs>);

        }

        /// <summary>
        ///  Test logging the build finished event
        /// </summary>
        [Test]
        public void LogBuildFinished()
        {
            engineLoggingServicesHelper.LogBuildFinished(true);
            engineLoggingServicesHelper.LogBuildFinished(false);
            DualQueue<BuildEventArgs> currentQueue = engineLoggingServicesHelper.GetCurrentQueueBuildEvents();
            Assert.IsTrue(currentQueue.Count == 2, "Expected to find two events in the queue");
            AssertForEachEventInQueue(currentQueue, IsInstanceOfType<BuildFinishedEventArgs>);
        }

        /// <summary>
        /// Checks to make sure that the event passed in is an instance of TType
        /// </summary>
        /// <typeparam name="TType">Type of event which should be found</typeparam>
        /// <param name="e">Event to check against TType</param>
        public void IsInstanceOfType<TType>(BuildEventArgs e)
        {
            Assert.IsTrue(typeof(TType).IsInstanceOfType(e), "Expected event to be a " + typeof(TType).Name);
        }

        /// <summary>
        /// Check that every event in the queue is of the correct event type
        /// </summary>
        /// <param name="queue">queue to check</param>
        /// <param name="action">An action which determines wheather or not a queue item is correct</param>
        private void AssertForEachEventInQueue(DualQueue<BuildEventArgs> queue, Action<BuildEventArgs> action)
        {
            BuildEventArgs eventArgs;

            while((eventArgs = queue.Dequeue())!=null)
            {
                action(eventArgs);
            }
        }

        /// <summary>
        /// Test logging of the task started event
        /// </summary>
        [Test]
        public void LogTaskStarted()
        {
            // Test the logging while only logging critical events
            engineLoggingServicesHelper.OnlyLogCriticalEvents = true;
            engineLoggingServicesHelper.LogTaskStarted(null, "taskName", "projectFile", "projectFileOfTaskNode");
            Assert.IsTrue(engineLoggingServicesHelper.GetCurrentQueueBuildEvents().Count == 0, "Expected to find no events in the queue");
            
            // Test logging while logging all events
            engineLoggingServicesHelper.OnlyLogCriticalEvents = false;
            engineLoggingServicesHelper.LogTaskStarted(null, "taskName", "projectFile", "projectFileOfTaskNode");
           DualQueue<BuildEventArgs> currentQueue = engineLoggingServicesHelper.GetCurrentQueueBuildEvents();
            Assert.IsTrue(currentQueue.Count == 1, "Expected to find one event in the queue");
            AssertForEachEventInQueue(currentQueue, IsInstanceOfType<TaskStartedEventArgs>);
        }

        /// <summary>
        ///  Test that the TaskFinished event logs correctly
        /// </summary>
        [Test]
        public void LogTaskFinished()
        {
            // Test the logging while only logging critical events
            engineLoggingServicesHelper.OnlyLogCriticalEvents = true;
            engineLoggingServicesHelper.LogTaskFinished(null, "taskName", "projectFile", "projectFileOfTaskNode", true);
            Assert.IsTrue(engineLoggingServicesHelper.GetCurrentQueueBuildEvents().Count == 0, "Expected to find no events in the queue");
            
            // Test logging while logging all events
            engineLoggingServicesHelper.OnlyLogCriticalEvents = false;
            engineLoggingServicesHelper.LogTaskFinished(null, "taskName", "projectFile", "projectFileOfTaskNode", true);
           DualQueue<BuildEventArgs> currentQueue = engineLoggingServicesHelper.GetCurrentQueueBuildEvents();
            Assert.IsTrue(currentQueue.Count == 1, "Expected to find one event in the queue");
            AssertForEachEventInQueue(currentQueue, IsInstanceOfType<TaskFinishedEventArgs>);
        }

        /// <summary>
        /// Test that the TargetStarted event logs correctly
        /// </summary>
        [Test]
        public void LogTargetStarted()
        {
            // Test logging while only logging critical events
            engineLoggingServicesHelper.OnlyLogCriticalEvents = true;
            engineLoggingServicesHelper.LogTargetStarted(null, "TargetName", "projectFile", "projectFileOfTargetNode");
            Assert.IsTrue(engineLoggingServicesHelper.GetCurrentQueueBuildEvents().Count == 0, "Expected to find no events in the queue");
            
            // Test logging while logging all events
            engineLoggingServicesHelper.OnlyLogCriticalEvents = false;
            engineLoggingServicesHelper.LogTargetStarted(null, "targetName", "projectFile", "projectFileOfTargetNode");
           DualQueue<BuildEventArgs> currentQueue = engineLoggingServicesHelper.GetCurrentQueueBuildEvents();
            Assert.IsTrue(currentQueue.Count == 1, "Expected to find one event in the queue");
            AssertForEachEventInQueue(currentQueue, IsInstanceOfType<TargetStartedEventArgs>);
        }

        /// <summary>
        /// Test that TargetFinished logs correctly
        /// </summary>
        [Test]
        public void LogTargetFinished()
        {
            // Test logging while only logging critical events
            engineLoggingServicesHelper.OnlyLogCriticalEvents = true;
            engineLoggingServicesHelper.LogTargetFinished(null, "TargetName", "projectFile", "projectFileOfTargetNode", true);
            Assert.IsTrue(engineLoggingServicesHelper.GetCurrentQueueBuildEvents().Count == 0, "Expected to find no events in the queue");
            
            // Test logging while logging all events, even non critical ones
            engineLoggingServicesHelper.OnlyLogCriticalEvents = false;
            engineLoggingServicesHelper.LogTargetFinished(null, "TargetName", "projectFile", "projectFileOfTargetNode", true);
           DualQueue<BuildEventArgs> currentQueue = engineLoggingServicesHelper.GetCurrentQueueBuildEvents();
            Assert.IsTrue(currentQueue.Count == 1, "Expected to find one event in the queue");
            AssertForEachEventInQueue(currentQueue, IsInstanceOfType<TargetFinishedEventArgs>);
        }

        /// <summary>
        /// Test logging the ProjectStarted event
        /// </summary>
        [Test]
        public void LogProjectStarted()
        {
            // Test logging while only logging critical events
            engineLoggingServicesHelper.OnlyLogCriticalEvents = true;
            engineLoggingServicesHelper.LogProjectStarted(-1, null, null, "projectFile", "targetNames", null, null);
            Assert.IsTrue(engineLoggingServicesHelper.GetCurrentQueueBuildEvents().Count == 0, "Expected to find no events in the queue");
            
            // Test logging while logging all events, even non critical ones
            engineLoggingServicesHelper.OnlyLogCriticalEvents = false;
            engineLoggingServicesHelper.LogProjectStarted(-1, null, null, "projectFile", "targetNames", null, null);
           DualQueue<BuildEventArgs> currentQueue = engineLoggingServicesHelper.GetCurrentQueueBuildEvents();
            Assert.IsTrue(currentQueue.Count == 1, "Expected to find one event in the queue");
            AssertForEachEventInQueue(currentQueue, IsInstanceOfType<ProjectStartedEventArgs>);
        }

        /// <summary>
        /// Test logging the ProjectFinished event
        /// </summary>
        [Test]
        public void LogProjectFinished()
        {
            // Test logging while only logging critical events
            engineLoggingServicesHelper.OnlyLogCriticalEvents = true;
            engineLoggingServicesHelper.LogProjectFinished(null, "projectFile", true);
            Assert.IsTrue(engineLoggingServicesHelper.GetCurrentQueueBuildEvents().Count == 0, "Expected no events in queue but found some");
            
            //Test logging while logging all events, even non critical ones
            engineLoggingServicesHelper.OnlyLogCriticalEvents = false;
            engineLoggingServicesHelper.LogProjectFinished(null, "projectFile", true);
           DualQueue<BuildEventArgs> currentQueue = engineLoggingServicesHelper.GetCurrentQueueBuildEvents();
            Assert.IsTrue(currentQueue.Count == 1, "Expected find one item in the queue");
            AssertForEachEventInQueue(currentQueue, IsInstanceOfType<ProjectFinishedEventArgs>);
        }
        #endregion

        #region LoggingMethodTests

        [Test]
        public void LogTaskWarningFromException()
        {
            engineLoggingServicesHelper.LogTaskWarningFromException(null, new Exception("testException"), new BuildEventFileInfo("noFile"), "taskName");
            engineLoggingServicesHelper.LogTaskWarningFromException(null, null, new BuildEventFileInfo("noFile"), "taskName");
           DualQueue<BuildEventArgs> currentQueue = engineLoggingServicesHelper.GetCurrentQueueBuildEvents();
            Assert.IsTrue(currentQueue.Count == 2, "Expected two warnings in queue items");
            AssertForEachEventInQueue(currentQueue, IsInstanceOfType<BuildWarningEventArgs>);
        }

        [Test]
        public void LogErrorWithoutSubcategoryResourceName()
        {
            engineLoggingServicesHelper.OnlyLogCriticalEvents = true;
            engineLoggingServicesHelper.LogError(null, new BuildEventFileInfo("file"), "BuildTargetCompletely", "target");
           DualQueue<BuildEventArgs> currentQueue = engineLoggingServicesHelper.GetCurrentQueueBuildEvents();
            Assert.IsTrue(currentQueue.Count == 1, "Expected one event in queue!");
            AssertForEachEventInQueue(currentQueue, IsInstanceOfType<BuildErrorEventArgs>);
        }

        [Test]
        public void LogErrorWithSubcategoryResourceName()
        {
            engineLoggingServicesHelper.OnlyLogCriticalEvents = true;
            engineLoggingServicesHelper.LogError(null, "SubCategoryForSchemaValidationErrors", new BuildEventFileInfo("file"), "BuildTargetCompletely", "target");
           DualQueue<BuildEventArgs> currentQueue = engineLoggingServicesHelper.GetCurrentQueueBuildEvents();
            Assert.IsTrue(currentQueue.Count == 1, "Expected one event in queue!");
            AssertForEachEventInQueue(currentQueue, IsInstanceOfType<BuildErrorEventArgs>);
        }

        [Test]
        public void LogErrorFromText()
        {
            engineLoggingServicesHelper.OnlyLogCriticalEvents = true;
            engineLoggingServicesHelper.LogErrorFromText(null, "SubCategoryForSchemaValidationErrors", "MSB4000", "helpKeyword", new BuildEventFileInfo("file"), "error");
           DualQueue<BuildEventArgs> currentQueue = engineLoggingServicesHelper.GetCurrentQueueBuildEvents();
            Assert.IsTrue(currentQueue.Count == 1, "Expected one event in queue!");
            AssertForEachEventInQueue(currentQueue, IsInstanceOfType<BuildErrorEventArgs>);
        }

        [Test]
        public void LogWarningWithoutSubcategoryResourceName()
        {
            engineLoggingServicesHelper.OnlyLogCriticalEvents = true;
            engineLoggingServicesHelper.LogWarning(null, new BuildEventFileInfo("file"), "BuildTargetCompletely", "target");
           DualQueue<BuildEventArgs> currentQueue = engineLoggingServicesHelper.GetCurrentQueueBuildEvents();
            Assert.IsTrue(currentQueue.Count == 1, "Expected one event in queue!");
            AssertForEachEventInQueue(currentQueue, IsInstanceOfType<BuildWarningEventArgs>);
        }

        [Test]
        public void LogWarningWithSubcategoryResourceName()
        {
            engineLoggingServicesHelper.OnlyLogCriticalEvents = true;
            engineLoggingServicesHelper.LogWarning(null, "SubCategoryForSchemaValidationErrors", new BuildEventFileInfo("file"), "BuildTargetCompletely", "target");
           DualQueue<BuildEventArgs> currentQueue = engineLoggingServicesHelper.GetCurrentQueueBuildEvents();
            Assert.IsTrue(currentQueue.Count == 1, "Expected one event in queue!");
            AssertForEachEventInQueue(currentQueue, IsInstanceOfType<BuildWarningEventArgs>);
        }

        [Test]
        public void LogWarningFromText()
        {
            engineLoggingServicesHelper.OnlyLogCriticalEvents = true;
            engineLoggingServicesHelper.LogWarningFromText(null, "SubCategoryForSchemaValidationErrors", "MSB4000", "helpKeyword", new BuildEventFileInfo("file"), "Warning");
           DualQueue<BuildEventArgs> currentQueue = engineLoggingServicesHelper.GetCurrentQueueBuildEvents();
            Assert.IsTrue(currentQueue.Count == 1, "Expected one event in queue!");
            AssertForEachEventInQueue(currentQueue, IsInstanceOfType<BuildWarningEventArgs>);
        }

        [Test]
        public void LogInvalidProjectFileError()
        {
            engineLoggingServicesHelper.OnlyLogCriticalEvents = true;
            engineLoggingServicesHelper.LogInvalidProjectFileError(null, new InvalidProjectFileException());
            engineLoggingServicesHelper.LogInvalidProjectFileError(null, new InvalidProjectFileException("invalidProjectFile"));
            InvalidProjectFileException invalidProjectFileException = new InvalidProjectFileException("anotherInvalidProjectFile");
            invalidProjectFileException.HasBeenLogged = true;
            engineLoggingServicesHelper.LogInvalidProjectFileError(null, invalidProjectFileException);
           DualQueue<BuildEventArgs> currentQueue = engineLoggingServicesHelper.GetCurrentQueueBuildEvents();
            Assert.IsTrue(currentQueue.Count == 2, "Expected two errors in Queue");
            AssertForEachEventInQueue(currentQueue, IsInstanceOfType<BuildErrorEventArgs>);
        }

        [Test]
        public void LogFatalBuildError()
        {
            engineLoggingServicesHelper.OnlyLogCriticalEvents = true;
            engineLoggingServicesHelper.LogFatalBuildError(null, new Exception("exception1!"), new BuildEventFileInfo("file1"));
            engineLoggingServicesHelper.LogFatalBuildError(null, new Exception("exception2!"), new BuildEventFileInfo("file2"));
            engineLoggingServicesHelper.LogFatalBuildError(null, new Exception("exception3!"), new BuildEventFileInfo("file3"));
           DualQueue<BuildEventArgs> currentQueue = engineLoggingServicesHelper.GetCurrentQueueBuildEvents();
            Assert.IsTrue(currentQueue.Count == 3, "Expected three errors in Queue");
            AssertForEachEventInQueue(currentQueue, IsInstanceOfType<BuildErrorEventArgs>);
        }

        [Test]
        public void LogFatalError()
        {
            engineLoggingServicesHelper.OnlyLogCriticalEvents = true;
            engineLoggingServicesHelper.LogFatalError(null, new Exception("exception1"), new BuildEventFileInfo("file1"), "BuildTargetCompletely", "target1");
            engineLoggingServicesHelper.LogFatalError(null, new Exception("exception2"), new BuildEventFileInfo("file2"), "BuildTargetCompletely", "target2");
            engineLoggingServicesHelper.LogFatalError(null, new Exception("exception3"), new BuildEventFileInfo("file3"), "BuildTargetCompletely", "target3");
           DualQueue<BuildEventArgs> currentQueue = engineLoggingServicesHelper.GetCurrentQueueBuildEvents();
            Assert.IsTrue(currentQueue.Count == 3, "Expected three errors in Queue");
            AssertForEachEventInQueue(currentQueue, IsInstanceOfType<BuildErrorEventArgs>);

        }

        [Test]
        public void LogFatalTaskError()
        {
            engineLoggingServicesHelper.OnlyLogCriticalEvents = true;
            engineLoggingServicesHelper.LogFatalTaskError(null, new Exception("exception1"), new BuildEventFileInfo("file1"), "task1");
            engineLoggingServicesHelper.LogFatalTaskError(null, new Exception("exception2"), new BuildEventFileInfo("file2"), "task2");
            engineLoggingServicesHelper.LogFatalTaskError(null, new Exception("exception3"), new BuildEventFileInfo("file3"), "task3");
           DualQueue<BuildEventArgs> currentQueue = engineLoggingServicesHelper.GetCurrentQueueBuildEvents();
            Assert.IsTrue(currentQueue.Count == 3, "Expected three errors in Queue");
            AssertForEachEventInQueue(currentQueue, IsInstanceOfType<BuildErrorEventArgs>);
        }
        #endregion

        #region InProcLoggingTests

        internal class VerifyEventSourceHelper
        {
            public VerifyEventSourceHelper()
            {
                sourceForEvents = new EventSource();
                sourceForEvents.AnyEventRaised += new AnyEventHandler(this.AnyEventRaised);
                sourceForEvents.BuildFinished += new BuildFinishedEventHandler(this.BuildFinished);
                sourceForEvents.BuildStarted += new BuildStartedEventHandler(this.BuildStarted);
                sourceForEvents.CustomEventRaised += new CustomBuildEventHandler(this.CustomEventRaised);
                sourceForEvents.ErrorRaised += new BuildErrorEventHandler(this.ErrorRaised);
                sourceForEvents.MessageRaised += new BuildMessageEventHandler(this.MessageRaised);
                sourceForEvents.ProjectFinished += new ProjectFinishedEventHandler(this.ProjectFinished);
                sourceForEvents.ProjectStarted += new ProjectStartedEventHandler(this.ProjectStarted);
                sourceForEvents.TargetFinished += new TargetFinishedEventHandler(this.TargetFinished);
                sourceForEvents.TargetStarted += new TargetStartedEventHandler(this.TargetStarted);
                sourceForEvents.TaskFinished += new TaskFinishedEventHandler(this.TaskFinished);
                sourceForEvents.TaskStarted += new TaskStartedEventHandler(this.TaskStarted);
                sourceForEvents.WarningRaised += new BuildWarningEventHandler(this.WarningRaised);
                sourceForEvents.StatusEventRaised += new BuildStatusEventHandler(this.StatusRaised);
                ClearEvents();
            }

            public void ClearEvents()
            {
                eventsRaisedHash = new Hashtable();
                eventsRaisedHash.Add("messageRaised", false);
                eventsRaisedHash.Add("errorRaised", false);
                eventsRaisedHash.Add("warningRaised", false);
                eventsRaisedHash.Add("buildStarted", false);
                eventsRaisedHash.Add("buildStatus", false);
                eventsRaisedHash.Add("buildFinished", false);
                eventsRaisedHash.Add("projectStarted", false);
                eventsRaisedHash.Add("projectFinished", false);
                eventsRaisedHash.Add("targetStarted", false);
                eventsRaisedHash.Add("targetFinished", false);
                eventsRaisedHash.Add("taskStarted", false);
                eventsRaisedHash.Add("taskFinished", false);
                eventsRaisedHash.Add("customEventRaised", false);
                eventsRaisedHash.Add("anyEventRaised", false);
                eventsRaisedHash.Add("statusRaised", false);

            }
            #region Fields
            Hashtable eventsRaisedHash;
            public EventSource sourceForEvents;
            #endregion
            #region EventHandlers
            public void MessageRaised(object sender, BuildMessageEventArgs arg)
            {
                eventsRaisedHash["messageRaised"] = true;
            }

            public void StatusRaised(object sender, BuildStatusEventArgs arg)
            {
                eventsRaisedHash["statusRaised"] = true;
            }
            public void ErrorRaised(object sender, BuildErrorEventArgs arg)
            {
                eventsRaisedHash["errorRaised"] = true;
            }
            public void WarningRaised(object sender, BuildWarningEventArgs arg)
            {
                eventsRaisedHash["warningRaised"] = true;
            }
            public void BuildStarted(object sender, BuildStartedEventArgs arg)
            {
                eventsRaisedHash["buildStarted"] = true;
            }
            public void BuildFinished(object sender, BuildFinishedEventArgs arg)
            {
                eventsRaisedHash["buildFinished"] = true;
            }
            public void ProjectStarted(object sender, ProjectStartedEventArgs arg)
            {
                eventsRaisedHash["projectStarted"] = true;
            }
            public void ProjectFinished(object sender, ProjectFinishedEventArgs arg)
            {
                eventsRaisedHash["projectFinished"] = true;
            }
            public void TargetStarted(object sender, TargetStartedEventArgs arg)
            {
                eventsRaisedHash["targetStarted"] = true;
            }
            public void TargetFinished(object sender, TargetFinishedEventArgs arg)
            {
                eventsRaisedHash["targetFinished"] = true;
            }
            public void TaskStarted(object sender, TaskStartedEventArgs arg)
            {
                eventsRaisedHash["taskStarted"] = true;
            }
            public void TaskFinished(object sender, TaskFinishedEventArgs arg)
            {
                eventsRaisedHash["taskFinished"] = true;
            }
            public void CustomEventRaised(object sender, CustomBuildEventArgs arg)
            {
                eventsRaisedHash["customEventRaised"] = true;
            }
            public void AnyEventRaised(object sender, BuildEventArgs arg)
            {
                eventsRaisedHash["anyEventRaised"] = true;
            }
            #endregion

            #region Assertions
            public void AssertEventsAndNoOthers(params string[] eventList)
            {
                List<string> events = new List<string>(eventList);
                foreach (string eventKey in eventList)
                {
                    Assert.IsTrue(eventsRaisedHash[eventKey] != null, string.Format("Key {0} was not found in events list", eventKey));
                }

                foreach (string key in eventsRaisedHash.Keys)
                {

                    if (events.Contains(key))
                    {
                        Assert.IsTrue((bool)(eventsRaisedHash[key]) == true, string.Format("Key {0} Should have been true", key));
                        continue;
                    }
                    Assert.IsFalse((bool)(eventsRaisedHash[key]) == true, string.Format("Key {0} Should not have been true", key));
                }
            }
            #endregion
        }
        internal class MyCustomBuildErrorEventArgs : BuildErrorEventArgs
        {
            public MyCustomBuildErrorEventArgs()
                : base()
            {
            }
        }

        internal class MyCustomBuildWarningEventArgs : BuildWarningEventArgs
        {
            public MyCustomBuildWarningEventArgs()
                : base()
            {
            }
        }

        internal class MyCustomBuildMessageEventArgs : BuildMessageEventArgs
        {
            public MyCustomBuildMessageEventArgs()
                : base()
            {
            }
        }

        internal class MyCustomBuildEventArg : BuildEventArgs
        {
            public MyCustomBuildEventArg()
                : base()
            {
            }
        }

        internal class MyCustomStatusEventArg : BuildStatusEventArgs
        {
            public MyCustomStatusEventArg()
                : base()
            {
            }
        }

        [Test]
        public void InProcProcessPostedLoggingEvents()
        {

            VerifyEventSourceHelper eventSourceHelper = new VerifyEventSourceHelper();

            List<EngineLoggingServicesInProc> engines = new List<EngineLoggingServicesInProc>();
            EngineLoggingServicesInProc inProcLoggingServicesEventsAllEvents = new EngineLoggingServicesInProc(eventSourceHelper.sourceForEvents, false, new ManualResetEvent(false));
            EngineLoggingServicesInProc inProcLoggingServicesEventsOnlyCriticalEvents = new EngineLoggingServicesInProc(eventSourceHelper.sourceForEvents, true, new ManualResetEvent(false));
            engines.Add(inProcLoggingServicesEventsAllEvents);
            engines.Add(inProcLoggingServicesEventsOnlyCriticalEvents);

            foreach (EngineLoggingServicesInProc inProcLoggingServicesEvents in engines)
            {
                inProcLoggingServicesEvents.PostLoggingEvent(new BuildMessageEventArgs("Message", "help", "sender", MessageImportance.Low));
                inProcLoggingServicesEvents.ProcessPostedLoggingEvents();
                eventSourceHelper.AssertEventsAndNoOthers("anyEventRaised", "messageRaised");
                eventSourceHelper.ClearEvents();

                inProcLoggingServicesEvents.PostLoggingEvent(new TaskStartedEventArgs("message", "help", "projectFile", "taskFile", "taskName"));
                inProcLoggingServicesEvents.ProcessPostedLoggingEvents();
                eventSourceHelper.AssertEventsAndNoOthers("anyEventRaised", "taskStarted", "statusRaised");
                eventSourceHelper.ClearEvents();

                inProcLoggingServicesEvents.PostLoggingEvent(new TaskFinishedEventArgs("message", "help", "projectFile", "taskFile", "taskName", true));
                inProcLoggingServicesEvents.ProcessPostedLoggingEvents();
                eventSourceHelper.AssertEventsAndNoOthers("anyEventRaised", "taskFinished", "statusRaised");
                eventSourceHelper.ClearEvents();

                inProcLoggingServicesEvents.PostLoggingEvent(new TaskCommandLineEventArgs("commandLine", "taskName", MessageImportance.Low));
                inProcLoggingServicesEvents.ProcessPostedLoggingEvents();
                eventSourceHelper.AssertEventsAndNoOthers("anyEventRaised", "messageRaised");
                eventSourceHelper.ClearEvents();

                inProcLoggingServicesEvents.PostLoggingEvent(new BuildWarningEventArgs("SubCategoryForSchemaValidationErrors", "MSB4000", "file", 1, 2, 3, 4, "message", "help", "sender"));
                inProcLoggingServicesEvents.ProcessPostedLoggingEvents();
                eventSourceHelper.AssertEventsAndNoOthers("anyEventRaised", "warningRaised");
                eventSourceHelper.ClearEvents();

                inProcLoggingServicesEvents.PostLoggingEvent(new BuildErrorEventArgs("SubCategoryForSchemaValidationErrors", "MSB4000", "file", 1, 2, 3, 4, "message", "help", "sender"));
                inProcLoggingServicesEvents.ProcessPostedLoggingEvents();
                eventSourceHelper.AssertEventsAndNoOthers("anyEventRaised", "errorRaised");
                eventSourceHelper.ClearEvents();

                inProcLoggingServicesEvents.PostLoggingEvent(new TargetStartedEventArgs("message", "help", "targetName", "ProjectFile", "targetFile"));
                inProcLoggingServicesEvents.ProcessPostedLoggingEvents();
                eventSourceHelper.AssertEventsAndNoOthers("anyEventRaised", "targetStarted", "statusRaised");
                eventSourceHelper.ClearEvents();

                inProcLoggingServicesEvents.PostLoggingEvent(new TargetFinishedEventArgs("message", "help", "targetName", "ProjectFile", "targetFile", true));
                inProcLoggingServicesEvents.ProcessPostedLoggingEvents();
                eventSourceHelper.AssertEventsAndNoOthers("anyEventRaised", "targetFinished", "statusRaised");
                eventSourceHelper.ClearEvents();

                inProcLoggingServicesEvents.PostLoggingEvent(new ProjectStartedEventArgs(-1, "message", "help", "ProjectFile", "targetNames", null, null, null));
                inProcLoggingServicesEvents.ProcessPostedLoggingEvents();
                eventSourceHelper.AssertEventsAndNoOthers("anyEventRaised", "projectStarted", "statusRaised");
                eventSourceHelper.ClearEvents();

                inProcLoggingServicesEvents.PostLoggingEvent(new ProjectFinishedEventArgs("message", "help", "ProjectFile", true));
                inProcLoggingServicesEvents.ProcessPostedLoggingEvents();
                eventSourceHelper.AssertEventsAndNoOthers("anyEventRaised", "projectFinished", "statusRaised");
                eventSourceHelper.ClearEvents();

                inProcLoggingServicesEvents.PostLoggingEvent(new BuildStartedEventArgs("message", "help"));
                inProcLoggingServicesEvents.ProcessPostedLoggingEvents();
                eventSourceHelper.AssertEventsAndNoOthers("anyEventRaised", "buildStarted", "statusRaised");
                eventSourceHelper.ClearEvents();

                inProcLoggingServicesEvents.PostLoggingEvent(new BuildFinishedEventArgs("message", "help", true));
                inProcLoggingServicesEvents.ProcessPostedLoggingEvents();
                eventSourceHelper.AssertEventsAndNoOthers("anyEventRaised", "buildFinished", "statusRaised");
                eventSourceHelper.ClearEvents();

                inProcLoggingServicesEvents.PostLoggingEvent(new ExternalProjectStartedEventArgs("message", "help", "senderName", "projectFile", "targetNames"));
                inProcLoggingServicesEvents.ProcessPostedLoggingEvents();
                eventSourceHelper.AssertEventsAndNoOthers("anyEventRaised", "customEventRaised");
                eventSourceHelper.ClearEvents();

                inProcLoggingServicesEvents.PostLoggingEvent(new ExternalProjectFinishedEventArgs("message", "help", "senderName", "projectFile", true));
                inProcLoggingServicesEvents.ProcessPostedLoggingEvents();
                eventSourceHelper.AssertEventsAndNoOthers("anyEventRaised", "customEventRaised");
                eventSourceHelper.ClearEvents();

                inProcLoggingServicesEvents.PostLoggingEvent(new MyCustomBuildEventArgs());
                inProcLoggingServicesEvents.ProcessPostedLoggingEvents();
                eventSourceHelper.AssertEventsAndNoOthers("anyEventRaised", "customEventRaised");
                eventSourceHelper.ClearEvents();

                inProcLoggingServicesEvents.PostLoggingEvent(new MyCustomBuildErrorEventArgs());
                inProcLoggingServicesEvents.ProcessPostedLoggingEvents();
                eventSourceHelper.AssertEventsAndNoOthers("anyEventRaised", "errorRaised");
                eventSourceHelper.ClearEvents();

                inProcLoggingServicesEvents.PostLoggingEvent(new MyCustomBuildWarningEventArgs());
                inProcLoggingServicesEvents.ProcessPostedLoggingEvents();
                eventSourceHelper.AssertEventsAndNoOthers("anyEventRaised", "warningRaised");
                eventSourceHelper.ClearEvents();

                inProcLoggingServicesEvents.PostLoggingEvent(new MyCustomBuildMessageEventArgs());
                inProcLoggingServicesEvents.ProcessPostedLoggingEvents();
                eventSourceHelper.AssertEventsAndNoOthers("anyEventRaised", "messageRaised");
                eventSourceHelper.ClearEvents();

                inProcLoggingServicesEvents.PostLoggingEvent(new MyCustomStatusEventArg());
                inProcLoggingServicesEvents.ProcessPostedLoggingEvents();
                eventSourceHelper.AssertEventsAndNoOthers("anyEventRaised", "statusRaised");
                eventSourceHelper.ClearEvents();
            }
        }



        [Test]
        public void TestCheckForFlushing()
        {

            VerifyEventSourceHelper eventSourceHelper = new VerifyEventSourceHelper();
            ManualResetEvent flushEvent = new ManualResetEvent(false);

            EngineLoggingServicesInProc inProcLoggingServicesEventsAllEvents = new EngineLoggingServicesInProc(eventSourceHelper.sourceForEvents, false, flushEvent);

            Assert.IsFalse(inProcLoggingServicesEventsAllEvents.NeedsFlush(DateTime.Now.Ticks), "Didn't expect to need a flush because of time passed");

            Assert.IsTrue(inProcLoggingServicesEventsAllEvents.NeedsFlush(DateTime.Now.Ticks + EngineLoggingServices.flushTimeoutInTicks + 1), "Expect to need a flush because of time passed");

            for (int i = 0; i < 1001; i++)
            {
                inProcLoggingServicesEventsAllEvents.PostLoggingEvent(new BuildMessageEventArgs("Message", "help", "sender", MessageImportance.Low));
            }

            Assert.IsTrue(inProcLoggingServicesEventsAllEvents.NeedsFlush(0), "Expect to need a flush because of number of events");

            // Expect the handle to be signaled
            long currentTicks = DateTime.Now.Ticks;
            flushEvent.WaitOne(5000, false);
            Assert.IsTrue((DateTime.Now.Ticks - currentTicks) / TimeSpan.TicksPerMillisecond < 4900, "Expected the handle to be signaled");
        }

        [Test]
        public void LocalForwardingOfLoggingEvents()
        {
            VerifyEventSourceHelper eventSourceHelper = new VerifyEventSourceHelper();
            EngineLoggingServicesInProc inProcLoggingServicesEventsAllEvents = new EngineLoggingServicesInProc(eventSourceHelper.sourceForEvents, false, new ManualResetEvent(false));

            VerifyEventSourceHelper localForwardingSourceHelper = new VerifyEventSourceHelper();
            VerifyEventSourceHelper centralLoggerEventSource   = new VerifyEventSourceHelper();
            inProcLoggingServicesEventsAllEvents.RegisterEventSource(EngineLoggingServicesInProc.LOCAL_FORWARDING_EVENTSOURCE, localForwardingSourceHelper.sourceForEvents);
            inProcLoggingServicesEventsAllEvents.RegisterEventSource(EngineLoggingServicesInProc.FIRST_AVAILABLE_LOGGERID, centralLoggerEventSource.sourceForEvents);

            // Create a local forwarding logger and initialize it
            ConfigurableForwardingLogger localForwardingLogger = new ConfigurableForwardingLogger();
            EventRedirector newRedirector = new EventRedirector(EngineLoggingServicesInProc.FIRST_AVAILABLE_LOGGERID, inProcLoggingServicesEventsAllEvents);
            localForwardingLogger.BuildEventRedirector = newRedirector;
            localForwardingLogger.Parameters = "TARGETSTARTEDEVENT;TARGETFINISHEDEVENT";
            localForwardingLogger.Initialize(localForwardingSourceHelper.sourceForEvents);

            // Verify that BuildStarted event is delivered both to the forwarding logger and ILoggers and
            // that the forwarding logger forwards it to the central logger
            inProcLoggingServicesEventsAllEvents.PostLoggingEvent(new TargetStartedEventArgs("message", "help", "targetName", "ProjectFile", "targetFile"));
            inProcLoggingServicesEventsAllEvents.ProcessPostedLoggingEvents();
            eventSourceHelper.AssertEventsAndNoOthers("anyEventRaised", "targetStarted", "statusRaised");
            localForwardingSourceHelper.AssertEventsAndNoOthers("anyEventRaised", "targetStarted", "statusRaised");
            inProcLoggingServicesEventsAllEvents.ProcessPostedLoggingEvents();
            centralLoggerEventSource.AssertEventsAndNoOthers("anyEventRaised", "targetStarted", "statusRaised");
            localForwardingSourceHelper.ClearEvents();
            centralLoggerEventSource.ClearEvents();
            eventSourceHelper.ClearEvents();

            // Verify that BuildFinished event is delivered both to the forwarding logger and ILoggers and
            // that the forwarding logger forwards it to the central logger
            inProcLoggingServicesEventsAllEvents.PostLoggingEvent(new TargetFinishedEventArgs("message", "help", "targetName", "ProjectFile", "targetFile", true));
            inProcLoggingServicesEventsAllEvents.ProcessPostedLoggingEvents();
            eventSourceHelper.AssertEventsAndNoOthers("anyEventRaised", "targetFinished", "statusRaised");
            localForwardingSourceHelper.AssertEventsAndNoOthers("anyEventRaised", "targetFinished", "statusRaised");
            inProcLoggingServicesEventsAllEvents.ProcessPostedLoggingEvents();
            centralLoggerEventSource.AssertEventsAndNoOthers("anyEventRaised", "targetFinished", "statusRaised");
            localForwardingSourceHelper.ClearEvents();
            centralLoggerEventSource.ClearEvents();
            eventSourceHelper.ClearEvents();

            // Verify that events that are not forwarded are not delivered to the central logger
            inProcLoggingServicesEventsAllEvents.PostLoggingEvent(new BuildMessageEventArgs("Message", "help", "sender", MessageImportance.Low));
            inProcLoggingServicesEventsAllEvents.ProcessPostedLoggingEvents();
            eventSourceHelper.AssertEventsAndNoOthers("anyEventRaised", "messageRaised");
            localForwardingSourceHelper.AssertEventsAndNoOthers("anyEventRaised", "messageRaised");
            inProcLoggingServicesEventsAllEvents.ProcessPostedLoggingEvents();
            centralLoggerEventSource.AssertEventsAndNoOthers();
            localForwardingSourceHelper.ClearEvents();
            centralLoggerEventSource.ClearEvents();
            eventSourceHelper.ClearEvents();

            // Verify that external events with no logger id are not delivered to the forwarding or central loggers
            inProcLoggingServicesEventsAllEvents.PostLoggingEvent(new NodeLoggingEvent(new BuildMessageEventArgs("Message", "help", "sender", MessageImportance.Low)));
            inProcLoggingServicesEventsAllEvents.ProcessPostedLoggingEvents();
            eventSourceHelper.AssertEventsAndNoOthers("anyEventRaised", "messageRaised");
            localForwardingSourceHelper.AssertEventsAndNoOthers();
            centralLoggerEventSource.AssertEventsAndNoOthers();
            localForwardingSourceHelper.ClearEvents();
            centralLoggerEventSource.ClearEvents();
            eventSourceHelper.ClearEvents();

            // Verify that external events with logger id are only delivered to central logger
            inProcLoggingServicesEventsAllEvents.PostLoggingEvent
                (new NodeLoggingEventWithLoggerId(new BuildMessageEventArgs("Message", "help", "sender", MessageImportance.Low), 2));
            inProcLoggingServicesEventsAllEvents.ProcessPostedLoggingEvents();
            eventSourceHelper.AssertEventsAndNoOthers();
            localForwardingSourceHelper.AssertEventsAndNoOthers();
            centralLoggerEventSource.AssertEventsAndNoOthers("anyEventRaised", "messageRaised");
            localForwardingSourceHelper.ClearEvents();
            centralLoggerEventSource.ClearEvents();
            eventSourceHelper.ClearEvents();
        }

        [Test]
        public void ConfigurationByEngine()
        {
            VerifyEventSourceHelper eventSourceHelper = new VerifyEventSourceHelper();
            EngineLoggingServicesInProc inProcLoggingServicesEventsAllEvents = new EngineLoggingServicesInProc(eventSourceHelper.sourceForEvents, false, new ManualResetEvent(false));
            VerifyEventSourceHelper eventSourcePrivateHelper = new VerifyEventSourceHelper();
            EngineLoggingServicesInProc inProcLoggingServicesEventsAllEventsPrivate = new EngineLoggingServicesInProc(eventSourcePrivateHelper.sourceForEvents, false, new ManualResetEvent(false));

            Engine buildEngine = new Engine();
            buildEngine.LoggingServices = inProcLoggingServicesEventsAllEvents;

            // Create a logger that points at the private engine service (we'll use that logger as the central logger)
            ConfigurableForwardingLogger localLogger = new ConfigurableForwardingLogger();
            EventRedirector newRedirector = new EventRedirector(EngineLoggingServicesInProc.FIRST_AVAILABLE_LOGGERID, inProcLoggingServicesEventsAllEventsPrivate);
            localLogger.BuildEventRedirector = newRedirector;
            localLogger.Parameters = "TARGETSTARTEDEVENT;TARGETFINISHEDEVENT";
            inProcLoggingServicesEventsAllEventsPrivate.RegisterEventSource(EngineLoggingServicesInProc.FIRST_AVAILABLE_LOGGERID, eventSourcePrivateHelper.sourceForEvents);

            string filename = null;
            Assembly assembly = Assembly.Load("MICROSOFT.BUILD.ENGINE");
            filename = assembly.Location;
            Console.WriteLine("Using the following engine assembly: " + filename);

            LoggerDescription description = new LoggerDescription("ConfigurableForwardingLogger", null, filename, "TARGETSTARTEDEVENT;TARGETFINISHEDEVENT", LoggerVerbosity.Normal);

            buildEngine.RegisterDistributedLogger(localLogger, description);

            // Verify that BuildStarted event is delivered both to the forwarding logger and ILoggers and
            // that the forwarding logger forwards it to the central logger
            inProcLoggingServicesEventsAllEvents.PostLoggingEvent(new TargetStartedEventArgs("message", "help", "targetName", "ProjectFile", "targetFile"));
            inProcLoggingServicesEventsAllEvents.ProcessPostedLoggingEvents();
            eventSourceHelper.AssertEventsAndNoOthers("anyEventRaised", "targetStarted", "statusRaised");
            inProcLoggingServicesEventsAllEvents.ProcessPostedLoggingEvents();
            inProcLoggingServicesEventsAllEventsPrivate.ProcessPostedLoggingEvents();
            eventSourcePrivateHelper.AssertEventsAndNoOthers("anyEventRaised", "targetStarted", "statusRaised");
            eventSourcePrivateHelper.ClearEvents();
            eventSourceHelper.ClearEvents();


            // Verify that BuildFinished event is delivered both to the forwarding logger and ILoggers and
            // that the forwarding logger forwards it to the central logger
            inProcLoggingServicesEventsAllEvents.PostLoggingEvent(
                    new TargetFinishedEventArgs("message", "help", "targetName", "ProjectFile", "targetFile", true));
            inProcLoggingServicesEventsAllEvents.ProcessPostedLoggingEvents();
            eventSourceHelper.AssertEventsAndNoOthers("anyEventRaised", "targetFinished", "statusRaised");
            inProcLoggingServicesEventsAllEvents.ProcessPostedLoggingEvents();
            inProcLoggingServicesEventsAllEventsPrivate.ProcessPostedLoggingEvents();
            eventSourcePrivateHelper.AssertEventsAndNoOthers("anyEventRaised", "targetFinished", "statusRaised");
            eventSourcePrivateHelper.ClearEvents();
            eventSourceHelper.ClearEvents();

            // Verify that events that are not forwarded are not delivered to the central logger
            inProcLoggingServicesEventsAllEvents.PostLoggingEvent(new BuildMessageEventArgs("Message", "help", "sender", MessageImportance.Low));
            inProcLoggingServicesEventsAllEvents.ProcessPostedLoggingEvents();
            eventSourceHelper.AssertEventsAndNoOthers("anyEventRaised", "messageRaised");
            inProcLoggingServicesEventsAllEvents.ProcessPostedLoggingEvents();
            inProcLoggingServicesEventsAllEventsPrivate.ProcessPostedLoggingEvents();
            eventSourcePrivateHelper.AssertEventsAndNoOthers();
            eventSourcePrivateHelper.ClearEvents();
            eventSourceHelper.ClearEvents();

            // Verify that external events with no logger id are not delivered to the forwarding or central loggers
            inProcLoggingServicesEventsAllEvents.PostLoggingEvent(new NodeLoggingEvent(new BuildMessageEventArgs("Message", "help", "sender", MessageImportance.Low)));
            inProcLoggingServicesEventsAllEvents.ProcessPostedLoggingEvents();
            eventSourceHelper.AssertEventsAndNoOthers("anyEventRaised", "messageRaised");
            inProcLoggingServicesEventsAllEvents.ProcessPostedLoggingEvents();
            inProcLoggingServicesEventsAllEventsPrivate.ProcessPostedLoggingEvents();
            eventSourcePrivateHelper.AssertEventsAndNoOthers();
            eventSourcePrivateHelper.ClearEvents();
            eventSourceHelper.ClearEvents();

            // Verify that external events with logger id are only delivered to central logger
            inProcLoggingServicesEventsAllEvents.PostLoggingEvent
                (new NodeLoggingEventWithLoggerId(
                        new TargetFinishedEventArgs("message", "help", "targetName", "ProjectFile", "targetFile", true), 2));
            inProcLoggingServicesEventsAllEvents.ProcessPostedLoggingEvents();
            eventSourceHelper.AssertEventsAndNoOthers();
            inProcLoggingServicesEventsAllEvents.ProcessPostedLoggingEvents();
            inProcLoggingServicesEventsAllEventsPrivate.ProcessPostedLoggingEvents();
            eventSourcePrivateHelper.AssertEventsAndNoOthers("anyEventRaised", "targetFinished", "statusRaised");
            eventSourcePrivateHelper.ClearEvents();
            eventSourceHelper.ClearEvents();
        }

        #endregion

        #region OutProcLoggingTest
       
        /// <summary>
        /// Test logging the out of proc logger by sending events to the logger and check 
        /// the inproc logger queue which is the eventual handler of the events
        /// </summary>
        [Test]
        public void OutProcLoggingTest()
        {

            VerifyEventSourceHelper eventSourceHelper = new VerifyEventSourceHelper();
            EngineLoggingServicesInProc inProcLoggingServicesEventsAllEvents = new EngineLoggingServicesInProc(eventSourceHelper.sourceForEvents, false, new ManualResetEvent(false));
            

            Engine buildEngine = new Engine();
            buildEngine.LoggingServices = inProcLoggingServicesEventsAllEvents;
            
            EngineCallback outProcessorProxy = new EngineCallback(buildEngine);
            int nodeId = buildEngine.GetNextNodeId();
            Node parentNode = new Node(nodeId, new LoggerDescription[0], outProcessorProxy, null, 
                ToolsetDefinitionLocations.ConfigurationFile | ToolsetDefinitionLocations.Registry, String.Empty);


            EngineLoggingServicesOutProc loggingServicesOutProc = new EngineLoggingServicesOutProc(parentNode, new ManualResetEvent(false));

            loggingServicesOutProc.PostLoggingEvent(new BuildMessageEventArgs("Message", "help", "sender", MessageImportance.Low));
            loggingServicesOutProc.ProcessPostedLoggingEvents();
            inProcLoggingServicesEventsAllEvents.ProcessPostedLoggingEvents();
            eventSourceHelper.AssertEventsAndNoOthers("anyEventRaised", "messageRaised");
            eventSourceHelper.ClearEvents();

            loggingServicesOutProc.PostLoggingEvent(new TaskStartedEventArgs("message", "help", "projectFile", "taskFile", "taskName"));
            loggingServicesOutProc.ProcessPostedLoggingEvents();
            inProcLoggingServicesEventsAllEvents.ProcessPostedLoggingEvents();
            eventSourceHelper.AssertEventsAndNoOthers("anyEventRaised", "taskStarted", "statusRaised");
            eventSourceHelper.ClearEvents();

            loggingServicesOutProc.PostLoggingEvent(new TaskFinishedEventArgs("message", "help", "projectFile", "taskFile", "taskName", true));
            loggingServicesOutProc.ProcessPostedLoggingEvents();
            inProcLoggingServicesEventsAllEvents.ProcessPostedLoggingEvents();
            eventSourceHelper.AssertEventsAndNoOthers("anyEventRaised", "taskFinished", "statusRaised");
            eventSourceHelper.ClearEvents();

            loggingServicesOutProc.PostLoggingEvent(new TaskCommandLineEventArgs("commandLine", "taskName", MessageImportance.Low));
            loggingServicesOutProc.ProcessPostedLoggingEvents();
            inProcLoggingServicesEventsAllEvents.ProcessPostedLoggingEvents();
            eventSourceHelper.AssertEventsAndNoOthers("anyEventRaised", "messageRaised");
            eventSourceHelper.ClearEvents();

            loggingServicesOutProc.PostLoggingEvent(new BuildWarningEventArgs("SubCategoryForSchemaValidationErrors", "MSB4000", "file", 1, 2, 3, 4, "message", "help", "sender"));
            loggingServicesOutProc.ProcessPostedLoggingEvents();
            inProcLoggingServicesEventsAllEvents.ProcessPostedLoggingEvents();
            eventSourceHelper.AssertEventsAndNoOthers("anyEventRaised", "warningRaised");
            eventSourceHelper.ClearEvents();

            loggingServicesOutProc.PostLoggingEvent(new BuildErrorEventArgs("SubCategoryForSchemaValidationErrors", "MSB4000", "file", 1, 2, 3, 4, "message", "help", "sender"));
            loggingServicesOutProc.ProcessPostedLoggingEvents();
            inProcLoggingServicesEventsAllEvents.ProcessPostedLoggingEvents();
            eventSourceHelper.AssertEventsAndNoOthers("anyEventRaised", "errorRaised");
            eventSourceHelper.ClearEvents();

            loggingServicesOutProc.PostLoggingEvent(new TargetStartedEventArgs("message", "help", "targetName", "ProjectFile", "targetFile"));
            loggingServicesOutProc.ProcessPostedLoggingEvents();
            inProcLoggingServicesEventsAllEvents.ProcessPostedLoggingEvents();
            eventSourceHelper.AssertEventsAndNoOthers("anyEventRaised", "targetStarted", "statusRaised");
            eventSourceHelper.ClearEvents();

            loggingServicesOutProc.PostLoggingEvent(new TargetFinishedEventArgs("message", "help", "targetName", "ProjectFile", "targetFile", true));
            loggingServicesOutProc.ProcessPostedLoggingEvents();
            inProcLoggingServicesEventsAllEvents.ProcessPostedLoggingEvents();
            eventSourceHelper.AssertEventsAndNoOthers("anyEventRaised", "targetFinished", "statusRaised");
            eventSourceHelper.ClearEvents();

            loggingServicesOutProc.PostLoggingEvent(new ProjectStartedEventArgs(-1, "message", "help", "ProjectFile", "targetNames", null, null, null));
            loggingServicesOutProc.ProcessPostedLoggingEvents();
            inProcLoggingServicesEventsAllEvents.ProcessPostedLoggingEvents();
            eventSourceHelper.AssertEventsAndNoOthers("anyEventRaised", "projectStarted", "statusRaised");
            eventSourceHelper.ClearEvents();

            loggingServicesOutProc.PostLoggingEvent(new ProjectFinishedEventArgs("message", "help", "ProjectFile", true));
            loggingServicesOutProc.ProcessPostedLoggingEvents();
            inProcLoggingServicesEventsAllEvents.ProcessPostedLoggingEvents();
            eventSourceHelper.AssertEventsAndNoOthers("anyEventRaised", "projectFinished", "statusRaised");
            eventSourceHelper.ClearEvents();

            loggingServicesOutProc.PostLoggingEvent(new BuildStartedEventArgs("message", "help"));
            loggingServicesOutProc.ProcessPostedLoggingEvents();
            inProcLoggingServicesEventsAllEvents.ProcessPostedLoggingEvents();
            eventSourceHelper.AssertEventsAndNoOthers("anyEventRaised", "buildStarted", "statusRaised");
            eventSourceHelper.ClearEvents();

            loggingServicesOutProc.PostLoggingEvent(new BuildFinishedEventArgs("message", "help", true));
            loggingServicesOutProc.ProcessPostedLoggingEvents();
            inProcLoggingServicesEventsAllEvents.ProcessPostedLoggingEvents();
            eventSourceHelper.AssertEventsAndNoOthers("anyEventRaised", "buildFinished", "statusRaised");
            eventSourceHelper.ClearEvents();

            loggingServicesOutProc.PostLoggingEvent(new ExternalProjectStartedEventArgs("message", "help", "senderName", "projectFile", "targetNames"));
            loggingServicesOutProc.ProcessPostedLoggingEvents();
            inProcLoggingServicesEventsAllEvents.ProcessPostedLoggingEvents();
            eventSourceHelper.AssertEventsAndNoOthers("anyEventRaised", "customEventRaised");
            eventSourceHelper.ClearEvents();

            loggingServicesOutProc.PostLoggingEvent(new ExternalProjectFinishedEventArgs("message", "help", "senderName", "projectFile", true));
            loggingServicesOutProc.ProcessPostedLoggingEvents();
            inProcLoggingServicesEventsAllEvents.ProcessPostedLoggingEvents();
            eventSourceHelper.AssertEventsAndNoOthers("anyEventRaised", "customEventRaised");
            eventSourceHelper.ClearEvents();

            loggingServicesOutProc.PostLoggingEvent(new MyCustomBuildEventArgs());
            loggingServicesOutProc.ProcessPostedLoggingEvents();
            inProcLoggingServicesEventsAllEvents.ProcessPostedLoggingEvents();
            eventSourceHelper.AssertEventsAndNoOthers("anyEventRaised", "customEventRaised");
            eventSourceHelper.ClearEvents();

            loggingServicesOutProc.PostLoggingEvent(new MyCustomBuildErrorEventArgs());
            loggingServicesOutProc.ProcessPostedLoggingEvents();
            inProcLoggingServicesEventsAllEvents.ProcessPostedLoggingEvents();
            eventSourceHelper.AssertEventsAndNoOthers("anyEventRaised", "errorRaised");
            eventSourceHelper.ClearEvents();

            loggingServicesOutProc.PostLoggingEvent(new MyCustomBuildWarningEventArgs());
            loggingServicesOutProc.ProcessPostedLoggingEvents();
            inProcLoggingServicesEventsAllEvents.ProcessPostedLoggingEvents();
            eventSourceHelper.AssertEventsAndNoOthers("anyEventRaised", "warningRaised");
            eventSourceHelper.ClearEvents();

            loggingServicesOutProc.PostLoggingEvent(new MyCustomBuildMessageEventArgs());
            loggingServicesOutProc.ProcessPostedLoggingEvents();
            inProcLoggingServicesEventsAllEvents.ProcessPostedLoggingEvents();
            eventSourceHelper.AssertEventsAndNoOthers("anyEventRaised", "messageRaised");
            eventSourceHelper.ClearEvents();

            loggingServicesOutProc.PostLoggingEvent(new MyCustomBuildMessageEventArgs());
            loggingServicesOutProc.PostLoggingEvent(new MyCustomBuildWarningEventArgs());
            loggingServicesOutProc.PostLoggingEvent(new MyCustomBuildErrorEventArgs());
            loggingServicesOutProc.ProcessPostedLoggingEvents();
            inProcLoggingServicesEventsAllEvents.ProcessPostedLoggingEvents();
            eventSourceHelper.AssertEventsAndNoOthers("anyEventRaised", "messageRaised", "warningRaised", "errorRaised");
            eventSourceHelper.ClearEvents();

            // Check that node logging events are forwarded correctly with Id
            loggingServicesOutProc.PostLoggingEvent(new NodeLoggingEventWithLoggerId(new BuildMessageEventArgs("Message", "help", "sender", MessageImportance.Low), 0));
            loggingServicesOutProc.ProcessPostedLoggingEvents();
            inProcLoggingServicesEventsAllEvents.ProcessPostedLoggingEvents();
            eventSourceHelper.AssertEventsAndNoOthers("anyEventRaised", "messageRaised");
            eventSourceHelper.ClearEvents();

            // Check that node logging events are forwarded correctly with no Id
            loggingServicesOutProc.PostLoggingEvent(new NodeLoggingEvent(new BuildMessageEventArgs("Message", "help", "sender", MessageImportance.Low)));
            loggingServicesOutProc.ProcessPostedLoggingEvents();
            inProcLoggingServicesEventsAllEvents.ProcessPostedLoggingEvents();
            eventSourceHelper.AssertEventsAndNoOthers("anyEventRaised", "messageRaised");
            eventSourceHelper.ClearEvents();

            // Register another event source and test that events are delivered correctly
            VerifyEventSourceHelper privateEventSourceHelper1 = new VerifyEventSourceHelper();
            VerifyEventSourceHelper privateEventSourceHelper2 = new VerifyEventSourceHelper();
            inProcLoggingServicesEventsAllEvents.RegisterEventSource(EngineLoggingServicesInProc.FIRST_AVAILABLE_LOGGERID, privateEventSourceHelper1.sourceForEvents);
            inProcLoggingServicesEventsAllEvents.RegisterEventSource(EngineLoggingServicesInProc.FIRST_AVAILABLE_LOGGERID + 1, privateEventSourceHelper2.sourceForEvents);

            // Check that node logging events are forwarded correctly with Id
            loggingServicesOutProc.PostLoggingEvent(new NodeLoggingEventWithLoggerId(new BuildMessageEventArgs("Message", "help", "sender", MessageImportance.Low), 0));
            loggingServicesOutProc.ProcessPostedLoggingEvents();
            inProcLoggingServicesEventsAllEvents.ProcessPostedLoggingEvents();
            eventSourceHelper.AssertEventsAndNoOthers("anyEventRaised", "messageRaised");
            eventSourceHelper.ClearEvents();
            
            //send a lot of events to test the event batching
            for (int i = 0; i < 600; i++)
            {
                loggingServicesOutProc.PostLoggingEvent(new NodeLoggingEventWithLoggerId(new BuildMessageEventArgs("Message", "help", "sender", MessageImportance.Low), 0));
            }
            loggingServicesOutProc.ProcessPostedLoggingEvents();
            inProcLoggingServicesEventsAllEvents.ProcessPostedLoggingEvents();
            eventSourceHelper.AssertEventsAndNoOthers("anyEventRaised", "messageRaised");
            eventSourceHelper.ClearEvents();

            // Check that the events are correctly sorted when posted with different logger ids
            loggingServicesOutProc.PostLoggingEvent(new BuildMessageEventArgs("Message", "help", "sender", MessageImportance.Low));
            loggingServicesOutProc.PostLoggingEvent(new NodeLoggingEventWithLoggerId(new BuildStartedEventArgs("message", "help"), EngineLoggingServicesInProc.FIRST_AVAILABLE_LOGGERID));
            loggingServicesOutProc.PostLoggingEvent(new NodeLoggingEventWithLoggerId(new TargetFinishedEventArgs("message", "help", "targetName", "ProjectFile", "targetFile", true), 
                                                                                     EngineLoggingServicesInProc.FIRST_AVAILABLE_LOGGERID +1));
            loggingServicesOutProc.ProcessPostedLoggingEvents();
            inProcLoggingServicesEventsAllEvents.ProcessPostedLoggingEvents();
            privateEventSourceHelper1.AssertEventsAndNoOthers("anyEventRaised", "statusRaised", "buildStarted");
            privateEventSourceHelper2.AssertEventsAndNoOthers("anyEventRaised", "statusRaised", "targetFinished");
            eventSourceHelper.AssertEventsAndNoOthers("anyEventRaised", "messageRaised" );
            eventSourceHelper.ClearEvents();

        }

        #endregion
    }
}
