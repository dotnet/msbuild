// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;

namespace Microsoft.DotNet.ProjectModel.Graph
{
    public struct LibraryType : IEquatable<LibraryType>
    {
        public static readonly LibraryType Package = new LibraryType(nameof(Package));
        public static readonly LibraryType Project = new LibraryType(nameof(Project));
        public static readonly LibraryType ReferenceAssembly = new LibraryType(nameof(ReferenceAssembly));

        // Default value
        public static readonly LibraryType Unspecified = new LibraryType();

        public string Value { get; }

        private LibraryType(string value)
        {
            Value = value;
        }

        public static bool TryParse(string value, out LibraryType type)
        {
            // We only support values we know about
            if (string.Equals(Package.Value, value, StringComparison.OrdinalIgnoreCase))
            {
                type = Package;
                return true;
            }
            else if (string.Equals(Project.Value, value, StringComparison.OrdinalIgnoreCase))
            {
                type = Project;
                return true;
            }
            type = Unspecified;
            return false;
        }

        public override string ToString()
        {
            return Value;
        }

        public bool CanSatisfyConstraint(LibraryType constraint)
        {
            // Reference assemblies must be explicitly asked for
            if (Equals(ReferenceAssembly, constraint))
            {
                return Equals(ReferenceAssembly, this);
            }
            else if (Equals(constraint, Unspecified))
            {
                return true;
            }
            else
            {
                return string.Equals(constraint.Value, Value, StringComparison.OrdinalIgnoreCase);
            }
        }

        public bool Equals(LibraryType other)
        {
            return string.Equals(other.Value, Value, StringComparison.OrdinalIgnoreCase);
        }

        public override bool Equals(object obj)
        {
            return obj is LibraryType && Equals((LibraryType)obj);
        }

        public static bool operator ==(LibraryType left, LibraryType right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(LibraryType left, LibraryType right)
        {
            return !Equals(left, right);
        }

        public override int GetHashCode()
        {
            if (string.IsNullOrEmpty(Value))
            {
                return 0;
            }
            return StringComparer.OrdinalIgnoreCase.GetHashCode(Value);
        }
    }
}