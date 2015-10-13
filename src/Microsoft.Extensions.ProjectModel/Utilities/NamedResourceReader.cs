// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json.Linq;

namespace Microsoft.Extensions.ProjectModel.Utilities
{
    internal static class NamedResourceReader
    {
        public static IDictionary<string, string> ReadNamedResources(JObject rawProject, string projectFilePath)
        {
            var prop = rawProject.Property("namedResource");
            if(prop == null)
            {
                return new Dictionary<string, string>();
            }

            var namedResourceToken = prop.Value as JObject;
            if (namedResourceToken == null)
            {
                throw FileFormatException.Create("Value must be object.", prop.Value, projectFilePath);
            }

            var namedResources = new Dictionary<string, string>();

            foreach (var namedResource in namedResourceToken)
            {
                if (namedResource.Value.Type != JTokenType.String)
                {
                    throw FileFormatException.Create("Value must be string.", namedResource.Value, projectFilePath);
                }
                var resourcePath = namedResource.Value.Value<string>();

                if (resourcePath.Contains("*"))
                {
                    throw FileFormatException.Create("Value cannot contain wildcards.", namedResource.Value, projectFilePath);
                }

                var resourceFileFullPath = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(projectFilePath), resourcePath));

                if (namedResources.ContainsKey(namedResource.Key))
                {
                    throw FileFormatException.Create(
                        $"The named resource {namedResource.Key} already exists.",
                        namedResource.Value,
                        projectFilePath);
                }

                namedResources.Add(
                    namedResource.Key,
                    resourceFileFullPath);
            }

            return namedResources;
        }

        public static void ApplyNamedResources(IDictionary<string, string> namedResources, IDictionary<string, string> resources)
        {
            foreach (var namedResource in namedResources)
            {
                // The named resources dictionary is like the project file
                // key = name, value = path to resource
                if (resources.ContainsKey(namedResource.Value))
                {
                    resources[namedResource.Value] = namedResource.Key;
                }
                else
                {
                    resources.Add(namedResource.Value, namedResource.Key);
                }
            }
        }
    }
}