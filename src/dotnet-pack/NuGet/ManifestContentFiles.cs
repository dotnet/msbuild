// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Xml.Serialization;

namespace NuGet
{
    public class ManifestContentFiles
    {
        public string Include { get; set; }
        
        public string Exclude { get; set; }
        
        public string BuildAction { get; set; }

        public string CopyToOutput { get; set; }

        public string Flatten { get; set; }
    }
}