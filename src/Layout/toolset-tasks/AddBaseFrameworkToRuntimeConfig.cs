// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.NET.Build.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace Microsoft.DotNet.Build.Tasks
{
    public sealed class AddBaseFrameworkToRuntimeConfig : Task
    {
        [Required]
        public string RuntimeConfigPath { get; set; }

        [Required]
        public string MicrosoftNetCoreAppVersion { get; set; }

        public override bool Execute()
        {
            JsonSerializer serializer = new JsonSerializer();
            serializer.ContractResolver = new CamelCasePropertyNamesContractResolver();
            serializer.Formatting = Formatting.Indented;
            serializer.DefaultValueHandling = DefaultValueHandling.Ignore;

            RuntimeConfig runtimeConfig;
            using (var sr = new StreamReader(RuntimeConfigPath))
            {
                runtimeConfig = serializer.Deserialize<RuntimeConfig>(new JsonTextReader(sr));
            }

            IEnumerable<RuntimeConfigFramework> currentFrameworks = runtimeConfig.RuntimeOptions.Frameworks ?? Enumerable.Empty<RuntimeConfigFramework>();
            if (runtimeConfig.RuntimeOptions.Framework != null)
            {
                currentFrameworks = currentFrameworks.Prepend(runtimeConfig.RuntimeOptions.Framework);
            }

            if (!currentFrameworks.Any(f => f.Name.Equals("Microsoft.NETCore.App", StringComparison.OrdinalIgnoreCase)))
            {
                var newFrameworks = currentFrameworks.Prepend(new RuntimeConfigFramework()
                {
                    Name = "Microsoft.NETCore.App",
                    Version = MicrosoftNetCoreAppVersion
                });

                runtimeConfig.RuntimeOptions.Framework = null;
                runtimeConfig.RuntimeOptions.Frameworks = newFrameworks.ToList();

                using (JsonTextWriter writer = new JsonTextWriter(new StreamWriter(File.Create(RuntimeConfigPath))))
                {
                    serializer.Serialize(writer, runtimeConfig);
                }
            }

            return true;
        }
    }
}
