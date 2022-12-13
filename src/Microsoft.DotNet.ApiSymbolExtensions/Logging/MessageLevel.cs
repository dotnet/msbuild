// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.DotNet.ApiSymbolExtensions.Logging
{
    public enum MessageLevel
    {
        // For efficient conversion, positive values map directly to MessageImportance:
        LowImportance = MessageImportance.Low,
        NormalImportance = MessageImportance.Normal,
        HighImportance = MessageImportance.High,

        // And negative values are for levels that are not informational (warning/error):
        Warning = -1,
        Error = -2,
    }
}
