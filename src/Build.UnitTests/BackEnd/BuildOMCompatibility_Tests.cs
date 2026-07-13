// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Reflection;
using Microsoft.Build.Execution;
using Microsoft.Build.Graph;
using Shouldly;

namespace Microsoft.Build.Engine.UnitTests.BackEnd
{
    [TestClass]
    public class BuildOMCompatibility_Tests
    {
        [MSBuildTestMethod]
        [DataRow("ProjectInstance")]
        [DataRow("ProjectFullPath")]
        [DataRow("TargetNames")]
        [DataRow("Flags")]
        [DataRow("GlobalProperties")]
        [DataRow("ExplicitlySpecifiedToolsVersion")]
        [DataRow("HostServices")]
        [DataRow("PropertiesToTransfer")]
        [DataRow("RequestedProjectState")]
        public void BuildRequestDataPropertyCompatTest(string propertyName)
            => VerifyPropertyExists(typeof(BuildRequestData), propertyName);

        [MSBuildTestMethod]
        [DataRow("ProjectGraph")]
        [DataRow("ProjectGraphEntryPoints")]
        [DataRow("TargetNames")]
        [DataRow("Flags")]
        [DataRow("GraphBuildOptions")]
        [DataRow("HostServices")]
        public void GraphBuildRequestDataPropertyCompatTest(string propertyName)
            => VerifyPropertyExists(typeof(GraphBuildRequestData), propertyName);

        [MSBuildTestMethod]
        [DataRow("BuildManager")]
        [DataRow("SubmissionId")]
        [DataRow("AsyncContext")]
        [DataRow("WaitHandle")]
        [DataRow("IsCompleted")]
        [DataRow("BuildResult")]
        public void BuildSubmissionDataPropertyCompatTest(string propertyName)
            => VerifyPropertyExists(typeof(BuildSubmission), propertyName);

        [MSBuildTestMethod]
        [DataRow("Execute")]
        [DataRow("ExecuteAsync")]
        public void BuildSubmissionDataMethodCompatTest(string methodName)
            => VerifyMethodExists(typeof(BuildSubmission), methodName);

        [MSBuildTestMethod]
        [DataRow("BuildManager")]
        [DataRow("SubmissionId")]
        [DataRow("AsyncContext")]
        [DataRow("WaitHandle")]
        [DataRow("IsCompleted")]
        [DataRow("BuildResult")]
        public void GraphBuildSubmissionDataPropertyCompatTest(string propertyName)
            => VerifyPropertyExists(typeof(BuildSubmission), propertyName);

        [MSBuildTestMethod]
        [DataRow("Execute")]
        [DataRow("ExecuteAsync")]
        public void GraphBuildSubmissionDataMethodCompatTest(string methodName)
            => VerifyMethodExists(typeof(BuildSubmission), methodName);

        [MSBuildTestMethod]
        [DataRow("SubmissionId")]
        [DataRow("ConfigurationId")]
        [DataRow("GlobalRequestId")]
        [DataRow("ParentGlobalRequestId")]
        [DataRow("NodeRequestId")]
        [DataRow("Exception")]
        [DataRow("CircularDependency")]
        [DataRow("OverallResult")]
        [DataRow("ResultsByTarget")]
        [DataRow("ProjectStateAfterBuild")]
        [DataRow("BuildRequestDataFlags")]
        public void BuildResultPropertyCompatTest(string propertyName)
            => VerifyPropertyExists(typeof(BuildResult), propertyName);

        [MSBuildTestMethod]
        [DataRow("AddResultsForTarget")]
        [DataRow("MergeResults")]
        [DataRow("HasResultsForTarget")]
        public void BuildResultMethodCompatTest(string methodName)
            => VerifyMethodExists(typeof(BuildResult), methodName);

        [MSBuildTestMethod]
        [DataRow("SubmissionId")]
        [DataRow("Exception")]
        [DataRow("CircularDependency")]
        [DataRow("OverallResult")]
        [DataRow("ResultsByNode")]
        public void GraphBuildResultPropertyCompatTest(string propertyName)
            => VerifyPropertyExists(typeof(GraphBuildResult), propertyName);

        private void VerifyPropertyExists(Type type, string propertyName)
        {
            type.GetProperty(
                    propertyName,
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
                .ShouldNotBeNull();
        }

        private void VerifyMethodExists(Type type, string propertyName)
        {
            type.GetMethod(
                    propertyName,
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
                .ShouldNotBeNull();
        }
    }
}
