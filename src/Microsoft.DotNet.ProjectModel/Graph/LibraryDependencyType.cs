// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace Microsoft.DotNet.ProjectModel.Graph
{
    public struct LibraryDependencyType : IEquatable<LibraryDependencyType>, IEquatable<string>
    {
        public static LibraryDependencyType Empty = new LibraryDependencyType();
        public static LibraryDependencyType Default = new LibraryDependencyType("default");
        public static LibraryDependencyType Build = new LibraryDependencyType("build");
        public static LibraryDependencyType Platform = new LibraryDependencyType("platform");

        public string Value { get; }

        private LibraryDependencyType(string value)
        {
            Value = value;
        }

        public static LibraryDependencyType Parse(string value) => new LibraryDependencyType(value.ToLowerInvariant());

        public override int GetHashCode() => Value.GetHashCode();

        public override bool Equals(object obj) => obj is LibraryDependencyType && Equals((LibraryDependencyType)obj);

        public bool Equals(string other) => string.Equals(Value, other, StringComparison.Ordinal);

        public bool Equals(LibraryDependencyType other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
    }
}
