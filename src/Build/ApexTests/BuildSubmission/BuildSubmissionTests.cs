//-----------------------------------------------------------------------
// <copyright file="BuildSubmissionTests.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
// <summary>Tests to cover Build submission feature in MSBuild.</summary>
//-----------------------------------------------------------------------
namespace Microsoft.Build.ApexTests.Tests
{
    using System;
    using System.Diagnostics;
    using System.Globalization;
    using System.IO;
    using System.Threading;
    using Microsoft.Build.ApexTests.Library;
    using Microsoft.Build.Execution;
    using Microsoft.Test.Apex;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// BuildSubmission tests.
    /// </summary>
    [TestClass]
    public class BuildSubmissionTests : ApexTest
    {
        /// <summary>
        /// Simulated time in mili-seconds a normal project should execute for.
        /// </summary>
        private const int NormalProjectExecutionTime = 1 * 1000;

        /// <summary>
        /// Simulated time in mili-seconds a long project should execute for.
        /// </summary>
        private const int LongProjectExecutionTime = 3 * 1000;

        /// <summary>
        /// Total time in mili-seconds the entire build should complete in.
        /// </summary>
        private const int DefaultBuildCompletionTimeout = 60 * 1000;

        /// <summary>
        /// Default node count for build is 1.
        /// </summary>
        private const int DefaultNodeCount = 1;

        /// <summary>
        /// When multiple nodes are specified the value to use will be 3.
        /// </summary>
        private const int MultipleNodeCount = 3;

        /// <summary>
        /// Unlimited memory usage when building projects.
        /// </summary>
        private const int DefaultMemoryLimit = 0;

        /// <summary>
        /// No node re-use by default;
        /// </summary>
        private const bool DefaultNodeReUseAction = false;

        /// <summary>
        /// Async action should wait for completion before returning control back to the test.
        /// </summary>
        private const bool WaitForCompletion = true;

        /// <summary>
        /// Minimim number of BuildRequests to create.
        /// </summary>
        private const int MinimimBuildRequestCount = 3;

        /// <summary>
        /// Maximum number of BuildRequests to create.
        /// </summary>
        private const int MaximumBuildRequestCount = 5;

        /// <summary>
        /// Used to generate random numbers.
        /// </summary>
        private Random randomNumbers;

        /// <summary>
        /// TestConfiguration parameters to use when creating the container.
        /// </summary>
        private BuildManagerContainerConfiguration testConfiguration = null;

        /// <summary>
        /// Test container which holds the default test extensions.
        /// </summary>
        private TestExtensionContainer testExtensionContainer = null;

        /// <summary>
        /// Build Manager to use for the tests.
        /// </summary>
        private BuildManagerTestExtension buildManagerTestExtension = null;

        /// <summary>
        /// Constructor.
        /// </summary>
        public BuildSubmissionTests()
        {
            this.randomNumbers = new Random((int)DateTime.Now.Ticks);
        }

        /// <summary>
        /// Method called before each test executes.
        /// </summary>
        [TestInitialize]
        public override void TestInitialize()
        {
            base.TestInitialize();
            this.testConfiguration = BuildManagerContainerConfiguration.Default;
            this.testExtensionContainer = this.Operations.GenerateContainer(this.testConfiguration);
            this.buildManagerTestExtension = this.testExtensionContainer.GetFirstTestExtension<BuildManagerTestExtension>();
        }

        /// <summary>
        /// Method called after each test completes.
        /// </summary>
        [TestCleanup]
        public override void TestCleanup()
        {
            base.TestCleanup();
            Microsoft.Build.Evaluation.ProjectCollection.GlobalProjectCollection.UnloadAllProjects();
            this.buildManagerTestExtension.Dispose();
            this.buildManagerTestExtension = null;
            this.testConfiguration.Dispose();
            this.testConfiguration = null;
            this.testExtensionContainer = null;
        }

