// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Xml;
using System.Text;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.IO;
using System.Threading;
using Microsoft.Build.Collections;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Exceptions;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using Microsoft.Build.BackEnd;
using Microsoft.Build.Shared;
using Microsoft.Build.Unittest;
using TaskItem = Microsoft.Build.Execution.ProjectItemInstance.TaskItem;
using Xunit;

namespace Microsoft.Build.UnitTests.BackEnd
{
    using ILoggingService = Microsoft.Build.BackEnd.Logging.ILoggingService;
    using NodeLoggingContext = Microsoft.Build.BackEnd.Logging.NodeLoggingContext;

    public class BuildRequestEngine_Tests : IDisposable
    {
        private delegate void EndpointOperationDelegate(NodeEndpointInProc endpoint);

        internal class MockRequestBuilder : IRequestBuilder, IBuildComponent
        {
            public bool ThrowExceptionOnRequest
            {
                get;
                set;
            }
            public bool ThrowExceptionOnContinue
            {
                get;
                set;
            }
            public bool ThrowExceptionOnCancel
            {
                get;
                set;
            }
            public bool CompleteRequestSuccessfully
            {
                get;
                set;
            }

            public List<FullyQualifiedBuildRequest[]> NewRequests
            {
                get;
                set;
            }


            private IBuildComponentHost _host;
            private Thread _builderThread;
            private BuildRequestEntry _entry;
            private AutoResetEvent _continueEvent;
            private AutoResetEvent _cancelEvent;

            public MockRequestBuilder()
            {
                ThrowExceptionOnRequest = false;
                ThrowExceptionOnContinue = false;
                ThrowExceptionOnCancel = false;
                CompleteRequestSuccessfully = true;
                NewRequests = new List<FullyQualifiedBuildRequest[]>();
            }

            #region IRequestBuilder Members

            public event NewBuildRequestsDelegate OnNewBuildRequests;

            public event BuildRequestCompletedDelegate OnBuildRequestCompleted;

            public event BuildRequestBlockedDelegate OnBuildRequestBlocked;

            public void BuildRequest(NodeLoggingContext context, BuildRequestEntry entry)
            {
                Assert.Null(_builderThread); // "Received BuildRequest while one was in progress"

                _continueEvent = new AutoResetEvent(false);
                _cancelEvent = new AutoResetEvent(false);
                _entry = entry;
                entry.Continue();

                _builderThread = new Thread(BuilderThreadProc);
                _builderThread.Start();
            }

            private void Delay()
            {
                Thread.Sleep(1000);
            }

            private void BuilderThreadProc()
            {
                _entry.RequestConfiguration.Project = CreateStandinProject();

                if (ThrowExceptionOnRequest)
                {
                    BuildResult errorResult = new BuildResult(_entry.Request, new InvalidOperationException("ContinueRequest not received in time."));
                    _entry.Complete(errorResult);
                    RaiseRequestComplete(_entry);
                    return;
                }

                bool completeSuccess = CompleteRequestSuccessfully;

                if (_cancelEvent.WaitOne(1000))
                {
                    BuildResult res = new BuildResult(_entry.Request, new BuildAbortedException());
                    _entry.Complete(res);
                    RaiseRequestComplete(_entry);
                    return;
                }

                for (int i = 0; i < NewRequests.Count; ++i)
                {
                    OnNewBuildRequests(_entry, NewRequests[i]);
                    WaitHandle[] handles = new WaitHandle[2] { _cancelEvent, _continueEvent };
                    int evt = WaitHandle.WaitAny(handles, 5000);
                    if (evt == 0)
                    {
                        BuildResult res = new BuildResult(_entry.Request, new BuildAbortedException());
                        _entry.Complete(res);
                        RaiseRequestComplete(_entry);
                        return;
                    }
                    else if (evt == 1)
                    {
                        IDictionary<int, BuildResult> results = _entry.Continue();
                        foreach (BuildResult configResult in results.Values)
                        {
                            if (configResult.OverallResult == BuildResultCode.Failure)
                            {
                                completeSuccess = false;
                                break;
                            }
                        }
                    }
                    else
                    {
                        BuildResult errorResult = new BuildResult(_entry.Request, new InvalidOperationException("ContinueRequest not received in time."));
                        _entry.Complete(errorResult);
                        RaiseRequestComplete(_entry);
                        return;
                    }
                    if (!completeSuccess)
                    {
                        break;
                    }
                    Delay();
                }

                BuildResult result = new BuildResult(_entry.Request);

                foreach (string target in _entry.Request.Targets)
                {
                    result.AddResultsForTarget(target, new TargetResult(new TaskItem[1] { new TaskItem("include", _entry.RequestConfiguration.ProjectFullPath) }, completeSuccess ? TestUtilities.GetSuccessResult() : TestUtilities.GetStopWithErrorResult()));
                }
                _entry.Complete(result);
            }

