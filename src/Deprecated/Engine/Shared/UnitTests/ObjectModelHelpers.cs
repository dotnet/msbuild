// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Xml;
using System.Text;
using System.Reflection;
using System.Collections;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Diagnostics;

using NUnit.Framework;

using Microsoft.Build.BuildEngine;
using Microsoft.Build.Framework;
using Microsoft.VisualStudio.Internal;
    
namespace Microsoft.Build.UnitTests
{
    /*
     * Class:   ObjectModelHelpers
     * Owner:   jomof
     *
     * Utility methods for unit tests that work through the object model.
     *
     */
    public static class ObjectModelHelpers        
    {
        private const string msbuildNamespace = "http://schemas.microsoft.com/developer/msbuild/2003";
        private const string msbuildDefaultToolsVersion = BrandNames.VSGeneralVersion;
        private const string msbuildAssemblyVersion = BrandNames.VSGeneralAssemblyVersion;

        /// <summary>
        /// Return the default tools version 
        /// </summary>
        internal static string MSBuildDefaultToolsVersion
        {
            get
            {
                return msbuildDefaultToolsVersion;
            }

            private set
            {
            }
        }

        /// <summary>
        /// Return the current assembly version 
        /// </summary>
        internal static string MSBuildAssemblyVersion
        {
            get
            {
                return msbuildAssemblyVersion;
            }

            private set
            {
            }
        }

        /// <summary>
        /// Helper method to tell us whether a particular metadata name is an MSBuild well-known metadata
        /// (e.g., "RelativeDir", "FullPath", etc.)
        /// </summary>
        /// <owner>RGoel</owner>
        private static Hashtable builtInMetadataNames = null;
        static private bool IsBuiltInItemMetadataName(string metadataName)
        {
            if (builtInMetadataNames == null)
            {
                builtInMetadataNames = new Hashtable();

                Microsoft.Build.Utilities.TaskItem dummyTaskItem = new Microsoft.Build.Utilities.TaskItem();
                foreach (string builtInMetadataName in dummyTaskItem.MetadataNames)
                {
                    builtInMetadataNames[builtInMetadataName] = String.Empty;
                }
            }

            return builtInMetadataNames.Contains(metadataName);
        }

        /// <summary>
        /// Asserts that there are no items in the project of the specified type
        /// </summary>
        static internal void AssertNoItem(Project p, string type)
        {
            BuildItemGroup items = p.GetEvaluatedItemsByName(type);
            Assertion.AssertEquals(0, items.Count);
        }

        /// <summary>
        /// Gets an item list from the project and assert that it contains
        /// exactly one item with the supplied name.
        /// </summary>
        /// <param name="p"></param>
        /// <param name="type"></param>
        /// <param name="itemInclude"></param>
        /// <owner>JomoF</owner>
        static internal BuildItem AssertSingleItem(Project p, string type, string itemInclude)
        {
            BuildItemGroup items = p.GetEvaluatedItemsByName(type);
            int count = 0;
            foreach(BuildItem item in items)
            {
                // This was item.Include before, but I believe it really should have been item.FinalItemSpec, which
                // is what is actually used by tasks, etc.
                Assertion.AssertEquals(itemInclude.ToUpperInvariant(), item.FinalItemSpec.ToUpperInvariant());
                ++count;
            }

            Assertion.AssertEquals(1, count);

            return items[0];
        }

        /// <summary>
        /// Given a hash table of ITaskItems, make sure there is exactly one
        /// item and that the key is 'key' and the Value is an ITaskItem with 
        /// an item spec of 'itemSpec'
        /// </summary>
        /// <param name="d"></param>
        /// <param name="key"></param>
        /// <param name="itemSpec"></param>
        /// <owner>JomoF</owner>
        static internal void AssertSingleItemInDictionary(IDictionary d, string expectedItemSpec)
        {
            List<ITaskItem> actualItems = new List<ITaskItem>();

            string projectDir= Path.GetTempPath();
            Console.WriteLine("cd={0}", projectDir);

            foreach(DictionaryEntry e in d)
            {
                foreach(ITaskItem i in (ITaskItem[])e.Value)
                {
                    i.ItemSpec = i.ItemSpec.Replace(projectDir, "<|proj|>");
                    actualItems.Add(i);
                }
            }

            AssertItemsMatch(expectedItemSpec, actualItems.ToArray());
        }

