// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Framework;
using Newtonsoft.Json;
using NuGet.Frameworks;

namespace Microsoft.DotNet.SdkCustomHelix.Sdk
{
    /// <summary>
    /// MSBuild custom task to create HelixWorkItems given xUnit project publish information
    /// </summary>
    public class SDKCustomCreateXUnitWorkItemsWithTestExclusion : Build.Utilities.Task
    {
        /// <summary>
        /// An array of XUnit project workitems containing the following metadata:
        /// - [Required] PublishDirectory: the publish output directory of the XUnit project
        /// - [Required] TargetPath: the output dll path
        /// - [Required] RuntimeTargetFramework: the target framework to run tests on
        /// - [Optional] Arguments: a string of arguments to be passed to the XUnit console runner
        /// The two required parameters will be automatically created if XUnitProject.Identity is set to the path of the XUnit csproj file
        /// </summary>
        [Required]
        public ITaskItem[] XUnitProjects { get; set; }

        /// <summary>
        /// The path to the dotnet executable on the Helix agent. Defaults to "dotnet"
        /// </summary>
        public string PathToDotnet { get; set; } = "dotnet";

        /// <summary>
        /// Boolean true if this is a posix shell, false if not.
        /// This does not need to be set by a user; it is automatically determined in Microsoft.DotNet.Helix.Sdk.MonoQueue.targets
        /// </summary>
        [Required]
        public bool IsPosixShell { get; set; }

        /// <summary>
        /// Optional timeout for all created workitems
        /// Defaults to 300s
        /// </summary>
        public string XUnitWorkItemTimeout { get; set; }

        public string XUnitArguments { get; set; }

        /// <summary>
        /// An array of ITaskItems of type HelixWorkItem
        /// </summary>
        [Output]
        public ITaskItem[] XUnitWorkItems { get; set; }

        /// <summary>
        /// The main method of this MSBuild task which calls the asynchronous execution method and
        /// collates logged errors in order to determine the success of HelixWorkItem creation per
        /// provided xUnit project data.
        /// </summary>
        /// <returns>A boolean value indicating the success of HelixWorkItem creation per provided xUnit project data.</returns>
        public override bool Execute()
        {
            ExecuteAsync().GetAwaiter().GetResult();
            return !Log.HasLoggedErrors;
        }

        /// <summary>
        /// The asynchronous execution method for this MSBuild task which verifies the integrity of required properties
        /// and validates their formatting, specifically determining whether the provided xUnit project data have a 
        /// one-to-one mapping. It then creates this mapping before asynchronously preparing the HelixWorkItem TaskItem
        /// objects via the PrepareWorkItem method.
        /// </summary>
        /// <returns></returns>
        private async Task ExecuteAsync()
        {
            XUnitWorkItems = (await Task.WhenAll(XUnitProjects.Select(PrepareWorkItem)))
                .SelectMany(i => i)
                .Where(wi => wi != null)
                .ToArray();
            return;
        }