            public void RaiseRequestComplete(BuildRequestEntry entry)
            {
                if (null != OnBuildRequestCompleted)
                {
                    OnBuildRequestCompleted(entry);
                }
            }

            public void RaiseRequestBlocked(BuildRequestEntry entry, int blockingId, string blockingTarget)
            {
                if (null != OnBuildRequestBlocked)
                {
                    OnBuildRequestBlocked(entry, blockingId, blockingTarget, null);
                }
            }

            public void ContinueRequest()
            {
                if (ThrowExceptionOnContinue)
                {
                    throw new InvalidOperationException("ThrowExceptionOnContinue set.");
                }
                _continueEvent.Set();
            }

            public void CancelRequest()
            {
                this.BeginCancel();
                this.WaitForCancelCompletion();
            }

            public void BeginCancel()
            {
                if (ThrowExceptionOnCancel)
                {
                    throw new InvalidOperationException("ThrowExceptionOnCancel set.");
                }
                _cancelEvent.Set();
            }

            public void WaitForCancelCompletion()
            {
                if (!_builderThread.Join(5000))
                {
                    Assert.True(false, "Builder thread did not terminate on cancel.");
#if FEATURE_THREAD_ABORT
                    _builderThread.Abort();
#endif
                }
            }

            #endregion

            #region IBuildComponent Members

            public void InitializeComponent(IBuildComponentHost host)
            {
                _host = host;
            }

            public void ShutdownComponent()
            {
                _host = null;
            }

            #endregion

            private ProjectInstance CreateStandinProject()
            {
                string content = ObjectModelHelpers.CleanupFileContents(@"
            <Project ToolsVersion='msbuilddefaulttoolsversion' xmlns='msbuildnamespace'>
            <Target Name='t'>
            </Target>
            </Project>");

                Project project = new Project(XmlReader.Create(new StringReader(content)));
                return project.CreateProjectInstance();
            }
        }

        private MockHost _host;

        private AutoResetEvent _requestCompleteEvent;
        private BuildRequest _requestComplete_Request;
        private BuildResult _requestComplete_Result;

        private AutoResetEvent _requestResumedEvent;
        private BuildRequest _requestResumed_Request;

        private AutoResetEvent _newRequestEvent;
        private BuildRequestBlocker _newRequest_Request;

        private AutoResetEvent _engineStatusChangedEvent;
        private BuildRequestEngineStatus _engineStatusChanged_Status;

        private AutoResetEvent _newConfigurationEvent;
        private BuildRequestConfiguration _newConfiguration_Config;

        private AutoResetEvent _engineExceptionEvent;
        private Exception _engineException_Exception;

        private IBuildRequestEngine _engine;
        private IConfigCache _cache;
        private int _nodeRequestId;
        private int _globalRequestId;

        public BuildRequestEngine_Tests()
        {
            _host = new MockHost();
            _nodeRequestId = 1;
            _globalRequestId = 1;
            _engineStatusChangedEvent = new AutoResetEvent(false);
            _requestCompleteEvent = new AutoResetEvent(false);
            _requestResumedEvent = new AutoResetEvent(false);
            _newRequestEvent = new AutoResetEvent(false);
            _newConfigurationEvent = new AutoResetEvent(false);
            _engineExceptionEvent = new AutoResetEvent(false);

            _engine = (IBuildRequestEngine)_host.GetComponent(BuildComponentType.RequestEngine);
            _cache = (IConfigCache)_host.GetComponent(BuildComponentType.ConfigCache);

            ConfigureEngine(_engine);
        }

        public void Dispose()
        {
            if (_engine.Status != BuildRequestEngineStatus.Uninitialized)
            {
                _engine.CleanupForBuild();
            }

            ((IBuildComponent)_engine).ShutdownComponent();
            _engineStatusChangedEvent.Dispose();
            _requestCompleteEvent.Dispose();
            _requestResumedEvent.Dispose();
            _newRequestEvent.Dispose();
            _newConfigurationEvent.Dispose();
            _engineExceptionEvent.Dispose();

            _host = null;
        }

