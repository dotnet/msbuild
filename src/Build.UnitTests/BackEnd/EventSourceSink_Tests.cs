// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using Microsoft.Build.Framework;
using Microsoft.Build.BackEnd.Logging;
using Microsoft.Build.Shared;

using InternalLoggerException = Microsoft.Build.Exceptions.InternalLoggerException;
using Xunit;

namespace Microsoft.Build.UnitTests.Logging
{
    /// <summary>
    /// Verify the event source sink functions correctly.
    /// </summary>
    public class EventSourceSink_Tests
    {
        /// <summary>
        /// Verify the properties on EventSourceSink properly work
        /// </summary>
        [Fact]
        public void PropertyTests()
        {
            EventSourceSink sink = new EventSourceSink();
            Assert.Null(sink.Name);
            string name = "Test Name";
            sink.Name = name;
            Assert.Equal(0, string.Compare(sink.Name, name, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Test out events
        /// </summary>
        [Fact]
        public void ConsumeEventsGoodEvents()
        {
            EventSourceSink sink = new EventSourceSink();
            RaiseEventHelper eventHelper = new RaiseEventHelper(sink);
            EventHandlerHelper testHandlers = new EventHandlerHelper(sink, null);
            VerifyRegisteredHandlers(RaiseEventHelper.BuildStarted, eventHelper, testHandlers);
            VerifyRegisteredHandlers(RaiseEventHelper.BuildFinished, eventHelper, testHandlers);
            VerifyRegisteredHandlers(RaiseEventHelper.NormalMessage, eventHelper, testHandlers);
            VerifyRegisteredHandlers(RaiseEventHelper.TaskFinished, eventHelper, testHandlers);
            VerifyRegisteredHandlers(RaiseEventHelper.CommandLine, eventHelper, testHandlers);
            VerifyRegisteredHandlers(RaiseEventHelper.Warning, eventHelper, testHandlers);
            VerifyRegisteredHandlers(RaiseEventHelper.Error, eventHelper, testHandlers);
            VerifyRegisteredHandlers(RaiseEventHelper.TargetStarted, eventHelper, testHandlers);
            VerifyRegisteredHandlers(RaiseEventHelper.TargetFinished, eventHelper, testHandlers);
            VerifyRegisteredHandlers(RaiseEventHelper.ProjectStarted, eventHelper, testHandlers);
            VerifyRegisteredHandlers(RaiseEventHelper.ProjectFinished, eventHelper, testHandlers);
            VerifyRegisteredHandlers(RaiseEventHelper.ExternalStartedEvent, eventHelper, testHandlers);
            VerifyRegisteredHandlers(RaiseEventHelper.BuildStarted, eventHelper, testHandlers);
            VerifyRegisteredHandlers(RaiseEventHelper.GenericStatusEvent, eventHelper, testHandlers);
        }

        /// <summary>
        /// Test out events when no event handlers are registered
        /// </summary>
        [Fact]
        public void ConsumeEventsGoodEventsNoHandlers()
        {
            EventSourceSink sink = new EventSourceSink();
            RaiseEventHelper eventHelper = new RaiseEventHelper(sink);
            eventHelper.RaiseBuildEvent(RaiseEventHelper.BuildStarted);
            eventHelper.RaiseBuildEvent(RaiseEventHelper.BuildFinished);
            eventHelper.RaiseBuildEvent(RaiseEventHelper.NormalMessage);
            eventHelper.RaiseBuildEvent(RaiseEventHelper.TaskFinished);
            eventHelper.RaiseBuildEvent(RaiseEventHelper.CommandLine);
            eventHelper.RaiseBuildEvent(RaiseEventHelper.TaskParameter);
            eventHelper.RaiseBuildEvent(RaiseEventHelper.Warning);
            eventHelper.RaiseBuildEvent(RaiseEventHelper.Error);
            eventHelper.RaiseBuildEvent(RaiseEventHelper.TargetStarted);
            eventHelper.RaiseBuildEvent(RaiseEventHelper.TargetFinished);
            eventHelper.RaiseBuildEvent(RaiseEventHelper.ProjectStarted);
            eventHelper.RaiseBuildEvent(RaiseEventHelper.ProjectFinished);
            eventHelper.RaiseBuildEvent(RaiseEventHelper.ExternalStartedEvent);
            eventHelper.RaiseBuildEvent(RaiseEventHelper.ExternalStartedEvent);
            eventHelper.RaiseBuildEvent(RaiseEventHelper.GenericStatusEvent);
        }

        #region TestsThrowingLoggingExceptions

        /// <summary>
        /// Verify when exceptions are thrown in the event handler, they are properly handled
        /// </summary>
        [Fact]
        public void LoggerExceptionInEventHandler()
        {
            List<Exception> exceptionList = new List<Exception>();
            exceptionList.Add(new LoggerException());
            exceptionList.Add(new ArgumentException());
            exceptionList.Add(new StackOverflowException());

            foreach (Exception exception in exceptionList)
            {
                RaiseExceptionInEventHandler(RaiseEventHelper.BuildStarted, exception);
                RaiseExceptionInEventHandler(RaiseEventHelper.BuildFinished, exception);
                RaiseExceptionInEventHandler(RaiseEventHelper.NormalMessage, exception);
                RaiseExceptionInEventHandler(RaiseEventHelper.TaskFinished, exception);
                RaiseExceptionInEventHandler(RaiseEventHelper.CommandLine, exception);
                RaiseExceptionInEventHandler(RaiseEventHelper.TaskParameter, exception);
                RaiseExceptionInEventHandler(RaiseEventHelper.Warning, exception);
                RaiseExceptionInEventHandler(RaiseEventHelper.Error, exception);
                RaiseExceptionInEventHandler(RaiseEventHelper.TargetStarted, exception);
                RaiseExceptionInEventHandler(RaiseEventHelper.TargetFinished, exception);
                RaiseExceptionInEventHandler(RaiseEventHelper.ProjectStarted, exception);
                RaiseExceptionInEventHandler(RaiseEventHelper.ProjectFinished, exception);
                RaiseExceptionInEventHandler(RaiseEventHelper.ExternalStartedEvent, exception);
                RaiseExceptionInEventHandler(RaiseEventHelper.GenericStatusEvent, exception);
            }
        }

        /// <summary>
        /// Verify raising a generic event derived from BuildEventArgs rather than CustomBuildEventArgs causes an internalErrorException
        /// </summary>
        [Fact]
        public void RaiseGenericBuildEventArgs()
        {
            Assert.Throws<InternalErrorException>(() =>
            {
                EventSourceSink sink = new EventSourceSink();
                RaiseEventHelper eventHelper = new RaiseEventHelper(sink);
                eventHelper.RaiseBuildEvent(RaiseEventHelper.GenericBuildEvent);
            }
           );
        }
        /// <summary>
        /// Verify that shutdown un registers all of the event handlers
        /// </summary>
        [Fact]
        public void VerifyShutdown()
        {
            EventSourceSink sink = new EventSourceSink();

            // Registers event handlers onto the event source
            EventHandlerHelper handlerHelper = new EventHandlerHelper(sink, null);
            RaiseEventHelper raiseEventHelper = new RaiseEventHelper(sink);

            raiseEventHelper.RaiseBuildEvent(RaiseEventHelper.ProjectStarted);
            Assert.True(handlerHelper.EnteredEventHandler);
            Assert.True(handlerHelper.EnteredAnyEventHandler);
            Assert.True(handlerHelper.EnteredStatusEventHandler);
            Assert.Equal(handlerHelper.RaisedEvent, RaiseEventHelper.ProjectStarted);
            Assert.Equal(handlerHelper.RaisedAnyEvent, RaiseEventHelper.ProjectStarted);
            Assert.Equal(handlerHelper.RaisedStatusEvent, RaiseEventHelper.ProjectStarted);

            sink.ShutDown();

            handlerHelper.ResetRaisedEvent();
            raiseEventHelper.RaiseBuildEvent(RaiseEventHelper.ProjectStarted);
            Assert.False(handlerHelper.EnteredEventHandler);
            Assert.False(handlerHelper.EnteredAnyEventHandler);
            Assert.False(handlerHelper.EnteredStatusEventHandler);
            Assert.Null(handlerHelper.RaisedEvent);
            Assert.Null(handlerHelper.RaisedAnyEvent);
            Assert.Null(handlerHelper.RaisedStatusEvent);
        }

        /// <summary>
        /// Verify aggregate exceptions are caught as critical if they contain critical exceptions
        /// </summary>
        [Fact]
        public void VerifyAggregateExceptionHandling()
        {
            try
            {
                // A simple non-critical exception
                throw new Exception();
            }
            catch (Exception e)
            {
                Assert.False(ExceptionHandling.IsCriticalException(e));
            }

            try
            {
                // An empty aggregate exception - non-critical
                throw new AggregateException();
            }
            catch (Exception e)
            {
                Assert.False(ExceptionHandling.IsCriticalException(e));
            }

            try
            {
                // Aggregate exception containing two non-critical exceptions - non-critical
                Exception[] exceptionArray = new Exception[] { new IndexOutOfRangeException(), new Exception() }; //// two non-critical exceptions
                throw new AggregateException(exceptionArray);
            }
            catch (Exception e)
            {
                Assert.False(ExceptionHandling.IsCriticalException(e));
            }

            try
            {
                // Nested aggregate exception containing non-critical exceptions - non-critical
                Exception[] exceptionArray1 = new Exception[] { new IndexOutOfRangeException(), new Exception() }; //// two non-critical exceptions
                AggregateException ae1 = new AggregateException(exceptionArray1);

                Exception[] exceptionArray2 = new Exception[] { ae1, new Exception() }; //// two non-critical exceptions (ae1 contains nested exceptions)
                throw new AggregateException(exceptionArray2);
            }
            catch (Exception e)
            {
                Assert.False(ExceptionHandling.IsCriticalException(e));
            }

            try
            {
                // A simple critical exception
                throw new OutOfMemoryException();
            }
            catch (Exception e)
            {
                Assert.True(ExceptionHandling.IsCriticalException(e));
            }

            try
            {
                // An aggregate exception containing one critical exception - critical
                Exception[] exceptionArray = new Exception[] { new OutOfMemoryException(), new IndexOutOfRangeException(), new Exception() }; //// two non-critical exceptions, one critical
                throw new AggregateException(exceptionArray);
            }
            catch (Exception e)
            {
                Assert.True(ExceptionHandling.IsCriticalException(e));
            }

            try
            {
                // Nested aggregate exception containing non-critical exceptions - non-critical
                Exception[] exceptionArray1 = new Exception[] { new OutOfMemoryException(), new IndexOutOfRangeException(), new Exception() }; //// two non-critical exceptions, one critical
                AggregateException ae1 = new AggregateException(exceptionArray1);

                Exception[] exceptionArray2 = new Exception[] { ae1, new Exception() }; //// one critical one non-critical (ae1 contains nested critical exception)
                throw new AggregateException(exceptionArray2);
            }
            catch (Exception e)
            {
                Assert.True(ExceptionHandling.IsCriticalException(e));
            }
        }

        #region Private methods
        /// <summary>
        /// Take an event and an exception to raise, create a new sink and raise the event on it.
        /// In the event handler registered on the sink, the exception will be thrown.
        /// </summary>
        /// <param name="buildEventToRaise">BuildEvent to raise on the </param>
        /// <param name="exceptionToRaise">Exception to throw in the event handler </param>
        private static void RaiseExceptionInEventHandler(BuildEventArgs buildEventToRaise, Exception exceptionToRaise)
        {
            EventSourceSink sink = new EventSourceSink();
            RaiseEventHelper eventHelper = new RaiseEventHelper(sink);
            EventHandlerHelper testHandlers = new EventHandlerHelper(sink, exceptionToRaise);
            try
            {
                eventHelper.RaiseBuildEvent(buildEventToRaise);
            }
            catch (Exception e)
            {
                // Logger exceptions should be rethrown as is with no wrapping
                if (exceptionToRaise is LoggerException)
                {
                    Assert.Equal(e, exceptionToRaise); // "Expected Logger exception to be raised in event handler and re-thrown by event source"
                }
                else
                {
                    if (ExceptionHandling.IsCriticalException(e))
                    {
                        Assert.Equal(e, exceptionToRaise); // "Expected Logger exception to be raised in event handler and re-thrown by event source"
                    }
                    else
                    {
                        // All other exceptions should be wrapped in an InternalLoggerException, with the original exception as the inner exception
                        Assert.True(e is InternalLoggerException); // "Expected general exception to be raised in event handler and re-thrown by event source as a InternalLoggerException"
                    }
                }
            }
        }

        /// <summary>
        /// Verify when an is raised the handlers which are  registered to handle the event should handle them
        /// </summary>
        /// <param name="buildEventToRaise">A buildEventArgs to raise on the event source</param>
        /// <param name="eventHelper">Helper class which events are raised on</param>
        /// <param name="testHandlers">Class which contains a set of event handlers registered on the event source</param>
        private static void VerifyRegisteredHandlers(BuildEventArgs buildEventToRaise, RaiseEventHelper eventHelper, EventHandlerHelper testHandlers)
        {
            try
            {
                eventHelper.RaiseBuildEvent(buildEventToRaise);
                if (buildEventToRaise.GetType() != typeof(GenericBuildStatusEventArgs))
                {
                    Assert.Equal(testHandlers.RaisedEvent, buildEventToRaise); // "Expected buildevent in handler to match buildevent raised on event source"
                    Assert.Equal(testHandlers.RaisedEvent, testHandlers.RaisedAnyEvent); // "Expected RaisedEvent and RaisedAnyEvent to match"
                    Assert.True(testHandlers.EnteredEventHandler); // "Expected to enter into event handler"
                }

                Assert.Equal(testHandlers.RaisedAnyEvent, buildEventToRaise); // "Expected buildEvent in any event handler to match buildevent raised on event source"
                Assert.True(testHandlers.EnteredAnyEventHandler); // "Expected  to enter into AnyEvent handler"

                if (buildEventToRaise is BuildStatusEventArgs)
                {
                    Assert.Equal(testHandlers.RaisedStatusEvent, buildEventToRaise); // "Expected buildevent in handler to match buildevent raised on event source"
                    Assert.True(testHandlers.EnteredStatusEventHandler); // "Expected to enter into Status event handler"
                }
                else
                {
                    Assert.Null(testHandlers.RaisedStatusEvent);
                    Assert.False(testHandlers.EnteredStatusEventHandler);
                }
            }
            finally
            {
                testHandlers.ResetRaisedEvent();
            }
        }

        #endregion

        #region HelperClasses

        /// <summary>
        /// Generic class derived from BuildEventArgs which is used to test the case
        /// where the event is not a well known event, or a custom event
        /// </summary>
        internal class GenericBuildEventArgs : BuildEventArgs
        {
            /// <summary>
            /// Default constructor
            /// </summary>
            internal GenericBuildEventArgs()
                : base()
            {
            }
        }

        /// <summary>
        /// Generic class derived from BuildStatusEvent which is used to test the case
        /// where a status event is raised but it is not a well known status event (build started ...)
        /// </summary>
        internal class GenericBuildStatusEventArgs : BuildStatusEventArgs
        {
            /// <summary>
            /// Default constructor
            /// </summary>
            internal GenericBuildStatusEventArgs()
                : base()
            {
            }
        }

        /// <summary>
        /// Create a test class which will register to the event source and have event handlers
        /// which can act normally or throw exceptions.
        /// </summary>
        internal class EventHandlerHelper
        {
            #region Data
            /// <summary>
            /// When an event handler raises an event it will
            /// set this to the event which was raised
            /// This can then be asserted upon to verify the event
            /// which was raised on the sink was the one received
            /// by the event handler
            /// </summary>
            private BuildEventArgs _raisedEvent;

            /// <summary>
            /// The any event handler will get all events, even if they are raised to another event handler
            /// We need to verify that both the event handler and the any event handler both get the events
            /// </summary>
            private BuildEventArgs _raisedAnyEvent;

            /// <summary>
            /// A status event message, this is set when status events are raised on the event handler
            /// </summary>
            private BuildEventArgs _raisedStatusEvent;

            /// <summary>
            /// To test the exception mechanism of the event source, we may want to
            /// throw certain exceptions in the event handlers. This can be null if
            /// no exception is to be thrown.
            /// </summary>
            private Exception _exceptionInHandlers;

            /// <summary>
            /// Was the event handler entered into, this tells us whether or not the event
            /// was actually raised
            /// </summary>
            private bool _enteredEventHandler;

            /// <summary>
            /// The any event handler will get all events, even if they are raised to another event handler
            /// We need to verify that both the event handler and the any event handler both get the events
            /// </summary>
            private bool _enteredAnyEventHandler;

            /// <summary>
            /// Events such as BuildStarted, ProjectStarted/Finished, ... are status events.
            /// In addition to being raised on their own events, they are also raised on the status event and any event.
            /// </summary>
            private bool _enteredStatusEventHandler;
            #endregion

            #region Constructors
            /// <summary>
            /// Default Constructor, registered event handlers for all the well know event types on the passed in event source
            /// </summary>
            /// <param name="source">Event source to register to for events</param>
            /// <param name="exceptionToThrow">What exception should be thrown from the event handler, this can be null</param>
            internal EventHandlerHelper(IEventSource source, Exception exceptionToThrow)
            {
                _exceptionInHandlers = exceptionToThrow;
                source.AnyEventRaised += Source_AnyEventRaised;
                source.BuildFinished += Source_BuildFinished;
                source.BuildStarted += Source_BuildStarted;
                source.CustomEventRaised += Source_CustomEventRaised;
                source.ErrorRaised += Source_ErrorRaised;
                source.MessageRaised += Source_MessageRaised;
                source.ProjectFinished += Source_ProjectFinished;
                source.ProjectStarted += Source_ProjectStarted;
                source.StatusEventRaised += Source_StatusEventRaised;
                source.TargetFinished += Source_TargetFinished;
                source.TargetStarted += Source_TargetStarted;
                source.TaskFinished += Source_TaskFinished;
                source.TaskStarted += Source_TaskStarted;
                source.WarningRaised += Source_WarningRaised;
            }
            #endregion

            #region Properties
            /// <summary>
            /// Was an event handler entered into
            /// </summary>
            public bool EnteredEventHandler
            {
                get { return _enteredEventHandler; }
            }

            /// <summary>
            /// Was  the Any event handler
            /// </summary>
            public bool EnteredAnyEventHandler
            {
                get
                {
                    return _enteredAnyEventHandler;
                }
            }

            /// <summary>
            /// Was  the Status event handler
            /// </summary>
            public bool EnteredStatusEventHandler
            {
                get
                {
                    return _enteredStatusEventHandler;
                }
            }

            /// <summary>
            /// Which event was raised on the event source, this can be asserted upon
            /// to verify the event passed to the event source is the same one which was
            /// received by the event handlers
            /// </summary>
            public BuildEventArgs RaisedEvent
            {
                get
                {
                    return _raisedEvent;
                }
            }

            /// <summary>
            /// Check the event raised by the AnyEventHandler
            /// </summary>
            public BuildEventArgs RaisedAnyEvent
            {
                get
                {
                    return _raisedAnyEvent;
                }
            }

            /// <summary>
            /// Check the event raised by the StatusEventHandler
            /// </summary>
            public BuildEventArgs RaisedStatusEvent
            {
                get
                {
                    return _raisedStatusEvent;
                }
            }
            #endregion

            #region Public Methods

            /// <summary>
            /// Reset the per event variables so that we can raise another
            /// event and capture the information for it.
            /// </summary>
            public void ResetRaisedEvent()
            {
                _raisedEvent = null;
                _raisedAnyEvent = null;
                _raisedStatusEvent = null;
                _enteredAnyEventHandler = false;
                _enteredEventHandler = false;
                _enteredStatusEventHandler = false;
            }
            #endregion

            #region EventHandlers

            /// <summary>
            /// Do the test work for all of the event handlers.
            /// </summary>
            /// <param name="e">Event which was raised by an event source this class was listening to</param>
            private void HandleEvent(BuildEventArgs e)
            {
                _enteredEventHandler = true;
                _raisedEvent = e;

                if (_exceptionInHandlers != null)
                {
                    throw _exceptionInHandlers;
                }
            }

            /// <summary>
            /// Handle a warning event
            /// </summary>
            /// <param name="sender">Who sent the event</param>
            /// <param name="e">Event raised on the event source</param>
            private void Source_WarningRaised(object sender, BuildWarningEventArgs e)
            {
                HandleEvent(e);
            }

            /// <summary>
            /// Handle a task started event
            /// </summary>
            /// <param name="sender">Who sent the event</param>
            /// <param name="e">Event raised on the event source</param>
            private void Source_TaskStarted(object sender, TaskStartedEventArgs e)
            {
                HandleEvent(e);
            }

            /// <summary>
            /// Handle a task finished event
            /// </summary>
            /// <param name="sender">Who sent the event</param>
            /// <param name="e">Event raised on the event source</param>
            private void Source_TaskFinished(object sender, TaskFinishedEventArgs e)
            {
                HandleEvent(e);
            }

            /// <summary>
            /// Handle a target started event
            /// </summary>
            /// <param name="sender">Who sent the event</param>
            /// <param name="e">Event raised on the event source</param>
            private void Source_TargetStarted(object sender, TargetStartedEventArgs e)
            {
                HandleEvent(e);
            }

            /// <summary>
            /// Handle a target finished event
            /// </summary>
            /// <param name="sender">Who sent the event</param>
            /// <param name="e">Event raised on the event source</param>
            private void Source_TargetFinished(object sender, TargetFinishedEventArgs e)
            {
                HandleEvent(e);
            }

            /// <summary>
            /// Handle a status event
            /// </summary>
            /// <param name="sender">Who sent the event</param>
            /// <param name="e">Event raised on the event source</param>
            private void Source_StatusEventRaised(object sender, BuildStatusEventArgs e)
            {
                _enteredStatusEventHandler = true;
                _raisedStatusEvent = e;

                if (_exceptionInHandlers != null)
                {
                    throw _exceptionInHandlers;
                }
            }

            /// <summary>
            /// Handle a project started event
            /// </summary>
            /// <param name="sender">Who sent the event</param>
            /// <param name="e">Event raised on the event source</param>
            private void Source_ProjectStarted(object sender, ProjectStartedEventArgs e)
            {
                HandleEvent(e);
            }

            /// <summary>
            /// Handle a project finished event
            /// </summary>
            /// <param name="sender">Who sent the event</param>
            /// <param name="e">Event raised on the event source</param>
            private void Source_ProjectFinished(object sender, ProjectFinishedEventArgs e)
            {
                HandleEvent(e);
            }

            /// <summary>
            /// Handle a message event
            /// </summary>
            /// <param name="sender">Who sent the event</param>
            /// <param name="e">Event raised on the event source</param>
            private void Source_MessageRaised(object sender, BuildMessageEventArgs e)
            {
                HandleEvent(e);
            }

            /// <summary>
            /// Handle a error event
            /// </summary>
            /// <param name="sender">Who sent the event</param>
            /// <param name="e">Event raised on the event source</param>
            private void Source_ErrorRaised(object sender, BuildErrorEventArgs e)
            {
                HandleEvent(e);
            }

            /// <summary>
            /// Handle a custom event, these are mostly user created events
            /// </summary>
            /// <param name="sender">Who sent the event</param>
            /// <param name="e">Event raised on the event source</param>
            private void Source_CustomEventRaised(object sender, CustomBuildEventArgs e)
            {
                HandleEvent(e);
            }

            /// <summary>
            /// Handle a build started event
            /// </summary>
            /// <param name="sender">Who sent the event</param>
            /// <param name="e">Event raised on the event source</param>
            private void Source_BuildStarted(object sender, BuildStartedEventArgs e)
            {
                HandleEvent(e);
            }

            /// <summary>
            /// Handle a build finished event
            /// </summary>
            /// <param name="sender">Who sent the event</param>
            /// <param name="e">Event raised on the event source</param>
            private void Source_BuildFinished(object sender, BuildFinishedEventArgs e)
            {
                HandleEvent(e);
            }

            /// <summary>
            /// Handle a events raised from the any event source. This source will
            /// raise all events no matter the type.
            /// </summary>
            /// <param name="sender">Who sent the event</param>
            /// <param name="e">Event raised on the event source</param>
            private void Source_AnyEventRaised(object sender, BuildEventArgs e)
            {
                _enteredAnyEventHandler = true;
                _raisedAnyEvent = e;
            }
            #endregion
        }

        /// <summary>
        /// Helper for the test, this class has methods
        /// individual types of events or a set of all well known events.
        /// The Events can be raised in multiple tests. The helper class keeps the code cleaner
        /// by not having to instantiate new objects everywhere and
        /// all the fields are set in one place which makes it more maintainable
        /// </summary>
        internal class RaiseEventHelper
        {
            #region Data
            /// <summary>
            /// Build Started Event
            /// </summary>
            private static BuildStartedEventArgs s_buildStarted = new BuildStartedEventArgs("Message", "Help");

            /// <summary>
            /// Generic Build Event
            /// </summary>
            private static GenericBuildEventArgs s_genericBuild = new GenericBuildEventArgs();

            /// <summary>
            /// Generic Build Status Event
            /// </summary>
            private static GenericBuildStatusEventArgs s_genericBuildStatus = new GenericBuildStatusEventArgs();

            /// <summary>
            /// Build Finished Event
            /// </summary>
            private static BuildFinishedEventArgs s_buildFinished = new BuildFinishedEventArgs("Message", "Keyword", true);

            /// <summary>
            /// Build Message Event
            /// </summary>
            private static BuildMessageEventArgs s_buildMessage = new BuildMessageEventArgs("Message2", "help", "sender", MessageImportance.Normal);

            /// <summary>
            /// Task Started Event
            /// </summary>
            private static TaskStartedEventArgs s_taskStarted = new TaskStartedEventArgs("message", "help", "projectFile", "taskFile", "taskName");

            /// <summary>
            /// Task Finished Event
            /// </summary>
            private static TaskFinishedEventArgs s_taskFinished = new TaskFinishedEventArgs("message", "help", "projectFile", "taskFile", "taskName", true);

            /// <summary>
            /// Task Command Line Event
            /// </summary>
            private static TaskCommandLineEventArgs s_taskCommandLine = new TaskCommandLineEventArgs("commandLine", "taskName", MessageImportance.Low);

            /// <summary>
            /// Task Parameter Event
            /// </summary>
            private static TaskParameterEventArgs s_taskParameter = new TaskParameterEventArgs(TaskParameterMessageKind.TaskInput, "ItemName", null, true, DateTime.MinValue);

            /// <summary>
            /// Build Warning Event
            /// </summary>
            private static BuildWarningEventArgs s_buildWarning = new BuildWarningEventArgs("SubCategoryForSchemaValidationErrors", "MSB4000", "file", 1, 2, 3, 4, "message", "help", "sender")
            {
                BuildEventContext = new BuildEventContext(1, 2, 3, 4, 5, 6)
            };

            /// <summary>
            /// Build Error Event
            /// </summary>
            private static BuildErrorEventArgs s_buildError = new BuildErrorEventArgs("SubCategoryForSchemaValidationErrors", "MSB4000", "file", 1, 2, 3, 4, "message", "help", "sender");

            /// <summary>
            /// Target Started Event
            /// </summary>
            private static TargetStartedEventArgs s_targetStarted = new TargetStartedEventArgs("message", "help", "targetName", "ProjectFile", "targetFile");

            /// <summary>
            /// Target Finished Event
            /// </summary>
            private static TargetFinishedEventArgs s_targetFinished = new TargetFinishedEventArgs("message", "help", "targetName", "ProjectFile", "targetFile", true);

            /// <summary>
            /// Project Started Event
            /// </summary>
            private static ProjectStartedEventArgs s_projectStarted = new ProjectStartedEventArgs(-1, "message", "help", "ProjectFile", "targetNames", null, null, null);

            /// <summary>
            /// Project Finished Event
            /// </summary>
            private static ProjectFinishedEventArgs s_projectFinished = new ProjectFinishedEventArgs("message", "help", "ProjectFile", true)
            {
                BuildEventContext = s_buildWarning.BuildEventContext
            };

            /// <summary>
            /// External Project Started Event
            /// </summary>
            private static ExternalProjectStartedEventArgs s_externalProjectStarted = new ExternalProjectStartedEventArgs("message", "help", "senderName", "projectFile", "targetNames");

            /// <summary>
            /// Event source on which the events will be raised.
            /// </summary>
            private EventSourceSink _sourceForEvents;

            #endregion

            #region Constructor
            /// <summary>
            /// Constructor
            /// </summary>
            /// <param name="eventSource">Event source on which the events will be raised</param>
            internal RaiseEventHelper(EventSourceSink eventSource)
            {
                _sourceForEvents = eventSource;
            }

            #endregion

            #region Properties

            /// <summary>
            /// Event which can be raised in multiple tests.
            /// </summary>
            internal static BuildStartedEventArgs BuildStarted
            {
                get
                {
                    return s_buildStarted;
                }
            }

            /// <summary>
            /// Event which can be raised in multiple tests.
            /// </summary>
            internal static GenericBuildEventArgs GenericBuildEvent
            {
                get
                {
                    return s_genericBuild;
                }
            }

            /// <summary>
            /// Event which can be raised in multiple tests.
            /// </summary>
            internal static GenericBuildStatusEventArgs GenericStatusEvent
            {
                get
                {
                    return s_genericBuildStatus;
                }
            }

            /// <summary>
            /// Event which can be raised in multiple tests.
            /// </summary>
            internal static BuildFinishedEventArgs BuildFinished
            {
                get
                {
                    return s_buildFinished;
                }
            }

            /// <summary>
            /// Event which can be raised in multiple tests.
            /// </summary>
            internal static BuildMessageEventArgs NormalMessage
            {
                get
                {
                    return s_buildMessage;
                }
            }

            /// <summary>
            /// Event which can be raised in multiple tests.
            /// </summary>
            internal static TaskStartedEventArgs TaskStarted
            {
                get
                {
                    return s_taskStarted;
                }
            }

            /// <summary>
            /// Event which can be raised in multiple tests.
            /// </summary>
            internal static TaskFinishedEventArgs TaskFinished
            {
                get
                {
                    return s_taskFinished;
                }
            }

            /// <summary>
            /// Event which can be raised in multiple tests.
            /// </summary>
            internal static TaskCommandLineEventArgs CommandLine
            {
                get
                {
                    return s_taskCommandLine;
                }
            }

            /// <summary>
            /// Event which can be raised in multiple tests.
            /// </summary>
            internal static TaskParameterEventArgs TaskParameter => s_taskParameter;

            /// <summary>
            /// Event which can be raised in multiple tests.
            /// </summary>
            internal static BuildWarningEventArgs Warning
            {
                get
                {
                    return s_buildWarning;
                }
            }

            /// <summary>
            /// Event which can be raised in multiple tests.
            /// </summary>
            internal static BuildErrorEventArgs Error
            {
                get
                {
                    return s_buildError;
                }
            }

            /// <summary>
            /// Event which can be raised in multiple tests.
            /// </summary>
            internal static TargetStartedEventArgs TargetStarted
            {
                get
                {
                    return s_targetStarted;
                }
            }

            /// <summary>
            /// Event which can be raised in multiple tests.
            /// </summary>
            internal static TargetFinishedEventArgs TargetFinished
            {
                get
                {
                    return s_targetFinished;
                }
            }

            /// <summary>
            /// Event which can be raised in multiple tests.
            /// </summary>
            internal static ProjectStartedEventArgs ProjectStarted
            {
                get
                {
                    return s_projectStarted;
                }
            }

            /// <summary>
            /// Event which can be raised in multiple tests.
            /// </summary>
            internal static ProjectFinishedEventArgs ProjectFinished
            {
                get
                {
                    return s_projectFinished;
                }
            }

            /// <summary>
            /// Event which can be raised in multiple tests.
            /// </summary>
            internal static ExternalProjectStartedEventArgs ExternalStartedEvent
            {
                get
                {
                    return s_externalProjectStarted;
                }
            }
            #endregion

            /// <summary>
            /// Raise a build event on the event source
            /// </summary>
            internal void RaiseBuildEvent(BuildEventArgs buildEvent)
            {
                _sourceForEvents.Consume(buildEvent);
                if (buildEvent is BuildStartedEventArgs)
                {
                    Assert.True(_sourceForEvents.HaveLoggedBuildStartedEvent);
                    _sourceForEvents.HaveLoggedBuildStartedEvent = false;
                    Assert.False(_sourceForEvents.HaveLoggedBuildStartedEvent);
                }
                else if (buildEvent is BuildFinishedEventArgs)
                {
                    Assert.True(_sourceForEvents.HaveLoggedBuildFinishedEvent);
                    _sourceForEvents.HaveLoggedBuildFinishedEvent = false;
                    Assert.False(_sourceForEvents.HaveLoggedBuildFinishedEvent);
                }
            }
        }
        #endregion
        #endregion
    }
}
