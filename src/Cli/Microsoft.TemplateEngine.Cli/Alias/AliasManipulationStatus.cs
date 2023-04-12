// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

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
