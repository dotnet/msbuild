// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;

namespace Microsoft.Extensions.EnvironmentAbstractions
{
    public struct DirectoryPath
    {
        public string Value { get; }

        /// <summary>
        /// Create DirectoryPath to repesent a absolute directory path. Note it may not exist.
        /// </summary>
        /// <param name="value">If the value is not rooted. Path.GetFullPath will be called during the consturctor.</param>
        public DirectoryPath(string value)
        {
            if (!Path.IsPathRooted(value))
            {
                value = Path.GetFullPath(value);
            }

            Value = value;
        }

        public DirectoryPath WithSubDirectories(params string[] paths)
        {
            string[] insertValueInFront = new string[paths.Length + 1];
            insertValueInFront[0] = Value;
            Array.Copy(paths, 0, insertValueInFront, 1, paths.Length);

            return new DirectoryPath(Path.Combine(insertValueInFront));
        }

        public FilePath WithFile(string fileName)
        {
            return new FilePath(Path.Combine(Value, fileName));
        }

        public string ToQuotedString()
        {
            return $"\"{Value}\"";
        }

        public string ToXmlEncodeString()
        {
            return System.Net.WebUtility.HtmlEncode(Value);
        }

        public override string ToString()
        {
            return ToQuotedString();
        }

        public DirectoryPath GetParentPath()
        {
            return new DirectoryPath(Path.GetDirectoryName(Value));
        }
    }
}
