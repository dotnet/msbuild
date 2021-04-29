// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Build.Framework
{
    /// <summary>
    ///
    /// </summary>
    internal interface IBuildEngineInternal
    {
        /// <summary>
        /// Returns the minimum message importance not guaranteed to be ignored by registered loggers.
        /// </summary>
        /// <remarks>
        /// Example: If we know that no logger is interested in MessageImportance.Low, this property returns
        /// MessageImportance.Normal. If loggers may consume any messages, this property returns MessageImportance.Low.
        /// </remarks>
        MessageImportance MinimumRequiredMessageImportance { get; }
    }
}
