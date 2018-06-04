// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System;
using System.IO;
using System.Security.Principal;
using System.Security.AccessControl;
using System.Xml;
using System.Xml.XPath;
using System.Reflection;
using NUnit.Framework;
using Microsoft.Build;
using Microsoft.Build.BuildEngine;
using Microsoft.Build.Framework;
using Microsoft.Build.UnitTests;

namespace Microsoft.Build.UnitTests.OM.OrcasCompatibility
{
    /// <summary>
    /// Compatiblity Unit Test Helper methods
    /// </summary>
    public static class CompatibilityTestHelpers
    {
        /// <summary>
        /// path for the msbuild core xml shema
        /// </summary>
        internal static readonly string SchemaPathBuildCore = Path.Combine(SuiteBinPath, @"MSBuild\Microsoft.Build.Core.xsd");

        /// <summary>
        /// URI for the msbuild xml schema
        /// </summary>
        internal static readonly Uri SchemaUrlMSBuild = new Uri("http://schemas.microsoft.com/developer/msbuild/2003");
        
        /// <summary>
        /// Path for the msbuild xml schema
        /// </summary>
        internal static readonly string SchemaPathMSBuild = Path.Combine(SuiteBinPath, @"Microsoft.Build.xsd");

        /// <summary>
        /// Field for suitbinPath, reference SuiteBinPath instead. 
        /// </summary>
        private static string suiteBinPath = null;

        /// <summary>
        /// Get the path of the current suite binaries. One time derivation.
        /// </summary>
        internal static string SuiteBinPath
        {
            get
            {
                if (suiteBinPath == null)
                {
                    suiteBinPath = Path.GetDirectoryName(new Uri(Assembly.GetExecutingAssembly().EscapedCodeBase).LocalPath);
                }

                return suiteBinPath;
            }
        }

        /// <summary>
        /// Creates p.proj on disk in default location with sample content.
        /// </summary>
        /// <returns>Project File Content as a Local system path</returns>
        internal static string CreateTempProjectFile()
        {
            return CreateTempProjectFile(TestData.ContentSimpleTools35);
        }

        /// <summary>
        /// Overload for project file content. 
        /// </summary>
        /// <returns>Project File Content</returns>
        internal static string CreateTempProjectFile(string projContent)
        {
            return ObjectModelHelpers.CreateFileInTempProjectDirectory("p.proj", projContent);
        }

        /// <summary>
        /// Generate a C:\ drive path that is over a certain minimum length
        /// </summary>
        /// 
        internal static string GenerateLongPath(int minLength)
        {
            string folderName = "directory";
            string drive = @"C:" + Path.DirectorySeparatorChar; 
            string longPath = drive;
            while (longPath.Length < (minLength + folderName.Length)) // does not consider slashes
            {
                longPath = Path.Combine(longPath, folderName + Path.DirectorySeparatorChar);
            }

            return longPath;
        }

        /// <summary>
        /// Sets file system rights for the current user on a given file.
        /// </summary>
        internal static void SetFileAccessPermissions(string filePath, FileSystemRights rights, AccessControlType state)
        {
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException(filePath + " not found. Cannot set permissions");
            }

            FileSecurity projectSecurity = File.GetAccessControl(filePath);
            FileSystemAccessRule rule =
                new FileSystemAccessRule(WindowsIdentity.GetCurrent().Name, rights, state);
            projectSecurity.AddAccessRule(rule);
            File.SetAccessControl(filePath, projectSecurity);
        }

        /// <summary>
        /// Deletes the given file from disk
        /// </summary>
        internal static void RemoveFile(string tempProjectFilePath)
        {
            if (File.Exists(tempProjectFilePath))
            {
                File.Delete(tempProjectFilePath);
            }
        }

        /// <summary>
        ///  Remove a given directory and its content
        /// </summary>
        /// <param name="targetDirectory">The directory to remove</param>
        internal static void CleanupDirectory(string targetDirectory)
        {
            foreach (string file in Directory.GetFiles(targetDirectory, "*.*", SearchOption.AllDirectories))
            {
                File.Delete(file);
            }

            if (Directory.Exists(targetDirectory) &&
               (Directory.GetFiles(targetDirectory, "*.*", SearchOption.AllDirectories).Length == 0))
            {
                Directory.Delete(targetDirectory, true);
            }
        }

        /// <summary>
        /// Create a number of files at a given location with certain naming conventions.
        /// </summary>
        /// <param name="numberOfFiles">The number of files to create</param>
        /// <param name="baseName">The base name of the file eg:  the "foo" part of "foo1.bar", "foo2.bar"</param>
        /// <param name="extension">File extension for all created files</param>
        /// <param name="createInPath">The path to an existing or non-existing directory in which the files should be created</param>
        internal static List<string> CreateFiles(int numberOfFiles, string baseName, string extension, string createInPath)
        {
            if (!Directory.Exists(createInPath))
            {
                Directory.CreateDirectory(createInPath);
            }

            List<string> files = new List<string>();
            for (int i = 0; i < numberOfFiles; i++)
            {
                string fileName = Path.Combine(createInPath, baseName + i + "." + extension);
                File.Create(fileName).Close();
                files.Add(fileName);
            }

            return files;
        }