        /// <summary>
        /// Amazingly sophisticated :) helper function to determine if the set of ITaskItems returned from 
        /// a task match the expected set of ITaskItems.  It can also check that the ITaskItems have the expected
        /// metadata, and that the ITaskItems are returned in the correct order.
        /// 
        /// The "expectedItemsString" is a formatted way of easily specifying which items you expect to see.
        /// The format is:
        /// 
        ///         itemspec1 :   metadataname1=metadatavalue1 ; metadataname2=metadatavalue2 ; ...
        ///         itemspec2 :   metadataname3=metadatavalue3 ; metadataname4=metadatavalue4 ; ...
        ///         itemspec3 :   metadataname5=metadatavalue5 ; metadataname6=metadatavalue6 ; ...
        /// 
        /// (Each item needs to be on its own line.)
        /// 
        /// </summary>
        /// <param name="expectedItemsString"></param>
        /// <param name="actualItems"></param>
        /// <owner>RGoel</owner>
        static internal void AssertItemsMatch(string expectedItemsString, ITaskItem[] actualItems)
        {
            AssertItemsMatch(expectedItemsString, actualItems, true);
        }

        /// <summary>
        /// Amazingly sophisticated :) helper function to determine if the set of ITaskItems returned from 
        /// a task match the expected set of ITaskItems.  It can also check that the ITaskItems have the expected
        /// metadata, and that the ITaskItems are returned in the correct order.
        /// 
        /// The "expectedItemsString" is a formatted way of easily specifying which items you expect to see.
        /// The format is:
        /// 
        ///         itemspec1 :   metadataname1=metadatavalue1 ; metadataname2=metadatavalue2 ; ...
        ///         itemspec2 :   metadataname3=metadatavalue3 ; metadataname4=metadatavalue4 ; ...
        ///         itemspec3 :   metadataname5=metadatavalue5 ; metadataname6=metadatavalue6 ; ...
        /// 
        /// (Each item needs to be on its own line.)
        /// 
        /// </summary>
        /// <param name="expectedItemsString"></param>
        /// <param name="actualItems"></param>
        /// <param name="orderOfItemsShouldMatch"></param>
        /// <owner>RGoel</owner>
        static internal void AssertItemsMatch(string expectedItemsString, ITaskItem[] actualItems, bool orderOfItemsShouldMatch)
        {
            List<ITaskItem> expectedItems = ParseExpectedItemsString(expectedItemsString);

            // Form a string of expected item specs.  For logging purposes only.
            StringBuilder expectedItemSpecs = new StringBuilder();
            foreach (ITaskItem expectedItem in expectedItems)
            {
                if (expectedItemSpecs.Length > 0)
                {
                    expectedItemSpecs.Append("; ");
                }

                expectedItemSpecs.Append(expectedItem.ItemSpec);
            }

            // Form a string of expected item specs.  For logging purposes only.
            StringBuilder actualItemSpecs = new StringBuilder();
            foreach (ITaskItem actualItem in actualItems)
            {
                if (actualItemSpecs.Length > 0)
                {
                    actualItemSpecs.Append("; ");
                }

                actualItemSpecs.Append(actualItem.ItemSpec);
            }

            bool outOfOrder = false;

            // Loop through all the actual items.
            for (int actualItemIndex = 0 ; actualItemIndex < actualItems.Length ; actualItemIndex++)
            {
                ITaskItem actualItem = actualItems[actualItemIndex];
                
                // Loop through all the expected items to find one with the same item spec.
                ITaskItem expectedItem = null;
                int expectedItemIndex;
                for (expectedItemIndex = 0 ; expectedItemIndex < expectedItems.Count ; expectedItemIndex++)
                {
                    if (expectedItems[expectedItemIndex].ItemSpec == actualItem.ItemSpec)
                    {
                        expectedItem = expectedItems[expectedItemIndex];

                        // If the items are expected to be in the same order, then the expected item
                        // should always be found at index zero, because we remove items from the expected
                        // list as we find them.
                        if ((expectedItemIndex != 0) && (orderOfItemsShouldMatch))
                        {
                            outOfOrder = true;
                        }

                        break;
                    }
                }

                Assertion.Assert(String.Format("Item '{0}' was returned but not expected.", actualItem.ItemSpec), expectedItem != null);

                // Make sure all the metadata on the expected item matches the metadata on the actual item.
                // Don't check built-in metadata ... only check custom metadata.
                foreach (string metadataName in expectedItem.MetadataNames)
                {
                    // This check filters out any built-in item metadata, like "RelativeDir", etc.
                    if (!IsBuiltInItemMetadataName(metadataName))
                    {
                        string expectedMetadataValue = expectedItem.GetMetadata(metadataName);
                        string actualMetadataValue = actualItem.GetMetadata(metadataName);

                        Assertion.Assert(string.Format("Item '{0}' does not have expected metadata '{1}'.", actualItem.ItemSpec, metadataName), 
                            actualMetadataValue.Length > 0 || expectedMetadataValue.Length == 0);

                        Assertion.Assert(string.Format("Item '{0}' has unexpected metadata {1}={2}.", actualItem.ItemSpec, metadataName, actualMetadataValue), 
                            actualMetadataValue.Length == 0 || expectedMetadataValue.Length > 0);

                        Assertion.Assert(string.Format("Item '{0}' has metadata {1}={2} instead of expected {1}={3}.", 
                            actualItem.ItemSpec, metadataName, actualMetadataValue, expectedMetadataValue),
                            actualMetadataValue == expectedMetadataValue);
                    }
                }
                expectedItems.RemoveAt(expectedItemIndex);
            }

            // Log an error for any leftover items in the expectedItems collection.
            foreach (ITaskItem expectedItem in expectedItems)
            {
                Assertion.Assert(String.Format("Item '{0}' was expected but not returned.", expectedItem.ItemSpec), false);
            }

            if (outOfOrder)
            {
                Console.WriteLine("ERROR:  Items were returned in the incorrect order...");
                Console.WriteLine("Expected:  " + expectedItemSpecs);
                Console.WriteLine("Actual:    " + actualItemSpecs);
                Assertion.Assert("Items were returned in the incorrect order.  See 'Standard Out' tab for more details.", false);
            }
        }

