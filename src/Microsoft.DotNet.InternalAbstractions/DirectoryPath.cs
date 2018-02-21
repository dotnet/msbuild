// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;

namespace Microsoft.Extensions.EnvironmentAbstractions
{
    public struct DirectoryPath
    {
        public string Value { get; }

        public DirectoryPath(string value)
        {
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

        public override string ToString()
        {
            return ToQuotedString();
        }

        public DirectoryPath GetParentPath()
        {
            return new DirectoryPath(Directory.GetParent(Path.GetFullPath(Value)).FullName);
        }
    }
}