        /// <summary>
        /// Search import collection and return the matched Import object
        /// </summary>
        internal static Import GetImportByProjectPath(ImportCollection imports, string projPath)
        {
            foreach (Import import in imports)
            {
                if (import.ProjectPath == projPath)
                {
                    return import;           
                }
            }

            return null;
        }

        /// <summary>
        /// Search xml documents for a node and count the occurances.
        /// </summary>
        /// <returns>Number of nodes found</returns>
        internal static int CountNodesWithName(string projectXml, string nodeName)
        {
            return GetNodesWithName(projectXml, nodeName).Count;
        }

        /// <summary>
        /// Search project xml for a node by name and return them
        /// </summary>
        /// <returns>Number of nodes found</returns>
        internal static XmlNodeList GetNodesWithName(string projectXml, string nodeName)
        {
            XmlDocument xmlDoc = new XmlDocument();
            xmlDoc.LoadXml(projectXml);
            XmlNodeList nodes = xmlDoc.DocumentElement.SelectNodes("//msb:" + nodeName, GetNsManager(xmlDoc));

            return nodes;
        }
        
        /// <summary>
        /// Find and return a specified BuildProperty from a given project
        /// </summary>
        internal static BuildProperty FindBuildProperty(Project p, string buildPropertyName)
        {
            foreach (BuildPropertyGroup propertyGroup in p.PropertyGroups)
            {
                foreach (BuildProperty property in propertyGroup)
                {
                    if (String.Compare(property.Name, buildPropertyName, StringComparison.OrdinalIgnoreCase) == 0)
                    {
                        return property;
                    }
                }
            }

            return null;
        }
           
        /// <summary>
        /// Find and return a specified BuildPropertyGroup from a given BuildProperty in a project
        /// </summary>
        internal static BuildPropertyGroup FindBuildPropertyGroupThatContainsProperty(Project p, string buildPropertyName)
        {
            foreach (BuildPropertyGroup propertyGroup in p.GlobalProperties)
            {
                foreach (BuildProperty property in propertyGroup)
                {
                    if (String.Compare(property.Name, buildPropertyName, StringComparison.OrdinalIgnoreCase) == 0)
                    {
                        return propertyGroup;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Find and return a specified BuildItem from a given project
        /// </summary>
        internal static BuildItem FindBuildItem(Project p, string itemName)
        {
            foreach (BuildItemGroup itemGroup in p.ItemGroups)
            {
                foreach (BuildItem item in itemGroup)
                {
                    if (String.Equals(item.Name, itemName, StringComparison.OrdinalIgnoreCase))
                    {
                        return item;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Iterate over a collection to find an import object matching on path
        /// </summary>
        internal static Import FindFirstMatchingImportByPath(ImportCollection importCollection, string pathToMatch)
        {
            foreach (Import import in importCollection)
            {
                if (import.ProjectPath.Equals(pathToMatch, StringComparison.OrdinalIgnoreCase))
                {
                    return import;
                }
            }

            return null;
        }

        /// <summary>
        /// Iterate over a collection to find an import object matching on path
        /// </summary>
        internal static Import FindFirstMatchingImportByEvaludatedPath(ImportCollection importCollection, string evaluatedPathToMatch)
        {
            foreach (Import import in importCollection)
            {
                if (import.EvaluatedProjectPath.Equals(evaluatedPathToMatch, StringComparison.OrdinalIgnoreCase))
                {
                    return import;
                }
            }

            return null;
        }

        /// <summary>
        /// Find and return the first UsingTask matching on taskName
        /// </summary>
        internal static UsingTask FindUsingTaskByName(string taskName, UsingTaskCollection usingTaskCollection)
        {
            foreach (UsingTask usingTask in usingTaskCollection)
            {
                if (String.Equals(usingTask.TaskName, taskName, StringComparison.CurrentCultureIgnoreCase))
                {
                    return usingTask;
                }
            }

            return null;
        }

        /// <summary>
        /// Construct a namespace manager 
        /// </summary>
        private static XmlNamespaceManager GetNsManager(XmlDocument xmlDoc)
        {
            XmlNamespaceManager namespaceManager = new XmlNamespaceManager(xmlDoc.NameTable);
            namespaceManager.AddNamespace("msb", CompatibilityTestHelpers.SchemaUrlMSBuild.ToString());
            return namespaceManager;
        }      
    }
}
