// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Xml;
using System.IO;
using System.Text;
using System.Globalization;
using System.Security;
using System.Text.RegularExpressions;

using error = Microsoft.Build.BuildEngine.Shared.ErrorUtilities;

namespace Microsoft.Build.BuildEngine.Shared
{
    /// <remarks>
    /// This class contains the functionality to parse a solution file and return a corresponding
    /// MSBuild project file containing the projects and dependencies defined in the solution.
    /// </remarks>
    internal class SolutionParser
    {
        #region Solution specific constants

        // An example of a project line looks like this:
        //  Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "ClassLibrary1", "ClassLibrary1\ClassLibrary1.csproj", "{05A5AD00-71B5-4612-AF2F-9EA9121C4111}"
        private static readonly Regex crackProjectLine = new Regex
        (
            "^"                                             // Beginning of line
            + "Project\\(\"(?<PROJECTTYPEGUID>.*)\"\\)"
            +"\\s*=\\s*"                                    // Any amount of whitespace plus "=" plus any amount of whitespace
            +"\"(?<PROJECTNAME>.*)\""
            + "\\s*,\\s*"                                   // Any amount of whitespace plus "," plus any amount of whitespace
            + "\"(?<RELATIVEPATH>.*)\""
            + "\\s*,\\s*"                                   // Any amount of whitespace plus "," plus any amount of whitespace
            + "\"(?<PROJECTGUID>.*)\""
            + "$"                                           // End-of-line
        );

        // An example of a property line looks like this:
        //      AspNetCompiler.VirtualPath = "/webprecompile"

        private static readonly Regex crackPropertyLine = new Regex
        (
            "^"                                             // Beginning of line
            + "(?<PROPERTYNAME>[^=]*)"
            +"\\s*=\\s*"                                    // Any amount of whitespace plus "=" plus any amount of whitespace
            +"(?<PROPERTYVALUE>[^=]*)"
            + "$"                                           // End-of-line
        );

        internal const int slnFileMinUpgradableVersion = 7; // Minimum version for MSBuild to give a nice message
        internal const int slnFileMinVersion           = 9; // Minimum version for MSBuild to actually do anything useful
        internal const int slnFileMaxVersion = VisualStudioConstants.CurrentVisualStudioSolutionFileVersion;

        private const string vbProjectGuid = "{F184B08F-C81C-45F6-A57F-5ABD9991F28F}";
        private const string csProjectGuid = "{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}";
        private const string vjProjectGuid = "{E6FDF86B-F3D1-11D4-8576-0002A516ECE8}";
        private const string vcProjectGuid = "{8BC9CEB8-8B4A-11D0-8D11-00A0C91BC942}";
        private const string webProjectGuid = "{E24C65DC-7377-472B-9ABA-BC803B73C61A}";
        private const string solutionFolderGuid = "{2150E333-8FDC-42A3-9474-1A3956D46DE8}";

        #endregion

        #region Member data

        private int slnFileActualVersion = 0;               // The major version number of the .SLN file we're reading.
        private string solutionFile = null;                 // Could be absolute or relative path to the .SLN file.
        private string solutionFileDirectory = null;        // Absolute path the solution file
        private bool solutionContainsWebProjects = false;    // Does this SLN contain any web projects?

        // The list of projects in this SLN, keyed by the project GUID.
        private Hashtable projects = null;

        // The list of projects in the SLN, in order of their appearance in the SLN.
        private ArrayList projectsInOrder = null;

        // The list of solution configurations in the solution
        private List<ConfigurationInSolution> solutionConfigurations;

        // cached default configuration name for GetDefaultConfigurationName
        private string defaultConfigurationName;

        // cached default platform name for GetDefaultPlatformName
        private string defaultPlatformName;

        //List of warnings that occurred while parsing solution
        private ArrayList solutionParserWarnings = null;

        //List of comments that occurred while parsing solution
        private ArrayList solutionParserComments = null;

        // unit-testing only
        private ArrayList solutionParserErrorCodes = null;

        StreamReader reader = null;
        int currentLineNumber = 0;

        #endregion

        #region Constructors

        /// <summary>
        /// Constructor
        /// </summary>
        /// <owner>RGoel</owner>
        internal SolutionParser()
        {
            this.solutionParserWarnings = new ArrayList();
            this.solutionParserErrorCodes = new ArrayList();
            this.solutionParserComments = new ArrayList();
        }

        #endregion

        #region Properties

        /// <summary>
        /// This property returns the list of warnings that were generated during solution parsing
        /// </summary>
        internal ArrayList SolutionParserWarnings
        {
            get
            {
                return solutionParserWarnings;
            }
        }

        /// <summary>
        /// This property returns the list of comments that were generated during the solution parsing
        /// </summary>
        internal ArrayList SolutionParserComments
        {
            get
            {
                return solutionParserComments;
            }
        }

        /// <summary>
        /// This property returns the list of error codes for warnings/errors that were generated during solution parsing. 
        /// UNIT TESTING ONLY
        /// </summary>
        internal ArrayList SolutionParserErrorCodes
        {
            get
            {
                return solutionParserErrorCodes;
            }
        }

        /// <summary>
        /// Returns the actual major version of the parsed solution file
        /// </summary>
        /// <owner>LukaszG</owner>
        internal int Version
        {
            get
            {
                return this.slnFileActualVersion;
            }
        }

        /// <summary>
        /// Returns true if the solution contains any web projects
        /// </summary>
        /// <owner>LukaszG</owner>
        internal bool ContainsWebProjects
        {
            get
            {
                return this.solutionContainsWebProjects;
            }
        }

        /// <summary>
        /// All projects in this solution, in the order they appeared in the solution file
        /// </summary>
        /// <owner>LukaszG</owner>
        internal ArrayList ProjectsInOrder
        {
            get
            {
                return this.projectsInOrder;
            }
            // For unit testing only
            set
            {
                this.projectsInOrder = value;
            }
        }

        /// <summary>
        /// The collection of projects in this solution, accessible by their guids
        /// </summary>
        internal Hashtable ProjectsByGuid
        {
            get
            {
                return this.projects;
            }
            // For unit testing only
            set
            {
                this.projects = value;
            }
        }

        /// <summary>
        /// This is the read/write accessor for the solution file which we will parse.  This
        /// must be set before calling any other methods on this class.
        /// </summary>
        /// <value></value>
        internal string SolutionFile
        {
            get
            {
                return solutionFile;
            }

            set
            {
                solutionFile = value;
            }
        }

        internal string SolutionFileDirectory
        {
            get
            {
                return solutionFileDirectory;
            }
            // This setter is only used by the unit tests
            set
            {
                this.solutionFileDirectory = value;
            }
        }

