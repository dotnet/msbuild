// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Construction;
using Microsoft.DotNet.ProjectModel;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Cli;
using System.Linq;
using System.IO;

namespace Microsoft.DotNet.ProjectJsonMigration
{
    public class MigrationRuleInputs
    {
        public ProjectRootElement ProjectXproj { get; }

        public ProjectRootElement OutputMSBuildProject { get; }

        public ProjectItemGroupElement CommonItemGroup { get; }

        public ProjectPropertyGroupElement CommonPropertyGroup { get; }
        
        public IEnumerable<ProjectContext> ProjectContexts { get; }

        public ProjectContext DefaultProjectContext
        {
            get
            {
                return ProjectContexts.First();
            }
        }

        public MigrationRuleInputs(
            IEnumerable<ProjectContext> projectContexts, 
            ProjectRootElement outputMSBuildProject,
            ProjectItemGroupElement commonItemGroup,
            ProjectPropertyGroupElement commonPropertyGroup,
            ProjectRootElement projectXproj=null)
        {
            ProjectXproj = projectXproj;
            ProjectContexts = projectContexts;
            OutputMSBuildProject = outputMSBuildProject;
            CommonItemGroup = commonItemGroup;
            CommonPropertyGroup = commonPropertyGroup;
        }
    }
}
