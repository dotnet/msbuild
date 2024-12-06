// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Reflection;
using Microsoft.Build.Execution;
using Microsoft.Build.Graph;
using Shouldly;
using Xunit;

namespace Microsoft.Build.Engine.UnitTests.BackEnd
{
    public class BuildOMCompatibility_Tests
    {
        [Theory]
        [InlineData("ProjectInstance")]
        [InlineData("ProjectFullPath")]
        [InlineData("TargetNames")]
        [InlineData("Flags")]
        [InlineData("GlobalProperties")]
        [InlineData("ExplicitlySpecifiedToolsVersion")]
        [InlineData("HostServices")]
        [InlineData("PropertiesToTransfer")]
        [InlineData("RequestedProjectState")]
        public void BuildRequestDataPropertyCompatTest(string propertyName)
            => VerifyPropertyExists(typeof(BuildRequestData), propertyName);

        [Theory]
        [InlineData("ProjectGraph")]
        [InlineData("ProjectGraphEntryPoints")]
        [InlineData("TargetNames")]
        [InlineData("Flags")]
        [InlineData("GraphBuildOptions")]
        [InlineData("HostServices")]
        public void GraphBuildRequestDataPropertyCompatTest(string propertyName)
            => VerifyPropertyExists(typeof(GraphBuildRequestData), propertyName);

        [Theory]
        [InlineData("BuildManager")]
        [InlineData("SubmissionId")]
        [InlineData("AsyncContext")]
        [InlineData("WaitHandle")]
        [InlineData("IsCompleted")]
        [InlineData("BuildResult")]
        public void BuildSubmissionDataPropertyCompatTest(string propertyName)
            => VerifyPropertyExists(typeof(BuildSubmission), propertyName);

        [Theory]
        [InlineData("Execute")]
        [InlineData("ExecuteAsync")]
        public void BuildSubmissionDataMethodCompatTest(string methodName)
            => VerifyMethodExists(typeof(BuildSubmission), methodName);

        [Theory]
        [InlineData("BuildManager")]
        [InlineData("SubmissionId")]
        [InlineData("AsyncContext")]
        [InlineData("WaitHandle")]
        [InlineData("IsCompleted")]
        [InlineData("BuildResult")]
        public void GraphBuildSubmissionDataPropertyCompatTest(string propertyName)
            => VerifyPropertyExists(typeof(BuildSubmission), propertyName);

        [Theory]
        [InlineData("Execute")]
        [InlineData("ExecuteAsync")]
        public void GraphBuildSubmissionDataMethodCompatTest(string methodName)
            => VerifyMethodExists(typeof(BuildSubmission), methodName);

        [Theory]
        [InlineData("SubmissionId")]
        [InlineData("ConfigurationId")]
        [InlineData("GlobalRequestId")]
        [InlineData("ParentGlobalRequestId")]
        [InlineData("NodeRequestId")]
        [InlineData("Exception")]
        [InlineData("CircularDependency")]
        [InlineData("OverallResult")]
        [InlineData("ResultsByTarget")]
        [InlineData("ProjectStateAfterBuild")]
        [InlineData("BuildRequestDataFlags")]
        public void BuildResultPropertyCompatTest(string propertyName)
            => VerifyPropertyExists(typeof(BuildResult), propertyName);

        [Theory]
        [InlineData("AddResultsForTarget")]
        [InlineData("MergeResults")]
        [InlineData("HasResultsForTarget")]
        public void BuildResultMethodCompatTest(string methodName)
            => VerifyMethodExists(typeof(BuildResult), methodName);

        [Theory]
        [InlineData("SubmissionId")]
        [InlineData("Exception")]
        [InlineData("CircularDependency")]
        [InlineData("OverallResult")]
        [InlineData("ResultsByNode")]
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
