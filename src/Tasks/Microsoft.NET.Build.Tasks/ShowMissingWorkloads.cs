// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.NET.Sdk.WorkloadManifestReader;

namespace Microsoft.NET.Build.Tasks
{
    public class ShowMissingWorkloads : TaskBase
    {
        public ITaskItem[] MissingWorkloadPacks { get; set; }

        public string NetCoreRoot { get; set; }

        public string NETCoreSdkVersion { get; set; }

        public bool GenerateErrorsForMissingWorkloads { get; set; }

        [Output]
        public ITaskItem[] SuggestedWorkloads { get; set; }

        protected override void ExecuteCore()
        {
            if (MissingWorkloadPacks.Any())
            {
                var workloadManifestProvider = new SdkDirectoryWorkloadManifestProvider(NetCoreRoot, NETCoreSdkVersion);
                var workloadResolver = WorkloadResolver.Create(workloadManifestProvider, NetCoreRoot, NETCoreSdkVersion);

                var suggestedWorkloads = workloadResolver.GetWorkloadSuggestionForMissingPacks(
                    MissingWorkloadPacks.Select(item => new WorkloadPackId (item.ItemSpec)).ToList(),
                    out ISet<WorkloadPackId> unsatisfiablePacks
                );

                if (GenerateErrorsForMissingWorkloads)
                {
                    if (suggestedWorkloads is not null)
                    {
                        var suggestedInstallCommand = "dotnet workload install " + string.Join(" ", suggestedWorkloads.Select(w => w.Id));
                        var errorMessage = string.Format(CultureInfo.CurrentCulture,
                            Strings.WorkloadNotInstalled, string.Join(" ", suggestedWorkloads.Select(w => w.Id)), suggestedInstallCommand);
                        Log.LogError(errorMessage);
                    }
                    else
                    {
                        Log.LogError(Strings.WorkloadNotAvailable, string.Join(" ", unsatisfiablePacks));
                    }
                }

                if (suggestedWorkloads is not null)
                {
                    SuggestedWorkloads = suggestedWorkloads.Select(suggestedWorkload =>
                    {
                        var taskItem = new TaskItem(suggestedWorkload.Id);
                        taskItem.SetMetadata("VisualStudioComponentId", ToSafeId(suggestedWorkload.Id));
                        return taskItem;
                    }).ToArray();
                }
            }
        }

        internal static string ToSafeId(string id)
        {
            return id.Replace("-", ".").Replace(" ", ".").Replace("_", ".");
        }
    }
}
