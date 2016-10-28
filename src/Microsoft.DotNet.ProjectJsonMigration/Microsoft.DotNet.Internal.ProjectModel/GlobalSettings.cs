// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using System.Linq;

namespace Microsoft.DotNet.Internal.ProjectModel
{
    internal class GlobalSettings
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

            try
            {
                using (var fs = File.OpenRead(globalJsonPath))
                {
                    globalSettings = GetGlobalSettings(fs, globalJsonPath);
                }
            }
            catch (Exception ex)
            {
                throw FileFormatException.Create(ex, globalJsonPath);
            }

            return true;
        }

        public static GlobalSettings GetGlobalSettings(Stream fs, string globalJsonPath)
        {
            var globalSettings = new GlobalSettings();

            var reader = new StreamReader(fs);
            JObject jobject;
            try
            {
                jobject = JObject.Parse(reader.ReadToEnd());
            }
            catch (JsonReaderException)
            {
                throw new InvalidOperationException("The JSON file can't be deserialized to a JSON object.");
            }

            IEnumerable<string> projectSearchPaths = Enumerable.Empty<string>();
            JToken projectSearchPathsToken;
            if (jobject.TryGetValue("projects", out projectSearchPathsToken) &&
                projectSearchPathsToken.Type == JTokenType.Array)
            {
                projectSearchPaths = projectSearchPathsToken.Values<string>();
            }
            else if (jobject.TryGetValue("sources", out projectSearchPathsToken) &&
                     projectSearchPathsToken.Type == JTokenType.Array)
            {
                projectSearchPaths = projectSearchPathsToken.Values<string>();
            }

            globalSettings.ProjectSearchPaths = new List<string>(projectSearchPaths);
            globalSettings.PackagesPath = jobject.Value<string>("packages");
            globalSettings.FilePath = globalJsonPath;

            return globalSettings;
        }

        public static bool HasGlobalFile(string path)
        {
            string projectPath = Path.Combine(path, FileName);

            return File.Exists(projectPath);
        }

    }
}
