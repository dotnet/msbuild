// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Build.Evaluation;
using NuGet.ProjectModel;

namespace Microsoft.DotNet.Cli.CommandResolution
{
    internal class MSBuildProject : IProject
    {
        private Project _project;

        public MSBuildProject(string msBuildProjectPath)
        {
            var globalProperties = new Dictionary<string, string>()
            {
               { "MSBuildExtensionsPath", AppContext.BaseDirectory }
            };

            _project = new Project(msBuildProjectPath, globalProperties, null);
        }

        public LockFile GetLockFile()
        {
            var intermediateOutputPath = _project
                .AllEvaluatedProperties
                .FirstOrDefault(p => p.Name.Equals("BaseIntermediateOutputPath"))
                .EvaluatedValue;
            var lockFilePath = Path.Combine(intermediateOutputPath, "project.assets.json");
            return new LockFileFormat().Read(lockFilePath);
        }

        public IEnumerable<SingleProjectInfo> GetTools()
        {
            var toolsReferences = _project.AllEvaluatedItems.Where(i => i.ItemType.Equals("DotNetCliToolReference"));
            var tools = toolsReferences.Select(t => new SingleProjectInfo(
                t.EvaluatedInclude,
                t.GetMetadataValue("Version"),
                Enumerable.Empty<ResourceAssemblyInfo>()));

            return tools;
        }
    }
}