        /// <summary>
        /// For unit-testing only.
        /// </summary>
        /// <value></value>
        /// <owner>RGoel</owner>
        internal StreamReader SolutionReader
        {
            get
            {
                return reader;
            }

            set
            {
                reader = value;
            }
        }

        /// <summary>
        /// For unit-testing only.
        /// </summary>
        /// <value></value>
        internal ProjectInSolution[] Projects
        {
            get
            {
                return (ProjectInSolution[]) this.projectsInOrder.ToArray(typeof(ProjectInSolution));
            }
        }

        /// <summary>
        /// The list of all full solution configurations (configuration + platform) in this solution
        /// </summary>
        /// <owner>LukaszG</owner>
        internal List<ConfigurationInSolution> SolutionConfigurations
        {
            get
            {
                return this.solutionConfigurations;
            }
        }

        #endregion

        #region Methods

        /// <summary>
        /// Reads a line from the StreamReader, trimming leading and trailing whitespace.
        /// </summary>
        /// <returns></returns>
        /// <owner>RGoel</owner>
        private string ReadLine()
        {
            error.VerifyThrow(reader != null, "ParseFileHeader(): reader is null!");

            string line = reader.ReadLine();
            this.currentLineNumber++;

            if (line != null)
            {
                line = line.Trim();
            }

            return line;
        }

        /// <summary>
        /// This method takes a path to a solution file, parses the projects and project dependencies
        /// in the solution file, and creates internal data structures representing the projects within
        /// the SLN.
        /// </summary>
        /// <owner>RGoel</owner>
        internal void ParseSolutionFile()
        {
            error.VerifyThrow(!string.IsNullOrEmpty(solutionFile), "ParseSolutionFile() got a null solution file!");

            FileStream fileStream = null;
            reader = null;

            try
            {
                // Open the file
                fileStream = File.OpenRead(solutionFile);
                // Store the directory of the file as the current directory may change while we are processes the file
                solutionFileDirectory = Path.GetDirectoryName(Path.GetFullPath(solutionFile));
                reader = new StreamReader(fileStream, Encoding.Default); // HIGHCHAR: If solution files have no byte-order marks, then assume ANSI rather than ASCII.
                this.ParseSolution();
            }
            finally
            {
                fileStream?.Close();

                reader?.Close();
            }
        }

        /// <summary>
        /// Parses the SLN file represented by the StreamReader in this.reader, and populates internal
        /// data structures based on the SLN file contents.
        /// </summary>
        /// <owner>RGoel</owner>
        internal void ParseSolution()
        {
            this.projects = new Hashtable(StringComparer.OrdinalIgnoreCase);
            this.projectsInOrder = new ArrayList();
            this.solutionContainsWebProjects = false;
            this.slnFileActualVersion = 0;
            this.currentLineNumber = 0;
            this.solutionConfigurations = new List<ConfigurationInSolution>();
            this.defaultConfigurationName = null;
            this.defaultPlatformName = null;

            // the raw list of project configurations in solution configurations, to be processed after it's fully read in.
            Hashtable rawProjectConfigurationsEntries = null;

            ParseFileHeader();

            string str;
            while ((str = ReadLine()) != null)
            {
                if (str.StartsWith("Project(", StringComparison.Ordinal))
                {
                    ParseProject(str);
                }
                else if (str.StartsWith("GlobalSection(NestedProjects)", StringComparison.Ordinal))
                {
                    ParseNestedProjects();
                }
                else if (str.StartsWith("GlobalSection(SolutionConfigurationPlatforms)", StringComparison.Ordinal))
                {
                    ParseSolutionConfigurations();
                }
                else if (str.StartsWith("GlobalSection(ProjectConfigurationPlatforms)", StringComparison.Ordinal))
                {
                    rawProjectConfigurationsEntries = ParseProjectConfigurations();
                }
                else
                {
                    // No other section types to process at this point, so just ignore the line
                    // and continue.
                }
            }

            if (rawProjectConfigurationsEntries != null)
            {
                ProcessProjectConfigurationSection(rawProjectConfigurationsEntries);
            }

            // Cache the unique name of each project, and check that we don't have any duplicates.
            Hashtable projectsByUniqueName = new Hashtable(StringComparer.OrdinalIgnoreCase);

            foreach (ProjectInSolution proj in projectsInOrder)
            {
                // Find the unique name for the project.  This method also caches the unique name,
                // so it doesn't have to be recomputed later.
                string uniqueName = proj.GetUniqueProjectName();

                // Throw an error if there are any duplicates
                ProjectFileErrorUtilities.VerifyThrowInvalidProjectFile(
                    projectsByUniqueName[uniqueName] == null,
                    "SubCategoryForSolutionParsingErrors",
                    new BuildEventFileInfo(SolutionFile),
                    "SolutionParseDuplicateProject",
                    uniqueName);

                // Update the hash table with this unique name
                projectsByUniqueName[uniqueName] = proj;
            }
        } // ParseSolutionFile()

        /// <summary>
        /// This method searches the first two lines of the solution file opened by the specified
        /// StreamReader for the solution file header.  An exception is thrown if it is not found.
        /// 
        /// The solution file header looks like this:
        /// 
        ///     Microsoft Visual Studio Solution File, Format Version 9.00
        /// 
        /// </summary>
        /// <owner>RGoel</owner>
        private void ParseFileHeader()
        {
            error.VerifyThrow(reader != null, "ParseFileHeader(): reader is null!");

            const string slnFileHeaderNoVersion = "Microsoft Visual Studio Solution File, Format Version ";

            // Read the file header.  This can be on either of the first two lines.
            for (int i=1 ; i<=2 ; i++)
            {
                string str = ReadLine();
                if (str == null)
                {
                    break;
                }

                if (str.StartsWith(slnFileHeaderNoVersion, StringComparison.Ordinal))
                {
                    // Found it.  Validate the version.
                    ValidateSolutionFileVersion(str.Substring(slnFileHeaderNoVersion.Length));
                    return;
                }
            }

            // Didn't find the header on either the first or second line, so the solution file
            // is invalid.
            ProjectFileErrorUtilities.VerifyThrowInvalidProjectFile(false, "SubCategoryForSolutionParsingErrors",
                new BuildEventFileInfo(SolutionFile), "SolutionParseNoHeaderError");
        }

