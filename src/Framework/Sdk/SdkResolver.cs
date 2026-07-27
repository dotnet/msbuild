// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;

namespace Microsoft.Build.Framework
{
    /// <summary>
    /// An abstract interface for classes that can resolve a Software Development Kit (SDK).
    /// </summary>
    public abstract class SdkResolver
    {
        /// <summary>
        /// Gets the name of the <see cref="SdkResolver"/> to be displayed in build output log.
        /// </summary>
        public abstract string Name { get; }

        /// <summary>
        /// Gets the self-described resolution priority order. MSBuild will sort resolvers
        /// by this value.
        /// </summary>
        public abstract int Priority { get; }

        /// <summary>
        /// Resolves the specified SDK reference.
        /// </summary>
        /// <param name="sdkReference">A <see cref="SdkReference" /> containing the referenced SDKs be resolved.</param>
        /// <param name="resolverContext">Context for resolving the SDK.</param>
        /// <param name="factory">Factory class to create an <see cref="SdkResult" /></param>
        /// <returns>
        /// An <see cref="SdkResult" /> containing the resolved SDKs or associated error / reason
        /// the SDK could not be resolved.  Return <see langword="null"/> if the resolver is not
        /// applicable for a particular <see cref="SdkReference"/>.
        /// </returns>
        /// <remarks>
        /// Note: You must use <see cref="SdkResultFactory"/> to return a result.
        /// </remarks>
        public abstract SdkResult? Resolve(SdkReference sdkReference,
                                          SdkResolverContext resolverContext,
                                          SdkResultFactory factory);

        /// <summary>
        /// The set of resolvers registered in-process via <see cref="Register"/>.
        /// </summary>
        private static readonly List<SdkResolver> s_registeredResolvers = new();

        /// <summary>
        /// Registers an <see cref="SdkResolver"/> to be consulted during SDK resolution by a host that runs
        /// the MSBuild engine in-process (for example the .NET SDK CLI), without MSBuild discovering and
        /// loading it from disk by reflection.
        /// </summary>
        /// <param name="resolver">The resolver instance to register.</param>
        /// <remarks>
        /// <para>
        /// This is the supported way to provide SDK resolvers in a trimmed or Native AOT host, where the
        /// on-disk <c>SdkResolvers</c> probing and reflection-based loading used for plugin resolvers are
        /// unavailable. The registered resolver is consulted on the same reflection-free code path as
        /// MSBuild's built-in resolver, so it never triggers the dynamic-loading failure (MSB4282).
        /// </para>
        /// <para>
        /// The registered resolver participates in resolution in <see cref="Priority"/> order alongside
        /// MSBuild's built-in resolver, with no assembly loading or reflection. It is consulted for every
        /// SDK reference in the process.
        /// </para>
        /// <para>
        /// Intended to be called once per resolver during host initialization, before the first project is
        /// evaluated. The set of registered resolvers is captured the first time an SDK is resolved in the
        /// process; registrations performed after that point are not guaranteed to take effect.
        /// </para>
        /// <para>
        /// This method is thread-safe. Registering the same instance more than once has no additional effect.
        /// </para>
        /// </remarks>
        /// <exception cref="ArgumentNullException"><paramref name="resolver"/> is <see langword="null"/>.</exception>
        public static void Register(SdkResolver resolver)
        {
            if (resolver is null)
            {
                throw new ArgumentNullException(nameof(resolver));
            }

            lock (s_registeredResolvers)
            {
                bool alreadyRegistered = false;
                foreach (SdkResolver registeredResolver in s_registeredResolvers)
                {
                    if (ReferenceEquals(registeredResolver, resolver))
                    {
                        alreadyRegistered = true;
                        break;
                    }
                }

                if (!alreadyRegistered)
                {
                    s_registeredResolvers.Add(resolver);
                }
            }
        }

        /// <summary>
        /// Gets a snapshot of the resolvers registered via <see cref="Register"/>, for the engine to fold
        /// into its reflection-free default-resolver pass.
        /// </summary>
        internal static IReadOnlyList<SdkResolver> RegisteredResolvers
        {
            get
            {
                lock (s_registeredResolvers)
                {
                    return s_registeredResolvers.Count == 0
                        ? Array.Empty<SdkResolver>()
                        : s_registeredResolvers.ToArray();
                }
            }
        }

        /// <summary>
        /// Clears all registered resolvers. For test use only, to reset the process-global registration state.
        /// </summary>
        internal static void ClearRegisteredResolversForTests()
        {
            lock (s_registeredResolvers)
            {
                s_registeredResolvers.Clear();
            }
        }
    }
}
