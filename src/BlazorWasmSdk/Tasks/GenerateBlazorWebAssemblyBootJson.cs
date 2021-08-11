// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization.Json;
using System.Text;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using ResourceHashesByNameDictionary = System.Collections.Generic.Dictionary<string, string>;

namespace Microsoft.NET.Sdk.BlazorWebAssembly
{
    public class GenerateBlazorWebAssemblyBootJson : Task
    {
        [Required]
        public string AssemblyPath { get; set; }

        [Required]
        public ITaskItem[] Resources { get; set; }

        [Required]
        public bool DebugBuild { get; set; }

        [Required]
        public bool LinkerEnabled { get; set; }

        [Required]
        public bool CacheBootResources { get; set; }

        public bool LoadAllICUData { get; set; }

        public string InvariantGlobalization { get; set; }

        public ITaskItem[] ConfigurationFiles { get; set; }

        [Required]
        public string OutputPath { get; set; }

        public ITaskItem[] LazyLoadedAssemblies { get; set; }

        public override bool Execute()
        {
            using var fileStream = File.Create(OutputPath);
            var entryAssemblyName = AssemblyName.GetAssemblyName(AssemblyPath).Name;

            try
            {
                WriteBootJson(fileStream, entryAssemblyName);
            }
            catch (Exception ex)
            {
                Log.LogError(ex.ToString());
            }

            return !Log.HasLoggedErrors;
        }

