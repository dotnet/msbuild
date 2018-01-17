// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.Shared;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;

using SdkResolverContextBase = Microsoft.Build.Framework.SdkResolverContext;

namespace NuGet.MSBuildSdkResolver
{
    /// <summary>
    /// Reads MSBuild related sections from a global.json.
    /// </summary>
    internal static class GlobalJsonReader
    {
        public const string GlobalJsonFileName = "global.json";

        public const string MSBuildSdksPropertyName = "msbuild-sdks";

        /// <summary>
        /// Walks up the directory tree to find the first global.json and reads the msbuild-sdks section.
        /// </summary>
        /// <returns>A <see cref="Dictionary{String,String}"/> of MSBuild SDK versions from a global.json if found, otherwise <code>null</code>.</returns>
        public static Dictionary<string, string> GetMSBuildSdkVersions(SdkResolverContextBase context)
        {
            DirectoryInfo projectDirectory = Directory.GetParent(context.ProjectFilePath);

            string globalJsonPath;

            if (projectDirectory == null
                || !projectDirectory.Exists
                || !FileUtilities.TryGetPathOfFileAbove(GlobalJsonFileName, projectDirectory.FullName, out globalJsonPath))
            {
                return null;
            }

            string contents = File.ReadAllText(globalJsonPath);

            // Look ahead in the contents to see if there is an msbuild-sdks section.  Deserializing the file requires us to load
            // Newtonsoft.Json which is 500 KB while a global.json is usually ~100 bytes of text.
            if (contents.IndexOf(MSBuildSdksPropertyName, StringComparison.Ordinal) == -1)
            {
                return null;
            }

            try
            {
                return Deserialize(contents);
            }
            catch (Exception e)
            {
                // Failed to parse "{0}". {1}
                string message = ResourceUtilities.FormatResourceString("FailedToParseGlobalJson", globalJsonPath, e.Message);
                context.Logger.LogMessage(message);
                return null;
            }
        }

        /// <summary>
        /// Deserializes a global.json and returns the MSBuild SDK versions
        /// </summary>
        private static Dictionary<string, string> Deserialize(string value)
        {
            GlobalJsonFile globalJsonFile = JsonConvert.DeserializeObject<GlobalJsonFile>(value);

            return globalJsonFile.MSBuildSdks;
        }

        private sealed class GlobalJsonFile
        {
            [JsonProperty(MSBuildSdksPropertyName)]
            public Dictionary<string, string> MSBuildSdks { get; set; }
        }
    }
}
