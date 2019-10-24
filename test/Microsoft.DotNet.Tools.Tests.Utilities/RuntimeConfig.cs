// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Text.Json;
using System.IO;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.TestFramework
{
    internal class RuntimeConfigFramework
    {
        public string Name { get; set; }
        public string Version { get; set; }
    }

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