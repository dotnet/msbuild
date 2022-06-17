// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.DotNet.Workloads.Workload.List;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.Components;

namespace Microsoft.DotNet.Tools.New
{
    internal class WorkloadsInfoProvider : IWorkloadsInfoProvider
    {
        private readonly IWorkloadsRepositoryEnumerator _workloadsRepositoryEnumerator;
        public Guid Id { get; } = Guid.Parse("{F8BA5B13-7BD6-47C8-838C-66626526817B}");

        public WorkloadsInfoProvider(IWorkloadsRepositoryEnumerator workloadsRepositoryEnumerator)
        {
            _workloadsRepositoryEnumerator = workloadsRepositoryEnumerator;
        }

        public Task<IEnumerable<WorkloadInfo>> GetInstalledWorkloadsAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(
                _workloadsRepositoryEnumerator.InstalledAndExtendedWorkloads.Select(w => new WorkloadInfo(w.Id, w.Description))
                );
        }

        public string ProvideConstraintRemedySuggestion(IReadOnlyList<string> supportedWorkloads) => LocalizableStrings.WorkloadInfoProvider_Message_AddWorkloads;
    }
}