        /// <summary>
        /// Parses the crazy string passed into AssertItemsMatch and returns a list of ITaskItems.
        /// </summary>
        /// <param name="expectedItemsString"></param>
        /// <returns></returns>
        /// <owner>RGoel</owner>
        static private List<ITaskItem> ParseExpectedItemsString(string expectedItemsString)
        {
            List<ITaskItem> expectedItems = new List<ITaskItem>();

            // First, parse this massive string that we've been given, and create an ITaskItem[] out of it,
            // so we can more easily compare it against the actual items.
            string[] expectedItemsStringSplit = expectedItemsString.Split(new char[] {'\r', '\n'}, StringSplitOptions.RemoveEmptyEntries);
            foreach (string singleExpectedItemString in expectedItemsStringSplit)
            {
                string singleExpectedItemStringTrimmed = singleExpectedItemString.Trim();
                if (singleExpectedItemStringTrimmed.Length > 0)
                {
                    int indexOfColon = singleExpectedItemStringTrimmed.IndexOf(": ");
                    if (indexOfColon == -1)
                    {
                        expectedItems.Add(new Microsoft.Build.Utilities.TaskItem(singleExpectedItemStringTrimmed));
                    }
                    else
                    {
                        // We found a colon, which means there's metadata in there.

                        // The item spec is the part before the colon.
                        string itemSpec = singleExpectedItemStringTrimmed.Substring(0, indexOfColon).Trim();

                        // The metadata is the part after the colon.
                        string itemMetadataString = singleExpectedItemStringTrimmed.Substring(indexOfColon + 1);

                        ITaskItem expectedItem = new Microsoft.Build.Utilities.TaskItem(itemSpec);

                        string[] itemMetadataPieces = itemMetadataString.Split(new char[]{';'}, StringSplitOptions.RemoveEmptyEntries);
                        foreach (string itemMetadataPiece in itemMetadataPieces)
                        {
                            string itemMetadataPieceTrimmed = itemMetadataPiece.Trim();
                            if (itemMetadataPieceTrimmed.Length > 0)
                            {
                                int indexOfEquals = itemMetadataPieceTrimmed.IndexOf('=');
                                Assertion.Assert(String.Format("Could not find <equals> in item metadata definition '{0}'", itemMetadataPieceTrimmed), indexOfEquals != -1);

                                string itemMetadataName = itemMetadataPieceTrimmed.Substring(0, indexOfEquals).Trim();
                                string itemMetadataValue = itemMetadataPieceTrimmed.Substring(indexOfEquals + 1).Trim();

                                expectedItem.SetMetadata(itemMetadataName, itemMetadataValue);
                            }
                        }

                        expectedItems.Add(expectedItem);
                    }
                }
            }

            return expectedItems;
        }

