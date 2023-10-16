// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Concurrent;
using System.Collections.Generic;
using Microsoft.Build.Construction;
using Microsoft.Build.Framework;

namespace Microsoft.Build.BackEnd.SdkResolution
{
    internal class CachingSdkResolverLoader : SdkResolverLoader
    {
        private readonly IReadOnlyList<SdkResolver> _defaultResolvers;
        private readonly ConcurrentDictionary<SdkResolverManifest, IReadOnlyList<SdkResolver>> _resolversByManifest = new();

        private IReadOnlyList<SdkResolver>? _allResolvers;
        private IReadOnlyList<SdkResolverManifest>? _resolversManifests;

        private readonly object _lock = new();

        public static CachingSdkResolverLoader Instance = new CachingSdkResolverLoader();

        public CachingSdkResolverLoader()
        {
            _defaultResolvers = base.GetDefaultResolvers();
        }

        internal override IReadOnlyList<SdkResolver> GetDefaultResolvers() => _defaultResolvers;

        internal override IReadOnlyList<SdkResolver> LoadAllResolvers(ElementLocation location)
        {
            lock (_lock)
            {
                return _allResolvers ??= base.LoadAllResolvers(location);
            }
        }

        internal override IReadOnlyList<SdkResolverManifest> GetResolversManifests(ElementLocation location)
        {
            lock (_lock)
            {
                return _resolversManifests ??= base.GetResolversManifests(location);
            }
        }

        protected internal override IReadOnlyList<SdkResolver> LoadResolversFromManifest(SdkResolverManifest manifest, ElementLocation location)
        {
            return _resolversByManifest.GetOrAdd(manifest, (manifest) => base.LoadResolversFromManifest(manifest, location));
        }
    }
}
