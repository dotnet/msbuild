// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace Microsoft.Build.Framework
{
    /// <summary>
    /// Exposes build engine functionality that was made available in newer versions of MSBuild.
    /// </summary>
    /// <remarks>
    /// Make all members virtual but not abstract, ensuring that implementations can override them and external implementations
    /// won't break when the class is extended with new members. This base implementation should be throwing <see cref="NotImplementedException"/>.
    /// </remarks>
    [Serializable]
    public abstract class BuildEngineInterface
    {
        /// <summary>
        /// Returns the minimum message importance not guaranteed to be ignored by registered loggers.
        /// </summary>
        /// <remarks>
        /// Example: If we know that no logger is interested in MessageImportance.Low, this property returns
        /// MessageImportance.Normal. If loggers may consume any messages, this property returns MessageImportance.Low.
        /// </remarks>
        public virtual MessageImportance MinimumRequiredMessageImportance => throw new NotImplementedException();
    }
}
