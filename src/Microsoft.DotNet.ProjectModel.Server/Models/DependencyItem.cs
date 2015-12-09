// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.DotNet.ProjectModel.Server.Models
{
    public class DependencyItem
    {
        public string Name { get; set; }

        public string Version { get; set; }

        public override bool Equals(object obj)
        {
            var other = obj as DependencyItem;
            return other != null &&
                   string.Equals(Name, other.Name) &&
                   object.Equals(Version, other.Version);
        }

        public override int GetHashCode()
        {
            // These objects are currently POCOs and we're overriding equals
            // so that things like Enumerable.SequenceEqual just work.
            return base.GetHashCode();
        }
    }
}
