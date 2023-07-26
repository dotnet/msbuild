// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json;

namespace Microsoft.DotNet.Tools.Common
{
    public class RuntimeConfig
    {
        public bool IsPortable { get; }
        internal RuntimeConfigFramework Framework { get; }

        public RuntimeConfig(string runtimeConfigPath)
        {
            var jsonDocumentOptions = new JsonDocumentOptions
            {
                AllowTrailingCommas = true,
                CommentHandling = JsonCommentHandling.Skip
            };

            using (var stream = File.OpenRead(runtimeConfigPath))
            using (JsonDocument doc = JsonDocument.Parse(stream, jsonDocumentOptions))
            {
                JsonElement root = doc.RootElement;
                if (root.TryGetProperty("runtimeOptions", out var runtimeOptionsRoot))
                {
                    if (runtimeOptionsRoot.TryGetProperty("framework", out var framework))
                    {
                        var runtimeConfigFramework = new RuntimeConfigFramework();
                        string name = null;
                        string version = null;
                        foreach (var property in framework.EnumerateObject())
                        {
                            if (property.Name.Equals("name", StringComparison.OrdinalIgnoreCase))
                            {
                                name = property.Value.GetString();
                            }

                            if (property.Name.Equals("version", StringComparison.OrdinalIgnoreCase))
                            {
                                version = property.Value.GetString();
                            }
                        }

                        if (name == null || version == null)
                        {
                            Framework = null;
                        }
                        else
                        {
                            Framework = new RuntimeConfigFramework
                            {
                                Name = name,
                                Version = version
                            };
                        }
                    }
                    else
                    {
                        Framework = null;
                    }
                }
            }

            IsPortable = Framework != null;
        }
    }
}
