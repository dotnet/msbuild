// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Build.Framework
{
    /// <summary>
    ///     An abstract interface for classes that can resolve a Software Development Kit (SDK).
    /// </summary>
    public abstract class SdkResolver
    {
        /// <summary>
        ///     Name of the SDK resolver to be displayed in build output log.
        /// </summary>
        public abstract string Name { get; }

        /// <summary>
        ///     Gets the self-described resolution priority order. MSBuild will sort resolvers
        ///     by this value.
        /// </summary>
        public abstract int Priority { get; }

        /// <summary>
        ///     Resolves the specified SDK reference.
        /// </summary>
        /// <param name="sdkReference">A <see cref="SdkReference" /> containing the referenced SDKs be resolved.</param>
        /// <param name="resolverContext">Context for resolving the SDK.</param>
        /// <param name="factory">Factory class to create an <see cref="SdkResult" /></param>
        /// <returns>
        ///     An <see cref="SdkResult" /> containing the resolved SDKs or associated error / reason
        ///     the SDK could not be resolved.  Return <code>null</code> if the resolver is not
        ///     applicable for a particular <see cref="SdkReference"/>.
        ///     <remarks>
        ///         Note: You must use the <see cref="SdkResultFactory" /> to return a result.
        ///     </remarks>
        /// </returns>
        public abstract SdkResult Resolve(SdkReference sdkReference, SdkResolverContext resolverContext,
            SdkResultFactory factory);
    }
}