        /// <summary>
        /// A simple successful single build of 1 project using Build.
        /// </summary>
        [TestMethod]
        [Description("A simple successful single build of 1 project using Build.")]
        public void SimpleSuccessfulBuild()
        {
            BuildRequestData data = this.buildManagerTestExtension.CreateBuildRequestData(BuildSubmissionTests.NormalProjectExecutionTime);
            BuildResultTestExtension resultTestExtension = this.buildManagerTestExtension.Build(BuildManagerTestExtension.DefaultBuildParameters, data);
            resultTestExtension.Verify.BuildSucceeded();
        }

        /// <summary>
        /// Multiple simple successful build of 1 project using Build with different parameters.
        /// </summary>
        [TestMethod]
        [Description("Multiple simple successful build of 1 project using Build with different parameters.")]
        public void MultipleSuccessfulBuildsWithDifferentParameters()
        {
            BuildRequestData requestData1 = this.buildManagerTestExtension.CreateBuildRequestData(BuildSubmissionTests.NormalProjectExecutionTime);
            BuildRequestData requestData2 = this.buildManagerTestExtension.CreateBuildRequestData(BuildSubmissionTests.NormalProjectExecutionTime);
            AsyncBuildRequestStatus[] requestData1Status = null;
            AsyncBuildRequestStatus[] requestData2Status = null;
            ConfigurationCacheTestExtension configurationCachetestExtension = this.testExtensionContainer.GetFirstTestExtension<ConfigurationCacheTestExtension>();

            this.buildManagerTestExtension.BeginBuild(BuildManagerTestExtension.CreateBuildParameters(BuildSubmissionTests.DefaultNodeReUseAction, BuildSubmissionTests.DefaultNodeCount, BuildSubmissionTests.DefaultMemoryLimit, "2.0"));
            bool success = this.buildManagerTestExtension.ExecuteAsyncBuildRequests(new BuildRequestData[] { requestData1 }, BuildSubmissionTests.DefaultBuildCompletionTimeout, BuildSubmissionTests.WaitForCompletion, out requestData1Status);
            Verify.IsTrue(success, "Submissions should have completed in the specified 60 seconds timeout.");
            requestData1Status[0].SubmissionTestExtension.BuildResultTestExtension.Verify.BuildSucceeded();
            configurationCachetestExtension.Verify.CacheContainsConfigurationForBuildRequest(requestData1, "2.0");
            this.buildManagerTestExtension.EndBuild();

            this.buildManagerTestExtension.BeginBuild(BuildManagerTestExtension.CreateBuildParameters(BuildSubmissionTests.DefaultNodeReUseAction, BuildSubmissionTests.DefaultNodeCount, BuildSubmissionTests.DefaultMemoryLimit, "4.0"));
            success = this.buildManagerTestExtension.ExecuteAsyncBuildRequests(new BuildRequestData[] { requestData2 }, BuildSubmissionTests.DefaultBuildCompletionTimeout, BuildSubmissionTests.WaitForCompletion, out requestData2Status);
            Verify.IsTrue(success, "Submissions should have completed in the specified 60 seconds timeout.");
            requestData2Status[0].SubmissionTestExtension.BuildResultTestExtension.Verify.BuildSucceeded();
            configurationCachetestExtension.Verify.CacheContainsConfigurationForBuildRequest(requestData2, "4.0");
            this.buildManagerTestExtension.EndBuild();
        }

