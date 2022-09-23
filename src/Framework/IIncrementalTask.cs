// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable disable

namespace Microsoft.Build.Framework
{
    /// <summary>
    /// Interface for tasks which is incremental
    /// </summary>
    public interface IIncrementalTask
    {
        bool Question { get; set; }

        bool CanBeIncremental { get; }
    }
}
