// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Configuration;
using System.IO;
using Microsoft.Build.Shared;

#pragma warning disable 436

namespace Microsoft.Build.UnitTests
{
    /// <summary>
    /// Helper class to simulate application configuration read
    /// </summary>
    internal class ToolsetConfigurationReaderTestHelper
    {
        private static ExeConfigurationFileMap s_configFile;
        private static string s_testFolderFullPath = null;
        private static Exception s_exceptionToThrow = null;

        internal static string WriteConfigFile(string content)
        {
            return WriteConfigFile(ObjectModelHelpers.CleanupFileContents(content), null);
        }

        internal static string WriteConfigFile(string content, Exception exception)
        {
            s_exceptionToThrow = exception;
            s_testFolderFullPath = Path.Combine(Path.GetTempPath(), "configFileTests");
            Directory.CreateDirectory(s_testFolderFullPath);
            string configFilePath = Path.Combine(s_testFolderFullPath, "test.exe.config");

            if (File.Exists(configFilePath))
            {
                File.Delete(configFilePath);
            }

            File.WriteAllText(configFilePath, content);
            s_configFile = new ExeConfigurationFileMap();
            s_configFile.ExeConfigFilename = configFilePath;
            return configFilePath;
        }

        internal static void CleanUp()
        {
            try
            {
                if (s_testFolderFullPath != null && Directory.Exists(s_testFolderFullPath))
                {
                    FileUtilities.DeleteDirectoryNoThrow(s_testFolderFullPath, true, 5, 1000);
                }
            }
            finally
            {
                s_exceptionToThrow = null;
            }
        }

        /// <summary>
        /// Creates a config file and loads a Configuration from it
        /// </summary>
        /// <returns>configuration object</returns>
        internal static Configuration ReadApplicationConfigurationTest()
        {
            if (s_exceptionToThrow != null)
            {
                throw s_exceptionToThrow;
            }

            return s_configFile == null
                       ? null
                       : ConfigurationManager.OpenMappedExeConfiguration(s_configFile, ConfigurationUserLevel.None);
        }
    }
}
