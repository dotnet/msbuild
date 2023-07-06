// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

//Microsoft.NET.Build.Extensions.Tasks (net7.0) has nullables disabled
#pragma warning disable IDE0240 // Remove redundant nullable directive
#nullable disable
#pragma warning restore IDE0240 // Remove redundant nullable directive

namespace Microsoft.NET.Build.Tasks
{
    internal readonly struct Message
    {
        public readonly MessageLevel Level;
        public readonly string Code;
        public readonly string Text;
        public readonly string File;

        public Message(
            MessageLevel level,
            string text,
            string code = default,
            string file = default)
        {
            Level = level;
            Code = code;
            Text = text;
            File = file;
        }
    }
}
