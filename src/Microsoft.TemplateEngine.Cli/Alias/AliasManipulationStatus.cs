// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.TemplateEngine.Cli.Alias
{
    internal enum AliasManipulationStatus
    {
        Created,
        Removed,
        RemoveNonExistentFailed,    // for trying to remove an alias that didn't exist.
        Updated,
        WouldCreateCycle,
        InvalidInput
    }
}