        /// <summary>
        /// Prepares HelixWorkItem given xUnit project information.
        /// </summary>
        /// <param name="publishPath">The non-relative path to the publish directory.</param>
        /// <returns>An ITaskItem instance representing the prepared HelixWorkItem.</returns>
        private async Task<List<ITaskItem>> PrepareWorkItem(ITaskItem xunitProject)
        {
            // Forces this task to run asynchronously
            await Task.Yield();

            if (!xunitProject.GetRequiredMetadata(Log, "PublishDirectory", out string publishDirectory))
            {
                return null;
            }
            if (!xunitProject.GetRequiredMetadata(Log, "TargetPath", out string targetPath))
            {
                return null;
            }
            if (!xunitProject.GetRequiredMetadata(Log, "RuntimeTargetFramework", out string runtimeTargetFramework))
            {
                return null;
            }

            xunitProject.TryGetMetadata("ExcludeAdditionalParameters", out string ExcludeAdditionalParameters);

            xunitProject.TryGetMetadata("Arguments", out string arguments);

            string assemblyName = Path.GetFileName(targetPath);

            string driver = $"{PathToDotnet}";

            // netfx tests should only run on Windows full framework for testing VS scenarios
            // These tests have to be executed slightly differently and we give them a different Identity so ADO can tell them apart
            var runtimeTargetFrameworkParsed = NuGetFramework.Parse(runtimeTargetFramework);
            var testIdentityDifferentiator = "";
            bool netFramework = false;
            if (runtimeTargetFrameworkParsed.Framework == ".NETFramework")
            {
                testIdentityDifferentiator = ".netfx";
                netFramework = true;
            }
            else if (runtimeTargetFrameworkParsed.Framework != ".NETCoreApp")
            {
                throw new NotImplementedException("does not support non support the runtime specified");
            }

            // On mac due to https://github.com/dotnet/sdk/issues/3923, we run against workitem directory
            // but on Windows, if we running against working item diretory, we would hit long path.
            string testExecutionDirectory = netFramework ? "-e DOTNET_SDK_TEST_EXECUTION_DIRECTORY=%TestExecutionDirectory%" : IsPosixShell ? "-testExecutionDirectory $TestExecutionDirectory"  : "-testExecutionDirectory %TestExecutionDirectory%";

            string msbuildAdditionalSdkResolverFolder = netFramework ? "-e DOTNET_SDK_TEST_MSBUILDSDKRESOLVER_FOLDER=%HELIX_CORRELATION_PAYLOAD%\\r" : IsPosixShell ? "" : "-msbuildAdditionalSdkResolverFolder %HELIX_CORRELATION_PAYLOAD%\\r";

            if (ExcludeAdditionalParameters.Equals("true"))
            {
                testExecutionDirectory = "";
                msbuildAdditionalSdkResolverFolder = "";
            }

            var scheduler = new AssemblyScheduler(methodLimit: !String.IsNullOrEmpty(Environment.GetEnvironmentVariable("TestFullMSBuild")) ? 32 : 16);
            var assemblyPartitionInfos = scheduler.Schedule(targetPath, netFramework: netFramework);

            var partitionedWorkItem = new List<ITaskItem>();
            foreach (var assemblyPartitionInfo in assemblyPartitionInfos)
            {
                string command = $"{driver} exec {assemblyName} {testExecutionDirectory} {msbuildAdditionalSdkResolverFolder} {(XUnitArguments != null ? " " + XUnitArguments : "")} -xml testResults.xml {assemblyPartitionInfo.ClassListArgumentString} {arguments}";
                if (netFramework)
                {
                    var testFilter = String.IsNullOrEmpty(assemblyPartitionInfo.ClassListArgumentString) ? "" : $"--filter \"{assemblyPartitionInfo.ClassListArgumentString}\"";
                    command = $"{driver} test {assemblyName} {testExecutionDirectory} {msbuildAdditionalSdkResolverFolder} {(XUnitArguments != null ? " " + XUnitArguments : "")} --results-directory .\\ --logger trx {testFilter}";
                }

                Log.LogMessage($"Creating work item with properties Identity: {assemblyName}, PayloadDirectory: {publishDirectory}, Command: {command}");

                TimeSpan timeout = TimeSpan.FromMinutes(5);
                if (!string.IsNullOrEmpty(XUnitWorkItemTimeout))
                {
                    if (!TimeSpan.TryParse(XUnitWorkItemTimeout, out timeout))
                    {
                        Log.LogWarning($"Invalid value \"{XUnitWorkItemTimeout}\" provided for XUnitWorkItemTimeout; falling back to default value of \"00:05:00\" (5 minutes)");
                    }
                }

                partitionedWorkItem.Add(new Microsoft.Build.Utilities.TaskItem(assemblyPartitionInfo.DisplayName + testIdentityDifferentiator, new Dictionary<string, string>()
                    {
                        { "Identity", assemblyPartitionInfo.DisplayName + testIdentityDifferentiator},
                        { "PayloadDirectory", publishDirectory },
                        { "Command", command },
                        { "Timeout", timeout.ToString() },
                    }));
            }

            return partitionedWorkItem;
        }
    }
}
