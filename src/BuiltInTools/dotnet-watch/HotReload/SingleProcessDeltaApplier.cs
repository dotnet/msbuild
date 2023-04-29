// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.CodeAnalysis.ExternalAccess.Watch.Api;

namespace Microsoft.DotNet.Watcher.Tools
{
    internal abstract class SingleProcessDeltaApplier : DeltaApplier
    {
        /// <summary>
        /// List of modules that can't receive changes anymore.
        /// A module is added when a change is requested for it that is not supported by the runtime.
        /// </summary>
        private readonly HashSet<Guid> _frozenModules = new();

        public override void Initialize(DotNetWatchContext context, CancellationToken cancellationToken)
        {
            _frozenModules.Clear();
        }

        public async Task<IReadOnlyList<WatchHotReloadService.Update>> FilterApplicableUpdatesAsync(DotNetWatchContext context, ImmutableArray<WatchHotReloadService.Update> updates, CancellationToken cancellationToken)
        {
            var availableCapabilities = await GetApplyUpdateCapabilitiesAsync(context, cancellationToken);
            var applicableUpdates = new List<WatchHotReloadService.Update>();

            foreach (var update in updates)
            {
                if (_frozenModules.Contains(update.ModuleId))
                {
                    // can't update frozen module:
                    continue;
                }

                if (update.RequiredCapabilities.Except(availableCapabilities).Any())
                {
                    // required capability not available:
                    _frozenModules.Add(update.ModuleId);
                }
                else
                {
                    applicableUpdates.Add(update);
                }
            }

            return applicableUpdates;
        }
    }
}
