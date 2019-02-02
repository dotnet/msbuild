// Copyright (c) .NET Foundation and contributors. All rights reserved. 
// Licensed under the MIT license. See LICENSE file in the project root for full license information. 

using Newtonsoft.Json;
using Newtonsoft.Json.Linq; 
using System.IO;
 
namespace Microsoft.DotNet.Cli.Utils
{ 
    public class RuntimeConfig 
    { 
        public bool IsPortable { get; } 
        internal RuntimeConfigFramework Framework { get; }
 
        public RuntimeConfig(string runtimeConfigPath) 
        {
            JObject runtimeConfigJson;
            using (var streamReader = new StreamReader(File.OpenRead(runtimeConfigPath)))
            {
                runtimeConfigJson = OpenRuntimeConfig(streamReader);
            }

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
 
        private JObject OpenRuntimeConfig(StreamReader streamReader) 
        {
            var reader = new JsonTextReader(streamReader);

            return JObject.Load(reader);
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
