// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Concurrent;
using System.Collections.Generic;
using Microsoft.Build.Construction;
using Microsoft.Build.Framework;

namespace Microsoft.Build.BackEnd.SdkResolution
{
    /// <summary>
    /// A subclass of <see cref="SdkResolverLoader"/> which creates resolver manifests and SDK resolvers only once and
    /// then returns cached results.
    /// </summary>
    internal sealed class CachingSdkResolverLoader : SdkResolverLoader
    {
        /// <summary>
        /// Cached list of default resolvers. Set eagerly.
        /// </summary>
        private readonly IReadOnlyList<SdkResolver> _defaultResolvers;

        /// <summary>
        /// Cached manifest -> resolver dictionary. Populated lazily.
        /// </summary>
        private readonly ConcurrentDictionary<SdkResolverManifest, IReadOnlyList<SdkResolver>> _resolversByManifest = new();

        /// <summary>
        /// Cached list of all resolvers. Set lazily.
        /// </summary>
        private IReadOnlyList<SdkResolver>? _allResolvers;

        /// <summary>
        /// Cached list of all resolver manifests. Set lazily.
        /// </summary>
        private IReadOnlyList<SdkResolverManifest>? _resolversManifests;

        /// <summary>
        /// A lock object protecting <see cref="_allResolvers"/> and <see cref="_resolversManifests"/>.
        /// </summary>
        private readonly object _lock = new();

        /// <summary>
        /// A static instance of <see cref="CachingSdkResolverLoader"/>.
        /// </summary>
        /// <remarks>
        /// The set of available SDK resolvers is expected to be fixed for the given MSBuild installation so it should be safe to use
        /// a static instance as opposed to creating <see cref="CachingSdkResolverLoader"/> or <see cref="SdkResolverLoader"/> for each
        /// <see cref="SdkResolverService" /> instance.
        /// </remarks>
        public static CachingSdkResolverLoader Instance = new CachingSdkResolverLoader();

        /// <summary>
        /// Initializes a new instance by setting <see cref="_defaultResolvers"/>.
        /// </summary>
        public CachingSdkResolverLoader()
        {
            _defaultResolvers = base.GetDefaultResolvers();
        }

        #region SdkResolverLoader overrides

        /// <inheritdoc />
        internal override IReadOnlyList<SdkResolver> GetDefaultResolvers() => _defaultResolvers;

        /// <inheritdoc />
        internal override IReadOnlyList<SdkResolver> LoadAllResolvers(ElementLocation location)
        {
            lock (_lock)
            {
                return _allResolvers ??= base.LoadAllResolvers(location);
            }
        }

        /// <inheritdoc />
        internal override IReadOnlyList<SdkResolverManifest> GetResolversManifests(ElementLocation location)
        {
            lock (_lock)
            {
                return _resolversManifests ??= base.GetResolversManifests(location);
            }
        }

        /// <inheritdoc />
        protected internal override IReadOnlyList<SdkResolver> LoadResolversFromManifest(SdkResolverManifest manifest, ElementLocation location)
        {
            return _resolversByManifest.GetOrAdd(manifest, (manifest) => base.LoadResolversFromManifest(manifest, location));
        }

        #endregion
    }
}