        private void ConfigureEngine(IBuildRequestEngine engine)
        {
            engine.OnNewConfigurationRequest += this.Engine_NewConfigurationRequest;
            engine.OnRequestBlocked += this.Engine_NewRequest;
            engine.OnRequestComplete += this.Engine_RequestComplete;
            engine.OnRequestResumed += this.Engine_RequestResumed;
            engine.OnStatusChanged += this.Engine_EngineStatusChanged;
            engine.OnEngineException += this.Engine_Exception;
        }

        /// <summary>
        /// This test verifies that the engine properly shuts down even if there is an active build request.
        /// This should cause that request to cancel and fail.
        /// </summary>
        [Fact]
        [Trait("CrashesOnNetCore", "true")]
        public void TestEngineShutdownWhileActive()
        {
            BuildRequestData data = new BuildRequestData("TestFile", new Dictionary<string, string>(), "TestToolsVersion", new string[0], null);
            BuildRequestConfiguration config = new BuildRequestConfiguration(1, data, "2.0");
            _cache.AddConfiguration(config);

            string[] targets = new string[3] { "target1", "target2", "target3" };
            BuildRequest request = CreateNewBuildRequest(1, targets);

            VerifyEngineStatus(BuildRequestEngineStatus.Uninitialized);
            _engine.InitializeForBuild(new NodeLoggingContext(_host.LoggingService, 0, false));
            _engine.SubmitBuildRequest(request);
            Thread.Sleep(250);
            VerifyEngineStatus(BuildRequestEngineStatus.Active);

            _engine.CleanupForBuild();

            WaitForEvent(_requestCompleteEvent, "RequestComplete");
            Assert.Equal(request, _requestComplete_Request);
            Assert.Equal(BuildResultCode.Failure, _requestComplete_Result.OverallResult);
            VerifyEngineStatus(BuildRequestEngineStatus.Uninitialized);
        }


        /// <summary>
        /// This test verifies that issuing a simple request results in a successful completion.
        /// </summary>
        [Fact]
        [Trait("CrashesOnNetCore", "true")]
        public void TestSimpleBuildScenario()
        {
            BuildRequestData data = new BuildRequestData("TestFile", new Dictionary<string, string>(), "TestToolsVersion", new string[0], null);
            BuildRequestConfiguration config = new BuildRequestConfiguration(1, data, "2.0");
            _cache.AddConfiguration(config);

            string[] targets = new string[3] { "target1", "target2", "target3" };
            BuildRequest request = CreateNewBuildRequest(1, targets);

            VerifyEngineStatus(BuildRequestEngineStatus.Uninitialized);
            _engine.InitializeForBuild(new NodeLoggingContext(_host.LoggingService, 0, false));
            _engine.SubmitBuildRequest(request);
            Thread.Sleep(250);
            VerifyEngineStatus(BuildRequestEngineStatus.Active);

            WaitForEvent(_requestCompleteEvent, "RequestComplete");
            Assert.Equal(request, _requestComplete_Request);
            Assert.Equal(BuildResultCode.Success, _requestComplete_Result.OverallResult);

            VerifyEngineStatus(BuildRequestEngineStatus.Idle);
        }

        /// <summary>
        /// This test verifies that a project which has project dependencies can issue and consume them through the
        /// engine interface.
        /// </summary>
        [Fact]
        [Trait("CrashesOnNetCore", "true")]
        public void TestBuildWithChildren()
        {
            BuildRequestData data = new BuildRequestData("TestFile", new Dictionary<string, string>(), "TestToolsVersion", new string[0], null);
            BuildRequestConfiguration config = new BuildRequestConfiguration(1, data, "2.0");
            _cache.AddConfiguration(config);

            // Configure the builder to spawn build requests
            MockRequestBuilder builder = (MockRequestBuilder)_host.GetComponent(BuildComponentType.RequestBuilder);
            builder.NewRequests.Add(new FullyQualifiedBuildRequest[1] { new FullyQualifiedBuildRequest(config, new string[1] { "requiredTarget1" }, true) });

            // Create the initial build request
            string[] targets = new string[3] { "target1", "target2", "target3" };
            BuildRequest request = CreateNewBuildRequest(1, targets);

            // Kick it off
            VerifyEngineStatus(BuildRequestEngineStatus.Uninitialized);
            _engine.InitializeForBuild(new NodeLoggingContext(_host.LoggingService, 0, false));
            _engine.SubmitBuildRequest(request);
            Thread.Sleep(250);
            VerifyEngineStatus(BuildRequestEngineStatus.Active);

            // Wait for the new requests to be spawned by the builder
            WaitForEvent(_newRequestEvent, "NewRequestEvent");
            Assert.Equal(1, _newRequest_Request.BuildRequests[0].ConfigurationId);
            Assert.Equal(1, _newRequest_Request.BuildRequests[0].Targets.Count);
            Assert.Equal("requiredTarget1", _newRequest_Request.BuildRequests[0].Targets[0]);

            // Wait for a moment, because the build request engine thread may not have gotten around
            // to going to the waiting state.
            Thread.Sleep(250);
            VerifyEngineStatus(BuildRequestEngineStatus.Waiting);

            // Report a result to satisfy the build request
            BuildResult result = new BuildResult(_newRequest_Request.BuildRequests[0]);
            result.AddResultsForTarget("requiredTarget1", TestUtilities.GetEmptySucceedingTargetResult());
            _engine.UnblockBuildRequest(new BuildRequestUnblocker(result));

            // Continue the request.
            _engine.UnblockBuildRequest(new BuildRequestUnblocker(request.GlobalRequestId));

            // Wait for the original request to complete
            WaitForEvent(_requestCompleteEvent, "RequestComplete");
            Assert.Equal(request, _requestComplete_Request);
            Assert.Equal(BuildResultCode.Success, _requestComplete_Result.OverallResult);

            VerifyEngineStatus(BuildRequestEngineStatus.Idle);
        }

