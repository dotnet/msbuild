// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//-----------------------------------------------------------------------
// </copyright>
// <summary>Test the logging service component</summary>
//-----------------------------------------------------------------------

using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Build.Framework;
using Microsoft.Build.BackEnd.Logging;
using Microsoft.Build.BackEnd;
using Microsoft.Build.Logging;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using Microsoft.Build.Exceptions;
using Microsoft.Build.Shared;
using Microsoft.Build.UnitTests.BackEnd;
using System.IO;
using System.Threading;
using System.Reflection;
using System.Collections.Generic;

namespace Microsoft.Build.UnitTests.Logging
{
    /// <summary>
    /// Test the logging service component
    /// </summary>
    [TestClass]
    public class LoggingService_Tests
    {
        #region Data
        /// <summary>
        /// An already instantiated and initialized service. 
        /// This is used so the host object does not need to be
        /// used in every test method.
        /// </summary>
        private LoggingService _initializedService;

        /// <summary>
        /// The event signalled when shutdown is complete.
        /// </summary>
        private ManualResetEvent _shutdownComplete = new ManualResetEvent(false);

        #endregion

        #region Setup

        /// <summary>
        /// This method is run before each test case is run.
        /// We instantiate and initialize a new logging service each time
        /// </summary>
        [TestInitialize]
        public void Setup()
        {
            InitializeLoggingService();
        }

        #endregion

        #region Test BuildComponent Methods

        /// <summary>
        /// Verify the CreateLogger method create a LoggingService in both Synchronous mode
        /// and Asynchronous mode. 
        /// </summary>
        [TestMethod]
        public void CreateLogger()
        {
            // Generic host which has some default properties set inside of it
            IBuildComponent logServiceComponent = (IBuildComponent)LoggingService.CreateLoggingService(LoggerMode.Synchronous, 1);

            // Create a synchronous logging service and do some quick checks
            Assert.IsNotNull(logServiceComponent);
            LoggingService logService = (LoggingService)logServiceComponent;
            Assert.IsTrue(logService.LoggingMode == LoggerMode.Synchronous);
            Assert.IsTrue(logService.ServiceState == LoggingServiceState.Instantiated);

            // Create an asynchronous logging service
            logServiceComponent = (IBuildComponent)LoggingService.CreateLoggingService(LoggerMode.Asynchronous, 1);
            Assert.IsNotNull(logServiceComponent);
            logService = (LoggingService)logServiceComponent;
            Assert.IsTrue(logService.LoggingMode == LoggerMode.Asynchronous);
            Assert.IsTrue(logService.ServiceState == LoggingServiceState.Instantiated);

            // Shutdown logging thread  
            logServiceComponent.InitializeComponent(new MockHost());
            logServiceComponent.ShutdownComponent();
            Assert.IsTrue(logService.ServiceState == LoggingServiceState.Shutdown);
        }

        /// <summary>
        /// Test the IBuildComponent method InitializeComponent, make sure the component gets the parameters it expects
        /// </summary>
        [TestMethod]
        public void InitializeComponent()
        {
            IBuildComponent logServiceComponent = (IBuildComponent)LoggingService.CreateLoggingService(LoggerMode.Synchronous, 1);

            BuildParameters parameters = new BuildParameters();
            parameters.MaxNodeCount = 4;
            parameters.OnlyLogCriticalEvents = true;

            IBuildComponentHost loggingHost = new MockHost(parameters);

            // Make sure we are in the Instantiated state before initializing
            Assert.IsTrue(((LoggingService)logServiceComponent).ServiceState == LoggingServiceState.Instantiated);

            logServiceComponent.InitializeComponent(loggingHost);

            // Makesure that the parameters in the host are set in the logging service
            LoggingService service = (LoggingService)logServiceComponent;
            Assert.IsTrue(service.ServiceState == LoggingServiceState.Initialized);
            Assert.IsTrue(service.MaxCPUCount == 4);
            Assert.IsTrue(service.OnlyLogCriticalEvents == true);
        }

