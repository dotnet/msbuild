// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable disable

using System;
using Microsoft.Build.Framework;

namespace Microsoft.NET.Build.Tasks
{
    internal enum MessageLevel
    {
        // For efficient conversion, positive values map directly to MessageImportance:
        LowImportance = MessageImportance.Low,
        NormalImportance = MessageImportance.Normal,
        HighImportance = MessageImportance.High,

        // And negative values are for levels that are not informational (warning/error):
        Warning = -1,
        Error = -2,
    }

    internal static class MessageLevelExtensions
    {
        public static MessageLevel ToLevel(this MessageImportance importance)
            => (MessageLevel)(importance);

        public static MessageImportance ToImportance(this MessageLevel level)
            => level >= 0 ? (MessageImportance)level : throw new InvalidCastException();
    }
}
