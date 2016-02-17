// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;

namespace Microsoft.DotNet.ProjectModel.Graph
{
    public struct BuildAction : IEquatable<BuildAction>
    {
        public static readonly BuildAction Compile = new BuildAction(nameof(Compile));
        public static readonly BuildAction EmbeddedResource = new BuildAction(nameof(EmbeddedResource));
        public static readonly BuildAction Resource = new BuildAction(nameof(Resource));

        // Default value
        public static readonly BuildAction None = new BuildAction(nameof(None));

        public string Value { get; }

        private BuildAction(string value)
        {
            Value = value;
        }

        public static bool TryParse(string value, out BuildAction type)
        {
            // We only support values we know about
            if (string.Equals(Compile.Value, value, StringComparison.OrdinalIgnoreCase))
            {
                type = Compile;
                return true;
            }
            else if (string.Equals(EmbeddedResource.Value, value, StringComparison.OrdinalIgnoreCase))
            {
                type = EmbeddedResource;
                return true;
            }
            else if (string.Equals(Resource.Value, value, StringComparison.OrdinalIgnoreCase))
            {
                type = Resource;
                return true;
            }
            else if (string.Equals(None.Value, value, StringComparison.OrdinalIgnoreCase))
            {
                type = None;
                return true;
            }
            type = None;
            return false;
        }

        public override string ToString()
        {
            return $"BuildAction.{Value}";
        }

        public bool Equals(BuildAction other)
        {
            return string.Equals(other.Value, Value, StringComparison.OrdinalIgnoreCase);
        }

        public override bool Equals(object obj)
        {
            return obj is BuildAction && Equals((BuildAction)obj);
        }

        public static bool operator ==(BuildAction left, BuildAction right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(BuildAction left, BuildAction right)
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