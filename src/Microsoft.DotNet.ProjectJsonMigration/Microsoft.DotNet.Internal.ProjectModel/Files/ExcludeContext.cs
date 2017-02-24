// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace Microsoft.DotNet.Internal.ProjectModel.Files
{
    // Similar to IncludeContext, except that it replaces the include information with the exclude information and clears
    // out the exclude information. This is to be used by migration to do CopyToOutput with Never set as its metadata.
    internal class ExcludeContext : IncludeContext
    {
        public ExcludeContext(
            string sourceBasePath,
            string option,
            JObject rawObject,
            string[] defaultBuiltInInclude,
            string[] defaultBuiltInExclude) : base(
                sourceBasePath,
                option,
                rawObject,
                defaultBuiltInInclude,
                defaultBuiltInExclude)
        {
            IncludePatterns = ExcludePatterns;
            ExcludePatterns = new List<string>();

            IncludeFiles = ExcludeFiles;
            ExcludeFiles = new List<string>();

            BuiltInsInclude = BuiltInsExclude;
            BuiltInsExclude = new List<string>();

            if (Mappings != null)
            {
                var newMappings = new Dictionary<string, IncludeContext>();
                foreach (var mapping in Mappings)
                {
                    newMappings.Add(mapping.Key, new ExcludeContext(
                        mapping.Value.SourceBasePath,
                        mapping.Value.Option,
                        mapping.Value.RawObject,
                        mapping.Value.BuiltInsInclude?.ToArray(),
                        mapping.Value.BuiltInsExclude?.ToArray()));
                }

                Mappings = newMappings;
            }
        }
    }
}