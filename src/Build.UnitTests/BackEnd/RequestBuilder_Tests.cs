// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Xml;
using System.Text;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.BackEnd;
using Microsoft.Build.BackEnd.Logging;
using Microsoft.Build.Shared;
using Microsoft.Build.Collections;
using Microsoft.Build.Execution;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Unittest;
using TaskItem = Microsoft.Build.Execution.ProjectItemInstance.TaskItem;
using Xunit;

namespace Microsoft.Build.UnitTests.BackEnd
{
    using InvalidProjectFileException = Microsoft.Build.Exceptions.InvalidProjectFileException;
    using System.Threading.Tasks;

    public class RequestBuilder_Tests : IDisposable
    {
        private AutoResetEvent _newBuildRequestsEvent;
        private BuildRequestEntry _newBuildRequests_Entry;
        private FullyQualifiedBuildRequest[] _newBuildRequests_FQRequests;
        private BuildRequest[] _newBuildRequests_BuildRequests;
        private AutoResetEvent _buildRequestCompletedEvent;
        private BuildRequestEntry _buildRequestCompleted_Entry;

        private MockHost _host;
        private IRequestBuilder _requestBuilder;
        private int _nodeRequestId;

        private string _originalWorkingDirectory;

        #pragma warning disable xUnit1013

        public void LoggingException(Exception e)
        {
        }

        #pragma warning restore xUnit1013

        public RequestBuilder_Tests()
        {
            _originalWorkingDirectory = Directory.GetCurrentDirectory();
            _nodeRequestId = 1;
            _host = new MockHost();
            _host.RequestBuilder = new RequestBuilder();
            ((IBuildComponent)_host.RequestBuilder).InitializeComponent(_host);

            _host.OnLoggingThreadException += this.LoggingException;

            _newBuildRequestsEvent = new AutoResetEvent(false);
            _buildRequestCompletedEvent = new AutoResetEvent(false);

            _requestBuilder = (IRequestBuilder)_host.GetComponent(BuildComponentType.RequestBuilder);
            _requestBuilder.OnBuildRequestCompleted += this.BuildRequestCompletedCallback;
            _requestBuilder.OnNewBuildRequests += this.NewBuildRequestsCallback;
        }

        public void Dispose()
        {
            ((IBuildComponent)_requestBuilder).ShutdownComponent();
            _host = null;

            // Normally, RequestBuilder ensures that this gets reset before completing
            // requests, but we call it in odd ways here so restore it manually
            // to keep the overall test invariant happy.
            Directory.SetCurrentDirectory(_originalWorkingDirectory);
        }

        [Fact]
        public void TestSimpleBuildRequest()
        {
            BuildRequestConfiguration configuration = CreateTestProject(1);
            try
            {
                TestTargetBuilder targetBuilder = (TestTargetBuilder)_host.GetComponent(BuildComponentType.TargetBuilder);
                IConfigCache configCache = (IConfigCache)_host.GetComponent(BuildComponentType.ConfigCache);

                configCache.AddConfiguration(configuration);

                BuildRequest request = CreateNewBuildRequest(1, new string[1] { "target1" });
                BuildRequestEntry entry = new BuildRequestEntry(request, configuration);
                BuildResult result = new BuildResult(request);
                result.AddResultsForTarget("target1", GetEmptySuccessfulTargetResult());
                targetBuilder.SetResultsToReturn(result);

                _requestBuilder.BuildRequest(GetNodeLoggingContext(), entry);

                WaitForEvent(_buildRequestCompletedEvent, "Build Request Completed");
                Assert.Equal(BuildRequestEntryState.Complete, entry.State);
                Assert.Equal(entry, _buildRequestCompleted_Entry);
                Assert.Equal(BuildResultCode.Success, _buildRequestCompleted_Entry.Result.OverallResult);
            }
            finally
            {
                DeleteTestProject(configuration);
            }
        }

