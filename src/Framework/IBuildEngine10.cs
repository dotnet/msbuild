// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

namespace Microsoft.Build.Framework
{
    /// <summary>
    /// This interface extends <see cref="IBuildEngine9" /> to provide a reference to the <see cref="EngineServices" /> class.
    /// Future engine API should be added to the class as opposed to introducing yet another version of the IBuildEngine interface.
    /// </summary>
    public interface IBuildEngine10 : IBuildEngine9
    {
        /// <summary>
        /// Returns the new build engine interface.
        /// </summary>
        EngineServices EngineServices { get; }
    }
}
