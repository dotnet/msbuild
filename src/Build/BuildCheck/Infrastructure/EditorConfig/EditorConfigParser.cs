// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing.Design;
using System.IO;
using System.Linq;
using System.Reflection.Metadata.Ecma335;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Build.Shared;
using static Microsoft.Build.BuildCheck.Infrastructure.EditorConfig.EditorConfigGlobsMatcher;

namespace Microsoft.Build.BuildCheck.Infrastructure.EditorConfig
{
    internal sealed class EditorConfigParser
    {
        private const string EditorconfigFile = ".editorconfig";

        /// <summary>
        /// Cache layer of the parsed editor configs the key is the path to the .editorconfig file.
        /// </summary>
        private readonly Dictionary<string, EditorConfigFile> _editorConfigFileCache = new Dictionary<string, EditorConfigFile>(StringComparer.InvariantCultureIgnoreCase);

        internal Dictionary<string, string> Parse(string filePath)
        {
            var editorConfigs = DiscoverEditorConfigFiles(filePath);
            return MergeEditorConfigFiles(editorConfigs, filePath);
        }

        /// <summary>
        /// Fetches the list of EditorconfigFile ordered from the nearest to the filePath.
        /// </summary>
        /// <param name="filePath"></param>
        internal List<EditorConfigFile> DiscoverEditorConfigFiles(string filePath)
        {
            var editorConfigDataFromFilesList = new List<EditorConfigFile>();

            var directoryOfTheProject = Path.GetDirectoryName(filePath);
            var editorConfigFilePath = FileUtilities.GetPathOfFileAbove(EditorconfigFile, directoryOfTheProject);

            while (editorConfigFilePath != string.Empty)
            {
                if (!_editorConfigFileCache.TryGetValue(editorConfigFilePath, out var editorConfig))
                {
                    using (FileStream stream = new FileStream(editorConfigFilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                    {
                        using StreamReader sr = new StreamReader(editorConfigFilePath);
                        var editorConfigfileContent = sr.ReadToEnd();
                        editorConfig = EditorConfigFile.Parse(editorConfigfileContent);
                        _editorConfigFileCache[editorConfigFilePath] = editorConfig;
                    }
                }

                editorConfigDataFromFilesList.Add(editorConfig);

                if (editorConfig.IsRoot)
                {
                    break;
                }
                else
                {
                    // search in upper directory
                    editorConfigFilePath = FileUtilities.GetPathOfFileAbove(EditorconfigFile, Path.GetDirectoryName(Path.GetDirectoryName(editorConfigFilePath)));
                }
            }

            return editorConfigDataFromFilesList;
        }

        /// <summary>
        /// Retrieves the config dictionary from the sections that matched the filePath. 
        /// </summary>
        /// <param name="editorConfigFiles"></param>
        /// <param name="filePath"></param>
        internal Dictionary<string, string> MergeEditorConfigFiles(List<EditorConfigFile> editorConfigFiles, string filePath)
        {
            var resultingDictionary = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase);
            editorConfigFiles.Reverse();

            foreach (var configData in editorConfigFiles)
            {
                foreach (var section in configData.NamedSections)
                {
                    SectionNameMatcher? sectionNameMatcher = TryCreateSectionNameMatcher(section.Name);
                    if (sectionNameMatcher != null)
                    {
                        if (sectionNameMatcher.Value.IsMatch(NormalizeWithForwardSlash(filePath)))
                        {
                            foreach (var property in section.Properties)
                            {
                                resultingDictionary[property.Key] = property.Value;
                            }
                        }
                    }
                }
            }
            
            return resultingDictionary;
        }

        internal static string NormalizeWithForwardSlash(string p) => Path.DirectorySeparatorChar == '/' ? p : p.Replace(Path.DirectorySeparatorChar, '/');
    }
}