        /// <summary>
        /// This test verifies that a project can issue a build request with an unresolved configuration and that if we resolve it,
        /// the build will continue and complete successfully.
        /// </summary>
        [Fact]
        [Trait("CrashesOnNetCore", "true")]

        public void TestBuildWithNewConfiguration()
        {
            BuildRequestData data = new BuildRequestData(Path.GetFullPath("TestFile"), new Dictionary<string, string>(), "TestToolsVersion", new string[0], null);
            BuildRequestConfiguration config = new BuildRequestConfiguration(1, data, "2.0");
            _cache.AddConfiguration(config);

            // Configure the builder to spawn build requests
            MockRequestBuilder builder = (MockRequestBuilder)_host.GetComponent(BuildComponentType.RequestBuilder);
            BuildRequestData data2 = new BuildRequestData(Path.GetFullPath("OtherFile"), new Dictionary<string, string>(), "TestToolsVersion", new string[0], null);
            BuildRequestConfiguration unresolvedConfig = new BuildRequestConfiguration(data2, "2.0");
            builder.NewRequests.Add(new FullyQualifiedBuildRequest[1] { new FullyQualifiedBuildRequest(unresolvedConfig, new string[1] { "requiredTarget1" }, true) });

            // Create the initial build request
            string[] targets = new string[3] { "target1", "target2", "target3" };
            BuildRequest request = CreateNewBuildRequest(1, targets);

            // Kick it off
            VerifyEngineStatus(BuildRequestEngineStatus.Uninitialized);
            _engine.InitializeForBuild(new NodeLoggingContext(_host.LoggingService, 0, false));
            _engine.SubmitBuildRequest(request);
            Thread.Sleep(250);
            VerifyEngineStatus(BuildRequestEngineStatus.Active);

            // Wait for the request to generate the child request with the unresolved configuration
            WaitForEvent(_newConfigurationEvent, "NewConfigurationEvent");
            Assert.Equal(Path.GetFullPath("OtherFile"), _newConfiguration_Config.ProjectFullPath);
            Assert.Equal("TestToolsVersion", _newConfiguration_Config.ToolsVersion);
            Assert.True(_newConfiguration_Config.WasGeneratedByNode);
            Thread.Sleep(250);
            VerifyEngineStatus(BuildRequestEngineStatus.Waiting);

            // Resolve the configuration
            BuildRequestConfigurationResponse response = new BuildRequestConfigurationResponse(_newConfiguration_Config.ConfigurationId, 2, 0);
            _engine.ReportConfigurationResponse(response);

            // Now wait for the actual requests to be issued.
            WaitForEvent(_newRequestEvent, "NewRequestEvent");
            Assert.Equal(2, _newRequest_Request.BuildRequests[0].ConfigurationId);
            Assert.Equal(2, _newRequest_Request.BuildRequests[0].ConfigurationId);
            Assert.Equal(1, _newRequest_Request.BuildRequests[0].Targets.Count);
            Assert.Equal("requiredTarget1", _newRequest_Request.BuildRequests[0].Targets[0]);

            // Report a result to satisfy the build request
            BuildResult result = new BuildResult(_newRequest_Request.BuildRequests[0]);
            result.AddResultsForTarget("requiredTarget1", TestUtilities.GetEmptySucceedingTargetResult());
            _engine.UnblockBuildRequest(new BuildRequestUnblocker(result));

            // Continue the request
            _engine.UnblockBuildRequest(new BuildRequestUnblocker(request.GlobalRequestId));

            // Wait for the original request to complete
            WaitForEvent(_requestCompleteEvent, "RequestComplete");
            Assert.Equal(request, _requestComplete_Request);
            Assert.Equal(BuildResultCode.Success, _requestComplete_Result.OverallResult);
            Thread.Sleep(250);
            VerifyEngineStatus(BuildRequestEngineStatus.Idle);
        }

