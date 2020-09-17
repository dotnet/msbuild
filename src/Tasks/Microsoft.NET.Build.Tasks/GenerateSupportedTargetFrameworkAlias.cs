// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using NuGet.Frameworks;

namespace Microsoft.NET.Build.Tasks
{
    public class GenerateSupportedTargetFrameworkAlias : TaskBase
    {
        [Required]
        public ITaskItem[] SupportedTargetFramework { get; set; }

        [Required]
        public string TargetFrameworkMoniker { get; set; }

        public string TargetPlatformMoniker { get; set; }

        public bool UseWpf { get; set; }

        public bool UseWindowsForms { get; set; }

        [Output]
        public ITaskItem[] SupportedTargetFrameworkAlias { get; private set; }

        protected override void ExecuteCore()
        {
            var targetFramework = NuGetFramework.Parse(TargetFrameworkMoniker);
            if (!(targetFramework.Framework.Equals(".NETCoreApp") && targetFramework.Version >= new Version(5, 0)))
            {
                if (UseWpf || UseWindowsForms)
                {
                    // Continue with windows as the target platform
                }
                else
                {
                    // Target platform properties were defaulted to windows prior to 5.0, ignore these values
                    TargetPlatformMoniker = string.Empty;
                }
            }

            IList<ITaskItem> convertedTfms = new List<ITaskItem>();
            foreach (var tfm in SupportedTargetFramework)
            {
                var targetFrameworkAlias = NuGetFramework.ParseComponents(tfm.ItemSpec, TargetPlatformMoniker).GetShortFolderName();
                var displayName = string.IsNullOrWhiteSpace(tfm.GetMetadata("DisplayName"))? targetFrameworkAlias : tfm.GetMetadata("DisplayName");
                convertedTfms.Add(new TaskItem(targetFrameworkAlias, new Dictionary<string, string>() { { "DisplayName", displayName } }));
            }
            SupportedTargetFrameworkAlias = convertedTfms.ToArray();
        }
    }
}
