// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

//Microsoft.NET.Build.Extensions.Tasks (net7.0) has nullables disabled
#pragma warning disable IDE0240 // Remove redundant nullable directive
#nullable disable
#pragma warning restore IDE0240 // Remove redundant nullable directive

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