        /// <summary>
        /// Does certain replacements in a string representing the project file contents.
        /// This makes it easier to write unit tests because the author doesn't have
        /// to worry about escaping double-quotes, etc.
        /// </summary>
        /// <param name="projectFileContents"></param>
        /// <returns></returns>
        static internal string CleanupFileContents(string projectFileContents)
        {
            // Replace reverse-single-quotes with double-quotes.
            projectFileContents = projectFileContents.Replace("`", "\"");

            // Place the correct MSBuild namespace into the <Project> tag.
            projectFileContents = projectFileContents.Replace("msbuildnamespace", msbuildNamespace);
            projectFileContents = projectFileContents.Replace("msbuilddefaulttoolsversion", msbuildDefaultToolsVersion);
            projectFileContents = projectFileContents.Replace("msbuildassemblyversion", msbuildAssemblyVersion);

            return projectFileContents;
        }

        /// <summary>
        /// Normalizes all the whitespace in an Xml document so that two documents that
        /// differ only in whitespace can be easily compared to each other for sameness.
        /// </summary>
        /// <param name="xmldoc"></param>
        /// <returns></returns>
        /// <owner>RGoel</owner>
        static private string NormalizeXmlWhitespace(XmlDocument xmldoc)
        {
            // Normalize all the whitespace by writing the Xml document out to a 
            // string, with PreserveWhitespace=false.
            xmldoc.PreserveWhitespace = false;
            StringWriter stringWriter = new StringWriter();
            xmldoc.Save(stringWriter);
            return stringWriter.ToString();
        }

        /// <summary>
        /// Create an MSBuild project file on disk and return the full path to it.
        /// </summary>
        /// <param name="xml"></param>
        /// <returns></returns>
        /// <owner>RGoel</owner>
        static internal string CreateTempFileOnDisk(string fileContents, params object[] args)
        {
            return CreateTempFileOnDiskNoFormat(String.Format(fileContents, args));
        }

        /// <summary>
        /// Create an MSBuild project file on disk and return the full path to it.
        /// </summary>
        /// <param name="xml"></param>
        /// <returns></returns>
        /// <owner>RGoel</owner>
        static internal string CreateTempFileOnDiskNoFormat(string fileContents)
        {
            string projectFilePath = Path.GetTempFileName();

            File.WriteAllText(projectFilePath, CleanupFileContents(fileContents));

            return projectFilePath;
        }

        /// <summary>
        /// Create a project in memory. Load up the given XML.
        /// </summary>
        /// <param name="xml"></param>
        /// <returns></returns>
        /// <owner>JomoF</owner>
        static internal Project CreateInMemoryProject(string xml)
        {
            return CreateInMemoryProject(xml, new ConsoleLogger());
        }
        
        /// <summary>
        /// Create a project in memory. Load up the given XML.
        /// </summary>
        /// <param name="xml"></param>
        /// <param name="logger"></param>
        /// <returns></returns>
        /// <owner>JomoF</owner>
        static internal Project CreateInMemoryProject(string xml, ILogger logger /* May be null */)
        {
            Engine e = new Engine();
            e.DefaultToolsVersion = "4.0";
            return CreateInMemoryProject(e, xml, logger);
        }

