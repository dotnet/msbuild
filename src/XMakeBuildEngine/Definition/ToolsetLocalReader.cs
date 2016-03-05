// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.Collections;
using Microsoft.Build.Construction;
using Microsoft.Build.Execution;
using Microsoft.Build.Shared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Build.Evaluation
{
    internal class ToolsetLocalReader : ToolsetReader
    {
        private IElementLocation _sourceLocation = new RegistryLocation("ToolsetLocalReader");

        internal ToolsetLocalReader(PropertyDictionary<ProjectPropertyInstance> environmentProperties, PropertyDictionary<ProjectPropertyInstance> globalProperties)
           : base(environmentProperties, globalProperties)
        {
        }

        protected override string DefaultOverrideToolsVersion
        {
            get
            {
                return MSBuildConstants.CurrentProductVersion;
            }
        }

        protected override string DefaultToolsVersion
        {
            get
            {
                return MSBuildConstants.CurrentProductVersion;
            }
        }

        protected override string MSBuildOverrideTasksPath
        {
            get
            {
                return FileUtilities.CurrentExecutableDirectory;
            }
        }

        protected override IEnumerable<ToolsetPropertyDefinition> ToolsVersions
        {
            get
            {
                yield return new ToolsetPropertyDefinition(MSBuildConstants.CurrentProductVersion, string.Empty, _sourceLocation);
            }
        }

        protected override IEnumerable<ToolsetPropertyDefinition> GetPropertyDefinitions(string toolsVersion)
        {
            yield return new ToolsetPropertyDefinition(MSBuildConstants.ToolsPath, FileUtilities.CurrentExecutableDirectory, _sourceLocation);
        }

        protected override IEnumerable<ToolsetPropertyDefinition> GetSubToolsetPropertyDefinitions(string toolsVersion, string subToolsetVersion)
        {
            return Enumerable.Empty<ToolsetPropertyDefinition>();
        }

        protected override IEnumerable<string> GetSubToolsetVersions(string toolsVersion)
        {
            return Enumerable.Empty<string>();
        }

        protected override Dictionary<MSBuildExtensionsPathReferenceKind, IList<string>> GetMSBuildExtensionPathsSearchPathsTable(string toolsVersion, string os)
        {
            return new Dictionary<MSBuildExtensionsPathReferenceKind, IList<string>>();
        }
    }
}
