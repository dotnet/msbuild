// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO;

namespace Microsoft.Extensions.EnvironmentAbstractions
{
    public struct FilePath
    {
        public string Value { get; }

        public FilePath(string value)
        {
            Value = value;
        }

        public string ToQuotedString()
        {
            return $"\"{Value}\"";
        }

        public override string ToString()
        {
            return ToQuotedString();
        }

        public DirectoryPath GetDirectoryPath()
        {
            return new DirectoryPath(Path.GetDirectoryName(Value));
        }
    }
}