        /// <summary>
        /// Verify the correct exception is thrown when a null Component host is passed in
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(InternalErrorException))]
        public void InitializeComponentNullHost()
        {
            IBuildComponent logServiceComponent = (IBuildComponent)LoggingService.CreateLoggingService(LoggerMode.Synchronous, 1);
            logServiceComponent.InitializeComponent(null);
        }

        /// <summary>
        /// Verify an exception is thrown if in itialized is called after the service has been shutdown
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(InternalErrorException))]
        public void InitializeComponentAfterShutdown()
        {
            _initializedService.ShutdownComponent();
            _initializedService.InitializeComponent(new MockHost());
        }

        /// <summary>
        /// Verify the correct exceptions are thrown if the loggers crash
        /// when they are shutdown
        /// </summary>
        [TestMethod]
        public void ShutDownComponentExceptionsInForwardingLogger()
        {
            // Cause a logger exception in the shutdown of the logger
            string className = "Microsoft.Build.UnitTests.Logging.LoggingService_Tests+ShutdownLoggerExceptionFL";
            Type exceptionType = typeof(LoggerException);
            VerifyShutdownExceptions(null, className, exceptionType);
            Assert.IsTrue(_initializedService.ServiceState == LoggingServiceState.Shutdown);

            // Cause a general exception which should result in an InternalLoggerException
            className = "Microsoft.Build.UnitTests.Logging.LoggingService_Tests+ShutdownGeneralExceptionFL";
            exceptionType = typeof(InternalLoggerException);
            VerifyShutdownExceptions(null, className, exceptionType);
            Assert.IsTrue(_initializedService.ServiceState == LoggingServiceState.Shutdown);

            // Cause a StackOverflow exception in the shutdown of the logger
            // this kind of exception should not be caught
            className = "Microsoft.Build.UnitTests.Logging.LoggingService_Tests+ShutdownStackoverflowExceptionFL";
            exceptionType = typeof(StackOverflowException);
            VerifyShutdownExceptions(null, className, exceptionType);

            Assert.IsTrue(_initializedService.ServiceState == LoggingServiceState.Shutdown);
        }

        /// <summary>
        /// Verify the correct exceptions are thrown when ILoggers
        /// throw exceptions during shutdown
        /// </summary>
        [TestMethod]
        public void ShutDownComponentExceptionsInLogger()
        {
            LoggerThrowException logger = new LoggerThrowException(true, false, new LoggerException("Hello"));
            VerifyShutdownExceptions(logger, null, typeof(LoggerException));

            logger = new LoggerThrowException(true, false, new Exception("boo"));
            VerifyShutdownExceptions(logger, null, typeof(InternalLoggerException));

            logger = new LoggerThrowException(true, false, new StackOverflowException());
            VerifyShutdownExceptions(logger, null, typeof(StackOverflowException));

            Assert.IsTrue(_initializedService.ServiceState == LoggingServiceState.Shutdown);
        }

        /// <summary>
        /// Log some events on one thread and verify that even
        /// when events are being logged while shutdown is occuring 
        /// that the shutdown still completes.
        /// </summary>
        [TestMethod]
        public void ShutdownWaitForEvents()
        {
            // LoggingBuildComponentHost loggingHost = new LoggingBuildComponentHost();
            // loggingHost.NumberOfNodes = 2;
            // IBuildComponent logServiceComponent = LoggingService.CreateLogger(LoggerMode.Asynchronous);
            // initializedService = logServiceComponent as LoggingService;
            // shutdownComplete = new ManualResetEvent(false);
            // Thread loggingThread = new Thread(new ThreadStart(TightLoopLogEvents));
            // loggingThread.Start();
            // Give it time to log some events
            // Thread.Sleep(100);
            // initializedService.ShutdownComponent();
            // Assert.IsFalse(initializedService.LoggingQueueHasEvents);
            // shutdownComplete.Set();
            // if (initializedService.ServiceState != LoggingServiceState.Shutdown)
            // {
            //    Assert.Fail();
            // }
        }

        /// <summary>
        /// Make sure an exception is thrown if shutdown is called 
        /// more than once
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(InternalErrorException))]
        public void DoubleShutdown()
        {
            _initializedService.ShutdownComponent();
            _initializedService.ShutdownComponent();
        }

        #endregion

        #region RegisterLogger
        /// <summary>
        /// Verify we get an exception when a null logger is passed in
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(InternalErrorException))]
        public void NullLogger()
        {
            _initializedService.RegisterLogger(null);
        }

        /// <summary>
        /// Verify we get an exception when we try and register a logger
        /// and the system has already shutdown
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(InternalErrorException))]
        public void RegisterLoggerServiceShutdown()
        {
            _initializedService.ShutdownComponent();
            RegularILogger regularILogger = new RegularILogger();
            _initializedService.RegisterLogger(regularILogger);
        }

        /// <summary>
        /// Verify a logger exception when initializing a logger is rethrown 
        /// as a logger exception
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(LoggerException))]
        public void LoggerExceptionInInitialize()
        {
            LoggerThrowException exceptionLogger = new LoggerThrowException(false, true, new LoggerException());
            _initializedService.RegisterLogger(exceptionLogger);
        }

        /// <summary>
        /// Verify a general exception when initializing a logger is wrapped 
        /// as a InternalLogger exception
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(InternalLoggerException))]
        public void GeneralExceptionInInitialize()
        {
            LoggerThrowException exceptionLogger = new LoggerThrowException(false, true, new Exception());
            _initializedService.RegisterLogger(exceptionLogger);
        }

        /// <summary>
        /// Verify a critical exception is not wrapped
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(StackOverflowException))]
        public void ILoggerExceptionInInitialize()
        {
            LoggerThrowException exceptionLogger = new LoggerThrowException(false, true, new StackOverflowException());
            _initializedService.RegisterLogger(exceptionLogger);
        }

        /// <summary>
        /// Register an good Logger and verify it was registered.
        /// </summary>
        [TestMethod]
        public void RegisterILoggerAndINodeLoggerGood()
        {
            ConsoleLogger consoleLogger = new ConsoleLogger();
            RegularILogger regularILogger = new RegularILogger();
            Assert.IsTrue(_initializedService.RegisterLogger(consoleLogger));
            Assert.IsTrue(_initializedService.RegisterLogger(regularILogger));
            Assert.IsNotNull(_initializedService.RegisteredLoggerTypeNames);

            // Should have 2 central loggers and 1 forwarding logger
            Assert.IsTrue(_initializedService.RegisteredLoggerTypeNames.Count == 3);
            Assert.IsTrue(_initializedService.RegisteredLoggerTypeNames.Contains("Microsoft.Build.BackEnd.Logging.CentralForwardingLogger"));
            Assert.IsTrue(_initializedService.RegisteredLoggerTypeNames.Contains("Microsoft.Build.UnitTests.Logging.LoggingService_Tests+RegularILogger"));
            Assert.IsTrue(_initializedService.RegisteredLoggerTypeNames.Contains("Microsoft.Build.Logging.ConsoleLogger"));

            // Should have 1 event sink
            Assert.IsNotNull(_initializedService.RegisteredSinkNames);
            Assert.IsTrue(_initializedService.RegisteredSinkNames.Count == 1);
        }

        /// <summary>
        /// Try and register the same logger multiple times
        /// </summary>
        [TestMethod]
        public void RegisterDuplicateLogger()
        {
            ConsoleLogger consoleLogger = new ConsoleLogger();
            RegularILogger regularILogger = new RegularILogger();
            Assert.IsTrue(_initializedService.RegisterLogger(consoleLogger));
            Assert.IsFalse(_initializedService.RegisterLogger(consoleLogger));
            Assert.IsTrue(_initializedService.RegisterLogger(regularILogger));
            Assert.IsFalse(_initializedService.RegisterLogger(regularILogger));
            Assert.IsNotNull(_initializedService.RegisteredLoggerTypeNames);

            Assert.IsTrue(_initializedService.RegisteredLoggerTypeNames.Count == 3);
            Assert.IsTrue(_initializedService.RegisteredLoggerTypeNames.Contains("Microsoft.Build.BackEnd.Logging.CentralForwardingLogger"));
            Assert.IsTrue(_initializedService.RegisteredLoggerTypeNames.Contains("Microsoft.Build.UnitTests.Logging.LoggingService_Tests+RegularILogger"));
            Assert.IsTrue(_initializedService.RegisteredLoggerTypeNames.Contains("Microsoft.Build.Logging.ConsoleLogger"));

            // Should have 1 event sink
            Assert.IsNotNull(_initializedService.RegisteredSinkNames);
            Assert.IsTrue(_initializedService.RegisteredSinkNames.Count == 1);
        }

        #endregion

        #region RegisterDistributedLogger
        /// <summary>
        /// Verify we get an exception when a null logger forwarding logger is passed in
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(InternalErrorException))]
        public void NullForwardingLogger()
        {
            _initializedService.RegisterDistributedLogger(null, null);
        }

        /// <summary>
        /// Verify we get an exception when we try and register a distributed logger
        /// and the system has already shutdown
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(InternalErrorException))]
        public void RegisterDistributedLoggerServiceShutdown()
        {
            _initializedService.ShutdownComponent();
            string className = "Microsoft.Build.Logging.ConfigurableForwardingLogger";
            LoggerDescription description = CreateLoggerDescription(className, Assembly.GetAssembly(typeof(ProjectCollection)).FullName, true);
            _initializedService.RegisterDistributedLogger(null, description);
        }

        /// <summary>
        /// Register both a good central logger and a good forwarding logger
        /// </summary>
        [TestMethod]
        public void RegisterGoodDistributedAndCentralLogger()
        {
            string configurableClassName = "Microsoft.Build.Logging.ConfigurableForwardingLogger";
            string distributedClassName = "Microsoft.Build.Logging.DistributedFileLogger";
            LoggerDescription configurableDescription = CreateLoggerDescription(configurableClassName, Assembly.GetAssembly(typeof(ProjectCollection)).FullName, true);
            LoggerDescription distributedDescription = CreateLoggerDescription(distributedClassName, Assembly.GetAssembly(typeof(ProjectCollection)).FullName, true);

            DistributedFileLogger fileLogger = new DistributedFileLogger();
            RegularILogger regularILogger = new RegularILogger();
            Assert.IsTrue(_initializedService.RegisterDistributedLogger(regularILogger, configurableDescription));
            Assert.IsTrue(_initializedService.RegisterDistributedLogger(null, distributedDescription));
            Assert.IsNotNull(_initializedService.RegisteredLoggerTypeNames);

            // Should have 2 central loggers and 2 forwarding logger
            Assert.IsTrue(_initializedService.RegisteredLoggerTypeNames.Count == 4);
            Assert.IsTrue(_initializedService.RegisteredLoggerTypeNames.Contains("Microsoft.Build.Logging.ConfigurableForwardingLogger"));
            Assert.IsTrue(_initializedService.RegisteredLoggerTypeNames.Contains("Microsoft.Build.UnitTests.Logging.LoggingService_Tests+RegularILogger"));
            Assert.IsTrue(_initializedService.RegisteredLoggerTypeNames.Contains("Microsoft.Build.Logging.DistributedFileLogger"));
            Assert.IsTrue(_initializedService.RegisteredLoggerTypeNames.Contains("Microsoft.Build.BackEnd.Logging.NullCentralLogger"));

            // Should have 2 event sink
            Assert.IsNotNull(_initializedService.RegisteredSinkNames);
            Assert.AreEqual(2, _initializedService.RegisteredSinkNames.Count);
            Assert.AreEqual(2, _initializedService.LoggerDescriptions.Count);
        }

        /// <summary>
        /// Have a one forwarding logger which forwards build started and finished and have one which does not and a regular logger. Expect the central loggers to all get 
        /// one build started and one build finished event only.
        /// </summary>
        [TestMethod]
        public void RegisterGoodDistributedAndCentralLoggerTestBuildStartedFinished()
        {
            string configurableClassNameA = "Microsoft.Build.Logging.ConfigurableForwardingLogger";
            string configurableClassNameB = "Microsoft.Build.Logging.ConfigurableForwardingLogger";

            LoggerDescription configurableDescriptionA = CreateLoggerDescription(configurableClassNameA, Assembly.GetAssembly(typeof(ProjectCollection)).FullName, true);
            LoggerDescription configurableDescriptionB = CreateLoggerDescription(configurableClassNameB, Assembly.GetAssembly(typeof(ProjectCollection)).FullName, false);

            RegularILogger regularILoggerA = new RegularILogger();
            RegularILogger regularILoggerB = new RegularILogger();
            RegularILogger regularILoggerC = new RegularILogger();
            Assert.IsTrue(_initializedService.RegisterDistributedLogger(regularILoggerA, configurableDescriptionA));
            Assert.IsTrue(_initializedService.RegisterDistributedLogger(regularILoggerB, configurableDescriptionB));
            Assert.IsTrue(_initializedService.RegisterLogger(regularILoggerC));
            Assert.IsNotNull(_initializedService.RegisteredLoggerTypeNames);

            _initializedService.LogBuildStarted();
            Assert.IsTrue(regularILoggerA.BuildStartedCount == 1);
            Assert.IsTrue(regularILoggerB.BuildStartedCount == 1);
            Assert.IsTrue(regularILoggerC.BuildStartedCount == 1);

            _initializedService.LogBuildFinished(true);
            Assert.IsTrue(regularILoggerA.BuildFinishedCount == 1);
            Assert.IsTrue(regularILoggerB.BuildFinishedCount == 1);
            Assert.IsTrue(regularILoggerC.BuildFinishedCount == 1);

            // Make sure if we call build started again we only get one other build started event. 
            _initializedService.LogBuildStarted();
            Assert.IsTrue(regularILoggerA.BuildStartedCount == 2);
            Assert.IsTrue(regularILoggerB.BuildStartedCount == 2);
            Assert.IsTrue(regularILoggerC.BuildStartedCount == 2);

            // Make sure if we call build started again we only get one other build started event. 
            _initializedService.LogBuildFinished(true);
            Assert.IsTrue(regularILoggerA.BuildFinishedCount == 2);
            Assert.IsTrue(regularILoggerB.BuildFinishedCount == 2);
            Assert.IsTrue(regularILoggerC.BuildFinishedCount == 2);
        }

        /// <summary>
        /// Try and register a duplicate central logger
        /// </summary>
        [TestMethod]
        public void RegisterDuplicateCentralLogger()
        {
            string className = "Microsoft.Build.Logging.ConfigurableForwardingLogger";
            LoggerDescription description = CreateLoggerDescription(className, Assembly.GetAssembly(typeof(ProjectCollection)).FullName, true);

            RegularILogger regularILogger = new RegularILogger();
            Assert.IsTrue(_initializedService.RegisterDistributedLogger(regularILogger, description));
            Assert.IsFalse(_initializedService.RegisterDistributedLogger(regularILogger, description));

            // Should have 2 central loggers and 1 forwarding logger
            Assert.IsTrue(_initializedService.RegisteredLoggerTypeNames.Count == 2);
            Assert.IsTrue(_initializedService.RegisteredLoggerTypeNames.Contains("Microsoft.Build.Logging.ConfigurableForwardingLogger"));
            Assert.IsTrue(_initializedService.RegisteredLoggerTypeNames.Contains("Microsoft.Build.UnitTests.Logging.LoggingService_Tests+RegularILogger"));

            // Should have 1 sink
            Assert.IsNotNull(_initializedService.RegisteredSinkNames);
            Assert.AreEqual(1, _initializedService.RegisteredSinkNames.Count);
            Assert.AreEqual(1, _initializedService.LoggerDescriptions.Count);
        }

        /// <summary>
        /// Try and register a duplicate Forwarding logger
        /// </summary>
        [TestMethod]
        public void RegisterDuplicateForwardingLoggerLogger()
        {
            string className = "Microsoft.Build.Logging.ConfigurableForwardingLogger";
            LoggerDescription description = CreateLoggerDescription(className, Assembly.GetAssembly(typeof(ProjectCollection)).FullName, true);

            RegularILogger regularILogger = new RegularILogger();
            Assert.IsTrue(_initializedService.RegisterDistributedLogger(regularILogger, description));
            Assert.IsTrue(_initializedService.RegisterDistributedLogger(null, description));
            Assert.AreEqual(4, _initializedService.RegisteredLoggerTypeNames.Count);

            // Verify there are two versions in the type names, one for each description
            int countForwardingLogger = 0;
            foreach (string loggerName in _initializedService.RegisteredLoggerTypeNames)
            {
                if (String.Compare("Microsoft.Build.Logging.ConfigurableForwardingLogger", loggerName, StringComparison.OrdinalIgnoreCase) == 0)
                {
                    countForwardingLogger++;
                }
            }

            Assert.AreEqual(2, countForwardingLogger);
            Assert.IsTrue(_initializedService.RegisteredLoggerTypeNames.Contains("Microsoft.Build.BackEnd.Logging.NullCentralLogger"));
            Assert.IsTrue(_initializedService.RegisteredLoggerTypeNames.Contains("Microsoft.Build.UnitTests.Logging.LoggingService_Tests+RegularILogger"));

            // Should have 2 sink
            Assert.IsNotNull(_initializedService.RegisteredSinkNames);
            Assert.AreEqual(2, _initializedService.RegisteredSinkNames.Count);
            Assert.AreEqual(2, _initializedService.LoggerDescriptions.Count);
        }

        #endregion

        #region RegisterLoggerDescriptions
        /// <summary>
        /// Verify we get an exception when a null description collection is passed in
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(InternalErrorException))]
        public void NullDescriptionCollection()
        {
            _initializedService.InitializeNodeLoggers(null, new EventSourceSink(), 3);
        }

        /// <summary>
        /// Verify we get an exception when an empty description collection is passed in
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(InternalErrorException))]
        public void EmptyDescriptionCollection()
        {
            _initializedService.InitializeNodeLoggers(new List<LoggerDescription>(), new EventSourceSink(), 3);
        }

        /// <summary>
        /// Verify we get an exception when we try and register a description and the component has already shutdown
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(InternalErrorException))]
        public void NullForwardingLoggerSink()
        {
            string className = "Microsoft.Build.Logging.ConfigurableForwardingLogger";
            LoggerDescription description = CreateLoggerDescription(className, Assembly.GetAssembly(typeof(ProjectCollection)).FullName, true);
            _initializedService.ShutdownComponent();
            List<LoggerDescription> tempList = new List<LoggerDescription>();
            tempList.Add(description);
            _initializedService.InitializeNodeLoggers(tempList, new EventSourceSink(), 2);
        }

        /// <summary>
        /// Register both a good central logger and a good forwarding logger
        /// </summary>
        [TestMethod]
        public void RegisterGoodDiscriptions()
        {
            string configurableClassName = "Microsoft.Build.Logging.ConfigurableForwardingLogger";
            string distributedClassName = "Microsoft.Build.BackEnd.Logging.CentralForwardingLogger";
            EventSourceSink sink = new EventSourceSink();
            EventSourceSink sink2 = new EventSourceSink();
            List<LoggerDescription> loggerDescriptions = new List<LoggerDescription>();
            loggerDescriptions.Add(CreateLoggerDescription(configurableClassName, Assembly.GetAssembly(typeof(ProjectCollection)).FullName, true));
            loggerDescriptions.Add(CreateLoggerDescription(distributedClassName, Assembly.GetAssembly(typeof(ProjectCollection)).FullName, true));

            // Register some descriptions with a sink
            _initializedService.InitializeNodeLoggers(loggerDescriptions, sink, 1);

            // Register the same descriptions with another sink (so we can see that another sink was added)
            _initializedService.InitializeNodeLoggers(loggerDescriptions, sink2, 1);

            // Register the descriptions again with the same sink so we can verify that another sink was not created
            _initializedService.InitializeNodeLoggers(loggerDescriptions, sink, 1);

            Assert.IsNotNull(_initializedService.RegisteredLoggerTypeNames);

            // Should have 6 forwarding logger. three of each type
            Assert.IsTrue(_initializedService.RegisteredLoggerTypeNames.Count == 6);
            Assert.IsTrue(_initializedService.RegisteredLoggerTypeNames.Contains("Microsoft.Build.Logging.ConfigurableForwardingLogger"));
            Assert.IsTrue(_initializedService.RegisteredLoggerTypeNames.Contains("Microsoft.Build.BackEnd.Logging.CentralForwardingLogger"));

            int countForwardingLogger = 0;
            foreach (string loggerName in _initializedService.RegisteredLoggerTypeNames)
            {
                if (String.Compare("Microsoft.Build.Logging.ConfigurableForwardingLogger", loggerName, StringComparison.OrdinalIgnoreCase) == 0)
                {
                    countForwardingLogger++;
                }
            }

            // Should be 3, one for each call to RegisterLoggerDescriptions
            Assert.AreEqual(3, countForwardingLogger);

            countForwardingLogger = 0;
            foreach (string loggerName in _initializedService.RegisteredLoggerTypeNames)
            {
                if (String.Compare("Microsoft.Build.BackEnd.Logging.CentralForwardingLogger", loggerName, StringComparison.OrdinalIgnoreCase) == 0)
                {
                    countForwardingLogger++;
                }
            }

            // Should be 3, one for each call to RegisterLoggerDescriptions
            Assert.AreEqual(3, countForwardingLogger);

            // Should have 2 event sink
            Assert.IsNotNull(_initializedService.RegisteredSinkNames);
            Assert.IsTrue(_initializedService.RegisteredSinkNames.Count == 2);

            // There should not be any (this method is to be called on a child node)
            Assert.AreEqual(0, _initializedService.LoggerDescriptions.Count);
        }

        /// <summary>
        /// Try and register a duplicate central logger
        /// </summary>
        [TestMethod]
        public void RegisterDuplicateDistributedCentralLogger()
        {
            string className = "Microsoft.Build.Logging.ConfigurableForwardingLogger";
            LoggerDescription description = CreateLoggerDescription(className, Assembly.GetAssembly(typeof(ProjectCollection)).FullName, true);

            RegularILogger regularILogger = new RegularILogger();
            Assert.IsTrue(_initializedService.RegisterDistributedLogger(regularILogger, description));
            Assert.IsFalse(_initializedService.RegisterDistributedLogger(regularILogger, description));

            // Should have 2 central loggers and 1 forwarding logger
            Assert.IsTrue(_initializedService.RegisteredLoggerTypeNames.Count == 2);
            Assert.IsTrue(_initializedService.RegisteredLoggerTypeNames.Contains("Microsoft.Build.Logging.ConfigurableForwardingLogger"));
            Assert.IsTrue(_initializedService.RegisteredLoggerTypeNames.Contains("Microsoft.Build.UnitTests.Logging.LoggingService_Tests+RegularILogger"));

            // Should have 1 sink
            Assert.IsNotNull(_initializedService.RegisteredSinkNames);
            Assert.AreEqual(1, _initializedService.RegisteredSinkNames.Count);
            Assert.AreEqual(1, _initializedService.LoggerDescriptions.Count);
        }
        #endregion

        #region Test Properties
        /// <summary>
        /// Verify the getters and setters for the properties work.
        /// </summary>
        [TestMethod]
        public void Properties()
        {
            // Test OnlyLogCriticalEvents
            LoggingService loggingService = (LoggingService)LoggingService.CreateLoggingService(LoggerMode.Synchronous, 1);
            Assert.IsFalse(loggingService.OnlyLogCriticalEvents, "Expected only log critical events to be false");
            loggingService.OnlyLogCriticalEvents = true;
            Assert.IsTrue(loggingService.OnlyLogCriticalEvents, "Expected only log critical events to be true");

            // Test LoggingMode
            Assert.IsTrue(loggingService.LoggingMode == LoggerMode.Synchronous, "Expected Logging mode to be Synchronous");

            // Test LoggerDescriptions
            Assert.AreEqual(0, loggingService.LoggerDescriptions.Count, "Expected LoggerDescriptions to be empty");

            // Test Number of InitialNodes
            Assert.AreEqual(1, loggingService.MaxCPUCount);
            loggingService.MaxCPUCount = 5;
            Assert.AreEqual(5, loggingService.MaxCPUCount);
        }

        #endregion

        #region PacketHandling Tests
        /// <summary>
        /// Verify how a null packet is handled. There should be an InternalErrorException thrown
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(InternalErrorException))]
        public void NullPacketReceived()
        {
            LoggingService loggingService = (LoggingService)LoggingService.CreateLoggingService(LoggerMode.Synchronous, 1);
            loggingService.PacketReceived(1, null);
        }

        /// <summary>
        /// Verify when a non logging packet is received. 
        /// An invalid operation should be thrown
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(InternalErrorException))]
        public void NonLoggingPacketPacketReceived()
        {
            LoggingService loggingService = (LoggingService)LoggingService.CreateLoggingService(LoggerMode.Synchronous, 1);
            NonLoggingPacket packet = new NonLoggingPacket();
            loggingService.PacketReceived(1, packet);
        }

        /// <summary>
        /// Verify when a logging packet is received the build event is 
        /// properly passed to ProcessLoggingEvent
        /// An invalid operation should be thrown
        /// </summary>
        [TestMethod]
        public void LoggingPacketReceived()
        {
            LoggingServicesLogMethod_Tests.ProcessBuildEventHelper loggingService = (LoggingServicesLogMethod_Tests.ProcessBuildEventHelper)LoggingServicesLogMethod_Tests.ProcessBuildEventHelper.CreateLoggingService(LoggerMode.Synchronous, 1);
            BuildMessageEventArgs messageEvent = new BuildMessageEventArgs("MyMessage", "HelpKeyword", "Sender", MessageImportance.High);
            LogMessagePacket packet = new LogMessagePacket(new KeyValuePair<int, BuildEventArgs>(1, messageEvent));
            loggingService.PacketReceived(1, packet);

            BuildMessageEventArgs messageEventFromPacket = loggingService.ProcessedBuildEvent as BuildMessageEventArgs;
            Assert.IsNotNull(messageEventFromPacket);
            Assert.IsTrue(messageEventFromPacket == messageEvent, "Expected messages to match");
        }

        #endregion

        #region PrivateMethods

        /// <summary>
        /// Instantiate and Initialize a new loggingService. 
        /// This is used by the test setup method to create 
        /// a new logging service before each test.
        /// </summary>
        private void InitializeLoggingService()
        {
            BuildParameters parameters = new BuildParameters();
            parameters.MaxNodeCount = 2;
            MockHost mockHost = new MockHost(parameters);

            IBuildComponent logServiceComponent = (IBuildComponent)LoggingService.CreateLoggingService(LoggerMode.Synchronous, 1);
            logServiceComponent.InitializeComponent(mockHost);
            _initializedService = logServiceComponent as LoggingService;
        }

        /// <summary>
        /// Log a message every 10ms, this is used to verify 
        /// the shutdown is not waiting forever for events as it 
        /// shutsdown.
        /// </summary>
        private void TightLoopLogEvents()
        {
            while (!_shutdownComplete.WaitOne(10, false))
            {
                _initializedService.LogBuildEvent(new BuildMessageEventArgs("Message", "Help", "Sender", MessageImportance.High));
            }
        }

        /// <summary>
        /// Register the correct logger and then call the shutdownComponent method. 
        /// This will call shutdown on the loggers, we should expect to see certain exceptions.
        /// </summary>
        /// <param name="logger">Logger to register, this will only be used if className is null</param>
        /// <param name="className">ClassName to instantiate a new distributed logger</param>
        /// <param name="expectedExceptionType">Exception type which is expected to be thrown</param>
        private void VerifyShutdownExceptions(ILogger logger, string className, Type expectedExceptionType)
        {
            InitializeLoggingService();
            if (className != null)
            {
                Assembly thisAssembly = Assembly.GetAssembly(typeof(LoggingService_Tests));
                string loggerAssemblyName = thisAssembly.FullName;
                LoggerDescription centralLoggerDescrption = CreateLoggerDescription(className, loggerAssemblyName, true);
                _initializedService.RegisterDistributedLogger(null, centralLoggerDescrption);
            }
            else
            {
                _initializedService.RegisterLogger(logger);
            }

            try
            {
                _initializedService.ShutdownComponent();
                Assert.Fail("No Exceptions Generated");
            }
            catch (Exception e)
            {
                if (e.GetType() != expectedExceptionType)
                {
                    Assert.Fail("Expected a " + expectedExceptionType + " but got a " + e.GetType() + " Stack:" + e.ToString());
                }
            }
        }

        /// <summary>
        /// Create a logger description from the class name and logger assembly
        /// This is used in any test which needs to register a distributed logger.
        /// </summary>
        /// <param name="loggerClassName">Fully qualified class name (dont for get ParentClass+Nestedclass, if nested)</param>
        /// <param name="loggerAssemblyName">Assembly name which contains class</param>
        /// <returns>A logger description which can be registered</returns>
        private LoggerDescription CreateLoggerDescription(string loggerClassName, string loggerAssemblyName, bool forwardAllEvents)
        {
            string eventsToForward = "CustomEvent";

            if (forwardAllEvents == true)
            {
                eventsToForward = "BuildStartedEvent;BuildFinishedEvent;ProjectStartedEvent;ProjectFinishedEvent;TargetStartedEvent;TargetFinishedEvent;TaskStartedEvent;TaskFinishedEvent;ErrorEvent;WarningEvent;HighMessageEvent;NormalMessageEvent;LowMessageEvent;CustomEvent;CommandLine";
            }

            LoggerDescription centralLoggerDescrption = new LoggerDescription
                                                                             (
                                                                              loggerClassName,
                                                                              loggerAssemblyName,
                                                                              null /*Not needed as we are loading from current assembly*/,
                                                                              eventsToForward,
                                                                              LoggerVerbosity.Diagnostic /*Not used, but the spirit of the logger is to forward everything so this is the most appropriate verbosity */
                                                                             );
            return centralLoggerDescrption;
        }
        #endregion

        #region HelperClasses

        /// <summary>
        /// A forwarding logger which will throw an exception
        /// </summary>
        public class BaseFLThrowException : LoggerThrowException, IForwardingLogger
        {
            #region Constructor

            /// <summary>
            /// Create a forwarding logger which will throw an exception on initialize or shutdown
            /// </summary>
            /// <param name="throwOnShutdown">Throw exception on shutdown</param>
            /// <param name="throwOnInitialize">Throw exception on initialize</param>
            /// <param name="exception">Exception to throw</param>
            internal BaseFLThrowException(bool throwOnShutdown, bool throwOnInitialize, Exception exception)
                : base(throwOnShutdown, throwOnInitialize, exception)
            {
            }
            #endregion

            #region IForwardingLogger Members

            /// <summary>
            /// Not used, implmented due to interface
            /// </summary>
            /// <value>Notused</value>
            public IEventRedirector BuildEventRedirector
            {
                get;
                set;
            }

            /// <summary>
            /// Not used, implemented due to interface
            /// </summary>
            /// <value>Not used</value>
            public int NodeId
            {
                get;
                set;
            }

            #endregion
        }

        /// <summary>
        /// Forwarding logger which throws a logger exception in the shutdown method.
        /// This is to test the logging service exception handling.
        /// </summary>
        public class ShutdownLoggerExceptionFL : BaseFLThrowException
        {
            /// <summary>
            /// Create a logger which will throw a logger exception 
            /// in the shutdown method
            /// </summary>
            public ShutdownLoggerExceptionFL()
                : base(true, false, new LoggerException("Hello"))
            {
            }
        }

        /// <summary>
        /// Forwarding logger which will throw a general exception in the shutdown method
        /// This is used to test the logging service shutdown handling method.
        /// </summary>
        public class ShutdownGeneralExceptionFL : BaseFLThrowException
        {
            /// <summary>
            /// Create a logger which logs a general exception in the shutdown method
            /// </summary>
            public ShutdownGeneralExceptionFL()
                : base(true, false, new Exception("Hello"))
            {
            }
        }

        /// <summary>
        /// Forwarding logger which will throw a StackOverflowException 
        /// in the shutdown method. This is to test the shutdown exception handling
        /// </summary>
        public class ShutdownStackoverflowExceptionFL : BaseFLThrowException
        {
            /// <summary>
            /// Create a logger which will throw a StackOverflow exception
            /// in the shutdown method.
            /// </summary>
            public ShutdownStackoverflowExceptionFL()
                : base(true, false, new StackOverflowException())
            {
            }
        }

        /// <summary>
        /// Logger which can throw a defined exception in the initialize or shutdown methods
        /// </summary>
        public class LoggerThrowException : INodeLogger
        {
            #region Constructor

            /// <summary>
            /// Constructor to tell the logger when to throw an exception and what excetption 
            /// to throw
            /// </summary>
            /// <param name="throwOnShutdown">True, throw the exception when shutdown is called</param>
            /// <param name="throwOnInitialize">True, throw the exception when Initialize is called</param>
            /// <param name="exception">The exception to throw</param>
            internal LoggerThrowException(bool throwOnShutdown, bool throwOnInitialize, Exception exception)
            {
                ExceptionToThrow = exception;
                ThrowExceptionOnShutdown = throwOnShutdown;
                ThrowExceptionOnInitialize = throwOnInitialize;
            }
            #endregion

            #region Propeties

            /// <summary>
            /// Not used, implemented due to ILoggerInterface
            /// </summary>
            /// <value>Not used</value>
            public LoggerVerbosity Verbosity
            {
                get;
                set;
            }

            /// <summary>
            /// Not used, implemented due to ILoggerInterface
            /// </summary>
            /// <value>Not used</value>
            public string Parameters
            {
                get;
                set;
            }

            /// <summary>
            /// Should the exception be thrown on the call to shutdown
            /// </summary>
            /// <value>Not used</value>
            protected bool ThrowExceptionOnShutdown
            {
                get;
                set;
            }

            /// <summary>
            /// Should the exception be thrown on the call to initalize
            /// </summary>
            /// <value>Not used</value>
            protected bool ThrowExceptionOnInitialize
            {
                get;
                set;
            }

            /// <summary>
            /// The exception which will be thrown in shutdown or initialize
            /// </summary>
            /// <value>Not used</value>
            protected Exception ExceptionToThrow
            {
                get;
                set;
            }
            #endregion

            #region ILogger Members

            /// <summary>
            /// Initialize the logger, throw an exception 
            /// if ThrowExceptionOnInitialize is set
            /// </summary>
            /// <param name="eventSource">Not used</param>
            public void Initialize(IEventSource eventSource)
            {
                if (ThrowExceptionOnInitialize && ExceptionToThrow != null)
                {
                    throw ExceptionToThrow;
                }
            }

            /// <summary>
            /// Shutdown the logger, throw an exception if
            /// ThrowExceptionOnShutdown is set
            /// </summary>
            public void Shutdown()
            {
                if (ThrowExceptionOnShutdown && ExceptionToThrow != null)
                {
                    throw ExceptionToThrow;
                }
            }

            /// <summary>
            /// Initialize using the INodeLogger Interface
            /// </summary>
            /// <param name="eventSource">Not used</param>
            /// <param name="nodeCount">Not used</param>
            public void Initialize(IEventSource eventSource, int nodeCount)
            {
                Initialize(eventSource);
            }
            #endregion
        }

        /// <summary>
        /// Create a regular ILogger to test Registering ILoggers.
        /// </summary>
        public class RegularILogger : ILogger
        {
            #region Properties

            /// <summary>
            /// ParametersForTheLogger
            /// </summary>
            public string Parameters
            {
                get;
                set;
            }

            /// <summary>
            /// Verbosity
            /// </summary>
            public LoggerVerbosity Verbosity
            {
                get;
                set;
            }

            /// <summary>
            /// Number of times build started was logged
            /// </summary>
            internal int BuildStartedCount
            {
                get;
                set;
            }

            /// <summary>
            /// Number of times build finished was logged
            /// </summary>
            internal int BuildFinishedCount
            {
                get;
                set;
            }

            /// <summary>
            /// Initialize
            /// </summary>
            public void Initialize(IEventSource eventSource)
            {
                eventSource.AnyEventRaised +=
                        new AnyEventHandler(LoggerEventHandler);
            }

            /// <summary>
            /// DoNothing
            /// </summary>
            public void Shutdown()
            {
                // do nothing
            }

            /// <summary>
            /// Log the event
            /// </summary>
            internal void LoggerEventHandler(object sender, BuildEventArgs eventArgs)
            {
                if (eventArgs is BuildStartedEventArgs)
                {
                    ++BuildStartedCount;
                }

                if (eventArgs is BuildFinishedEventArgs)
                {
                    ++BuildFinishedCount;
                }
            }
        }

        /// <summary>
        /// Create a regular ILogger which keeps track of how many of each event were logged
        /// </summary>
        public class TestLogger : ILogger
        {
            /// <summary>
            /// Not Used
            /// </summary>
            /// <value>Not used</value>
            public LoggerVerbosity Verbosity
            {
                get;
                set;
            }

            /// <summary>
            /// Do Nothing
            /// </summary>
            /// <value>Not Used</value>
            public string Parameters
            {
                get;
                set;
            }

            /// <summary>
            /// Do Nothing
            /// </summary>
            /// <param name="eventSource">Not Used</param>
            public void Initialize(IEventSource eventSource)
            {
            }

            /// <summary>
            /// Do Nothing
            /// </summary>
            public void Shutdown()
            {
            }

            #endregion
        }

        /// <summary>
        ///  Create a non logging packet to test the packet handling code
        /// </summary>
        internal class NonLoggingPacket : INodePacket
        {
            #region Members

            /// <summary>
            /// Inform users of the class, this class is a BuildRequest packet
            /// </summary>
            public NodePacketType Type
            {
                get
                {
                    return NodePacketType.BuildRequest;
                }
            }

            /// <summary>
            /// Serialize the packet
            /// </summary>
            public void Translate(INodePacketTranslator translator)
            {
                throw new NotImplementedException();
            }

            #endregion
        }
        #endregion
    }
}