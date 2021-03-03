// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if !FEATURE_SYSTEM_CONFIGURATION
/*  This test is designed especially to test Configuration parsing in net5.0
 *  which means it WON'T work in net472 and thus we don't run it in net472 */

using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;

using Xunit;
using System.Collections.Generic;

namespace Microsoft.Build.UnitTests.Evaluation
{
    /// <summary>
    /// Unit tests for MSBuild Net5.0 Configuration Parsing
    /// </summary>
    public class ToolsetConfigurationNet5Test
    {
        [Fact]
        // The default ToolsetDefintionLocations is None, which results in only the local which results in only the several included
        // paths such as SDK path and RoslynTargetPath and nothing else. This behavior is expected and the exact same as before.
        public void ToolsetDefinitionLocationsIsDefault()
        {
            var projectCollection = new ProjectCollection();
            IDictionary<string, string> toolsetProperties
                = new Dictionary<string, string>();

            foreach (Toolset toolset in projectCollection.Toolsets)
            {
                foreach (KeyValuePair<string, ProjectPropertyInstance> properties in toolset.Properties)
                {
                    toolsetProperties[properties.Value.Name] = properties.Value.EvaluatedValue;
                }
            }

            Assert.True(toolsetProperties.ContainsKey("MSBuildSDKsPath"));
            Assert.True(toolsetProperties.ContainsKey("RoslynTargetsPath"));
            Assert.Contains("net5.0", toolsetProperties["MSBuildSDKsPath"]);
            Assert.Contains("net5.0", toolsetProperties["RoslynTargetsPath"]);
            Assert.False(toolsetProperties.ContainsKey("VCTargetsPath"));
            Assert.False(toolsetProperties.ContainsKey("MSBuildToolsRoot"));
            Assert.False(toolsetProperties.ContainsKey("MSBuildExtensionsPath"));
        }

        [Fact]
        // With ToolsetDefintionLocations set to ConfigurationFile (Which would only happen in net5.0 if the user decides to set it). 
        // Most toolsets are available and the MsBuildTools and SDK paths are all in the net5.0 runtime.
        public void ToolsetDefinitionLocationsIsConfiguration()
        {
            var projectCollection = new ProjectCollection(ToolsetDefinitionLocations.ConfigurationFile);
            IDictionary<string, string> toolsetProperties
                = new Dictionary<string, string>();

            foreach (Toolset toolset in projectCollection.Toolsets)
            {
                foreach (KeyValuePair<string, ProjectPropertyInstance> properties in toolset.Properties)
                {
                    toolsetProperties[properties.Value.Name] = properties.Value.EvaluatedValue;
                }
            }

            Assert.True(toolsetProperties.ContainsKey("MSBuildSDKsPath"));
            Assert.True(toolsetProperties.ContainsKey("RoslynTargetsPath"));
            Assert.Contains("net5.0", toolsetProperties["MSBuildSDKsPath"]);
            Assert.Contains("net5.0", toolsetProperties["RoslynTargetsPath"]);

            Assert.True(toolsetProperties.ContainsKey("VCTargetsPath"));
            Assert.True(toolsetProperties.ContainsKey("MSBuildToolsRoot"));
            Assert.True(toolsetProperties.ContainsKey("MSBuildExtensionsPath"));
            Assert.Contains("net5.0", toolsetProperties["VCTargetsPath"]);
            Assert.Contains("net5.0", toolsetProperties["MSBuildExtensionsPath"]);
        }
    }
}
#endif
