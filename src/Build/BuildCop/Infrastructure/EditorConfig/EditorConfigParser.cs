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
using static Microsoft.Build.BuildCop.Infrastructure.EditorConfig.EditorConfigGlobsMatcher;

namespace Microsoft.Build.BuildCop.Infrastructure.EditorConfig
{
    internal class EditorConfigParser : IEditorConfigParser
    {
        private const string EditorconfigFile = ".editorconfig";
        private Dictionary<string, EditorConfigFile> editorConfigFileCache;

        internal EditorConfigParser()
        {
            editorConfigFileCache = new Dictionary<string, EditorConfigFile>();
        }

        public Dictionary<string, string> Parse(string filePath)
        {
            var editorConfigs = EditorConfigFileDiscovery(filePath);
            return MergeEditorConfigFiles(editorConfigs, filePath);
        }

        public IList<EditorConfigFile> EditorConfigFileDiscovery(string filePath)
        {
            var editorConfigDataFromFilesList = new List<EditorConfigFile>();

            var directoryOfTheProject = Path.GetDirectoryName(filePath);
            var editorConfigFile = FileUtilities.GetPathOfFileAbove(EditorconfigFile, directoryOfTheProject);

            while (editorConfigFile != string.Empty)
            {
                EditorConfigFile editorConfig;

                if (editorConfigFileCache.ContainsKey(editorConfigFile))
                {
                    editorConfig = editorConfigFileCache[editorConfigFile];
                }
                else
                {
                    var editorConfigfileContent = File.ReadAllText(editorConfigFile);
                    editorConfig = EditorConfigFile.Parse(editorConfigfileContent);
                    editorConfigFileCache[editorConfigFile] = editorConfig;
                }

                editorConfigDataFromFilesList.Add(editorConfig);

                if (editorConfig.IsRoot)
                {
                    break;
                }
                else
                {
                    editorConfigFile = FileUtilities.GetPathOfFileAbove(EditorconfigFile, Path.GetDirectoryName(Path.GetDirectoryName(editorConfigFile)));
                }
            }

            return editorConfigDataFromFilesList;
        }

        public Dictionary<string, string> MergeEditorConfigFiles(IEnumerable<EditorConfigFile> editorConfigFiles, string filePath)
        {
            var resultingDictionary = new Dictionary<string, string>();

            if (editorConfigFiles.Any())
            {
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
            }

            return resultingDictionary;
        }

        private static string NormalizeWithForwardSlash(string p) => Path.DirectorySeparatorChar == '/' ? p : p.Replace(Path.DirectorySeparatorChar, '/');
    }
}