        /// <summary>
        /// Create an in-memory project and attach it to the passed-in engine.
        /// </summary>
        /// <param name="engine"></param>
        /// <param name="xml"></param>
        /// <param name="logger">May be null</param>
        /// <returns></returns>
        static internal Project CreateInMemoryProject(Engine e, string xml, ILogger logger /* May be null */)
        {
            return CreateInMemoryProject(e, xml, logger, null);
        }

        /// <summary>
        /// Create an in-memory project and attach it to the passed-in engine.
        /// </summary>
        /// <param name="logger">May be null</param>
        /// <param name="toolsVersion">May be null</param>
        static internal Project CreateInMemoryProject(Engine e, string xml, ILogger logger /* May be null */, string toolsVersion/* may be null */)
        {
            return CreateInMemoryProject(e, xml, logger, toolsVersion, ProjectLoadSettings.None);
        }

        /// <summary>
        /// Create an in-memory project and attach it to the passed-in engine.
        /// </summary>
        /// <param name="logger">May be null</param>
        /// <param name="toolsVersion">May be null</param>
        static internal Project CreateInMemoryProject(Engine e, string xml, ILogger logger /* May be null */, string toolsVersion/* may be null */, ProjectLoadSettings projectLoadSettings)
        {
            // Anonymous in-memory projects use the current directory for $(MSBuildProjectDirectory).
            // We need to set the directory to something reasonable.
            string originalDir = Directory.GetCurrentDirectory();
            Directory.SetCurrentDirectory(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));

#if _NEVER
            // Attach a console logger so that build output can go to the test
            // harness.
            e.RegisterLogger(new ConsoleLogger(ConsoleLogger.Verbosity.Verbose));
#endif

            Project p = new Project(e, toolsVersion);
            p.FullFileName = Path.Combine(Path.GetTempPath(), "Temporary.csproj");

            if (logger != null)
            {
                p.ParentEngine.RegisterLogger(logger);
            }
            p.LoadXml(CleanupFileContents(xml), projectLoadSettings);

            // Return to the original directory.
            Directory.SetCurrentDirectory(originalDir);

            return p;   
        }

        /// <summary>
        /// Creates a project in memory and builds the default targets.  The build is
        /// expected to succeed.
        /// </summary>
        /// <param name="projectContents"></param>
        /// <returns></returns>
        /// <owner>RGoel</owner>
        internal static MockLogger BuildProjectExpectSuccess
            (
            string projectContents
            )
        {
            MockLogger logger = new MockLogger();
            Project project = ObjectModelHelpers.CreateInMemoryProject(projectContents, logger);

            bool success = project.Build(null, null);
            Assertion.Assert("Build failed.  See Standard Out tab for details", success);

            return logger;
        }

        /// <summary>
        /// Creates a project in memory and builds the default targets.  The build is
        /// expected to fail.
        /// </summary>
        /// <param name="projectContents"></param>
        /// <returns></returns>
        /// <owner>RGoel</owner>
        internal static MockLogger BuildProjectExpectFailure
            (
            string projectContents
            )
        {
            MockLogger logger = new MockLogger();
            Project project = ObjectModelHelpers.CreateInMemoryProject(projectContents, logger);

            bool success = project.Build(null, null);
            Assertion.Assert("Build succeeded, but shouldn't have.  See Standard Out tab for details", !success);

            return logger;
        }

        /// <summary>
        /// This helper method compares the final project contents with the expected
        /// value.
        /// </summary>
        /// <param name="project"></param>
        /// <param name="newExpectedProjectContents"></param>
        /// <owner>RGoel</owner>
        internal static void CompareProjectContents
            (
            Project project,
            string newExpectedProjectContents
            )
        {
            // Get the new XML for the project, normalizing the whitespace.
            string newActualProjectContents = project.Xml;

            // Replace single-quotes with double-quotes, and normalize whitespace.
            XmlDocument xmldoc = new XmlDocument();
            xmldoc.LoadXml(ObjectModelHelpers.CleanupFileContents(newExpectedProjectContents));
            newExpectedProjectContents = ObjectModelHelpers.NormalizeXmlWhitespace(xmldoc);

            // Compare the actual XML with the expected XML.
            Console.WriteLine("================================= EXPECTED ===========================================");
            Console.WriteLine(newExpectedProjectContents);
            Console.WriteLine();
            Console.WriteLine("================================== ACTUAL ============================================");
            Console.WriteLine(newActualProjectContents);
            Console.WriteLine();
            Assertion.AssertEquals("Project XML does not match expected XML.  See 'Standard Out' tab for details.",
                newExpectedProjectContents, newActualProjectContents);
        }

