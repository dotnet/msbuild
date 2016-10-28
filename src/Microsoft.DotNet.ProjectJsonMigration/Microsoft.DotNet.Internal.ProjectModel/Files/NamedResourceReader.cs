// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json.Linq;

namespace Microsoft.DotNet.Internal.ProjectModel.Files
{
    internal static class NamedResourceReader
    {
        public static IDictionary<string, string> ReadNamedResources(JObject rawProject, string projectFilePath)
        {
            JToken namedResourceToken;
            if (!rawProject.TryGetValue("namedResource", out namedResourceToken))
            {
                return new Dictionary<string, string>();
            }

            if (namedResourceToken.Type != JTokenType.Object)
            {
                throw FileFormatException.Create(
                    "Value must be object.",
                    rawProject.Value<JToken>("namedResource"), projectFilePath);
            }

            var namedResources = new Dictionary<string, string>();
            foreach (var namedResource in (JObject)namedResourceToken)
            {
                if (namedResource.Value.Type != JTokenType.String)
                {
                    throw FileFormatException.Create("Value must be string.", namedResource.Key, projectFilePath);
                }

                var resourcePath = namedResource.Value.ToString();
                if (resourcePath.Contains("*"))
                {
                    throw FileFormatException.Create("Value cannot contain wildcards.", resourcePath, projectFilePath);
                }

                var resourceFileFullPath =
                    Path.GetFullPath(Path.Combine(Path.GetDirectoryName(projectFilePath), resourcePath));

                if (namedResources.ContainsKey(namedResource.Key))
                {
                    throw FileFormatException.Create(
                        string.Format("The named resource {0} already exists.", namedResource.Key),
                        resourcePath,
                        projectFilePath);
                }

                namedResources.Add(namedResource.Key, resourceFileFullPath);
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