        /// <summary>
        /// Multiple simple successful build of projects using Build with same parameters.
        /// </summary>
        [TestMethod]
        [Description("Multiple simple successful build of projects using Build with same parameters.")]
        public void MultipleSuccessfulBuildsWithSameParameters()
        {
            BuildRequestData requestData1 = this.buildManagerTestExtension.CreateBuildRequestData(BuildSubmissionTests.NormalProjectExecutionTime);
            BuildRequestData requestData2 = this.buildManagerTestExtension.CreateBuildRequestData(BuildSubmissionTests.NormalProjectExecutionTime);
            AsyncBuildRequestStatus[] requestData1Status = null;
            AsyncBuildRequestStatus[] requestData2Status = null;
            ConfigurationCacheTestExtension configurationCachetestExtension = this.testExtensionContainer.GetFirstTestExtension<ConfigurationCacheTestExtension>();

            this.buildManagerTestExtension.BeginBuild(BuildManagerTestExtension.CreateBuildParameters(BuildSubmissionTests.DefaultNodeReUseAction, BuildSubmissionTests.DefaultNodeCount, BuildSubmissionTests.DefaultMemoryLimit, "4.0"));
            bool success = this.buildManagerTestExtension.ExecuteAsyncBuildRequests(new BuildRequestData[] { requestData1 }, BuildSubmissionTests.DefaultBuildCompletionTimeout, BuildSubmissionTests.WaitForCompletion, out requestData1Status);
            Verify.IsTrue(success, "Submissions should have completed in the specified 60 seconds timeout.");
            requestData1Status[0].SubmissionTestExtension.BuildResultTestExtension.Verify.BuildSucceeded();
            configurationCachetestExtension.Verify.CacheContainsConfigurationForBuildRequest(requestData1, "4.0");
            this.buildManagerTestExtension.EndBuild();

            this.buildManagerTestExtension.BeginBuild(BuildManagerTestExtension.CreateBuildParameters(BuildSubmissionTests.DefaultNodeReUseAction, BuildSubmissionTests.DefaultNodeCount, BuildSubmissionTests.DefaultMemoryLimit, "4.0"));
            success = this.buildManagerTestExtension.ExecuteAsyncBuildRequests(new BuildRequestData[] { requestData2 }, BuildSubmissionTests.DefaultBuildCompletionTimeout, BuildSubmissionTests.WaitForCompletion, out requestData2Status);
            Verify.IsTrue(success, "Submissions should have completed in the specified 60 seconds timeout.");
            requestData2Status[0].SubmissionTestExtension.BuildResultTestExtension.Verify.BuildSucceeded();
            configurationCachetestExtension.Verify.CacheContainsConfigurationForBuildRequest(requestData2, "4.0");
            this.buildManagerTestExtension.EndBuild();
        }

        /// <summary>
        /// Asynchronous build with multiple submissions on multiple nodes.
        /// </summary>
        [TestMethod]
        [Description("Asynchronous build with multiple submissions on multiple nodes.")]
        public void AsyncBuildWithMultipleSubmissionsOnMultipleNodes()
        {
            BuildRequestData[] buildRequests = GenerateBuildRequests(BuildSubmissionTests.NormalProjectExecutionTime);

            ConfigurationCacheTestExtension configurationCachetestExtension = this.testExtensionContainer.GetFirstTestExtension<ConfigurationCacheTestExtension>();
            this.buildManagerTestExtension.BeginBuild(BuildManagerTestExtension.CreateBuildParameters(BuildSubmissionTests.DefaultNodeReUseAction, BuildSubmissionTests.MultipleNodeCount, BuildSubmissionTests.DefaultMemoryLimit, "4.0"));
            AsyncBuildRequestStatus[] requestsStatus = null;
            bool success = this.buildManagerTestExtension.ExecuteAsyncBuildRequests(buildRequests, BuildSubmissionTests.DefaultBuildCompletionTimeout, BuildSubmissionTests.WaitForCompletion, out requestsStatus);
            Verify.IsTrue(success, "Submissions should have completed in the specified 60 seconds timeout.");
            Verify.IsTrue(requestsStatus.Length == buildRequests.Length, "Number of submission should match the number of requests.");
            for (int j = 0; j < buildRequests.Length; j++)
            {
                requestsStatus[j].SubmissionTestExtension.BuildResultTestExtension.Verify.BuildSucceeded();
                configurationCachetestExtension.Verify.CacheContainsConfigurationForBuildRequest(buildRequests[j], "4.0");
            }

            this.buildManagerTestExtension.EndBuild(); 
        }