        private static string tempProjectDir = null;

        /// <summary>
        /// Returns the path %TEMP%\TempDirForMSBuildUnitTests
        /// </summary>
        internal static string TempProjectDir
        {
            get
            {
                if (tempProjectDir == null)
                {
                    tempProjectDir = Path.Combine(Path.GetTempPath(), "TempDirForMSBuildUnitTests");
                }

                return tempProjectDir;
            }
        }

        /// <summary>
        /// Deletes the directory %TEMP%\TempDirForMSBuildUnitTests, and all its contents.
        /// </summary>
        internal static void DeleteTempProjectDirectory()
        {
            // For some reason sometimes get "directory is not empty"
            // Try to be as robust as possible using retries and catching all exceptions.
            for (int retries = 0; retries < 5; retries++)
            {          
                try
                {
                    // Manually deleting all children, but intentionally leaving the 
                    // Temp project directory behind due to locking issues which were causing
                    // failures in main on Amd64-WOW runs.
                    if (Directory.Exists(TempProjectDir))
                    {
                        foreach (string directory in Directory.GetDirectories(TempProjectDir))
                        {
                            Directory.Delete(directory, true);
                        }
                        foreach (string file in Directory.GetFiles(TempProjectDir))
                        {
                            File.Delete(file);
                        }
                    }
                }
                catch(Exception ex)
                {
                    Console.WriteLine(ex.ToString());
                }
            }
        }

        /// <summary>
        /// Creates a file in the %TEMP%\TempDirForMSBuildUnitTests directory, after cleaning
        /// up the file contents (replacing single-back-quote with double-quote, etc.).
        /// </summary>
        /// <param name="fileRelativePath"></param>
        /// <param name="fileContents"></param>
        internal static string CreateFileInTempProjectDirectory(string fileRelativePath, string fileContents)
        {
            Assertion.Assert(!String.IsNullOrEmpty(fileRelativePath));
            string fullFilePath = Path.Combine(TempProjectDir, fileRelativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(fullFilePath));

            // retries to deal with occasional locking issues where the file can't be written to initially
            for (int retries = 0; retries < 5; retries++)
            {          
                try
                {
                    File.WriteAllText(fullFilePath, CleanupFileContents(fileContents));
                    break;
                }
                catch(Exception ex)
                {
                    if (retries < 4)
                    {
                        Console.WriteLine(ex.ToString());
                    }
                    else 
                    {
                        // All the retries have failed, so we're pretty much screwed. Might as well fail with the 
                        // actual problem now instead of with some more difficult-to-understand 
                        // issue later. 
                        throw ex;
                    }
                }
            }

            return fullFilePath;
        }

        /// <summary>
        /// Loads a project file from disk
        /// </summary>
        /// <param name="fileRelativePath"></param>
        /// <returns></returns>
        /// <owner>LukaszG</owner>
        internal static Project LoadProjectFileInTempProjectDirectory(string projectFileRelativePath, ILogger logger)
        {
            return LoadProjectFileInTempProjectDirectory(projectFileRelativePath, logger, false /* don't touch project*/);
        }

        /// <summary>
        /// Loads a project file from disk
        /// </summary>
        /// <param name="fileRelativePath"></param>
        /// <returns></returns>
        /// <owner>LukaszG</owner>
        internal static Project LoadProjectFileInTempProjectDirectory(string projectFileRelativePath, ILogger logger, bool touchProject)
        {
            string projectFileFullPath = Path.Combine(TempProjectDir, projectFileRelativePath);

            // Create/initialize a new engine.
            Engine engine = new Engine();

            if (logger != null)
            {
                engine.RegisterLogger(logger);
            }

            // Load the project off disk.
            Project project = engine.CreateNewProject();

            if (touchProject)
            {
                File.SetLastWriteTime(projectFileFullPath, DateTime.Now);
            }

            project.Load(projectFileFullPath);

            return project;
        }

