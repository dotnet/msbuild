// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Extensions.EnvironmentAbstractions
{
    public struct DirectoryPath
    {
        public string Value { get; }

        /// <summary>
        /// Create DirectoryPath to represent an absolute directory path. Note it may not exist.
        /// </summary>
        /// <param name="value">If the value is not rooted. Path.GetFullPath will be called during the constructor.</param>
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
            // new DirectoryInfo and directoryInfo.Parent does not have side effects

            var directoryInfo = new DirectoryInfo(Value);

            DirectoryInfo parentDirectory = directoryInfo.Parent;
            if (directoryInfo.Parent is null)
            {
                throw new InvalidOperationException(Value + " does not have parent directory.");
            }

            return new DirectoryPath(parentDirectory.FullName);
        }

        public DirectoryPath? GetParentPathNullable()
        {
            var directoryInfo = new DirectoryInfo(Value);

            DirectoryInfo parentDirectory = directoryInfo.Parent;
            if (directoryInfo.Parent is null)
            {
                return null;
            }

            return new DirectoryPath(parentDirectory.FullName);
        }

        internal static void MoveDirectory(string sourcePath, string destPath)
        {
            try
            {
                Directory.Move(sourcePath, destPath);
            }
            catch (IOException)
            {
                // Note: cannot use Directory.Move because it errors when copying across mounts
                CopyDirectoryAcrossMounts(sourcePath, destPath);
            }
        }

        private static void CopyDirectoryAcrossMounts(string sourcePath, string destPath)
        {
            if (!Directory.Exists(destPath))
            {
                Directory.CreateDirectory(destPath);
            }

            foreach (var dir in Directory.GetDirectories(sourcePath))
            {
                CopyDirectoryAcrossMounts(dir, Path.Combine(destPath, Path.GetFileName(dir)));
            }

            foreach (var file in Directory.GetFiles(sourcePath))
            {
                new FileInfo(file).CopyTo(Path.Combine(destPath, Path.GetFileName(file)), true);
            }
        }
    }
}