        [Fact]
        public void TestSimpleBuildRequestCancelled()
        {
            BuildRequestConfiguration configuration = CreateTestProject(1);
            try
            {
                TestTargetBuilder targetBuilder = (TestTargetBuilder)_host.GetComponent(BuildComponentType.TargetBuilder);
                IConfigCache configCache = (IConfigCache)_host.GetComponent(BuildComponentType.ConfigCache);

                configCache.AddConfiguration(configuration);

                BuildRequest request = CreateNewBuildRequest(1, new string[1] { "target1" });
                BuildRequestEntry entry = new BuildRequestEntry(request, configuration);
                BuildResult result = new BuildResult(request);
                result.AddResultsForTarget("target1", GetEmptySuccessfulTargetResult());
                targetBuilder.SetResultsToReturn(result);

                _requestBuilder.BuildRequest(GetNodeLoggingContext(), entry);

                Thread.Sleep(500);
                _requestBuilder.CancelRequest();

                WaitForEvent(_buildRequestCompletedEvent, "Build Request Completed");
                Assert.Equal(BuildRequestEntryState.Complete, entry.State);
                Assert.Equal(entry, _buildRequestCompleted_Entry);
                Assert.Equal(BuildResultCode.Failure, _buildRequestCompleted_Entry.Result.OverallResult);
            }
            finally
            {
                DeleteTestProject(configuration);
            }
        }

        [Fact]
        public void TestRequestWithReference()
        {
            BuildRequestConfiguration configuration = CreateTestProject(1);
            try
            {
                TestTargetBuilder targetBuilder = (TestTargetBuilder)_host.GetComponent(BuildComponentType.TargetBuilder);
                IConfigCache configCache = (IConfigCache)_host.GetComponent(BuildComponentType.ConfigCache);
                FullyQualifiedBuildRequest[] newRequest = new FullyQualifiedBuildRequest[1] { new FullyQualifiedBuildRequest(configuration, new string[1] { "testTarget2" }, true) };
                targetBuilder.SetNewBuildRequests(newRequest);
                configCache.AddConfiguration(configuration);

                BuildRequest request = CreateNewBuildRequest(1, new string[1] { "target1" });
                BuildRequestEntry entry = new BuildRequestEntry(request, configuration);
                BuildResult result = new BuildResult(request);
                result.AddResultsForTarget("target1", GetEmptySuccessfulTargetResult());
                targetBuilder.SetResultsToReturn(result);

                _requestBuilder.BuildRequest(GetNodeLoggingContext(), entry);
                WaitForEvent(_newBuildRequestsEvent, "New Build Requests");
                Assert.Equal(_newBuildRequests_Entry, entry);
                ObjectModelHelpers.AssertArrayContentsMatch(_newBuildRequests_FQRequests, newRequest);

                BuildResult newResult = new BuildResult(_newBuildRequests_BuildRequests[0]);
                newResult.AddResultsForTarget("testTarget2", GetEmptySuccessfulTargetResult());
                entry.ReportResult(newResult);
                _requestBuilder.ContinueRequest();

                WaitForEvent(_buildRequestCompletedEvent, "Build Request Completed");
                Assert.Equal(BuildRequestEntryState.Complete, entry.State);
                Assert.Equal(entry, _buildRequestCompleted_Entry);
                Assert.Equal(BuildResultCode.Success, _buildRequestCompleted_Entry.Result.OverallResult);
            }
            finally
            {
                DeleteTestProject(configuration);
            }
        }

        [Fact]
        public void TestRequestWithReferenceCancelled()
        {
            BuildRequestConfiguration configuration = CreateTestProject(1);
            try
            {
                TestTargetBuilder targetBuilder = (TestTargetBuilder)_host.GetComponent(BuildComponentType.TargetBuilder);
                IConfigCache configCache = (IConfigCache)_host.GetComponent(BuildComponentType.ConfigCache);
                FullyQualifiedBuildRequest[] newRequest = new FullyQualifiedBuildRequest[1] { new FullyQualifiedBuildRequest(configuration, new string[1] { "testTarget2" }, true) };
                targetBuilder.SetNewBuildRequests(newRequest);
                configCache.AddConfiguration(configuration);

                BuildRequest request = CreateNewBuildRequest(1, new string[1] { "target1" });
                BuildRequestEntry entry = new BuildRequestEntry(request, configuration);
                BuildResult result = new BuildResult(request);
                result.AddResultsForTarget("target1", GetEmptySuccessfulTargetResult());
                targetBuilder.SetResultsToReturn(result);

                _requestBuilder.BuildRequest(GetNodeLoggingContext(), entry);
                WaitForEvent(_newBuildRequestsEvent, "New Build Requests");
                Assert.Equal(_newBuildRequests_Entry, entry);
                ObjectModelHelpers.AssertArrayContentsMatch(_newBuildRequests_FQRequests, newRequest);

                BuildResult newResult = new BuildResult(_newBuildRequests_BuildRequests[0]);
                newResult.AddResultsForTarget("testTarget2", GetEmptySuccessfulTargetResult());
                entry.ReportResult(newResult);

                _requestBuilder.ContinueRequest();
                Thread.Sleep(500);
                _requestBuilder.CancelRequest();

                WaitForEvent(_buildRequestCompletedEvent, "Build Request Completed");
                Assert.Equal(BuildRequestEntryState.Complete, entry.State);
                Assert.Equal(entry, _buildRequestCompleted_Entry);
                Assert.Equal(BuildResultCode.Failure, _buildRequestCompleted_Entry.Result.OverallResult);
            }
            finally
            {
                DeleteTestProject(configuration);
            }
        }