        /// <summary>
        /// Builds a project file from disk, and asserts if the build does not succeed.
        /// </summary>
        /// <param name="projectFileRelativePath"></param>
        /// <param name="targets"></param>
        /// <param name="additionalProperties">Can be null.</param>
        /// <param name="logger"></param>
        /// <returns></returns>
        internal static bool BuildTempProjectFileWithTargets
        (
            string projectFileRelativePath,
            string[] targets,
            BuildPropertyGroup additionalProperties,
            MockLogger logger
        )
        {
            return BuildTempProjectFileWithTargets(projectFileRelativePath, targets, additionalProperties, logger, false /* don't touch project*/);
        }

        /// <summary>
        /// Builds a project file from disk, and asserts if the build does not succeed.
        /// </summary>
        /// <param name="projectFileRelativePath"></param>
        /// <param name="targets"></param>
        /// <param name="additionalProperties">Can be null.</param>
        /// <param name="logger"></param>
        /// <returns></returns>
        internal static bool BuildTempProjectFileWithTargets
        (
            string projectFileRelativePath,
            string[] targets,
            BuildPropertyGroup additionalProperties,
            MockLogger logger,
            bool touchProject
        )
        {
            Project project = LoadProjectFileInTempProjectDirectory(projectFileRelativePath, logger, touchProject);

            if (additionalProperties != null)
            {
                // add extra properties
                foreach (BuildProperty additionalProperty in additionalProperties)
                {
                    project.GlobalProperties.SetProperty(additionalProperty.Name, additionalProperty.Value);
                }
            }

            // Build the default targets.
            return project.Build(targets, null);
        }

        /// <summary>
        /// Builds a project file from disk, and asserts if the build does not succeed.
        /// </summary>
        /// <param name="projectFileRelativePath"></param>
        /// <returns></returns>
        /// <owner>RGoel</owner>
        internal static MockLogger BuildTempProjectFileExpectSuccess(string projectFileRelativePath)
        {
            return BuildTempProjectFileWithTargetsExpectSuccess(projectFileRelativePath, null, null, false);
        }

        /// <summary>
        /// Builds a project file from disk, and asserts if the build does not succeed.
        /// </summary>
        internal static MockLogger BuildTempProjectFileWithTargetsExpectSuccess(string projectFileRelativePath, string[] targets, BuildPropertyGroup additionalProperties)
        {
            MockLogger logger = new MockLogger();
            bool success = BuildTempProjectFileWithTargets(projectFileRelativePath, targets, additionalProperties, logger, false);

            Assertion.Assert("Build failed.  See Standard Out tab for details", success);

            return logger;
        }

        /// <summary>
        /// Builds a project file from disk, and asserts if the build does not succeed.
        /// </summary>
        internal static MockLogger BuildTempProjectFileWithTargetsExpectSuccess(string projectFileRelativePath, string[] targets, BuildPropertyGroup additionalProperties, bool touchProject)
        {
            MockLogger logger = new MockLogger();
            bool success = BuildTempProjectFileWithTargets(projectFileRelativePath, targets, additionalProperties, logger, touchProject);

            Assertion.Assert("Build failed.  See Standard Out tab for details", success);

            return logger;
        }

        /// <summary>
        /// Builds a project file from disk, and asserts if the build succeeds.
        /// </summary>
        internal static MockLogger BuildTempProjectFileExpectFailure(string projectFileRelativePath)
        {
            return BuildTempProjectFileWithTargetsExpectFailure(projectFileRelativePath, null, null);
        }

        /// <summary>
        /// Builds a project file from disk, and asserts if the build succeeds.
        /// </summary>
        private static MockLogger BuildTempProjectFileWithTargetsExpectFailure(string projectFileRelativePath, string[] targets, BuildPropertyGroup additionalProperties)
        {
            MockLogger logger = new MockLogger();
            bool success = BuildTempProjectFileWithTargets(projectFileRelativePath, targets, additionalProperties, logger);

            Assertion.Assert("Build unexpectedly succeeded.  See Standard Out tab for details", !success);

            return logger;
        }

