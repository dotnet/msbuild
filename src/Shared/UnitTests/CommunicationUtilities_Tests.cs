// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections;
using System.Collections.Generic;
using Shouldly;
using Xunit;
using CommunicationsUtilities = Microsoft.Build.Internal.CommunicationsUtilities;

namespace Microsoft.Build.UnitTests
{
    public class CommunicationUtilitiesTests
    {
        /// <summary>
        /// Verify our custom way of getting env vars gives the same results as the BCL.
        /// </summary>
        [Fact]
        public void GetEnvVars()
        {
            IDictionary<string, string> envVars = CommunicationsUtilities.GetEnvironmentVariables();
            IDictionary referenceVars = Environment.GetEnvironmentVariables();
            IDictionary<string, string> referenceVars2 = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (DictionaryEntry item in referenceVars)
            {
                referenceVars2.Add((string)item.Key!, (string)item.Value!);
            }

            Helpers.AssertCollectionsValueEqual(envVars, referenceVars2);
        }

        /// <summary>
        /// Verify that we correctly restore environment variables.
        /// </summary>
        [Fact]
        public void RestoreEnvVars()
        {
            string testName1 = "_MSBUILD_TEST_ENV_VAR1";
            string testName2 = "_MSBUILD_TEST_ENV_VAR2";

            // A long value exceeding the former limit of 32,767 characters.
            string testValue = new string('a', 1_000_000);

            CommunicationsUtilities.SetEnvironmentVariable(testName1, testValue);
            try
            {
                IDictionary<string, string> envVars = CommunicationsUtilities.GetEnvironmentVariables();

                CommunicationsUtilities.SetEnvironmentVariable(testName1, null);
                CommunicationsUtilities.SetEnvironmentVariable(testName2, testValue);

                CommunicationsUtilities.SetEnvironment(envVars);

                Environment.GetEnvironmentVariable(testName1).ShouldBe(testValue);
                Environment.GetEnvironmentVariable(testName2).ShouldBe(null);
            }
            finally
            {
                CommunicationsUtilities.SetEnvironmentVariable(testName1, null);
                CommunicationsUtilities.SetEnvironmentVariable(testName2, null);
            }
        }

        /// <summary>
        /// Tests for TaskHostParameters readonly struct
        /// </summary>
        [Fact]
        public void TaskHostParameters_DefaultConstructor_ShouldHaveEmptyValues()
        {
            var parameters = new Microsoft.Build.Internal.TaskHostParameters();
            
            parameters.Runtime.ShouldBe(string.Empty);
            parameters.Architecture.ShouldBe(string.Empty);
            parameters.DotnetHostPath.ShouldBe(string.Empty);
            parameters.MSBuildAssemblyPath.ShouldBe(string.Empty);
        }

        [Fact]
        public void TaskHostParameters_ParameterizedConstructor_ShouldSetValues()
        {
            var parameters = new Microsoft.Build.Internal.TaskHostParameters("net", "x64", "/path/to/dotnet", "/path/to/msbuild");
            
            parameters.Runtime.ShouldBe("net");
            parameters.Architecture.ShouldBe("x64");
            parameters.DotnetHostPath.ShouldBe("/path/to/dotnet");
            parameters.MSBuildAssemblyPath.ShouldBe("/path/to/msbuild");
        }

        [Fact]
        public void TaskHostParameters_ConstructorWithNulls_ShouldUseEmptyStrings()
        {
            var parameters = new Microsoft.Build.Internal.TaskHostParameters(null, null, null, null);
            
            parameters.Runtime.ShouldBe(string.Empty);
            parameters.Architecture.ShouldBe(string.Empty);
            parameters.DotnetHostPath.ShouldBe(string.Empty);
            parameters.MSBuildAssemblyPath.ShouldBe(string.Empty);
        }

        [Fact]
        public void TaskHostParameters_TryGetValue_ShouldReturnCorrectValues()
        {
            var parameters = new Microsoft.Build.Internal.TaskHostParameters("clr4", "x86");
            
            parameters.TryGetValue(Microsoft.Build.Shared.XMakeAttributes.runtime, out string runtime).ShouldBeTrue();
            runtime.ShouldBe("clr4");
            
            parameters.TryGetValue(Microsoft.Build.Shared.XMakeAttributes.architecture, out string architecture).ShouldBeTrue();
            architecture.ShouldBe("x86");
            
            parameters.TryGetValue("UnknownKey", out string unknown).ShouldBeFalse();
            unknown.ShouldBeNull();
        }

        [Fact]
        public void TaskHostParameters_TryGetValue_EmptyValues_ShouldReturnFalse()
        {
            var parameters = new Microsoft.Build.Internal.TaskHostParameters();
            
            parameters.TryGetValue(Microsoft.Build.Shared.XMakeAttributes.runtime, out string runtime).ShouldBeFalse();
            parameters.TryGetValue(Microsoft.Build.Shared.XMakeAttributes.architecture, out string architecture).ShouldBeFalse();
        }

