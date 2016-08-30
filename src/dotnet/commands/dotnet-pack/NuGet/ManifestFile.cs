// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace NuGet.Legacy
{
    public class ManifestFile
    {
        public ManifestFile(string source, string target, string exclude)
        {
            Source = source;
            Target = target;
            Exclude = exclude;
        }

        public string Source { get; }
        
        public string Target { get; }

        public string Exclude { get; }
    }
}
