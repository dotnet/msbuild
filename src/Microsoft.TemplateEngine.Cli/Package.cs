// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace Microsoft.TemplateEngine.Cli
{
    internal class Package
    {
        public string Name { get; }

        public string Version { get; }

        public Package(string name, string version)
        {
            Name = name;
            Version = version;
        }

        public static bool TryParse(string spec, out Package package)
        {
            int index = 0;
            if (string.IsNullOrEmpty(spec) || (index = spec.IndexOf("::", StringComparison.Ordinal)) < 0 || index == spec.Length - 1)
            {
                package = null;
                return false;
            }

            string name = spec.Substring(0, index);
            string version = spec.Substring(index + 2);

            package = new Package(name, version);
            return true;
        }
    }
}