        /// <summary>
        /// Asynchronous build with multiple submissions on single nodes.
        /// </summary>
        [TestMethod]
        [Description("Asynchronous build with multiple submissions on single nodes.")]
        public void AsyncBuildWithMultipleSubmissionsOnSingleNodes()
        {
            BuildRequestData[] buildRequests = GenerateBuildRequests(BuildSubmissionTests.NormalProjectExecutionTime);

            ConfigurationCacheTestExtension configurationCachetestExtension = this.testExtensionContainer.GetFirstTestExtension<ConfigurationCacheTestExtension>();
            this.buildManagerTestExtension.BeginBuild(BuildManagerTestExtension.CreateBuildParameters(BuildSubmissionTests.DefaultNodeReUseAction, BuildSubmissionTests.DefaultNodeCount, BuildSubmissionTests.DefaultMemoryLimit, "4.0"));
            AsyncBuildRequestStatus[] requestsStatus = null;
            bool success = this.buildManagerTestExtension.ExecuteAsyncBuildRequests(buildRequests, BuildSubmissionTests.DefaultBuildCompletionTimeout, BuildSubmissionTests.WaitForCompletion, out requestsStatus);
            Verify.IsTrue(success, "Submissions should have completed in the specified 60 seconds timeout.");
            Verify.IsTrue(requestsStatus.Length == buildRequests.Length, "Number of submission should match the number of requests.");
            for (int j = 0; j < buildRequests.Length; j++)
            {
                requestsStatus[j].SubmissionTestExtension.BuildResultTestExtension.Verify.BuildSucceeded();
                configurationCachetestExtension.Verify.CacheContainsConfigurationForBuildRequest(buildRequests[j], "4.0");
            }

            this.buildManagerTestExtension.EndBuild();
        }

        /// <summary>
        /// Asynchronous build with multiple submissions and then cancellation on single node.
        /// </summary>
        [TestMethod]
        [Description("Asynchronous build with multiple submissions and then cancellations on a single node.")]
        public void AsyncBuildWithMultipleSubmissionsAndThenCancelOnSingleNode()
        {
            BuildRequestData[] buildRequests = GenerateBuildRequests(BuildSubmissionTests.LongProjectExecutionTime);

            ConfigurationCacheTestExtension configurationCachetestExtension = this.testExtensionContainer.GetFirstTestExtension<ConfigurationCacheTestExtension>();
            ResultsCacheTestExtension resultsCacheTestExtension = this.testExtensionContainer.GetFirstTestExtension<ResultsCacheTestExtension>();
            this.buildManagerTestExtension.BeginBuild(BuildManagerTestExtension.CreateBuildParameters(BuildSubmissionTests.DefaultNodeReUseAction, BuildSubmissionTests.DefaultNodeCount, BuildSubmissionTests.DefaultMemoryLimit, "4.0"));
            AsyncBuildRequestStatus[] requestsStatus = null;
            this.buildManagerTestExtension.ExecuteAsyncBuildRequests(buildRequests, BuildSubmissionTests.DefaultBuildCompletionTimeout, !BuildSubmissionTests.WaitForCompletion, out requestsStatus);
            this.buildManagerTestExtension.CancelAllSubmissions();
            
            for (int j = 0; j < buildRequests.Length; j++)
            {
                Verify.IsTrue(BuildManagerTestExtension.Wait(requestsStatus[j].SubmissionCompletedEvent, BuildSubmissionTests.DefaultBuildCompletionTimeout), "Submissions should have completed in the specified 60 seconds timeout.");
                requestsStatus[j].SubmissionCompletedEvent.Close();
                requestsStatus[j].SubmissionTestExtension.BuildResultTestExtension.Verify.BuildWasAborted();
                BuildResultTestExtension resultFromCacheTestExtension = resultsCacheTestExtension.GetResultFromCache(requestsStatus[j].SubmissionTestExtension.ConfigurationIdForSubmission);
                resultFromCacheTestExtension.Verify.BuildWasAborted();
            }

            this.buildManagerTestExtension.EndBuild();
        }

