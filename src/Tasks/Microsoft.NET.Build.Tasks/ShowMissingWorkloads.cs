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

namespace Microsoft.NET.Build.Tasks
{
    public class ShowMissingWorkloads : TaskBase
    {
        public ITaskItem[] MissingWorkloadPacks { get; set; }

        protected override void ExecuteCore()
        {
            if (MissingWorkloadPacks.Any())
            {
                //  TODO: Once IWorkloadResolver.GetWorkloadSuggestionForMissingPacks is implemented, switch to using that to report recommended workloads to install,
                //  instead of reporting which workload packs are missing
                var errorMessage = string.Format(CultureInfo.CurrentCulture,
                    Strings.WorkloadNotInstalled, string.Join(" ", MissingWorkloadPacks.Select(p => p.ItemSpec)));

                Log.LogError(errorMessage);
            }
        }
    }
}
