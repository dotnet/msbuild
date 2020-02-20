// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO;

namespace Microsoft.Extensions.EnvironmentAbstractions
{
    public struct FilePath
    {
        public string Value { get; }

        /// <summary>
        /// Create FilePath to represent an absolute file path. Note it may not exist.
        /// </summary>
        /// <param name="value">If the value is not rooted. Path.GetFullPath will be called during the constructor.</param>
        public FilePath(string value)
        {
            if (!Path.IsPathRooted(value))
            {
                value = Path.GetFullPath(value);
            }

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
