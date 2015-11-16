// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace Microsoft.Extensions.ProjectModel.Graph
{
    public struct LibraryDependencyType
    {
        private readonly LibraryDependencyTypeFlag _flags;

        public static LibraryDependencyType Default = LibraryDependencyType.Parse("default");

        private LibraryDependencyType(LibraryDependencyTypeFlag flags)
        {
            _flags = flags;
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
            return (_flags & flag) != 0;
        }
    }
}