        /// <summary>
        /// Asynchronous build with multiple submissions and then cancellation on multiple nodes.
        /// </summary>
        [TestMethod]
        [Description("Asynchronous build with multiple submissions and then cancellations on a multiple node.")]
        public void AsyncBuildWithMultipleSubmissionsAndThenCancelOnMultipleNodes()
        {
            BuildRequestData[] buildRequests = GenerateBuildRequests(BuildSubmissionTests.LongProjectExecutionTime);

            ConfigurationCacheTestExtension configurationCachetestExtension = this.testExtensionContainer.GetFirstTestExtension<ConfigurationCacheTestExtension>();
            ResultsCacheTestExtension resultsCacheTestExtension = this.testExtensionContainer.GetFirstTestExtension<ResultsCacheTestExtension>();
            this.buildManagerTestExtension.BeginBuild(BuildManagerTestExtension.CreateBuildParameters(BuildSubmissionTests.DefaultNodeReUseAction, BuildSubmissionTests.MultipleNodeCount, BuildSubmissionTests.DefaultMemoryLimit, "4.0"));
            AsyncBuildRequestStatus[] requestsStatus = null;
            this.buildManagerTestExtension.ExecuteAsyncBuildRequests(buildRequests, BuildSubmissionTests.DefaultBuildCompletionTimeout, !BuildSubmissionTests.WaitForCompletion, out requestsStatus);
            this.buildManagerTestExtension.CancelAllSubmissions();

            for (int j = 0; j < buildRequests.Length; j++)
            {
                Verify.IsTrue(BuildManagerTestExtension.Wait(requestsStatus[j].SubmissionCompletedEvent, BuildSubmissionTests.DefaultBuildCompletionTimeout), "Submissions should have completed in the specified 60 seconds timeout.");
                requestsStatus[j].SubmissionCompletedEvent.Close();
                requestsStatus[j].SubmissionTestExtension.BuildResultTestExtension.Verify.BuildWasAborted();
                BuildResultTestExtension resultFromCacheTestExtension = resultsCacheTestExtension.GetResultFromCache(requestsStatus[j].SubmissionTestExtension.ConfigurationIdForSubmission);
                resultFromCacheTestExtension.Verify.BuildWasAborted();
            }

            this.buildManagerTestExtension.EndBuild();
        }

        /// <summary>
        /// Asynchronous build with multiple submissions and then EndBuild without completion.
        /// </summary>
        [TestMethod]
        [Description("Asynchronous build with multiple submissions and then EndBuild without completion.")]
        public void AsyncBuildWithMultipleSubmissionsAndThenEndWithoutCompletion()
        {
            BuildRequestData[] buildRequests = GenerateBuildRequests(BuildSubmissionTests.NormalProjectExecutionTime);

            ConfigurationCacheTestExtension configurationCachetestExtension = this.testExtensionContainer.GetFirstTestExtension<ConfigurationCacheTestExtension>();
            ResultsCacheTestExtension resultsCacheTestExtension = this.testExtensionContainer.GetFirstTestExtension<ResultsCacheTestExtension>();
            this.buildManagerTestExtension.BeginBuild(BuildManagerTestExtension.CreateBuildParameters(BuildSubmissionTests.DefaultNodeReUseAction, BuildSubmissionTests.DefaultNodeCount, BuildSubmissionTests.DefaultMemoryLimit, "4.0"));
            AsyncBuildRequestStatus[] requestsStatus = null;
            this.buildManagerTestExtension.ExecuteAsyncBuildRequests(buildRequests, BuildSubmissionTests.DefaultBuildCompletionTimeout, !BuildSubmissionTests.WaitForCompletion, out requestsStatus);
            this.buildManagerTestExtension.EndBuild();

            for (int j = 0; j < buildRequests.Length; j++)
            {
                Verify.IsTrue(BuildManagerTestExtension.Wait(requestsStatus[j].SubmissionCompletedEvent, BuildSubmissionTests.DefaultBuildCompletionTimeout), "Submissions should have completed in the specified 60 seconds timeout.");
                requestsStatus[j].SubmissionCompletedEvent.Close();
                requestsStatus[j].SubmissionTestExtension.BuildResultTestExtension.Verify.BuildSucceeded();
            }
        }

