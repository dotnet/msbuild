// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;

namespace Microsoft.Build.Framework
{
    /// <summary>
    /// This interface extends <see cref="IBuildEngine7" /> to allow tasks know if a warning
    /// they logged was turned into an error.
    /// </summary>
    public interface IBuildEngine8 : IBuildEngine7
    {
        public HashSet<string> WarningsAsErrors { get; }
    }
}
