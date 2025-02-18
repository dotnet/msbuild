// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using Microsoft.Build.Shared;
using static Microsoft.Build.Experimental.BuildCheck.Infrastructure.EditorConfig.EditorConfigGlobsMatcher;

namespace Microsoft.Build.Experimental.BuildCheck.Infrastructure.EditorConfig;

internal sealed class EditorConfigParser
{
    private const string EditorconfigFile = ".editorconfig";

    /// <summary>
    /// Cache layer of the parsed editor configs the key is the path to the .editorconfig file.
    /// </summary>
    private readonly ConcurrentDictionary<string, EditorConfigFile> _editorConfigFileCache = new ConcurrentDictionary<string, EditorConfigFile>(StringComparer.InvariantCultureIgnoreCase);

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
        // The method will look for the file in parent directory if not found in current until found or the directory is root. 
        var editorConfigFilePath = FileUtilities.GetPathOfFileAbove(EditorconfigFile, directoryOfTheProject);

        while (editorConfigFilePath != string.Empty)
        {
            var editorConfig = _editorConfigFileCache.GetOrAdd(editorConfigFilePath, (key) =>
            {
                return EditorConfigFile.Parse(File.ReadAllText(editorConfigFilePath));
            });

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

        for (int i = editorConfigFiles.Count - 1; i >= 0; i--)
        {
            foreach (var section in editorConfigFiles[i].NamedSections)
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
