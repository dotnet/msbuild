// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;

namespace Microsoft.Build.Framework
{
    /// <summary>
    /// This interface extends <see cref="IBuildEngine7" /> to allow tasks to know what
    /// warnings they logged were converted to errors.
    /// </summary>
    public interface IBuildEngine8 : IBuildEngine7
    {
        /// <summary>
        /// A set containing warning codes that the build engine converted into an error.
        /// </summary>
        public HashSet<string> WarningsLoggedAsErrors { get; }
    }
}