        /// <summary>
        /// Runs an EXE and captures the stdout.
        /// </summary>
        /// <param name="builtExe"></param>
        /// <returns></returns>
        /// <owner>RGoel</owner>
        internal static string RunTempProjectBuiltApplication(string builtExe)
        {
            string builtExeFullPath = Path.Combine(TempProjectDir, builtExe);

            Assertion.Assert(@"Did not find expected file " + builtExe, File.Exists(builtExeFullPath));

            ProcessStartInfo startInfo = new ProcessStartInfo(builtExeFullPath);
            startInfo.UseShellExecute = false;
            startInfo.CreateNoWindow = true;
            startInfo.RedirectStandardOutput = true;

            Process process = Process.Start(startInfo);
            process.WaitForExit();

            string stdout = process.StandardOutput.ReadToEnd();
            Console.WriteLine("=================================================================");
            Console.WriteLine("======= OUTPUT OF BUILT APPLICATION =============================");
            Console.WriteLine("=================================================================");
            Console.WriteLine(stdout);

            Assertion.Assert("ConsoleApplication37.exe returned a non-zero exit code.", process.ExitCode == 0);

            return stdout;
        }

        /// <summary>
        /// Assert that a given file exists within the temp project directory.
        /// </summary>
        /// <param name="fileRelativePath"></param>
        /// <owner>RGoel</owner>
        internal static void AssertFileExistsInTempProjectDirectory(string fileRelativePath)
        {
            AssertFileExistsInTempProjectDirectory(fileRelativePath, null);
        }

        /// <summary>
        /// Assert that a given file exists within the temp project directory.
        /// </summary>
        /// <param name="fileRelativePath"></param>
        /// <param name="message">Can be null.</param>
        internal static void AssertFileExistsInTempProjectDirectory(string fileRelativePath, string message)
        {
            if (message == null)
            {
                message = fileRelativePath + " doesn't exist, but it should.";
            }

            AssertFileExistenceInTempProjectDirectory(fileRelativePath, message, true);
        }

        /// <summary>
        /// Assert that a given file does not exist within the temp project directory.
        /// </summary>
        /// <param name="fileRelativePath"></param>
        internal static void AssertFileDoesNotExistInTempProjectDirectory(string fileRelativePath)
        {
            AssertFileDoesNotExistInTempProjectDirectory(fileRelativePath, null);
        }

        /// <summary>
        /// Assert that a given file does not exist within the temp project directory.
        /// </summary>
        /// <param name="fileRelativePath"></param>
        /// <param name="message">Can be null.</param>
        internal static void AssertFileDoesNotExistInTempProjectDirectory(string fileRelativePath, string message)
        {
            if (message == null)
            {
                message = fileRelativePath + " exists, but it should not.";
            }

            AssertFileExistenceInTempProjectDirectory(fileRelativePath, message, false);
        }

        /// <summary>
        /// Assert that a given file exists (or not) within the temp project directory.
        /// </summary>
        /// <param name="fileRelativePath"></param>
        /// <param name="message">Can be null.</param>
        private static void AssertFileExistenceInTempProjectDirectory(string fileRelativePath, string message, bool exists)
        {
            Assertion.Assert(message, (exists == File.Exists(Path.Combine(TempProjectDir, fileRelativePath))));
        }

        /// <summary>
        /// Delete any files in the list that currently exist.
        /// </summary>
        /// <param name="files"></param>
        internal static void DeleteTempFiles(string[] files)
        {
            for (int i = 0; i < files.Length; i++)
            {
                if (File.Exists(files[i])) File.Delete(files[i]);
            }
        }

        /// <summary>
        /// Returns the requested number of temporary files.
        /// </summary>
        internal static string[] GetTempFiles(int number)
        {
            return GetTempFiles(number, DateTime.Now);
        }

        /// <summary>
        /// Returns the requested number of temporary files, with the requested write time.
        /// </summary>
        internal static string[] GetTempFiles(int number, DateTime lastWriteTime)
        {
            string[] files = new string[number];

            for (int i = 0; i < number; i++)
            {
                files[i] = Path.GetTempFileName();
                File.SetLastWriteTime(files[i], lastWriteTime);
            }
            return files;
        }
    }
}
