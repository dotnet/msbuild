//-----------------------------------------------------------------------
// <copyright file="BuildManagerTestExtension.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
// <summary>Test extension for the BuildManager implementation</summary>
//-----------------------------------------------------------------------
namespace Microsoft.Build.ApexTests.Library
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Text;
    using System.Threading;
    using Microsoft.Build.BackEnd;
    using Microsoft.Build.Collections;
    using Microsoft.Build.Evaluation;
    using Microsoft.Build.Execution;
    using Microsoft.Build.Shared;
    using Microsoft.Test.Apex;

    /// <summary>
    /// Test extension for BuildManager implementation. BuildManager is the main entry point to the MSBuild Backend Engine. The test extension below allows us to create an instance of the actual
    /// implementation of the BuildManager and invoke methods provided by the build manager. The test extension also allows a mechanism to overwrite any internal components that the BuildManager
    /// may have created so that we can mock internal components. This allows us to acheive isolating testing of components.
    /// </summary>
    public class BuildManagerTestExtension : TestExtension<BuildManagerVerifier>, IDisposable
    {
        /// <summary>
        /// Path to the temporary folder.
        /// </summary>
        private string tempPath = null;

        /// <summary>
        /// Path to the loaded assembly.
        /// </summary>
        private string assemblyPath = null;

        /// <summary>
        /// List of all the temp folder that we created that needs to be cleaned up
        /// </summary>
        private List<string> tempFoldersCreated = null;

        /// <summary>
        /// Initializes a new instance of the BuildManagerTestExtension class.
        /// </summary>
        /// <param name="buildManager">Instance of the actual BuildManager.</param>
        internal BuildManagerTestExtension(BuildManager buildManager) 
            : base()
        {
            this.BuildManager = buildManager;
            this.tempPath = Path.GetTempPath();
            this.tempFoldersCreated = new List<string>();
            this.tempPath = Path.GetTempPath();
            this.assemblyPath = Assembly.GetAssembly(typeof(BuildManagerTestExtension)).Location;
        }

        /// <summary>
        /// Gets the default build parameters to use.
        /// </summary>
        public static BuildParameters DefaultBuildParameters
        {
            get
            {
                return new BuildParameters();
            }
        }

        /// <summary>
        /// Gets or sets the instance of the actual MSBuild BuildManager
        /// </summary>
        internal BuildManager BuildManager
        {
            get;
            private set;
        }

        /// <summary>
        /// Creates a new BuildParameters object with the specified parameters.
        /// </summary>
        /// <param name="nodeReuse">Remove node should be re-used between different builds.</param>
        /// <param name="maxNodeCount">Max number of nodes to use in a build process.</param>
        /// <param name="maxMemoryLimit">Max memory in KB to use for the build process.</param>
        /// <param name="defaultToolsVersion">Default tools version to use if one is not specified.</param>
        /// <returns>New instance of the BuildParameters object created using the above parameters.</returns>
        public static BuildParameters CreateBuildParameters(bool nodeReuse, int maxNodeCount, int maxMemoryLimit, string defaultToolsVersion)
        {
            BuildParameters parameter = new BuildParameters();
            parameter.EnableNodeReuse = nodeReuse;
            parameter.DefaultToolsVersion = defaultToolsVersion;
            parameter.MaxNodeCount = maxNodeCount;
            parameter.MemoryUseLimit = maxMemoryLimit;
            return parameter;
        }

        /// <summary>
        /// Method to mimic WaitAll in STA. WaitAll for multiple handles on an STA thread is not supported.
        /// </summary>
        /// <param name="waitHandles">Handles to wait for.</param>
        /// <param name="timeout">Number of miliseconds to wait for before timing out.</param>
        /// <returns>True if the wait succeedes.</returns>
        public static bool WaitAll(WaitHandle[] waitHandles, int timeout)
        {
            bool waitStatus = true;

            if (Thread.CurrentThread.GetApartmentState() == ApartmentState.STA)
            {
                foreach (WaitHandle myWaitHandle in waitHandles)
                {
                    if (!BuildManagerTestExtension.Wait(myWaitHandle, timeout))
                    {
                        return false;
                    }
                }
            }
            else
            {
                waitStatus = WaitHandle.WaitAll(waitHandles);
            }

            return waitStatus;
        }

        /// <summary>
        /// Wait for a single handle.
        /// </summary>
        /// <param name="myWaitHandle">Handle to wait for</param>
        /// <param name="timeout">Number of miliseconds to wait for before timing out.</param>
        /// <returns>True if the wait succeedes.</returns>
        public static bool Wait(WaitHandle myWaitHandle, int timeout)
        {
            if (WaitHandle.WaitAny(new WaitHandle[] { myWaitHandle }, timeout) == WaitHandle.WaitTimeout)
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Prepares the BuildManager to receive build requests.
        /// </summary>
        /// <param name="parameters">The build parameters.  May be null.</param>
        /// <exception cref="InvalidOperationException">Thrown if a build is already in progress.</exception>
        public void BeginBuild(BuildParameters parameters)
        {
            this.BuildManager.BeginBuild(parameters);
        }

        /// <summary>
        /// Cancels all outstanding submissions.
        /// </summary>
        public void CancelAllSubmissions()
        {
            this.BuildManager.CancelAllSubmissions();
        }

        /// <summary>
        /// Creates a new BuildRequestData object with the specified parameters. This will also create a project file with the specified file contents.
        /// </summary>
        /// <param name="projectFileName">Name of the project file to create.</param>
        /// <param name="globalProperties">Global properties to pass to the project when building it. If this parameter is null then an empty dictonary will be used.</param>
        /// <param name="toolsVersion">Tools version to use when building the project file. If this parameter is null or empty then 2.0 will be used.</param>
        /// <param name="targetsToBuild">Targets to build. If this parameter is null then a empty string array will be used.</param>
        /// <param name="hostServices">Hostservices for tasks being executed when building the project.</param>
        /// <param name="fileContents">Contents of the project file. If the content is empty then a default content is used.</param>
        /// <param name="projectExecutionTime">Number of miliseconds the project should try to run.</param>
        /// <returns>New instance of the BuildRequestData object created using the above parameters.</returns>
        public BuildRequestData CreateBuildRequestData(string projectFileName, Dictionary<string, string> globalProperties, string toolsVersion, string[] targetsToBuild, HostServices hostServices, string fileContents, int projectExecutionTime)
        {
            string projectFilePath = Path.Combine(this.tempPath, String.Format(CultureInfo.InvariantCulture, "MSBuildBackEndApexTests{0}", DateTime.Now.Ticks.ToString(CultureInfo.InvariantCulture)));

            if (!Directory.Exists(projectFilePath))
            {
                Directory.CreateDirectory(projectFilePath);
            }
            else
            {
                for (int i = 0; i < 100; i++)
                {
                    projectFilePath = projectFilePath + "_" + i;

                    if (!Directory.Exists(projectFilePath))
                    {
                        Directory.CreateDirectory(projectFilePath);
                        break;
                    }
                }
            }

            this.tempFoldersCreated.Add(projectFilePath);
            projectFilePath = Path.Combine(projectFilePath, (String.IsNullOrEmpty(projectFileName) ? "testproject.proj" : projectFileName));

            string defaultFileContent =
             @"<Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                <UsingTask TaskName='SimpleTaskHelper' AssemblyFile='{0}' />
                <Target Name='t1'>
                    <SimpleTaskHelper ExpectedOutput='Foo' TaskShouldThrowException='false' SleepTime='{1}'>
                        <Output TaskParameter='TaskOutput' PropertyName='SomeProperty'/>
                    </SimpleTaskHelper>
                </Target>
            </Project>";
            string fileContent = String.Format(CultureInfo.InvariantCulture, (String.IsNullOrEmpty(fileContents) ? defaultFileContent : fileContents), this.assemblyPath, projectExecutionTime.ToString(CultureInfo.InvariantCulture));
            using (TextWriter ts = new StreamWriter(projectFilePath, false, Encoding.UTF8))
            {
                ts.WriteLine(fileContent);
            }

            globalProperties = (globalProperties == null ? new Dictionary<string, string>() : globalProperties);
            targetsToBuild = (targetsToBuild == null ? new string[] { } : targetsToBuild);
            return new BuildRequestData(projectFilePath, globalProperties, toolsVersion, targetsToBuild, hostServices);
        }

        /// <summary>
        /// Creates a new BuildRequestData object with the specified parameters. This will also create a project file with the specified file contents.
        /// </summary>
        /// <param name="projectExecutionTime">Number of miliseconds the project should try to run.</param>
        /// <returns>New instance of the BuildRequestData object created using the above parameters.</returns>
        public BuildRequestData CreateBuildRequestData(int projectExecutionTime)
        {
            return this.CreateBuildRequestData(null, null, null, null, null, null, projectExecutionTime);
        }

        /// <summary>
        /// Submits a build request to the current build but does not start it immediately.  Allows the user to
        /// perform asynchronous execution or access the submission ID prior to executing the request.
        /// </summary>
        /// <param name="requestData">Data containing the request to build.</param>
        /// <returns>BuildSubmissionTestExtension which contains the BuildSubmission instance returned to the internal BuildManager.</returns>
        public BuildSubmissionTestExtension PendBuildRequest(BuildRequestData requestData)
        {
            BuildSubmission submission = this.BuildManager.PendBuildRequest(requestData);
            return TestExtensionHelper.Create<BuildSubmissionTestExtension, BuildSubmission>(submission, this);
        }

        /// <summary>
        /// Convenience method. Submits a build request and blocks until the results are available.
        /// </summary>
        /// <param name="requestData">Data containing the request to build.</param>
        /// <returns>BuildResultTestExtension which contains the BuildResult instance returned to the internal BuildManager.</returns>
        public BuildResultTestExtension BuildRequest(BuildRequestData requestData)
        {
            BuildResult result = this.BuildManager.BuildRequest(requestData);
            return TestExtensionHelper.Create<BuildResultTestExtension, BuildResult>(result, this);
        }

        /// <summary>
        /// Signals that no more build requests are expected (or allowed) and the BuildManager may clean up.
        /// </summary>
        public void EndBuild()
        {
            this.BuildManager.EndBuild();
        }

        /// <summary>
        /// Convenience method.  Submits a lone build request and blocks until results are available.
        /// </summary>
        /// <param name="parameters">Build settings to use.</param>
        /// <param name="requestData">Data containing the request to build.</param>
        /// <returns>BuildResultTestExtension which contains the BuildResult instance returned to the internal BuildManager.</returns>
        public BuildResultTestExtension Build(BuildParameters parameters, BuildRequestData requestData)
        {
            BuildResult result = this.BuildManager.Build(parameters, requestData);
            return TestExtensionHelper.Create<BuildResultTestExtension, BuildResult>(result, this);
        }

        /// <summary>
        /// Cleanup any resources created by this object.
        /// </summary>
        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Executes each of the requests and waits for all of them to be completed.
        /// </summary>
        /// <param name="buildRequests">Array of BuildRequestData to be built.</param>
        /// <param name="timeout">Number of mili seconds to wait for the pending build requests.</param>
        /// <param name="waitForCompletion">Should wait for the builds to complete.</param>
        /// <param name="asyncBuildRequestsStatus">Array of AsyncBuildRequestStatus which contain information about each request executed asynchronously.</param>
        /// <returns>True if the builds completed successfully.</returns>
        public bool ExecuteAsyncBuildRequests(BuildRequestData[] buildRequests, int timeout, bool waitForCompletion, out AsyncBuildRequestStatus[] asyncBuildRequestsStatus)
        {
            BuildSubmissionTestExtension[] submissionTestExtensions = new BuildSubmissionTestExtension[buildRequests.Length];
            AutoResetEvent[] buildCompletedEvents = new AutoResetEvent[buildRequests.Length];
            asyncBuildRequestsStatus = new AsyncBuildRequestStatus[buildRequests.Length];
            try
            {
                for (int i = 0; i < buildRequests.Length; i++)
                {
                    buildCompletedEvents[i] = new AutoResetEvent(false);
                    submissionTestExtensions[i] = this.PendBuildRequest(buildRequests[i]);
                    asyncBuildRequestsStatus[i] = new AsyncBuildRequestStatus(buildCompletedEvents[i], submissionTestExtensions[i]);
                    submissionTestExtensions[i].ExecuteAsync(asyncBuildRequestsStatus[i].SubmissionCompletedCallback, null);
                }

                if (waitForCompletion)
                {
                    if (!BuildManagerTestExtension.WaitAll(buildCompletedEvents, timeout))
                    {
                        return false;
                    }

                    return true;
                }
                else
                {
                    return true;
                }
            }
            finally
            {
                if (waitForCompletion)
                {
                    for (int i = 0; i < buildRequests.Length; i++)
                    {
                        buildCompletedEvents[i].Close();
                        buildCompletedEvents[i] = null;
                    }
                }
            }
        }

        /// <summary>
        /// Returns an instance of the requested component type.
        /// </summary>
        /// <param name="componentType">Instance of the type of component.</param>
        /// <returns>IBuildComponent instance returned by the internal BuildManager.</returns>
        internal IBuildComponent GetComponent(BuildComponentType componentType)
        {
            return ((IBuildComponentHost)this.BuildManager).GetComponent(componentType);
        }

        /// <summary>
        /// Registers a factory which will be used to create the necessary components of the build
        /// system.
        /// </summary>
        /// <param name="componentType">The type which is created by this factory.</param>
        /// <param name="componentProvider">The factory to be registered.</param>
        internal void ReplaceRegisterdFactory(BuildComponentType componentType, BuildComponentFactoryDelegate componentProvider)
        {
            ((IBuildComponentHost)this.BuildManager).RegisterFactory(componentType, componentProvider);
        }

        /// <summary>
        /// Cleanup all the temp folders created. Also cleanup the buildmanager.
        /// </summary>
        /// <param name="disposing">If we are in the process of disposing.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                foreach (string path in this.tempFoldersCreated)
                {
                    if (Directory.Exists(path))
                    {
                        Directory.Delete(path, true);
                    }
                }

                this.BuildManager = null;
            }
        }

        /// <summary>
        /// Class to wrap the completed callback for the build submission so that each submission can have it own instance of the callback and event handlers.
        /// This way we can have multiple tests which can be doing async calls and failure in one does not affect the other tests.
        /// </summary>
        public class BuildSubmissionCompleteCallBack
        {
            /// <summary>
            /// Initializes a new instance of the BuildSubmissionCompleteCallBack class.
            /// </summary>
            /// <param name="submissionCompletedEvent">Event handler which is to be set when the callback is called.</param>
            public BuildSubmissionCompleteCallBack(AutoResetEvent submissionCompletedEvent)
            {
                this.SubmissionCompletedEvent = submissionCompletedEvent;
            }

            /// <summary>
            /// Initializes a new instance of the BuildSubmissionCompleteCallBack class.
            /// </summary>
            /// <param name="submissionCompletedEvent">Event handler which is to be set when the callback is called.</param>
            /// <param name="cancelledAfterExecute">This submission was cancelled after execute.</param>
            public BuildSubmissionCompleteCallBack(AutoResetEvent submissionCompletedEvent, bool cancelledAfterExecute)
                : this(submissionCompletedEvent)
            {
                this.CancelledAfterExecute = cancelledAfterExecute;
            }

            /// <summary>
            /// Gets the event to signal when the submission callback has been called.
            /// </summary>
            public AutoResetEvent SubmissionCompletedEvent
            {
                get;
                private set;
            }

            /// <summary>
            /// Gets a value indicating whether the submission could be cancelled after the build was executed.
            /// </summary>
            public bool CancelledAfterExecute
            {
                get;
                private set;
            }

            /// <summary>
            /// Callback method when the asynchronous BuildSubmission is completed. The verification done on completed submission is
            /// that the build completed and succeeded. This is the default behavior. If the verification is to be different then SubmissionCompletedVerificationType
            /// has to be used.
            /// </summary>
            /// <param name="submissionTestExtension">Contains the BuildSubmission for which the request was completed.</param>
            public void SubmissionCompletedCallback(BuildSubmissionTestExtension submissionTestExtension)
            {
                try
                {
                    submissionTestExtension.Verify.BuildIsCompleted();
                    if (this.CancelledAfterExecute)
                    {
                        submissionTestExtension.Verify.BuildCompletedButFailed();
                    }
                    else
                    {
                        submissionTestExtension.Verify.BuildCompletedSuccessfully();
                    }
                }
                finally
                {
                    this.SubmissionCompletedEvent.Set();
                }
            }
        }
    }
}
