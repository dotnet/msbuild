// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

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
            if (!(targetFramework.Framework.Equals(".NETCoreApp") && targetFramework.Version >= new Version(5, 0)) || UseWpf || UseWindowsForms)
            {
                // Target platform properties were defaulted to windows prior to 5.0, ignore these values
                TargetPlatformMoniker = string.Empty;
            }

            IList<ITaskItem> convertedTfms = new List<ITaskItem>();
            foreach (var tfm in SupportedTargetFramework)
            {
                var currentTargetFramework = NuGetFramework.ParseComponents(tfm.ItemSpec, TargetPlatformMoniker);
                if (currentTargetFramework.Framework.Equals(targetFramework.Framework))
                {
                    var targetFrameworkAlias = currentTargetFramework.GetShortFolderName();
                    if ((UseWindowsForms || UseWpf) && currentTargetFramework.Framework.Equals(".NETCoreApp") && currentTargetFramework.Version >= new Version(5, 0))
                    {
                        // Set versionless windows as target platform for wpf and windows forms
                        targetFrameworkAlias = $"{targetFrameworkAlias}-windows";
                    }

                    var displayName = string.IsNullOrWhiteSpace(tfm.GetMetadata("DisplayName")) ? targetFrameworkAlias : tfm.GetMetadata("DisplayName");
                    convertedTfms.Add(new TaskItem(targetFrameworkAlias, new Dictionary<string, string>() { { "DisplayName", displayName } }));
                }
            }

            SupportedTargetFrameworkAlias = convertedTfms.ToArray();
        }
    }
}
