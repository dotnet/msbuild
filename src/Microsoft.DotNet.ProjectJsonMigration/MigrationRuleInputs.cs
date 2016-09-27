// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using Microsoft.Build.Construction;
using Microsoft.DotNet.ProjectModel;
using System.Linq;

namespace Microsoft.DotNet.ProjectJsonMigration
{
    public class MigrationRuleInputs
    {
        public ProjectRootElement ProjectXproj { get; }

        public ProjectRootElement OutputMSBuildProject { get; }

        public ProjectItemGroupElement CommonItemGroup { get; }

        public ProjectPropertyGroupElement CommonPropertyGroup { get; }
        
        public List<ProjectContext> ProjectContexts { get; }

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
            ProjectContexts = projectContexts.ToList();
            OutputMSBuildProject = outputMSBuildProject;
            CommonItemGroup = commonItemGroup;
            CommonPropertyGroup = commonPropertyGroup;
        }
    }
}
