// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable disable

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