        // Internal for tests
        public void WriteBootJson(Stream output, string entryAssemblyName)
        {
            var icuDataMode = ICUDataMode.Sharded;

            if (string.Equals(InvariantGlobalization, "true", StringComparison.OrdinalIgnoreCase))
            {
                icuDataMode = ICUDataMode.Invariant;
            }
            else if (LoadAllICUData)
            {
                icuDataMode = ICUDataMode.All;
            }

            var result = new BootJsonData
            {
                entryAssembly = entryAssemblyName,
                cacheBootResources = CacheBootResources,
                debugBuild = DebugBuild,
                linkerEnabled = LinkerEnabled,
                resources = new ResourcesData(),
                config = new List<string>(),
                icuDataMode = icuDataMode,
            };

            // Build a two-level dictionary of the form:
            // - assembly:
            //   - UriPath (e.g., "System.Text.Json.dll")
            //     - ContentHash (e.g., "4548fa2e9cf52986")
            // - runtime:
            //   - UriPath (e.g., "dotnet.js")
            //     - ContentHash (e.g., "3448f339acf512448")
            if (Resources != null)
            {
                var remainingLazyLoadAssemblies = new List<ITaskItem>(LazyLoadedAssemblies ?? Array.Empty<ITaskItem>());
                var resourceData = result.resources;
                foreach (var resource in Resources)
                {
                    ResourceHashesByNameDictionary resourceList;

                    var fileName = resource.GetMetadata("FileName");
                    var assetTraitName = resource.GetMetadata("AssetTraitName");
                    var assetTraitValue = resource.GetMetadata("AssetTraitValue");
                    var resourceName = Path.GetFileName(resource.GetMetadata("RelativePath"));

                    if (TryGetLazyLoadedAssembly(resourceName, out var lazyLoad))
                    {
                        Log.LogMessage("Candidate '{0}' is defined as a lazy loaded assembly.", resource.ItemSpec);
                        remainingLazyLoadAssemblies.Remove(lazyLoad);
                        resourceData.lazyAssembly ??= new ResourceHashesByNameDictionary();
                        resourceList = resourceData.lazyAssembly;
                    }
                    else if (string.Equals("Culture", assetTraitName))
                    {
                        Log.LogMessage("Candidate '{0}' is defined as satellite assembly with culture '{1}'.", resource.ItemSpec, assetTraitValue);
                        resourceData.satelliteResources ??= new Dictionary<string, ResourceHashesByNameDictionary>(StringComparer.OrdinalIgnoreCase);
                        resourceName = assetTraitValue + "/" + resourceName;

                        if (!resourceData.satelliteResources.TryGetValue(assetTraitValue, out resourceList))
                        {
                            resourceList = new ResourceHashesByNameDictionary();
                            resourceData.satelliteResources.Add(assetTraitValue, resourceList);
                        }
                    }
                    else if (string.Equals("symbol", assetTraitValue, StringComparison.OrdinalIgnoreCase))
                    {
                        if (TryGetLazyLoadedAssembly($"{fileName}.dll", out _))
                        {
                            Log.LogMessage("Candidate '{0}' is defined as a lazy loaded symbols file.", resource.ItemSpec);
                            resourceData.lazyAssembly ??= new ResourceHashesByNameDictionary();
                            resourceList = resourceData.lazyAssembly;
                        }
                        else
                        {
                            Log.LogMessage("Candidate '{0}' is defined as symbols file.", resource.ItemSpec);
                            resourceData.pdb ??= new ResourceHashesByNameDictionary();
                            resourceList = resourceData.pdb;
                        }
                    }
                    else if (string.Equals("runtime", assetTraitValue, StringComparison.OrdinalIgnoreCase))
                    {
                        Log.LogMessage("Candidate '{0}' is defined as an app assembly.", resource.ItemSpec);
                        resourceList = resourceData.assembly;
                    }
                    else if (string.Equals(assetTraitName, "BlazorWebAssemblyResource", StringComparison.OrdinalIgnoreCase) &&
                            string.Equals(assetTraitValue, "native", StringComparison.OrdinalIgnoreCase))
                    {
                        Log.LogMessage("Candidate '{0}' is defined as a native application resource.", resource.ItemSpec);
                        resourceList = resourceData.runtime;
                    }
                    else if (string.Equals("JSModule", assetTraitName, StringComparison.OrdinalIgnoreCase) &&
                             string.Equals(assetTraitValue, "JSLibraryModule", StringComparison.OrdinalIgnoreCase))
                    {
                        Log.LogMessage("Candidate '{0}' is defined as a library initializer resource.", resource.ItemSpec);
                        resourceData.libraryInitializers ??= new();
                        resourceList = resourceData.libraryInitializers;
                        AddResourceToList(resource, resourceList, resource.GetMetadata("TargetPath"));
                        continue;
                    }
                    else if (string.Equals("BlazorWebAssemblyResource", assetTraitName, StringComparison.OrdinalIgnoreCase) &&
                             assetTraitValue.StartsWith("extension:", StringComparison.OrdinalIgnoreCase))
                    {
                        Log.LogMessage("Candidate '{0}' is defined as an extension resource '{1}'.", resource.ItemSpec, assetTraitValue);
                        var extensionName = assetTraitValue.Substring("extension:".Length);
                        resourceData.extensions ??= new();
                        if (!resourceData.extensions.TryGetValue(extensionName, out resourceList))
                        {
                            resourceList = new();
                            resourceData.extensions[extensionName] = resourceList;
                        }
                        AddResourceToList(resource, resourceList, resource.GetMetadata("TargetPath"));
                        continue;
                    }
                    else
                    {
                        Log.LogMessage("Skipping resource '{0}' since it doesn't belong to a defined category.", resource.ItemSpec);
                        // This should include items such as XML doc files, which do not need to be recorded in the manifest.
                        continue;
                    }

                    AddResourceToList(resource, resourceList, resourceName);
                }

                if (remainingLazyLoadAssemblies.Count > 0)
                {
                    const string message = "Unable to find '{0}' to be lazy loaded later. Confirm that project or " +
                        "package references are included and the reference is used in the project.";

                    Log.LogError(
                        subcategory: null,
                        errorCode: "BLAZORSDK1001",
                        helpKeyword: null,
                        file: null,
                        lineNumber: 0,
                        columnNumber: 0,
                        endLineNumber: 0,
                        endColumnNumber: 0,
                        message: message,
                        string.Join(";", LazyLoadedAssemblies.Select(a => a.ItemSpec)));

                    return;
                }
            }

            if (ConfigurationFiles != null)
            {
                foreach (var configFile in ConfigurationFiles)
                {
                    result.config.Add(Path.GetFileName(configFile.ItemSpec));
                }
            }

            var serializer = new DataContractJsonSerializer(typeof(BootJsonData), new DataContractJsonSerializerSettings
            {
                UseSimpleDictionaryFormat = true
            });

            using var writer = JsonReaderWriterFactory.CreateJsonWriter(output, Encoding.UTF8, ownsStream: false, indent: true);
            serializer.WriteObject(writer, result);

            void AddResourceToList(ITaskItem resource, ResourceHashesByNameDictionary resourceList, string resourceKey)
            {
                if (!resourceList.ContainsKey(resourceKey))
                {
                    Log.LogMessage("Added resource '{0}' to the manifest.", resource.ItemSpec);
                    resourceList.Add(resourceKey, $"sha256-{resource.GetMetadata("FileHash")}");
                }
            }
        }

        private bool TryGetLazyLoadedAssembly(string fileName, out ITaskItem lazyLoadedAssembly)
        {
            return (lazyLoadedAssembly = LazyLoadedAssemblies?.SingleOrDefault(a => a.ItemSpec == fileName)) != null;
        }
    }
}
