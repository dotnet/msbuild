// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace Microsoft.DotNet.ProjectModel.Graph
{
    public struct LibraryDependencyType
    {
        public static LibraryDependencyType Default = LibraryDependencyType.Parse("default");

        public static LibraryDependencyType Build = LibraryDependencyType.Parse("build");

        public LibraryDependencyTypeFlag Flags { get; private set; }

        private LibraryDependencyType(LibraryDependencyTypeFlag flags)
        {
            Flags = flags;
        }

        public static LibraryDependencyType Parse(string keyword)
        {
            if (string.Equals(keyword, "default", StringComparison.OrdinalIgnoreCase) ||
                string.IsNullOrEmpty(keyword)) // Need the default value of the struct to behave like "default"
            {
                return new LibraryDependencyType(
                       LibraryDependencyTypeFlag.MainReference |
                       LibraryDependencyTypeFlag.MainSource |
                       LibraryDependencyTypeFlag.MainExport |
                       LibraryDependencyTypeFlag.RuntimeComponent |
                       LibraryDependencyTypeFlag.BecomesNupkgDependency);
            }

            if (string.Equals(keyword, "build", StringComparison.OrdinalIgnoreCase))
            {
                return new LibraryDependencyType(
                    LibraryDependencyTypeFlag.MainSource |
                    LibraryDependencyTypeFlag.PreprocessComponent);
            }

            throw new InvalidOperationException(string.Format("unknown keyword {0}", keyword));
        }

        public bool HasFlag(LibraryDependencyTypeFlag flag)
        {
            return (Flags & flag) != 0;
        }
    }
}
