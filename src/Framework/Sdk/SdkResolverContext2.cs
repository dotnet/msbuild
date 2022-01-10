// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Build.Framework
{
    /// <summary>
    ///     Context used by an <see cref="SdkResolver" /> to resolve an SDK.
    /// </summary>
    public abstract class SdkResolverContext2 : SdkResolverContext
    {

        /// <summary>
        ///     Options specified as Import attribute.
        /// </summary>
        public virtual string Options { get; protected set; }
    }
}
