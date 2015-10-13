// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using NuGet;
using NuGet.Versioning;

namespace Microsoft.Extensions.ProjectModel.Graph
{
    public struct LibraryIdentity : IEquatable<LibraryIdentity>
    {
        public string Name { get; }

        public NuGetVersion Version { get; }

        public LibraryType Type { get; }

        public LibraryIdentity(string name, LibraryType type)
            : this(name, null, type)
        { }

        public LibraryIdentity(string name, NuGetVersion version, LibraryType type)
        {
            Name = name;
            Version = version;
            Type = type;
        }

        public override string ToString()
        {
            return $"{Name} {Version?.ToString()}";
        }

        public bool Equals(LibraryIdentity other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return string.Equals(Name, other.Name) &&
                Equals(Version, other.Version) &&
                Equals(Type, other.Type);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((LibraryIdentity)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ((Name != null ? Name.GetHashCode() : 0) * 397) ^
                    (Version != null ? Version.GetHashCode() : 0) ^
                    (Type.GetHashCode());
            }
        }

        public static bool operator ==(LibraryIdentity left, LibraryIdentity right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(LibraryIdentity left, LibraryIdentity right)
        {
            return !Equals(left, right);
        }

        public LibraryRange ToLibraryRange()
        {
            return new LibraryRange(Name, CreateVersionRange(Version), Type, LibraryDependencyType.Default);
        }

        private static VersionRange CreateVersionRange(NuGetVersion version)
        {
            return version == null ? null : new VersionRange(version, new FloatRange(NuGetVersionFloatBehavior.None));
        }

        public LibraryIdentity ChangeName(string name)
        {
            return new LibraryIdentity(name, Version, Type);
        }
    }
}