        [Fact]
        public void TestMissingProjectFile()
        {
            TestTargetBuilder targetBuilder = (TestTargetBuilder)_host.GetComponent(BuildComponentType.TargetBuilder);
            IConfigCache configCache = (IConfigCache)_host.GetComponent(BuildComponentType.ConfigCache);
            BuildRequestConfiguration configuration = new BuildRequestConfiguration(1, new BuildRequestData("testName", new Dictionary<string, string>(), "3.5", new string[0], null), "2.0");
            configCache.AddConfiguration(configuration);

            BuildRequest request = CreateNewBuildRequest(1, new string[1] { "target1" });
            BuildRequestEntry entry = new BuildRequestEntry(request, configuration);
            _requestBuilder.BuildRequest(GetNodeLoggingContext(), entry);
            WaitForEvent(_buildRequestCompletedEvent, "Build Request Completed");
            Assert.Equal(BuildRequestEntryState.Complete, entry.State);
            Assert.Equal(entry, _buildRequestCompleted_Entry);
            Assert.Equal(BuildResultCode.Failure, _buildRequestCompleted_Entry.Result.OverallResult);
            Assert.Equal(typeof(InvalidProjectFileException), _buildRequestCompleted_Entry.Result.Exception.GetType());
        }

        private BuildRequestConfiguration CreateTestProject(int configId)
        {
            string projectFileContents = @"
                <Project ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`http://schemas.microsoft.com/developer/msbuild/2003`>

                    <ItemGroup>
                        <Compile Include=`b.cs` />
                        <Compile Include=`c.cs` />
                    </ItemGroup>

                    <ItemGroup>
                        <Reference Include=`System` />
                    </ItemGroup>

                    <Target Name=`Build` />

                </Project>
                ";

            string projectFile = GetTestProjectFile(configId);
            File.WriteAllText(projectFile, projectFileContents.Replace('`', '"'));

            string defaultToolsVersion = null;
            defaultToolsVersion = FrameworkLocationHelper.PathToDotNetFrameworkV20 == null
                                      ? ObjectModelHelpers.MSBuildDefaultToolsVersion
                                      : "2.0";

            BuildRequestConfiguration config = new BuildRequestConfiguration(
                configId,
                new BuildRequestData(
                    projectFile,
                    new Dictionary<string, string>(),
                    ObjectModelHelpers.MSBuildDefaultToolsVersion,
                    new string[0],
                    null),
                defaultToolsVersion);
            return config;
        }

        private void DeleteTestProject(BuildRequestConfiguration config)
        {
            string fileName = GetTestProjectFile(config.ConfigurationId);
            if (File.Exists(fileName))
            {
                File.Delete(fileName);
            }
        }

        private string GetTestProjectFile(int configId)
        {
            return Path.GetTempPath() + "testProject" + configId + ".proj";
        }