        /// <summary>
        /// Asynchronous build with multiple submissions Cancel and then new submissions.
        /// </summary>
        [TestMethod]
        [Description("Asynchronous build with multiple submissions, Cancel and then new submissions.")]
        public void AsyncBuildWithMultipleSubmissionsCancelAndNewSubmission()
        {
            BuildRequestData[] buildRequests = GenerateBuildRequests(BuildSubmissionTests.NormalProjectExecutionTime);
            ConfigurationCacheTestExtension configurationCachetestExtension = this.testExtensionContainer.GetFirstTestExtension<ConfigurationCacheTestExtension>();
            ResultsCacheTestExtension resultsCacheTestExtension = this.testExtensionContainer.GetFirstTestExtension<ResultsCacheTestExtension>();

            this.buildManagerTestExtension.BeginBuild(BuildManagerTestExtension.CreateBuildParameters(BuildSubmissionTests.DefaultNodeReUseAction, BuildSubmissionTests.MultipleNodeCount, BuildSubmissionTests.DefaultMemoryLimit, "4.0"));
            AsyncBuildRequestStatus[] requestsStatus = null;
            this.buildManagerTestExtension.ExecuteAsyncBuildRequests(buildRequests, BuildSubmissionTests.DefaultBuildCompletionTimeout, !BuildSubmissionTests.WaitForCompletion, out requestsStatus);
            this.buildManagerTestExtension.CancelAllSubmissions();

            for (int j = 0; j < buildRequests.Length; j++)
            {
                Verify.IsTrue(BuildManagerTestExtension.Wait(requestsStatus[j].SubmissionCompletedEvent, BuildSubmissionTests.DefaultBuildCompletionTimeout), "Submissions should have completed in the specified 60 seconds timeout.");
                requestsStatus[j].SubmissionCompletedEvent.Close();
                requestsStatus[j].SubmissionTestExtension.BuildResultTestExtension.Verify.BuildWasAborted();
                BuildResultTestExtension resultFromCacheTestExtension = resultsCacheTestExtension.GetResultFromCache(requestsStatus[j].SubmissionTestExtension.ConfigurationIdForSubmission);
                resultFromCacheTestExtension.Verify.BuildWasAborted();
            }

            this.buildManagerTestExtension.EndBuild();

            this.buildManagerTestExtension.BeginBuild(BuildManagerTestExtension.CreateBuildParameters(BuildSubmissionTests.DefaultNodeReUseAction, BuildSubmissionTests.MultipleNodeCount, BuildSubmissionTests.DefaultMemoryLimit, "4.0"));
            BuildRequestData[] buildRequestsAfterCancel = GenerateBuildRequests(BuildSubmissionTests.NormalProjectExecutionTime);
          
            AsyncBuildRequestStatus[] requestsStatusAfterCancel = null;
            bool success = this.buildManagerTestExtension.ExecuteAsyncBuildRequests(buildRequestsAfterCancel, BuildSubmissionTests.DefaultBuildCompletionTimeout, BuildSubmissionTests.WaitForCompletion, out requestsStatusAfterCancel);
            Verify.IsTrue(success, "Submissions should have completed in the specified 60 seconds timeout.");

            for (int k = 0; k < buildRequestsAfterCancel.Length; k++)
            {
                requestsStatusAfterCancel[k].SubmissionTestExtension.BuildResultTestExtension.Verify.BuildSucceeded();
            }

            this.buildManagerTestExtension.EndBuild();
        }

