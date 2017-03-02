// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Configuration;
using Microsoft.Win32;
using NUnit.Framework;
using Microsoft.Build.BuildEngine;
using System.Threading;

namespace Microsoft.Build.UnitTests
{
    /// <summary>
    /// Helper class to simulate application configuration read
    /// </summary>
    internal class ToolsetConfigurationReaderTestHelper
    {
        private static ExeConfigurationFileMap configFile;
        private static string testFolderFullPath = null;
        private static Exception exceptionToThrow = null;

        internal static string WriteConfigFile(string content)
        {
            return WriteConfigFile(content, null);
        }

        internal static string WriteConfigFile(string content, Exception exception)
        {
            exceptionToThrow = exception;
            testFolderFullPath = Path.Combine(Path.GetTempPath(), "configFileTests");
            Directory.CreateDirectory(testFolderFullPath);
            string configFilePath = Path.Combine(testFolderFullPath, "test.exe.config");

            if (File.Exists(configFilePath))
            {
                File.Delete(configFilePath);
            }

            File.WriteAllText(configFilePath, content);
            configFile = new ExeConfigurationFileMap();
            configFile.ExeConfigFilename = configFilePath;
            return configFilePath;
        }

        internal static void CleanUp()
        {
            try
            {
                if (testFolderFullPath != null && Directory.Exists(testFolderFullPath))
                {
                    for (int i = 0; i < 5; i++)
                    {
                        try
                        {
                            Directory.Delete(testFolderFullPath, true /* recursive */);
                            break;
                        }
                        catch (Exception)
                        {
                            Thread.Sleep(1000);
                            // Eat exceptions from the delete
                        }
                    }
                }
            }
            finally
            {
                exceptionToThrow = null;
            }
        }

        /// <summary>
        /// Creates a config file and loads a Configuration from it
        /// </summary>
        /// <returns>configuration object</returns>
        internal static Configuration ReadApplicationConfigurationTest()
        {
            if (exceptionToThrow != null)
            {
                throw exceptionToThrow;
            }

            return ConfigurationManager.OpenMappedExeConfiguration(configFile, ConfigurationUserLevel.None);
        }
    }
}
