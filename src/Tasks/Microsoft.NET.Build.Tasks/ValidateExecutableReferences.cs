// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
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

                if (string.IsNullOrEmpty(nearestTargetFramework) || !project.HasMetadataValue("AdditionalPropertiesFromProject"))
                {
                    //  Referenced project doesn't have the right metadata.  This may be because it's a different project type (C++, for example)
                    //  In this case just skip the checks
                    continue;
                }

                var additionalPropertiesXml = XElement.Parse(project.GetMetadata("AdditionalPropertiesFromProject"));
                var targetFrameworkElement = additionalPropertiesXml.Element(nearestTargetFramework);
                Dictionary<string, string> projectAdditionalProperties = new(StringComparer.OrdinalIgnoreCase);
                foreach (var propertyElement in targetFrameworkElement.Elements())
                {
                    projectAdditionalProperties[propertyElement.Name.LocalName] = propertyElement.Value;
                }

                var shouldBeValidatedAsExecutableReference = MSBuildUtilities.ConvertStringToBool(projectAdditionalProperties["ShouldBeValidatedAsExecutableReference"], true);
                var referencedProjectIsExecutable = MSBuildUtilities.ConvertStringToBool(projectAdditionalProperties["_IsExecutable"]);
                var referencedProjectIsSelfContained = MSBuildUtilities.ConvertStringToBool(projectAdditionalProperties["SelfContained"]);

                if (referencedProjectIsExecutable && shouldBeValidatedAsExecutableReference)
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
