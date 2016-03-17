// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.ProjectModel;
using Microsoft.DotNet.ProjectModel.Compilation;
using Microsoft.DotNet.ProjectModel.Graph;
using Microsoft.Extensions.DependencyModel;
using NuGet.Frameworks;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;

namespace Microsoft.DotNet.Cli.Utils
{
    public static class RuntimeConfigGenerator
    {
        // GROOOOOSS
        private static readonly string RedistPackageName = "Microsoft.NETCore.App";

        public static void WriteRuntimeConfigToFile(LibraryExporter exporter, string runtimeConfigJsonFile)
        {
            // TODO: Suppress this file if there's nothing to write? RuntimeOutputFiles would have to be updated
            // in order to prevent breaking incremental compilation...

            var json = new JObject();
            var runtimeOptions = new JObject();
            json.Add("runtimeOptions", runtimeOptions);

            var redistExport = exporter
                .GetAllExports()
                .FirstOrDefault(l => l.Library.Identity.Name.Equals(RedistPackageName, StringComparison.OrdinalIgnoreCase));
            if (redistExport != null)
            {
                var framework = new JObject(
                    new JProperty("name", redistExport.Library.Identity.Name),
                    new JProperty("version", redistExport.Library.Identity.Version.ToNormalizedString()));
                runtimeOptions.Add("framework", framework);
            }

            using (var writer = new JsonTextWriter(new StreamWriter(File.Create(runtimeConfigJsonFile))))
            {
                writer.Formatting = Formatting.Indented;
                json.WriteTo(writer);
            }
        
        }
    }
}