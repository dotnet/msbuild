// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.IO;

namespace Microsoft.DotNet.ProjectModel
{
    public class RuntimeConfig
    {
        public bool IsPortable { get; }
        public RuntimeConfigFramework Framework { get; }

        public RuntimeConfig(string runtimeConfigPath)
        {
            var runtimeConfigJson = OpenRuntimeConfig(runtimeConfigPath);

            Framework = ParseFramework(runtimeConfigJson);

            IsPortable = Framework != null;
        }

        public static bool IsApplicationPortable(string entryAssemblyPath)
        {
            var runtimeConfigFile = Path.ChangeExtension(entryAssemblyPath, FileNameSuffixes.RuntimeConfigJson);
            if (File.Exists(runtimeConfigFile))
            {
                var runtimeConfig = new RuntimeConfig(runtimeConfigFile);
                return runtimeConfig.IsPortable;
            }
            return false;
        }

        private JObject OpenRuntimeConfig(string runtimeConfigPath)
        {
            return JObject.Parse(File.ReadAllText(runtimeConfigPath));
        }

        private RuntimeConfigFramework ParseFramework(JObject runtimeConfigRoot)
        {
            var runtimeOptionsRoot = runtimeConfigRoot["runtimeOptions"];
            if (runtimeOptionsRoot == null)
            {
                return null;
            }

            var framework = (JObject) runtimeOptionsRoot["framework"];
            if (framework == null)
            {
                return null;
            }

            return RuntimeConfigFramework.ParseFromFrameworkRoot(framework);
        }
    }
}