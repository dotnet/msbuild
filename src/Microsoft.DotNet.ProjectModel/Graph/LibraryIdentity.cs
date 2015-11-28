// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using Microsoft.Extensions.Internal;
using NuGet;
using NuGet.Versioning;

namespace Microsoft.DotNet.ProjectModel.Graph
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
            return string.Equals(Name, other.Name) &&
                Equals(Version, other.Version) &&
                Equals(Type, other.Type);
        }

        public override bool Equals(object obj)
        {
            return obj is LibraryIdentity && Equals((LibraryIdentity)obj);
        }

        public override int GetHashCode()
        {
            var combiner = HashCodeCombiner.Start();
            combiner.Add(Name);
            combiner.Add(Version);
            combiner.Add(Type);
            return combiner.CombinedHash;
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
