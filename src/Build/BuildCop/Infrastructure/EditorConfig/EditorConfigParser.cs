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
        private Dictionary<string, Dictionary<string, string>> filePathConfigCache;

        internal EditorConfigParser()
        {
            filePathConfigCache = new Dictionary<string, Dictionary<string, string>>();
        }

        public Dictionary<string, string> Parse(string filePath)
        {
            if (filePathConfigCache.ContainsKey(filePath))
            {
                return filePathConfigCache[filePath];
            }

            var editorConfigDataFromFilesList = new List<EditorConfigFile>();
            var directoryOfTheProject = Path.GetDirectoryName(filePath);
            var editorConfigFile = FileUtilities.GetPathOfFileAbove(EditorconfigFile, directoryOfTheProject);

            while (editorConfigFile != string.Empty)
            {
                // TODO: Change the API of EditorconfigFile Parse to accept the text value instead of file path. 
                var editorConfigData = EditorConfigFile.Parse(editorConfigFile);
                editorConfigDataFromFilesList.Add(editorConfigData);

                if (editorConfigData.IsRoot)
                {
                    break;
                }
                else
                {
                    editorConfigFile = FileUtilities.GetPathOfFileAbove(EditorconfigFile, Path.GetDirectoryName(Path.GetDirectoryName(editorConfigFile)));
                }
            }

            var resultingDictionary = new Dictionary<string, string>();

            if (editorConfigDataFromFilesList.Any())
            {
                editorConfigDataFromFilesList.Reverse();
                
                foreach (var configData in editorConfigDataFromFilesList)
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

            filePathConfigCache[filePath] = resultingDictionary;
            return resultingDictionary;
        }

        private static string NormalizeWithForwardSlash(string p) => Path.DirectorySeparatorChar == '/' ? p : p.Replace(Path.DirectorySeparatorChar, '/');
    }
}