        private void NewBuildRequestsCallback(BuildRequestEntry entry, FullyQualifiedBuildRequest[] requests)
        {
            _newBuildRequests_FQRequests = requests;
            _newBuildRequests_BuildRequests = new BuildRequest[requests.Length];
            _newBuildRequests_Entry = entry;

            int index = 0;
            foreach (FullyQualifiedBuildRequest request in requests)
            {
                IConfigCache configCache = (IConfigCache)_host.GetComponent(BuildComponentType.ConfigCache);
                BuildRequestConfiguration matchingConfig = configCache.GetMatchingConfiguration(request.Config);
                BuildRequest newRequest = CreateNewBuildRequest(matchingConfig.ConfigurationId, request.Targets);

                entry.WaitForResult(newRequest);
                _newBuildRequests_BuildRequests[index++] = newRequest;
            }
            _newBuildRequestsEvent.Set();
        }

        private void BuildRequestCompletedCallback(BuildRequestEntry entry)
        {
            _buildRequestCompleted_Entry = entry;
            _buildRequestCompletedEvent.Set();
        }

        private BuildRequest CreateNewBuildRequest(int configurationId, string[] targets)
        {
            return new BuildRequest(1 /* submissionId */, _nodeRequestId++, configurationId, targets, null, BuildEventContext.Invalid, null);
        }

        private TargetResult GetEmptySuccessfulTargetResult()
        {
            return new TargetResult(new TaskItem[0] { }, new WorkUnitResult(WorkUnitResultCode.Success, WorkUnitActionCode.Continue, null));
        }

        private void WaitForEvent(WaitHandle evt, string eventName)
        {
            if (!evt.WaitOne(5000))
            {
                Assert.True(false, "Did not receive " + eventName + " callback before the timeout expired.");
            }
        }

        private NodeLoggingContext GetNodeLoggingContext()
        {
            return new NodeLoggingContext(_host, 1, false);
        }
    }

    internal class TestTargetBuilder : ITargetBuilder, IBuildComponent
    {
        private IBuildComponentHost _host;
        private IResultsCache _cache;
        private FullyQualifiedBuildRequest[] _newRequests;
        private IRequestBuilderCallback _requestBuilderCallback;

        internal void SetResultsToReturn(BuildResult result)
        {
            _cache.AddResult(result);
        }

        internal void SetNewBuildRequests(FullyQualifiedBuildRequest[] requests)
        {
            _newRequests = requests;
        }

        #region ITargetBuilder Members

        public Task<BuildResult> BuildTargets(ProjectLoggingContext loggingContext, BuildRequestEntry entry, IRequestBuilderCallback callback, string[] targets, Lookup baseLookup, CancellationToken cancellationToken)
        {
            _requestBuilderCallback = callback;

            if (cancellationToken.WaitHandle.WaitOne(1500))
            {
                BuildResult result = new BuildResult(entry.Request);
                foreach (string target in targets)
                {
                    result.AddResultsForTarget(target, BuildResultUtilities.GetEmptyFailingTargetResult());
                }
                return Task<BuildResult>.FromResult(result);
            }

            if (null != _newRequests)
            {
                string[] projectFiles = new string[_newRequests.Length];
                PropertyDictionary<ProjectPropertyInstance>[] properties = new PropertyDictionary<ProjectPropertyInstance>[_newRequests.Length];
                string[] toolsVersions = new string[_newRequests.Length];

                for (int i = 0; i < projectFiles.Length; ++i)
                {
                    projectFiles[i] = _newRequests[i].Config.ProjectFullPath;
                    properties[i] = new PropertyDictionary<ProjectPropertyInstance>(_newRequests[i].Config.GlobalProperties);
                    toolsVersions[i] = _newRequests[i].Config.ToolsVersion;
                }

                _requestBuilderCallback.BuildProjects(projectFiles, properties, toolsVersions, _newRequests[0].Targets, _newRequests[0].ResultsNeeded);

                if (cancellationToken.WaitHandle.WaitOne(1500))
                {
                    BuildResult result = new BuildResult(entry.Request);
                    foreach (string target in targets)
                    {
                        result.AddResultsForTarget(target, BuildResultUtilities.GetEmptyFailingTargetResult());
                    }
                    return Task<BuildResult>.FromResult(result);
                }
            }

            return Task<BuildResult>.FromResult(_cache.GetResultForRequest(entry.Request));
        }

        #endregion

        #region IBuildComponent Members

        public void InitializeComponent(IBuildComponentHost host)
        {
            _host = host;
            _cache = new ResultsCache();
        }

        public void ShutdownComponent()
        {
            _host = null;
            _cache = null;
        }
        #endregion
    }
}
