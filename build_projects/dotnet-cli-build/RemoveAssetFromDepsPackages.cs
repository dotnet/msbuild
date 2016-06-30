using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Net.Http;
using Microsoft.Build.Utilities;
using Microsoft.DotNet.Cli.Build.Framework;
using Microsoft.DotNet.InternalAbstractions;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using static Microsoft.DotNet.Cli.Build.Framework.BuildHelpers;

namespace Microsoft.DotNet.Cli.Build
{
    public class RemoveAssetFromDepsPackages : Task
    {
        [Required]
        public string DepsFile { get; set; }

        [Required]
        public string SectionName { get; set; }

        [Required]
        public string AssetPath { get; set; }

        public override bool Execute()
        {
            DoRemoveAssetFromDepsPackages(DepsFile, SectionName, AssetPath);

            return true;
        }

        public static void DoRemoveAssetFromDepsPackages(string depsFile, string sectionName, string assetPath)
        {
            JToken deps;
            using (var file = File.OpenText(depsFile))
            using (JsonTextReader reader = new JsonTextReader(file))
            {
                deps = JObject.ReadFrom(reader);
            }

            foreach (JProperty target in deps["targets"])
            {
                foreach (JProperty pv in target.Value.Children<JProperty>())
                {
                    var section = pv.Value[sectionName];
                    if (section != null)
                    {
                        foreach (JProperty relPath in section)
                        {
                            if (assetPath.Equals(relPath.Name))
                            {
                                relPath.Remove();
                                break;
                            }
                        }
                    }
                }
            }
            using (var file = File.CreateText(depsFile))
            using (var writer = new JsonTextWriter(file) { Formatting = Formatting.Indented })
            {
                deps.WriteTo(writer);
            }
        }
    }
}