        [Fact]
        public void TestShutdown()
        {
        }

        private BuildRequest CreateNewBuildRequest(int configurationId, string[] targets)
        {
            BuildRequest request = new BuildRequest(1 /* submission id */, _nodeRequestId++, configurationId, targets, null, BuildEventContext.Invalid, null);
            request.GlobalRequestId = _globalRequestId++;
            return request;
        }

        private void WaitForEngineStatus(BuildRequestEngineStatus expectedStatus)
        {
            DateTime time = DateTime.Now;
            while (DateTime.Now - time > new TimeSpan(0, 0, 5))
            {
                WaitForEvent(_engineStatusChangedEvent, "EngineStatusChanged");
                if (expectedStatus == _engineStatusChanged_Status)
                {
                    return;
                }
            }
            Assert.True(false, "Engine failed to change to status " + expectedStatus);
        }

        private void VerifyEngineStatus(BuildRequestEngineStatus expectedStatus)
        {
            IBuildRequestEngine engine = (IBuildRequestEngine)_host.GetComponent(BuildComponentType.RequestEngine);

            if (engine.Status == expectedStatus)
            {
                return;
            }

            WaitForEvent(_engineStatusChangedEvent, "EngineStatusChanged");
            BuildRequestEngineStatus engineStatus = engine.Status;
            Assert.Equal(expectedStatus, engineStatus);
        }

        private void WaitForEvent(WaitHandle evt, string eventName)
        {
            WaitHandle[] events = new WaitHandle[2] { _engineExceptionEvent, evt };
            int index = WaitHandle.WaitAny(events, 5000);
            if (WaitHandle.WaitTimeout == index)
            {
                Assert.True(false, "Did not receive " + eventName + " callback before the timeout expired.");
            }
            else if (index == 0)
            {
                Assert.True(false, "Received engine exception " + _engineException_Exception);
            }
        }

        /// <summary>
        /// Callback for event raised when a build request is completed
        /// </summary>
        /// <param name="request">The request which completed</param>
        /// <param name="result">The result for the request</param>
        private void Engine_RequestComplete(BuildRequest request, BuildResult result)
        {
            _requestComplete_Request = request;
            _requestComplete_Result = result;
            _requestCompleteEvent.Set();
        }

        /// <summary>
        /// Callback for event raised when a request is resumed
        /// </summary>
        /// <param name="request">The request being resumed</param>
        private void Engine_RequestResumed(BuildRequest request)
        {
            _requestResumed_Request = request;
            _requestResumedEvent.Set();
        }

        /// <summary>
        /// Callback for event raised when a new build request is generated by an MSBuild callback
        /// </summary>
        /// <param name="request">The new build request</param>
        private void Engine_NewRequest(BuildRequestBlocker blocker)
        {
            _newRequest_Request = blocker;
            _newRequestEvent.Set();
        }

        /// <summary>
        /// Callback for event raised when the build request engine's status changes.
        /// </summary>
        /// <param name="newStatus">The new status for the engine</param>
        private void Engine_EngineStatusChanged(BuildRequestEngineStatus newStatus)
        {
            _engineStatusChanged_Status = newStatus;
            _engineStatusChangedEvent.Set();
        }

        /// <summary>
        /// Callback for event raised when a new configuration needs an ID resolved.
        /// </summary>
        /// <param name="config">The configuration needing an ID</param>
        private void Engine_NewConfigurationRequest(BuildRequestConfiguration config)
        {
            _newConfiguration_Config = config;
            _newConfigurationEvent.Set();
        }

        /// <summary>
        /// Callback for event raised when a new configuration needs an ID resolved.
        /// </summary>
        /// <param name="config">The configuration needing an ID</param>
        private void Engine_Exception(Exception e)
        {
            _engineException_Exception = e;
            _engineExceptionEvent.Set();
        }
    }
}
