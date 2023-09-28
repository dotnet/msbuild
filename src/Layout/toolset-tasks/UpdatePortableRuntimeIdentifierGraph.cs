// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Microsoft.DotNet.Cli.Build
{
    public class UpdatePortableRuntimeIdentifierGraph : Task
    {
        [Required]
        public string InputFile { get; set; }

        [Required]
        public string OutputFile { get; set; }


        //  ItemSpec should be a RID, and "Imports" metadata should be a semicolon-separated list of RIDs that the ItemSpec RID imports
        public ITaskItem[] AdditionalRuntimeIdentifiers { get; set; }

        public override bool Execute()
        {
            JToken json;

            using (var file = File.OpenText(InputFile))
            using (JsonTextReader reader = new(file))
            {
                json = JToken.ReadFrom(reader);
            }

            JObject runtimes = (JObject)json["runtimes"];

            if (AdditionalRuntimeIdentifiers != null)
            {
                foreach (var rid in AdditionalRuntimeIdentifiers)
                {
                    var importedRids = rid.GetMetadata("Imports").Split(';');
                    runtimes.Add(rid.ItemSpec, new JObject(new JProperty("#import", new JArray(importedRids))));
                }
            }

            using (var file = File.CreateText(OutputFile))
            using (var writer = new JsonTextWriter(file) { Formatting = Formatting.Indented })
            {
                json.WriteTo(writer);
            }

            return true;
        }
    }
}
