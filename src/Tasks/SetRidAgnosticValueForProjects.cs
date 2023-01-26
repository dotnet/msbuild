// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.Utilities;

namespace Microsoft.Build.Tasks
{
    public sealed class SetRidAgnosticValueForProjects : TaskExtension
    {
        public ITaskItem[] Projects { get; set; } = Array.Empty<ITaskItem>();

        [Output]
        public ITaskItem[] UpdatedProjects { get; set; } = Array.Empty<ITaskItem>();

        public override bool Execute()
        {
            UpdatedProjects = Projects
                .Select(p =>
                {
                    var hasSingleTargetFrameworkString = p.GetMetadata("HasSingleTargetFramework");
                    if (!ConversionUtilities.ValidBooleanFalse(hasSingleTargetFrameworkString))
                    {
                        // No change to item, it should already have a single-valued IsRidAgnostic value
                        return p;
                    }
                    var updatedItem = new TaskItem(p);

                    var nearestTargetFramework = p.GetMetadata("NearestTargetFramework");
                    if (string.IsNullOrEmpty(nearestTargetFramework))
                    {
                        return p;
                    }

                    var targetFrameworksArray = p.GetMetadata("TargetFrameworks").Split(';');

                    int targetFrameworkIndex = Array.IndexOf(targetFrameworksArray, nearestTargetFramework);
                    if (targetFrameworkIndex < 0)
                    {
                        return p;
                    }

                    var isRidAgnosticArray = p.GetMetadata("IsRidAgnostic").Split(';');
                    if (isRidAgnosticArray.Length != targetFrameworksArray.Length)
                    {
                        return p;
                    }

                    updatedItem.SetMetadata("IsRidAgnostic", isRidAgnosticArray[targetFrameworkIndex]);

                    return updatedItem;
                })
                .ToArray();

            return true;
        }
    }
}
