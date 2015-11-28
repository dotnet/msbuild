// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Extensions.JsonParser.Sources;

namespace Microsoft.DotNet.ProjectModel
{
    public class GlobalSettings
    {
        public const string FileName = "global.json";

        public IList<string> ProjectSearchPaths { get; private set; }
        public string PackagesPath { get; private set; }
        public string FilePath { get; private set; }
        public string DirectoryPath
        {
            get
            {
                return Path.GetFullPath(Path.GetDirectoryName(FilePath));
            }
        }

        public static bool TryGetGlobalSettings(string path, out GlobalSettings globalSettings)
        {
            globalSettings = null;
            string globalJsonPath = null;

            if (Path.GetFileName(path) == FileName)
            {
                globalJsonPath = path;
                path = Path.GetDirectoryName(path);
            }
            else if (!HasGlobalFile(path))
            {
                return false;
            }
            else
            {
                globalJsonPath = Path.Combine(path, FileName);
            }

            globalSettings = new GlobalSettings();

            try
            {
                using (var fs = File.OpenRead(globalJsonPath))
                {
                    var reader = new StreamReader(fs);
                    var jobject = JsonDeserializer.Deserialize(reader) as JsonObject;

                    if (jobject == null)
                    {
                        throw new InvalidOperationException("The JSON file can't be deserialized to a JSON object.");
                    }

                    var projectSearchPaths = jobject.ValueAsStringArray("projects") ??
                                             jobject.ValueAsStringArray("sources") ??
                                             new string[] { };

                    globalSettings.ProjectSearchPaths = new List<string>(projectSearchPaths);
                    globalSettings.PackagesPath = jobject.ValueAsString("packages");
                    globalSettings.FilePath = globalJsonPath;
                }
            }
            catch (Exception ex)
            {
                throw FileFormatException.Create(ex, globalJsonPath);
            }

            return true;
        }

        public static bool HasGlobalFile(string path)
        {
            string projectPath = Path.Combine(path, FileName);

            return File.Exists(projectPath);
        }

    }
}