        [Fact]
        public void TaskHostParameters_Indexer_ShouldReturnCorrectValues()
        {
            var parameters = new Microsoft.Build.Internal.TaskHostParameters("net", "arm64");
            
            parameters[Microsoft.Build.Shared.XMakeAttributes.runtime].ShouldBe("net");
            parameters[Microsoft.Build.Shared.XMakeAttributes.architecture].ShouldBe("arm64");
        }

        [Fact]
        public void TaskHostParameters_Indexer_UnknownKey_ShouldThrow()
        {
            var parameters = new Microsoft.Build.Internal.TaskHostParameters("net", "x64");
            
            Should.Throw<KeyNotFoundException>(() => parameters["UnknownKey"]);
        }

        [Fact]
        public void TaskHostParameters_FromDictionary_ShouldCreateCorrectStruct()
        {
            var dict = new Dictionary<string, string>
            {
                { Microsoft.Build.Shared.XMakeAttributes.runtime, "clr2" },
                { Microsoft.Build.Shared.XMakeAttributes.architecture, "x64" },
                { "DotnetHostPath", "/dotnet/path" },
                { "MSBuildAssemblyPath", "/msbuild/path" }
            };
            
            var parameters = Microsoft.Build.Internal.TaskHostParameters.FromDictionary(dict);
            
            parameters.Runtime.ShouldBe("clr2");
            parameters.Architecture.ShouldBe("x64");
            parameters.DotnetHostPath.ShouldBe("/dotnet/path");
            parameters.MSBuildAssemblyPath.ShouldBe("/msbuild/path");
        }

        [Fact]
        public void TaskHostParameters_FromDictionary_NullDictionary_ShouldReturnDefault()
        {
            var parameters = Microsoft.Build.Internal.TaskHostParameters.FromDictionary(null);
            
            parameters.Runtime.ShouldBe(string.Empty);
            parameters.Architecture.ShouldBe(string.Empty);
            parameters.DotnetHostPath.ShouldBe(string.Empty);
            parameters.MSBuildAssemblyPath.ShouldBe(string.Empty);
        }

        [Fact]
        public void TaskHostParameters_ToDictionary_ShouldCreateCorrectDictionary()
        {
            var parameters = new Microsoft.Build.Internal.TaskHostParameters("net", "x64", "/dotnet", "/msbuild");
            
            var dict = parameters.ToDictionary();
            
            dict.Count.ShouldBe(4);
            dict[Microsoft.Build.Shared.XMakeAttributes.runtime].ShouldBe("net");
            dict[Microsoft.Build.Shared.XMakeAttributes.architecture].ShouldBe("x64");
            dict["DotnetHostPath"].ShouldBe("/dotnet");
            dict["MSBuildAssemblyPath"].ShouldBe("/msbuild");
        }

        [Fact]
        public void TaskHostParameters_ToDictionary_EmptyValues_ShouldCreateEmptyDictionary()
        {
            var parameters = new Microsoft.Build.Internal.TaskHostParameters();
            
            var dict = parameters.ToDictionary();
            
            dict.ShouldBeEmpty();
        }

        [Fact]
        public void TaskHostParameters_ToDictionary_PartialValues_ShouldIncludeOnlyNonEmpty()
        {
            var parameters = new Microsoft.Build.Internal.TaskHostParameters("net", "x64", null, null);
            
            var dict = parameters.ToDictionary();
            
            dict.Count.ShouldBe(2);
            dict[Microsoft.Build.Shared.XMakeAttributes.runtime].ShouldBe("net");
            dict[Microsoft.Build.Shared.XMakeAttributes.architecture].ShouldBe("x64");
            dict.ContainsKey("DotnetHostPath").ShouldBeFalse();
            dict.ContainsKey("MSBuildAssemblyPath").ShouldBeFalse();
        }

        [Fact]
        public void GetHandshakeOptionsWithStruct_ShouldDelegateToOriginalMethod()
        {
            var structParams = new Microsoft.Build.Internal.TaskHostParameters("net", "x64");
            var dictParams = new Dictionary<string, string>
            {
                { Microsoft.Build.Shared.XMakeAttributes.runtime, "net" },
                { Microsoft.Build.Shared.XMakeAttributes.architecture, "x64" }
            };

            var structResult = CommunicationsUtilities.GetHandshakeOptionsWithStruct(taskHost: true, structParams);
            var dictResult = CommunicationsUtilities.GetHandshakeOptions(taskHost: true, taskHostParameters: dictParams);

            structResult.ShouldBe(dictResult);
        }
    }
}