        /// <summary>
        /// Asynchronous build with multiple submissions Cancel and then re-submit the same requests.
        /// </summary>
        [TestMethod]
        [Description("Asynchronous build with multiple submissions, Cancel and then re-submit.")]
        public void AsyncBuildWithMultipleSubmissionsCancelAndReSubmit()
        {
            BuildRequestData[] buildRequests = GenerateBuildRequests(BuildSubmissionTests.NormalProjectExecutionTime);
            ConfigurationCacheTestExtension configurationCachetestExtension = this.testExtensionContainer.GetFirstTestExtension<ConfigurationCacheTestExtension>();
            ResultsCacheTestExtension resultsCacheTestExtension = this.testExtensionContainer.GetFirstTestExtension<ResultsCacheTestExtension>();

            this.buildManagerTestExtension.BeginBuild(BuildManagerTestExtension.CreateBuildParameters(BuildSubmissionTests.DefaultNodeReUseAction, BuildSubmissionTests.MultipleNodeCount, BuildSubmissionTests.DefaultMemoryLimit, "4.0"));
            AsyncBuildRequestStatus[] requestsStatus = null;
            this.buildManagerTestExtension.ExecuteAsyncBuildRequests(buildRequests, BuildSubmissionTests.DefaultBuildCompletionTimeout, !BuildSubmissionTests.WaitForCompletion, out requestsStatus);
            this.buildManagerTestExtension.CancelAllSubmissions();

            for (int j = 0; j < buildRequests.Length; j++)
            {
                Verify.IsTrue(BuildManagerTestExtension.Wait(requestsStatus[j].SubmissionCompletedEvent, BuildSubmissionTests.DefaultBuildCompletionTimeout), "Submissions should have completed in the specified 60 seconds timeout.");
                requestsStatus[j].SubmissionCompletedEvent.Close();
                requestsStatus[j].SubmissionTestExtension.BuildResultTestExtension.Verify.BuildWasAborted();
                BuildResultTestExtension resultFromCacheTestExtension = resultsCacheTestExtension.GetResultFromCache(requestsStatus[j].SubmissionTestExtension.ConfigurationIdForSubmission);
                resultFromCacheTestExtension.Verify.BuildWasAborted();
            }

            this.buildManagerTestExtension.EndBuild();

            this.buildManagerTestExtension.BeginBuild(BuildManagerTestExtension.CreateBuildParameters(BuildSubmissionTests.DefaultNodeReUseAction, BuildSubmissionTests.MultipleNodeCount, BuildSubmissionTests.DefaultMemoryLimit, "4.0"));
            AsyncBuildRequestStatus[] requestsStatusAfterCancel = null;
            bool success = this.buildManagerTestExtension.ExecuteAsyncBuildRequests(buildRequests, BuildSubmissionTests.DefaultBuildCompletionTimeout, BuildSubmissionTests.WaitForCompletion, out requestsStatusAfterCancel);
            Verify.IsTrue(success, "Submissions should have completed in the specified 60 seconds timeout.");

            for (int k = 0; k < buildRequests.Length; k++)
            {
                requestsStatusAfterCancel[k].SubmissionTestExtension.BuildResultTestExtension.Verify.BuildSucceeded();
            }

            this.buildManagerTestExtension.EndBuild();
        }

        /// <summary>
        /// Generates a random amout of BuildRequestData between 5 and 10.
        /// </summary>
        /// <param name="simulatedProjectBuildTime">Time in mili-seconds the project default target will sleep for before completion.</param>
        /// <returns>Generated BuildRequestData.</returns>
        private BuildRequestData[] GenerateBuildRequests(int simulatedProjectBuildTime)
        {
            int requestCount = this.randomNumbers.Next(BuildSubmissionTests.MinimimBuildRequestCount, BuildSubmissionTests.MaximumBuildRequestCount);
            BuildRequestData[] buildRequests = new BuildRequestData[requestCount];
            for (int i = 0; i < requestCount; i++)
            {
                buildRequests[i] = buildManagerTestExtension.CreateBuildRequestData(simulatedProjectBuildTime);
            }

            return buildRequests;
        }
    }
}