        /// <summary>
        /// This method extracts the whole part of the version number from the specified line
        /// containing the solution file format header, and throws an exception if the version number
        /// is outside of the valid range.
        /// 
        /// The solution file header looks like this:
        /// 
        ///     Microsoft Visual Studio Solution File, Format Version 9.00
        /// 
        /// </summary>
        /// <param name="versionString"></param>
        /// <owner>RGoel</owner>
        private void ValidateSolutionFileVersion(string versionString)
        {
            error.VerifyThrow(versionString != null, "ValidateSolutionFileVersion() got a null line!");

            Version version = null;
            try
            {
                version = new Version(versionString);
            }
            catch (FormatException)
            {
                // This happens if the version stamp wasn't a properly formed version number,
                // as in "1.a.b.c".
                ProjectFileErrorUtilities.VerifyThrowInvalidProjectFile(false, "SubCategoryForSolutionParsingErrors",
                    new BuildEventFileInfo(SolutionFile, this.currentLineNumber, 0), "SolutionParseVersionMismatchError", 
                    slnFileMinUpgradableVersion, slnFileMaxVersion);
            }
            catch (ArgumentException)
            {
                // This happens if the version stamp wasn't a properly formed version number.
                ProjectFileErrorUtilities.VerifyThrowInvalidProjectFile(false, "SubCategoryForSolutionParsingErrors",
                    new BuildEventFileInfo(SolutionFile, this.currentLineNumber, 0), "SolutionParseVersionMismatchError", 
                    slnFileMinUpgradableVersion, slnFileMaxVersion);
            }

            this.slnFileActualVersion = version.Major;

            // Validate against our min & max
            ProjectFileErrorUtilities.VerifyThrowInvalidProjectFile(
                this.slnFileActualVersion >= slnFileMinUpgradableVersion,
                "SubCategoryForSolutionParsingErrors",
                new BuildEventFileInfo(SolutionFile, this.currentLineNumber, 0), 
                "SolutionParseVersionMismatchError", 
                slnFileMinUpgradableVersion, slnFileMaxVersion);

            // If the solution file version is greater than the maximum one we will create a comment rather than warn
            // as users such as blend opening a dev10 project cannot do anything about it.
            if (this.slnFileActualVersion > slnFileMaxVersion)
            {
                solutionParserComments.Add(ResourceUtilities.FormatResourceString("UnrecognizedSolutionComment", this.slnFileActualVersion));
            }
        }

        /// <summary>
        /// 
        /// This method processes a "Project" section in the solution file opened by the specified
        /// StreamReader, and returns a populated ProjectInSolution instance, if successful.
        /// An exception is thrown if the solution file is invalid.
        ///
        /// The format of the parts of a Project section that we care about is as follows:
        ///
        ///  Project("{Project type GUID}") = "Project name", "Relative path to project file", "{Project GUID}"
        ///      ProjectSection(ProjectDependencies) = postProject
        ///          {Parent project unique name} = {Parent project unique name}
        ///          ...
        ///      EndProjectSection
        ///  EndProject
        /// 
        /// </summary>
        /// <param name="firstLine"></param>
        /// <returns></returns>
        /// <owner>RGoel</owner>
        private void ParseProject(string firstLine)
        {
            error.VerifyThrow(!string.IsNullOrEmpty(firstLine), "ParseProject() got a null firstLine!");
            error.VerifyThrow(reader != null, "ParseProject() got a null reader!");

            ProjectInSolution proj = new ProjectInSolution(this);

            // Extract the important information from the first line.
            ParseFirstProjectLine(firstLine, proj);

            // Search for project dependencies.  Keeping reading lines until we either 1.) reach
            // the end of the file, 2.) see "ProjectSection(ProjectDependencies)" at the beginning
            // of the line, or 3.) see "EndProject" at the beginning of the line.
            string line;
            while ((line = ReadLine()) != null)
            {
                // If we see an "EndProject", well ... that's the end of this project!
                if (line == "EndProject")
                {
                    break;
                }
                else if (line.StartsWith("ProjectSection(ProjectDependencies)", StringComparison.Ordinal))
                {
                    // We have a ProjectDependencies section.  Each subsequent line should identify
                    // a dependency.
                    line = ReadLine();
                    while ((line?.StartsWith("EndProjectSection", StringComparison.Ordinal) == false))
                    {
                        // This should be a dependency.  The GUID identifying the parent project should
                        // be both the property name and the property value.
                        Match match = crackPropertyLine.Match(line);
                        ProjectFileErrorUtilities.VerifyThrowInvalidProjectFile(match.Success, "SubCategoryForSolutionParsingErrors",
                            new BuildEventFileInfo(SolutionFile, this.currentLineNumber, 0), "SolutionParseProjectDepGuidError", proj.ProjectName);

                        string parentGuid = match.Groups["PROPERTYNAME"].Value.Trim();
                        proj.Dependencies.Add(parentGuid);

                        line = ReadLine();
                    }
                }
                else if (line.StartsWith("ProjectSection(WebsiteProperties)", StringComparison.Ordinal))
                {
                    // We have a WebsiteProperties section.  This section is present only in Venus
                    // projects, and contains properties that we'll need in order to call the 
                    // AspNetCompiler task.
                    line = ReadLine();
                    while ((line?.StartsWith("EndProjectSection", StringComparison.Ordinal) == false))
                    {
                        Match match = crackPropertyLine.Match(line);
                        ProjectFileErrorUtilities.VerifyThrowInvalidProjectFile(match.Success, "SubCategoryForSolutionParsingErrors",
                            new BuildEventFileInfo(SolutionFile, this.currentLineNumber, 0), "SolutionParseWebProjectPropertiesError", proj.ProjectName);

                        string propertyName = match.Groups["PROPERTYNAME"].Value.Trim();
                        string propertyValue = match.Groups["PROPERTYVALUE"].Value.Trim();

                        ParseAspNetCompilerProperty(proj, propertyName, propertyValue);

                        line = ReadLine();
                    }
                }
            }

            ProjectFileErrorUtilities.VerifyThrowInvalidProjectFile(line != null, "SubCategoryForSolutionParsingErrors",
                new BuildEventFileInfo(SolutionFile), "SolutionParseProjectEofError", proj.ProjectName);

            if (proj != null)
            {
                // Add the project to the collection
                AddProjectToSolution(proj);
                // If the project is an etp project then parse the etp project file 
                // to get the projects contained in it.
                if (IsEtpProjectFile(proj.RelativePath))
                {
                    ParseEtpProject(proj);
                }
            }
        } // ParseProject()

