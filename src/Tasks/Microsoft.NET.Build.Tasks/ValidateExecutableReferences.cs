// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Build.Framework;

namespace Microsoft.NET.Build.Tasks
{
    public class ValidateExecutableReferences : TaskBase
    {
        public bool SelfContained { get; set; }

        public bool IsExecutable { get; set; }

        public ITaskItem[] ReferencedProjects { get; set; } = Array.Empty<ITaskItem>();

        protected override void ExecuteCore()
        {
            if (!IsExecutable)
            {
                //  If current project is not executable, then we don't need to check its references
                return;
            }

            foreach (var project in ReferencedProjects)
            {
                string nearestTargetFramework = project.GetMetadata("NearestTargetFramework");
                int targetFrameworkIndex = project.GetMetadata("TargetFrameworks").Split(';').ToList().IndexOf(nearestTargetFramework);
                string projectAdditionalPropertiesMetadata = project.GetMetadata("AdditionalPropertiesFromProject").Split(new[] { ";;" }, StringSplitOptions.None)[targetFrameworkIndex];
                Dictionary<string, string> projectAdditionalProperties = new(StringComparer.OrdinalIgnoreCase);
                foreach (var propAndValue in projectAdditionalPropertiesMetadata.Split(';'))
                {
                    var split = propAndValue.Split('=');
                    projectAdditionalProperties[split[0]] = split[1];
                }

                var referencedProjectIsExecutable = MSBuildUtilities.ConvertStringToBool(projectAdditionalProperties["_IsExecutable"]);
                var referencedProjectIsSelfContained = MSBuildUtilities.ConvertStringToBool(projectAdditionalProperties["SelfContained"]);

                if (referencedProjectIsExecutable)
                {
                    if (SelfContained && !referencedProjectIsSelfContained)
                    {
                        Log.LogError(Strings.SelfContainedExeCannotReferenceNonSelfContained, project.ItemSpec);
                    }
                    else if (!SelfContained && referencedProjectIsSelfContained)
                    {
                        Log.LogError(Strings.NonSelfContainedExeCannotReferenceSelfContained, project.ItemSpec);
                    }
                }
            }
        }
    }
}