        /// <summary>
        /// This method will parse a .etp project recursively and 
        /// add all the projects found to projects and projectsInOrder
        /// </summary>
        /// <param name="etpProj">ETP Project</param>
        internal void ParseEtpProject(ProjectInSolution etpProj)
        {
            XmlDocument etpProjectDocument = new XmlDocument();
            // Get the full path to the .etp project file
            string fullPathToEtpProj = Path.Combine(solutionFileDirectory, etpProj.RelativePath);
            string etpProjectRelativeDir = Path.GetDirectoryName(etpProj.RelativePath);
            try
            {
                /****************************************************************************
                * A Typical .etp project file will look like this
                *<?xml version="1.0"?>
                *<EFPROJECT>
                *    <GENERAL>
                *        <BANNER>Microsoft Visual Studio Application Template File</BANNER>
                *        <VERSION>1.00</VERSION>
                *        <Views>
                *            <ProjectExplorer>
                *                <File>ClassLibrary2\ClassLibrary2.csproj</File>
                *            </ProjectExplorer>
                *        </Views>
                *        <References>
                *            <Reference>
                *                <FILE>ClassLibrary2\ClassLibrary2.csproj</FILE>
                *                <GUIDPROJECTID>{73D0F4CE-D9D3-4E8B-81E4-B26FBF4CC2FE}</GUIDPROJECTID>
                *            </Reference>
                *        </References>
                *    </GENERAL>
                *</EFPROJECT>
                **********************************************************************************/
                // Make sure the XML reader ignores DTD processing
                XmlReaderSettings readerSettings = new XmlReaderSettings();
                readerSettings.DtdProcessing = DtdProcessing.Ignore;

                // Load the .etp project file thru the XML reader
                using (XmlReader xmlReader = XmlReader.Create(fullPathToEtpProj, readerSettings))
                {
                    etpProjectDocument.Load(xmlReader);
                }
                
                // We need to parse the .etp project file to get the names of projects contained
                // in the .etp Project. The projects are listed under /EFPROJECT/GENERAL/References/Reference node in the .etp project file.
                // The /EFPROJECT/GENERAL/Views/ProjectExplorer node will not necessarily contain 
                // all the projects in the .etp project. Therefore, we need to look at 
                // /EFPROJECT/GENERAL/References/Reference.
                // Find the /EFPROJECT/GENERAL/References/Reference node
                // Note that this is case sensitive
                XmlNodeList referenceNodes = etpProjectDocument.DocumentElement.SelectNodes("/EFPROJECT/GENERAL/References/Reference");
                // Do the right thing for each <REference> element
                foreach (XmlNode referenceNode in referenceNodes)
                {
                    // Get the relative path to the project file
                    string fileElementValue = referenceNode.SelectSingleNode("FILE").InnerText;
                    // If <FILE>  element is not present under <Reference> then we don't do anything.
                    if (fileElementValue != null)
                    {
                        // Create and populate a ProjectInSolution for the project
                        ProjectInSolution proj = new ProjectInSolution(this);
                        proj.RelativePath = Path.Combine(etpProjectRelativeDir, fileElementValue);

                        // Verify the relative path specified in the .etp proj file
                        ValidateProjectRelativePath(proj);
                        proj.ProjectType = SolutionProjectType.EtpSubProject;
                        proj.ProjectName = proj.RelativePath;
                        XmlNode projGuidNode = referenceNode.SelectSingleNode("GUIDPROJECTID");
                        if (projGuidNode != null)
                        {
                            proj.ProjectGuid = projGuidNode.InnerText;
                        }
                        // It is ok for a project to not have a guid inside an etp project.
                        // If a solution file contains a project without a guid it fails to 
                        // load in Everett. But if an etp project contains a project without 
                        // a guid it loads well in Everett and p2p references to/from this project
                        // are preserved. So we should make sure that we don�t error in this 
                        // situation while upgrading.
                        else
                        {
                            proj.ProjectGuid = String.Empty;
                        }
                        // Add the recently created proj to the collection of projects
                        AddProjectToSolution(proj);
                        // If the project is an etp project recurse
                        if (IsEtpProjectFile(fileElementValue))
                        {
                            ParseEtpProject(proj);
                        }
                    }
                }
            }
            // catch all sorts of exceptions - if we encounter any problems here, we just assume the .etp project file is not in the correct format

            // handle security errors
            catch (SecurityException e)
            {
                // Log a warning 
                string errorCode, ignoredKeyword;
                string warning = ResourceUtilities.FormatResourceString(out errorCode, out ignoredKeyword, "Shared.ProjectFileCouldNotBeLoaded",
                    etpProj.RelativePath, e.Message);
                solutionParserWarnings.Add(warning);
                solutionParserErrorCodes.Add(errorCode);
            }
            // handle errors in path resolution
            catch (NotSupportedException e)
            {
                // Log a warning 
                string errorCode, ignoredKeyword;
                string warning = ResourceUtilities.FormatResourceString(out errorCode, out ignoredKeyword, "Shared.ProjectFileCouldNotBeLoaded",
                    etpProj.RelativePath, e.Message);
                solutionParserWarnings.Add(warning);
                solutionParserErrorCodes.Add(errorCode);
            }
            // handle errors in loading project file
            catch (IOException e)
            {
                // Log a warning 
                string errorCode, ignoredKeyword;
                string warning = ResourceUtilities.FormatResourceString(out errorCode, out ignoredKeyword, "Shared.ProjectFileCouldNotBeLoaded",
                    etpProj.RelativePath, e.Message);
                solutionParserWarnings.Add(warning);
                solutionParserErrorCodes.Add(errorCode);
            }
            // handle errors in loading project file
            catch (UnauthorizedAccessException e)
            {
                // Log a warning 
                string errorCode, ignoredKeyword;
                string warning = ResourceUtilities.FormatResourceString(out errorCode, out ignoredKeyword, "Shared.ProjectFileCouldNotBeLoaded",
                    etpProj.RelativePath, e.Message);
                solutionParserWarnings.Add(warning);
                solutionParserErrorCodes.Add(errorCode);
            }
            // handle XML parsing errors 
            catch (XmlException e)
            {
                // Log a warning 
                string errorCode, ignoredKeyword;
                string warning = ResourceUtilities.FormatResourceString(out errorCode, out ignoredKeyword, "Shared.InvalidProjectFile",
                   etpProj.RelativePath, e.Message);
                solutionParserWarnings.Add(warning);
                solutionParserErrorCodes.Add(errorCode);
            }  
        }

        /// <summary>
        /// Adds a given project to the project collections of this class
        /// </summary>
        /// <param name="proj">proj</param>
        private void AddProjectToSolution(ProjectInSolution proj)
        {
            if (!String.IsNullOrEmpty(proj.ProjectGuid))
            {
                projects[proj.ProjectGuid] = proj;
            }
            projectsInOrder.Add(proj);
        }
        
        /// <summary>
        /// Checks whether a given project has a .etp extension.
        /// </summary>
        /// <param name="projectFile"></param>
        private bool IsEtpProjectFile(string projectFile)
        {
            return projectFile.EndsWith(".etp", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Validate relative path of a project
        /// </summary>
        /// <param name="proj">proj</param>
        private void ValidateProjectRelativePath(ProjectInSolution proj)
        {
            // Verify the relative path is not null
            ErrorUtilities.VerifyThrow(proj.RelativePath != null, "Project relative path cannot be null.");

            // Verify the relative path does not contain invalid characters
            ProjectFileErrorUtilities.VerifyThrowInvalidProjectFile(proj.RelativePath.IndexOfAny(Path.GetInvalidPathChars()) == -1,
              "SubCategoryForSolutionParsingErrors",
              new BuildEventFileInfo(SolutionFile, this.currentLineNumber, 0),
              "SolutionParseInvalidProjectFileNameCharacters",
              proj.ProjectName, proj.RelativePath);

            // Verify the relative path is not empty string
            ProjectFileErrorUtilities.VerifyThrowInvalidProjectFile(proj.RelativePath.Length > 0,
                  "SubCategoryForSolutionParsingErrors",
                  new BuildEventFileInfo(SolutionFile, this.currentLineNumber, 0),
                  "SolutionParseInvalidProjectFileNameEmpty",
                  proj.ProjectName);
        }

        /// <summary>
        /// Takes a property name / value that comes from the SLN file for a Venus project, and
        /// stores it appropriately in our data structures.
        /// </summary>
        /// <param name="proj"></param>
        /// <param name="propertyName"></param>
        /// <param name="propertyValue"></param>
        /// <owner>RGoel</owner>
        private void ParseAspNetCompilerProperty
            (
            ProjectInSolution proj,
            string propertyName,
            string propertyValue
            )
        {
            // What we expect to find in the SLN file is something that looks like this:
            //
            // Project("{E24C65DC-7377-472B-9ABA-BC803B73C61A}") = "c:\...\myfirstwebsite\", "..\..\..\..\..\..\rajeev\temp\websites\myfirstwebsite", "{956CC04E-FD59-49A9-9099-96888CB6F366}"
            //     ProjectSection(WebsiteProperties) = preProject
            //       ProjectReferences = "{FD705688-88D1-4C22-9BFF-86235D89C2FC}|CSClassLibrary1.dll;{F0726D09-042B-4A7A-8A01-6BED2422BD5D}|VCClassLibrary1.dll;" 
            //       Debug.AspNetCompiler.VirtualPath = "/publishfirst"
            //       Debug.AspNetCompiler.PhysicalPath = "..\..\..\..\..\..\rajeev\temp\websites\myfirstwebsite\"
            //       Debug.AspNetCompiler.TargetPath = "..\..\..\..\..\..\rajeev\temp\publishfirst\"
            //       Debug.AspNetCompiler.ForceOverwrite = "true"
            //       Debug.AspNetCompiler.Updateable = "true"
            //       Debug.AspNetCompiler.Enabled = "true"
            //       Debug.AspNetCompiler.Debug = "true"
            //       Debug.AspNetCompiler.KeyFile = ""
            //       Debug.AspNetCompiler.KeyContainer = ""
            //       Debug.AspNetCompiler.DelaySign = "true"
            //       Debug.AspNetCompiler.AllowPartiallyTrustedCallers = "true"
            //       Debug.AspNetCompiler.FixedNames = "true"
            //       Release.AspNetCompiler.VirtualPath = "/publishfirst"
            //       Release.AspNetCompiler.PhysicalPath = "..\..\..\..\..\..\rajeev\temp\websites\myfirstwebsite\"
            //       Release.AspNetCompiler.TargetPath = "..\..\..\..\..\..\rajeev\temp\publishfirst\"
            //       Release.AspNetCompiler.ForceOverwrite = "true"
            //       Release.AspNetCompiler.Updateable = "true"
            //       Release.AspNetCompiler.Enabled = "true"
            //       Release.AspNetCompiler.Debug = "false"
            //       Release.AspNetCompiler.KeyFile = ""
            //       Release.AspNetCompiler.KeyContainer = ""
            //       Release.AspNetCompiler.DelaySign = "true"
            //       Release.AspNetCompiler.AllowPartiallyTrustedCallers = "true"
            //       Release.AspNetCompiler.FixedNames = "true"
            //     EndProjectSection
            // EndProject
            //
            // This method is responsible for parsing each of the lines within the "WebsiteProperties" section.
            // The first component of each property name is actually the configuration for which that
            // property applies.

            int indexOfFirstDot = propertyName.IndexOf('.');
            if (indexOfFirstDot != -1)
            {
                // The portion before the first dot is the configuration name.
                string configurationName = propertyName.Substring(0, indexOfFirstDot);

                // The rest of it is the actual property name.
                string aspNetPropertyName = propertyName.Substring(indexOfFirstDot + 1, propertyName.Length - indexOfFirstDot - 1);

                // And the part after the <equals> sign is the property value (which was parsed out for us prior
                // to calling this method).
                propertyValue = TrimQuotes(propertyValue);

                // Grab the parameters for this specific configuration if they exist.
                object aspNetCompilerParametersObject = proj.AspNetConfigurations[configurationName];
                AspNetCompilerParameters aspNetCompilerParameters;

                if (aspNetCompilerParametersObject == null)
                {
                    // If it didn't exist, create a new one.
                    aspNetCompilerParameters = new AspNetCompilerParameters();
                    aspNetCompilerParameters.aspNetVirtualPath = String.Empty;
                    aspNetCompilerParameters.aspNetPhysicalPath = String.Empty;
                    aspNetCompilerParameters.aspNetTargetPath = String.Empty;
                    aspNetCompilerParameters.aspNetForce = String.Empty;
                    aspNetCompilerParameters.aspNetUpdateable = String.Empty;
                    aspNetCompilerParameters.aspNetDebug = String.Empty;
                    aspNetCompilerParameters.aspNetKeyFile = String.Empty;
                    aspNetCompilerParameters.aspNetKeyContainer = String.Empty;
                    aspNetCompilerParameters.aspNetDelaySign = String.Empty;
                    aspNetCompilerParameters.aspNetAPTCA = String.Empty;
                    aspNetCompilerParameters.aspNetFixedNames = String.Empty;
                }
                else
                {
                    // Otherwise just unbox it.
                    aspNetCompilerParameters = (AspNetCompilerParameters)aspNetCompilerParametersObject;
                }

                // Update the appropriate field within the parameters struct.
                if (aspNetPropertyName == "AspNetCompiler.VirtualPath")
                {
                    aspNetCompilerParameters.aspNetVirtualPath = propertyValue;
                }
                else if (aspNetPropertyName == "AspNetCompiler.PhysicalPath")
                {
                    aspNetCompilerParameters.aspNetPhysicalPath = propertyValue;
                }
                else if (aspNetPropertyName == "AspNetCompiler.TargetPath")
                {
                    aspNetCompilerParameters.aspNetTargetPath = propertyValue;
                }
                else if (aspNetPropertyName == "AspNetCompiler.ForceOverwrite")
                {
                    aspNetCompilerParameters.aspNetForce = propertyValue;
                }
                else if (aspNetPropertyName == "AspNetCompiler.Updateable")
                {
                    aspNetCompilerParameters.aspNetUpdateable = propertyValue;
                }
                else if (aspNetPropertyName == "AspNetCompiler.Debug")
                {
                    aspNetCompilerParameters.aspNetDebug = propertyValue;
                }
                else if (aspNetPropertyName == "AspNetCompiler.KeyFile")
                {
                    aspNetCompilerParameters.aspNetKeyFile = propertyValue;
                }
                else if (aspNetPropertyName == "AspNetCompiler.KeyContainer")
                {
                    aspNetCompilerParameters.aspNetKeyContainer = propertyValue;
                }
                else if (aspNetPropertyName == "AspNetCompiler.DelaySign")
                {
                    aspNetCompilerParameters.aspNetDelaySign = propertyValue;
                }
                else if (aspNetPropertyName == "AspNetCompiler.AllowPartiallyTrustedCallers")
                {
                    aspNetCompilerParameters.aspNetAPTCA = propertyValue;
                }
                else if (aspNetPropertyName == "AspNetCompiler.FixedNames")
                {
                    aspNetCompilerParameters.aspNetFixedNames = propertyValue;
                }

                // Store the updated parameters struct back into the hashtable by configuration name.
                proj.AspNetConfigurations[configurationName] = aspNetCompilerParameters;
            }
            else
            {
                // ProjectReferences = "{FD705688-88D1-4C22-9BFF-86235D89C2FC}|CSClassLibrary1.dll;{F0726D09-042B-4A7A-8A01-6BED2422BD5D}|VCClassLibrary1.dll;" 
                if (string.Equals(propertyName, "ProjectReferences", StringComparison.OrdinalIgnoreCase))
                {
                    string[] projectReferenceEntries = propertyValue.Split(new char[] { ';' });

                    foreach (string projectReferenceEntry in projectReferenceEntries)
                    {
                        int indexOfBar = projectReferenceEntry.IndexOf('|');

                        // indexOfBar could be -1 if we had semicolons in the file names, so skip entries that 
                        // don't contain a guid. File names may not contain the '|' character
                        if (indexOfBar != -1)
                        {
                            int indexOfOpeningBrace = projectReferenceEntry.IndexOf('{');
                            int indexOfClosingBrace = projectReferenceEntry.IndexOf('}', indexOfOpeningBrace);

                            // Cut out the guid part
                            if ((indexOfOpeningBrace != -1) && (indexOfClosingBrace != -1))
                            {
                                string referencedProjectGuid = projectReferenceEntry.Substring(indexOfOpeningBrace, 
                                    indexOfClosingBrace - indexOfOpeningBrace + 1);

                                proj.Dependencies.Add(referencedProjectGuid);
                                proj.ProjectReferences.Add(referencedProjectGuid);
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Strips a single pair of leading/trailing double-quotes from a string.
        /// </summary>
        /// <param name="property"></param>
        /// <returns></returns>
        /// <owner>RGoel</owner>
        private string TrimQuotes
            (
            string property
            )
        {
            // If the incoming string starts and ends with a double-quote, strip the double-quotes.
            if (!string.IsNullOrEmpty(property) && (property[0] == '"') && (property[property.Length - 1] == '"'))
            {
                return property.Substring(1, property.Length - 2);
            }
            else
            {
                return property;
            }
        }

        /// <summary>
        /// Parse the first line of a Project section of a solution file. This line should look like:
        ///
        ///  Project("{Project type GUID}") = "Project name", "Relative path to project file", "{Project GUID}"
        /// 
        /// </summary>
        /// <param name="firstLine"></param>
        /// <param name="proj"></param>
        /// <owner>RGoel</owner>
        internal void ParseFirstProjectLine
        (
            string firstLine,
            ProjectInSolution proj
        )
        {
            Match match = crackProjectLine.Match(firstLine);
            ProjectFileErrorUtilities.VerifyThrowInvalidProjectFile(match.Success, "SubCategoryForSolutionParsingErrors",
                new BuildEventFileInfo(SolutionFile, this.currentLineNumber, 0), "SolutionParseProjectError");

            string projectTypeGuid = match.Groups["PROJECTTYPEGUID"].Value.Trim();
            proj.ProjectName = match.Groups["PROJECTNAME"].Value.Trim();
            proj.RelativePath = match.Groups["RELATIVEPATH"].Value.Trim();
            proj.ProjectGuid = match.Groups["PROJECTGUID"].Value.Trim();
            
            // Validate project relative path
            ValidateProjectRelativePath(proj);
            
            // Figure out what type of project this is.
            if ((String.Equals(projectTypeGuid, vbProjectGuid, StringComparison.OrdinalIgnoreCase)) ||
                (String.Equals(projectTypeGuid, csProjectGuid, StringComparison.OrdinalIgnoreCase)) ||
                (String.Equals(projectTypeGuid, vjProjectGuid, StringComparison.OrdinalIgnoreCase)))
            {
                proj.ProjectType = SolutionProjectType.ManagedProject;
            }
            else if (String.Equals(projectTypeGuid, solutionFolderGuid, StringComparison.OrdinalIgnoreCase))
            {
                proj.ProjectType = SolutionProjectType.SolutionFolder;
            }
            else if (String.Equals(projectTypeGuid, vcProjectGuid, StringComparison.OrdinalIgnoreCase))
            {
                proj.ProjectType = SolutionProjectType.VCProject;
            }
            else if (String.Equals(projectTypeGuid, webProjectGuid, StringComparison.OrdinalIgnoreCase))
            {
                proj.ProjectType = SolutionProjectType.WebProject;
                solutionContainsWebProjects = true;
            }
            else
            {
                proj.ProjectType = SolutionProjectType.Unknown;
            }
        }

        /// <summary>
        /// Read nested projects section.
        /// This is required to find a unique name for each project's target
        /// </summary>
        /// <owner>RGoel</owner>
        internal void ParseNestedProjects()
        {
            string str;

            do
            {
                str = ReadLine();
                if ((str == null) || (str == "EndGlobalSection"))
                {
                    break;
                }

                Match match = crackPropertyLine.Match(str);
                ProjectFileErrorUtilities.VerifyThrowInvalidProjectFile(match.Success, "SubCategoryForSolutionParsingErrors",
                    new BuildEventFileInfo(SolutionFile, this.currentLineNumber, 0), "SolutionParseNestedProjectError");

                string projectGuid = match.Groups["PROPERTYNAME"].Value.Trim();
                string parentProjectGuid = match.Groups["PROPERTYVALUE"].Value.Trim();

                ProjectInSolution proj = (ProjectInSolution)projects[projectGuid];
                ProjectFileErrorUtilities.VerifyThrowInvalidProjectFile(proj != null, "SubCategoryForSolutionParsingErrors",
                    new BuildEventFileInfo(SolutionFile, this.currentLineNumber, 0), "SolutionParseNestedProjectError");

                proj.ParentProjectGuid = parentProjectGuid;

            } while (true);
        }

        /// <summary>
        /// Read solution configuration section. 
        /// </summary>
        /// <remarks>
        /// A sample section:
        /// 
        /// GlobalSection(SolutionConfigurationPlatforms) = preSolution
        ///     Debug|Any CPU = Debug|Any CPU
        ///     Release|Any CPU = Release|Any CPU
        /// EndGlobalSection
        /// </remarks>
        /// <owner>LukaszG</owner>
        internal void ParseSolutionConfigurations()
        {
            string str;
            char[] nameValueSeparators = new char[] { '=' };
            char[] configPlatformSeparators = new char[] { ConfigurationInSolution.configurationPlatformSeparator };

            do
            {
                str = ReadLine();

                if ((str == null) || (str == "EndGlobalSection"))
                {
                    break;
                }

                string[] configurationNames = str.Split(nameValueSeparators);

                // There should be exactly one '=' character, separating two names. 
                ProjectFileErrorUtilities.VerifyThrowInvalidProjectFile(configurationNames.Length == 2, "SubCategoryForSolutionParsingErrors",
                    new BuildEventFileInfo(SolutionFile, this.currentLineNumber, 0), "SolutionParseInvalidSolutionConfigurationEntry", str);

                string fullConfigurationName = configurationNames[0].Trim();

                //Fixing bug 555577: Solution file can have description information, in which case we ignore.
                if (String.Equals(fullConfigurationName, "DESCRIPTION", StringComparison.OrdinalIgnoreCase))
                    continue;

                // Both names must be identical
                ProjectFileErrorUtilities.VerifyThrowInvalidProjectFile(fullConfigurationName == configurationNames[1].Trim(), "SubCategoryForSolutionParsingErrors",
                    new BuildEventFileInfo(SolutionFile, this.currentLineNumber, 0), "SolutionParseInvalidSolutionConfigurationEntry", str);

                string[] configurationPlatformParts = fullConfigurationName.Split(configPlatformSeparators);

                ProjectFileErrorUtilities.VerifyThrowInvalidProjectFile(configurationPlatformParts.Length == 2, "SubCategoryForSolutionParsingErrors",
                    new BuildEventFileInfo(SolutionFile, this.currentLineNumber, 0), "SolutionParseInvalidSolutionConfigurationEntry", str);

                this.solutionConfigurations.Add(new ConfigurationInSolution(configurationPlatformParts[0], configurationPlatformParts[1]));

            } while (true);
        }

        /// <summary>
        /// Read project configurations in solution configurations section.
        /// </summary>
        /// <remarks>
        /// A sample (incomplete) section:
        /// 
        /// GlobalSection(ProjectConfigurationPlatforms) = postSolution
        /// 	{6185CC21-BE89-448A-B3C0-D1C27112E595}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
        /// 	{6185CC21-BE89-448A-B3C0-D1C27112E595}.Debug|Any CPU.Build.0 = Debug|Any CPU
        /// 	{6185CC21-BE89-448A-B3C0-D1C27112E595}.Debug|Mixed Platforms.ActiveCfg = Release|Any CPU
        /// 	{6185CC21-BE89-448A-B3C0-D1C27112E595}.Debug|Mixed Platforms.Build.0 = Release|Any CPU
        /// 	{6185CC21-BE89-448A-B3C0-D1C27112E595}.Debug|Win32.ActiveCfg = Debug|Any CPU
        /// 	{A6F99D27-47B9-4EA4-BFC9-25157CBDC281}.Release|Any CPU.ActiveCfg = Release|Win32
        /// 	{A6F99D27-47B9-4EA4-BFC9-25157CBDC281}.Release|Mixed Platforms.ActiveCfg = Release|Win32
        /// 	{A6F99D27-47B9-4EA4-BFC9-25157CBDC281}.Release|Mixed Platforms.Build.0 = Release|Win32
        /// 	{A6F99D27-47B9-4EA4-BFC9-25157CBDC281}.Release|Win32.ActiveCfg = Release|Win32
        /// 	{A6F99D27-47B9-4EA4-BFC9-25157CBDC281}.Release|Win32.Build.0 = Release|Win32
        /// EndGlobalSection
        /// </remarks>
        /// <returns>An unprocessed hashtable of entries in this section</returns>
        internal Hashtable ParseProjectConfigurations()
        {
            Hashtable rawProjectConfigurationsEntries = new Hashtable(StringComparer.OrdinalIgnoreCase);
            string str;

            do
            {
                str = ReadLine();

                if ((str == null) || (str == "EndGlobalSection"))
                {
                    break;
                }

                string[] nameValue = str.Split(new char[] { '=' });

                // There should be exactly one '=' character, separating the name and value. 
                ProjectFileErrorUtilities.VerifyThrowInvalidProjectFile(nameValue.Length == 2, "SubCategoryForSolutionParsingErrors",
                    new BuildEventFileInfo(SolutionFile, this.currentLineNumber, 0), "SolutionParseInvalidProjectSolutionConfigurationEntry", str);

                rawProjectConfigurationsEntries[nameValue[0].Trim()] = nameValue[1].Trim();

            } while (true);

            return rawProjectConfigurationsEntries;
        }

        /// <summary>
        /// Read the project configuration information for every project in the solution, using pre-cached 
        /// solution section data. 
        /// </summary>
        /// <param name="rawProjectConfigurationsEntries">Cached data from the project configuration section</param>
        /// <owner>LukaszG</owner>
        internal void ProcessProjectConfigurationSection(Hashtable rawProjectConfigurationsEntries)
        {
            // Instead of parsing the data line by line, we parse it project by project, constructing the 
            // entry name (e.g. "{A6F99D27-47B9-4EA4-BFC9-25157CBDC281}.Release|Any CPU.ActiveCfg") and retrieving its 
            // value from the raw data. The reason for this is that the IDE does it this way, and as the result
            // the '.' character is allowed in configuration names although it technically separates different
            // parts of the entry name string. This could lead to ambiguous results if we tried to parse 
            // the entry name instead of constructing it and looking it up. Although it's pretty unlikely that
            // this would ever be a problem, it's safer to do it the same way VS IDE does it.
            char[] configPlatformSeparators = new char[] { ConfigurationInSolution.configurationPlatformSeparator };
            
            foreach (ProjectInSolution project in this.projectsInOrder)
            {
                // Solution folders don't have configurations
                if (project.ProjectType != SolutionProjectType.SolutionFolder)
                {
                    foreach (ConfigurationInSolution solutionConfiguration in this.solutionConfigurations)
                    {
                        // The "ActiveCfg" entry defines the active project configuration in the given solution configuration
                        // This entry must be present for every possible solution configuration/project combination.
                        string entryNameActiveConfig = string.Format(CultureInfo.InvariantCulture, "{0}.{1}.ActiveCfg", 
                            project.ProjectGuid, solutionConfiguration.FullName);

                        // The "Build.0" entry tells us whether to build the project configuration in the given solution configuration.
                        // Technically, it specifies a configuration name of its own which seems to be a remnant of an initial, 
                        // more flexible design of solution configurations (as well as the '.0' suffix - no higher values are ever used). 
                        // The configuration name is not used, and the whole entry means "build the project configuration" 
                        // if it's present in the solution file, and "don't build" if it's not.
                        string entryNameBuild = string.Format(CultureInfo.InvariantCulture, "{0}.{1}.Build.0", 
                            project.ProjectGuid, solutionConfiguration.FullName);

                        if (rawProjectConfigurationsEntries.ContainsKey(entryNameActiveConfig))
                        {
                            string[] configurationPlatformParts = ((string)(rawProjectConfigurationsEntries[entryNameActiveConfig])).Split(configPlatformSeparators);

                            // Project configuration may not necessarily contain the platform part. Some project support only the configuration part.
                            ProjectFileErrorUtilities.VerifyThrowInvalidProjectFile(configurationPlatformParts.Length <= 2, "SubCategoryForSolutionParsingErrors",
                                new BuildEventFileInfo(SolutionFile), "SolutionParseInvalidProjectSolutionConfigurationEntry",
                                string.Format(CultureInfo.InvariantCulture, "{0} = {1}", entryNameActiveConfig, rawProjectConfigurationsEntries[entryNameActiveConfig]));

                            ProjectConfigurationInSolution projectConfiguration = new ProjectConfigurationInSolution(
                                configurationPlatformParts[0],
                                (configurationPlatformParts.Length > 1) ? configurationPlatformParts[1] : string.Empty,
                                rawProjectConfigurationsEntries.ContainsKey(entryNameBuild)
                            );

                            project.ProjectConfigurations[solutionConfiguration.FullName] = projectConfiguration;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Gets the default configuration name for this solution. Usually it's Debug, unless it's not present
        /// in which case it's the first configuration name we find.
        /// </summary>
        /// <returns></returns>
        /// <owner>LukaszG</owner>
        internal string GetDefaultConfigurationName()
        {
            // Have we done this already? Return the cached name
            if (defaultConfigurationName != null)
            {
                return defaultConfigurationName;
            }

            defaultConfigurationName = string.Empty;

            // Pick the Debug configuration as default if present
            foreach (ConfigurationInSolution solutionConfiguration in this.SolutionConfigurations)
            {
                if (string.Equals(solutionConfiguration.ConfigurationName, "Debug", StringComparison.OrdinalIgnoreCase))
                {
                    defaultConfigurationName = solutionConfiguration.ConfigurationName;
                    break;
                }
            }

            // Failing that, just pick the first configuration name as default
            if ((defaultConfigurationName.Length == 0) && (this.SolutionConfigurations.Count > 0))
            {
                defaultConfigurationName = this.SolutionConfigurations[0].ConfigurationName;
            }

            return defaultConfigurationName;
        }

        /// <summary>
        /// Gets the default platform name for this solution. Usually it's Mixed Platforms, unless it's not present
        /// in which case it's the first platform name we find.
        /// </summary>
        /// <returns></returns>
        /// <owner>LukaszG</owner>
        internal string GetDefaultPlatformName()
        {
            // Have we done this already? Return the cached name
            if (defaultPlatformName != null)
            {
                return defaultPlatformName;
            }

            defaultPlatformName = string.Empty;

            // Pick the Mixed Platforms platform as default if present
            foreach (ConfigurationInSolution solutionConfiguration in this.SolutionConfigurations)
            {
                if (string.Equals(solutionConfiguration.PlatformName, "Mixed Platforms", StringComparison.OrdinalIgnoreCase))
                {
                    defaultPlatformName = solutionConfiguration.PlatformName;
                    break;
                }
            }

            // Failing that, just pick the first platform name as default
            if ((defaultPlatformName.Length == 0) && (this.SolutionConfigurations.Count > 0))
            {
                defaultPlatformName = this.SolutionConfigurations[0].PlatformName;
            }

            return defaultPlatformName;
        }

        /// <summary>
        /// This method takes a string representing one of the project's unique names (guid), and
        /// returns the corresponding "friendly" name for this project.
        /// </summary>
        /// <param name="projectGuid"></param>
        /// <returns></returns>
        /// <owner>RGoel</owner>
        internal string GetProjectUniqueNameByGuid(string projectGuid)
        {
            ProjectInSolution proj = (ProjectInSolution) projects[projectGuid];
            return proj?.GetUniqueProjectName();
        }

        /// <summary>
        /// This method takes a string representing one of the project's unique names (guid), and
        /// returns the corresponding relative path to this project.
        /// </summary>
        /// <param name="projectGuid"></param>
        /// <returns></returns>
        /// <owner>RGoel</owner>
        internal string GetProjectRelativePathByGuid(string projectGuid)
        {
            ProjectInSolution proj = (ProjectInSolution) projects[projectGuid];
            return proj?.RelativePath;
        }

        #endregion
    } // class SolutionParser
} // namespace Microsoft.Build.BuildEngine
