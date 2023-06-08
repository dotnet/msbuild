// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Runtime.InteropServices;
using System.Security;
using System.Text;
using System.Text.Json;
using System.Xml;
using Microsoft.Build.Exceptions;
using Microsoft.Build.Shared;
using Microsoft.Build.Shared.FileSystem;
using Microsoft.Build.Utilities;

using BuildEventFileInfo = Microsoft.Build.Shared.BuildEventFileInfo;
using ErrorUtilities = Microsoft.Build.Shared.ErrorUtilities;
using ExceptionUtilities = Microsoft.Build.Shared.ExceptionHandling;
using ProjectFileErrorUtilities = Microsoft.Build.Shared.ProjectFileErrorUtilities;
using ResourceUtilities = Microsoft.Build.Shared.ResourceUtilities;
using VisualStudioConstants = Microsoft.Build.Shared.VisualStudioConstants;

// Suppress some style rules to reduce the amount of noise in this file. These can be removed in future when the issues are fixed.
#pragma warning disable SA1201 // Elements should appear in the correct order
#pragma warning disable SA1202 // Elements should be ordered by access
#pragma warning disable SA1204 // Static elements should appear before instance elements
#pragma warning disable SA1303 // Const field names should begin with upper-case letter
#pragma warning disable SA1308 // Variable names should not be prefixed
#pragma warning disable SA1311 // Static readonly fields should begin with upper-case letter
#pragma warning disable SA1611 // Element parameters should be documented
#pragma warning disable SA1623 // Property summary documentation should match accessors
#pragma warning disable IDE0022 // Use expression body for method
#pragma warning disable IDE0046 // Convert to conditional expression
#pragma warning disable IDE0058 // Expression value is never used
#pragma warning disable IDE0130 // Namespace does not match folder structure
#pragma warning disable IDE1006 // Naming Styles

namespace Microsoft.Build.Construction
{
    /// <remarks>
    /// This class contains the functionality to parse a solution file and return a corresponding
    /// MSBuild project file containing the projects and dependencies defined in the solution.
    /// </remarks>
    public sealed class SolutionFile
    {
        #region Solution specific constants

        internal const int slnFileMinUpgradableVersion = 7; // Minimum version for MSBuild to give a nice message
        internal const int slnFileMinVersion = 9; // Minimum version for MSBuild to actually do anything useful
        internal const int slnFileMaxVersion = VisualStudioConstants.CurrentVisualStudioSolutionFileVersion;

        private const string vbProjectGuid = "{F184B08F-C81C-45F6-A57F-5ABD9991F28F}";
        private const string csProjectGuid = "{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}";
        private const string cpsProjectGuid = "{13B669BE-BB05-4DDF-9536-439F39A36129}";
        private const string cpsCsProjectGuid = "{9A19103F-16F7-4668-BE54-9A1E7A4F7556}";
        private const string cpsVbProjectGuid = "{778DAE3C-4631-46EA-AA77-85C1314464D9}";
        private const string cpsFsProjectGuid = "{6EC3EE1D-3C4E-46DD-8F32-0CC8E7565705}";
        private const string vjProjectGuid = "{E6FDF86B-F3D1-11D4-8576-0002A516ECE8}";
        private const string vcProjectGuid = "{8BC9CEB8-8B4A-11D0-8D11-00A0C91BC942}";
        private const string fsProjectGuid = "{F2A71F9B-5D33-465A-A702-920D77279786}";
        private const string dbProjectGuid = "{C8D11400-126E-41CD-887F-60BD40844F9E}";
        private const string wdProjectGuid = "{2CFEAB61-6A3B-4EB8-B523-560B4BEEF521}";
        private const string synProjectGuid = "{BBD0F5D1-1CC4-42FD-BA4C-A96779C64378}";
        private const string webProjectGuid = "{E24C65DC-7377-472B-9ABA-BC803B73C61A}";
        private const string solutionFolderGuid = "{2150E333-8FDC-42A3-9474-1A3956D46DE8}";
        private const string sharedProjectGuid = "{D954291E-2A0B-460D-934E-DC6B0785DB48}";

        private const char CommentStartChar = '#';

        #endregion

        #region Member data

        private string? _solutionFile;                // Could be absolute or relative path to the .SLN file.
        private string? _solutionFilterFile;          // Could be absolute or relative path to the .SLNF file.
        private HashSet<string>? _solutionFilter;     // The project files to include in loading the solution.
        private bool _parsingForConversionOnly;      // Are we parsing this solution to get project reference data during
                                                     // conversion, or in preparation for actually building the solution?

        // The list of projects in this SLN, keyed by the project GUID.
        private Dictionary<string, ProjectInSolution>? _projects;

        // The list of projects in the SLN, in order of their appearance in the SLN.
        private List<ProjectInSolution>? _projectsInOrder;

        // The list of solution configurations in the solution
        private List<SolutionConfigurationInSolution>? _solutionConfigurations;

        // cached default configuration name for GetDefaultConfigurationName
        private string? _defaultConfigurationName;

        // cached default platform name for GetDefaultPlatformName
        private string? _defaultPlatformName;

        // VisualStudioVersion specified in Dev12+ solutions
        private Version? _currentVisualStudioVersion;
        private int _currentLineNumber;

        /// <summary>
        /// Cache the return value of this method, as each invocation returns a new instance.
        /// The method has to defend against code changing the values within the array, but we don't.
        /// By caching it we avoid a per-call allocation.
        /// </summary>
        private static readonly char[] s_invalidPathChars = Path.GetInvalidPathChars();

        // TODO: Unify to NativeMethodsShared.OSUsesCaseSensitive paths when possible.
        private static readonly StringComparer s_pathComparer = RuntimeInformation.IsOSPlatform(OSPlatform.Linux)
            ? StringComparer.Ordinal
            : StringComparer.OrdinalIgnoreCase;

        #endregion

        #region Constructors

        /// <summary>
        /// Constructor
        /// </summary>
        internal SolutionFile()
        {
        }

        #endregion

        #region Properties

        /// <summary>
        /// This property returns the list of warnings that were generated during solution parsing
        /// </summary>
        internal List<string> SolutionParserWarnings { get; } = new List<string>();

        /// <summary>
        /// This property returns the list of comments that were generated during the solution parsing
        /// </summary>
        internal List<string> SolutionParserComments { get; } = new List<string>();

        /// <summary>
        /// This property returns the list of error codes for warnings/errors that were generated during solution parsing. 
        /// </summary>
        internal List<string> SolutionParserErrorCodes { get; } = new List<string>();

        /// <summary>
        /// Returns the actual major version of the parsed solution file
        /// </summary>
        internal int Version { get; private set; }

        /// <summary>
        /// Returns Visual Studio major version
        /// </summary>
        internal int VisualStudioVersion => _currentVisualStudioVersion?.Major ?? Version - 1;

        /// <summary>
        /// Returns true if the solution contains any web projects
        /// </summary>
        internal bool ContainsWebProjects { get; private set; }

        /// <summary>
        /// Returns true if the solution contains any .wdproj projects.  Used to determine
        /// whether we need to load up any projects to examine dependencies. 
        /// </summary>
        internal bool ContainsWebDeploymentProjects { get; private set; }

        /// <summary>
        /// All projects in this solution, in the order they appeared in the solution file
        /// </summary>
        public IReadOnlyList<ProjectInSolution> ProjectsInOrder => _projectsInOrder?.AsReadOnly() ?? (IReadOnlyList<ProjectInSolution>)Array.Empty<ProjectInSolution>();

        /// <summary>
        /// The collection of projects in this solution, accessible by their guids as a 
        /// string in "{XXXXXXXX-XXXX-XXXX-XXXX-XXXXXXXXXXXX}" form
        /// </summary>
        public IReadOnlyDictionary<string, ProjectInSolution> ProjectsByGuid => new ReadOnlyDictionary<string, ProjectInSolution>(_projects!);

        /// <summary>
        /// This is the read/write accessor for the solution file which we will parse.  This
        /// must be set before calling any other methods on this class.
        /// </summary>
        [DisallowNull]
        internal string? FullPath
        {
            get => _solutionFile;

            set
            {
                // Should already be canonicalized to a full path
                ErrorUtilities.VerifyThrowInternalRooted(value);
                // To reduce code duplication, this should be
                //   if (FileUtilities.IsSolutionFilterFilename(value))
                // But that's in Microsoft.Build.Framework and this codepath
                // is called from old versions of NuGet that can't resolve
                // Framework (see https://github.com/dotnet/msbuild/issues/5313).
                if (value.EndsWith(".slnf", StringComparison.OrdinalIgnoreCase))
                {
                    ParseSolutionFilter(value);
                }
                else
                {
                    _solutionFile = value;
                    _solutionFilter = null;

                    SolutionFileDirectory = Path.GetDirectoryName(_solutionFile);
                }
            }
        }

        // Setter only used by the unit tests
        internal string? SolutionFileDirectory { get; set; }

        // Setter only used by the unit tests
        internal StreamLineSpanReader? SolutionReader { get; set; }

        /// <summary>
        /// The list of all full solution configurations (configuration + platform) in this solution
        /// </summary>
        public IReadOnlyList<SolutionConfigurationInSolution> SolutionConfigurations => _solutionConfigurations?.AsReadOnly() ?? (IReadOnlyList<SolutionConfigurationInSolution>)Array.Empty<SolutionConfigurationInSolution>();

        #endregion

        #region Methods

        internal bool ProjectShouldBuild(string projectFile)
        {
            return _solutionFilter?.Contains(FileUtilities.FixFilePath(projectFile)) != false;
        }

        /// <summary>
        /// This method takes a path to a solution file, parses the projects and project dependencies
        /// in the solution file, and creates internal data structures representing the projects within
        /// the SLN.
        /// </summary>
        public static SolutionFile Parse(string solutionFile)
        {
            var parser = new SolutionFile { FullPath = solutionFile };
            parser.ParseSolutionFile();
            return parser;
        }

        /// <summary>
        /// Returns "true" if it's a project that's expected to be buildable, or false if it's 
        /// not (e.g. a solution folder) 
        /// </summary>
        /// <param name="project">The project in the solution</param>
        /// <returns>Whether the project is expected to be buildable</returns>
        internal static bool IsBuildableProject(ProjectInSolution project)
        {
            return project.ProjectType != SolutionProjectType.SolutionFolder && project.ProjectConfigurations.Count > 0;
        }

        /// <summary>
        /// Parses the header of the specified solution file, returning some version number data.
        /// </summary>
        /// <param name="solutionFile">The full path of the solution file.</param>
        /// <param name="solutionVersion">The returned solution file format version (major version only).</param>
        /// <param name="visualStudioMajorVersion">The returned Visual Studio version (major version only).</param>
        /// <exception cref="InvalidProjectFileException">The solution header is invalid, or the version is less than our minimum required version.</exception>
        internal static void GetSolutionFileAndVisualStudioMajorVersions(string solutionFile, out int solutionVersion, out int visualStudioMajorVersion)
        {
            ErrorUtilities.VerifyThrow(!string.IsNullOrEmpty(solutionFile), "null solution file passed to GetSolutionFileMajorVersion!");
            ErrorUtilities.VerifyThrowInternalRooted(solutionFile);

            // Open the file
            using FileStream fileStream = new(
                solutionFile,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 256,
                FileOptions.SequentialScan);

            using StreamReader reader = new(
                fileStream,
                Encoding.GetEncoding(0), // HIGHCHAR: If solution files have no byte-order marks, then assume ANSI rather than ASCII.
                detectEncodingFromByteOrderMarks: true,
                bufferSize: 256, // The default buffer size is much larger than we need. We will only read a few lines from the file.
                leaveOpen: true); // We explicitly close the stream so don't need the reader to do it.

            GetSolutionFileAndVisualStudioMajorVersions(reader, solutionFile, out solutionVersion, out visualStudioMajorVersion);
        }

        /// <summary>
        /// Parses the header of the specified solution file, returning some version number data.
        /// </summary>
        /// <param name="reader">To read the content of the solution file.</param>
        /// <param name="solutionFile">The full path of the solution file.</param>
        /// <param name="solutionVersion">The returned solution file format version (major version only).</param>
        /// <param name="visualStudioMajorVersion">The returned Visual Studio version (major version only).</param>
        /// <exception cref="InvalidProjectFileException">The solution header is invalid, or the version is less than our minimum required version.</exception>
        internal static void GetSolutionFileAndVisualStudioMajorVersions(TextReader reader, string solutionFile, out int solutionVersion, out int visualStudioMajorVersion)
        {
            ErrorUtilities.VerifyThrow(!string.IsNullOrEmpty(solutionFile), "null solution file passed to GetSolutionFileMajorVersion!");
            ErrorUtilities.VerifyThrowInternalRooted(solutionFile);

            const string slnFileHeaderNoVersion = "Microsoft Visual Studio Solution File, Format Version ";
            const string slnFileVSVLinePrefix = "VisualStudioVersion";

            bool validVersionFound = false;

            solutionVersion = 0;
            visualStudioMajorVersion = 0;

            // Read first 4 lines of the solution file. 
            // The header is expected to be in line 1 or 2
            // VisualStudioVersion is expected to be in line 3 or 4.
            for (int i = 0; i < 4; i++)
            {
                string? line = reader.ReadLine();

                if (line == null)
                {
                    break;
                }

                ReadOnlySpan<char> lineSpan = line.AsSpan().Trim();

                if (lineSpan.StartsWith(slnFileHeaderNoVersion.AsSpan(), StringComparison.Ordinal))
                {
                    // Found it. Validate the version.
                    string fileVersionFromHeader = lineSpan.Slice(slnFileHeaderNoVersion.Length).ToString();

                    if (!System.Version.TryParse(fileVersionFromHeader, out Version? version))
                    {
                        ProjectFileErrorUtilities.ThrowInvalidProjectFile(
                            "SubCategoryForSolutionParsingErrors",
                            new BuildEventFileInfo(solutionFile),
                            "SolutionParseVersionMismatchError",
                            slnFileMinUpgradableVersion,
                            slnFileMaxVersion);
                    }

                    solutionVersion = version.Major;

                    // Validate against our min & max
                    if (solutionVersion < slnFileMinUpgradableVersion)
                    {
                        ProjectFileErrorUtilities.ThrowInvalidProjectFile(
                            "SubCategoryForSolutionParsingErrors",
                            new BuildEventFileInfo(solutionFile),
                            "SolutionParseVersionMismatchError",
                            slnFileMinUpgradableVersion,
                            slnFileMaxVersion);
                    }

                    validVersionFound = true;
                }
                else if (lineSpan.StartsWith(slnFileVSVLinePrefix.AsSpan(), StringComparison.Ordinal))
                {
                    if (ParseVisualStudioVersion(line.AsSpan()) is { Major: int major })
                    {
                        visualStudioMajorVersion = major;
                    }
                }
            }

            if (validVersionFound)
            {
                return;
            }

            // Didn't find the header in lines 1-4, so the solution file is invalid.
            ProjectFileErrorUtilities.ThrowInvalidProjectFile(
                "SubCategoryForSolutionParsingErrors",
                new BuildEventFileInfo(solutionFile),
                "SolutionParseNoHeaderError");
        }

        private void ParseSolutionFilter(string solutionFilterFile)
        {
            _solutionFilterFile = solutionFilterFile;
            try
            {
                _solutionFile = ParseSolutionFromSolutionFilter(solutionFilterFile, out JsonElement solution);
                if (!FileSystems.Default.FileExists(_solutionFile))
                {
                    ProjectFileErrorUtilities.ThrowInvalidProjectFile(
                        "SubCategoryForSolutionParsingErrors",
                        new BuildEventFileInfo(_solutionFile),
                        "SolutionFilterMissingSolutionError",
                        solutionFilterFile,
                        _solutionFile);
                }

                SolutionFileDirectory = Path.GetDirectoryName(_solutionFile);

                _solutionFilter = new HashSet<string>(s_pathComparer);
                foreach (JsonElement project in solution.GetProperty("projects").EnumerateArray())
                {
                    _solutionFilter.Add(FileUtilities.FixFilePath(project.GetString()));
                }
            }
            catch (Exception e) when (e is JsonException or KeyNotFoundException or InvalidOperationException)
            {
                ProjectFileErrorUtilities.VerifyThrowInvalidProjectFile(
                    false, /* Just throw the exception */
                    "SubCategoryForSolutionParsingErrors",
                    new BuildEventFileInfo(solutionFilterFile),
                    e,
                    "SolutionFilterJsonParsingError",
                    solutionFilterFile);
            }
        }

        internal static string ParseSolutionFromSolutionFilter(string solutionFilterFile, out JsonElement solution)
        {
            try
            {
                // This is to align MSBuild with what VS permits in loading solution filter files. These are not in them by default but can be added manually.
                JsonDocumentOptions options = new JsonDocumentOptions() { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip };
                JsonDocument text = JsonDocument.Parse(File.ReadAllText(solutionFilterFile), options);
                solution = text.RootElement.GetProperty("solution");
                return FileUtilities.GetFullPath(solution.GetProperty("path").GetString(), Path.GetDirectoryName(solutionFilterFile));
            }
            catch (Exception e) when (e is JsonException or KeyNotFoundException or InvalidOperationException)
            {
                ProjectFileErrorUtilities.VerifyThrowInvalidProjectFile(
                    false, /* Just throw the exception */
                    "SubCategoryForSolutionParsingErrors",
                    new BuildEventFileInfo(solutionFilterFile),
                    e,
                    "SolutionFilterJsonParsingError",
                    solutionFilterFile);
            }
            solution = new JsonElement();
            return string.Empty;
        }

        /// <summary>
        /// Adds a configuration to this solution
        /// </summary>
        internal void AddSolutionConfiguration(string configurationName, string platformName)
        {
            _solutionConfigurations!.Add(new SolutionConfigurationInSolution(configurationName, platformName));
        }

        private bool TryReadLine(out ReadOnlySpan<char> span)
        {
            if (SolutionReader!.TryReadLine(out span))
            {
                span = span.Trim();
                return true;
            }

            return false;
        }

        /// <summary>
        /// This method takes a path to a solution file, parses the projects and project dependencies
        /// in the solution file, and creates internal data structures representing the projects within
        /// the SLN.  Used for conversion, which means it allows situations that we refuse to actually build.
        /// </summary>
        internal void ParseSolutionFileForConversion()
        {
            _parsingForConversionOnly = true;
            ParseSolutionFile();
        }

        /// <summary>
        /// This method takes a path to a solution file, parses the projects and project dependencies
        /// in the solution file, and creates internal data structures representing the projects within
        /// the SLN.
        /// </summary>
        internal void ParseSolutionFile()
        {
            ErrorUtilities.VerifyThrow(!string.IsNullOrEmpty(_solutionFile), "ParseSolutionFile() got a null solution file!");
            ErrorUtilities.VerifyThrowInternalRooted(_solutionFile!);

            SolutionReader = null;

            try
            {
                // Open the file.
                using FileStream fileStream = File.OpenRead(_solutionFile!);

                SolutionReader = new StreamLineSpanReader(
                    fileStream,
                    Encoding.GetEncoding(0), // HIGHCHAR: If solution files have no byte-order marks, then assume ANSI rather than ASCII.
                    byteBufferSize: 1024,
                    charBufferSize: 1024);

                ParseSolution();
            }
            catch (Exception e) when (ExceptionUtilities.IsIoRelatedException(e))
            {
                ProjectFileErrorUtilities.ThrowInvalidProjectFile(new BuildEventFileInfo(_solutionFile), "InvalidProjectFile", e.Message);
            }
        }

        /// <summary>
        /// Parses the SLN file represented by the StreamReader in this.reader, and populates internal
        /// data structures based on the SLN file contents.
        /// </summary>
        internal void ParseSolution()
        {
            _projects = new Dictionary<string, ProjectInSolution>(StringComparer.OrdinalIgnoreCase);
            _projectsInOrder = new List<ProjectInSolution>();
            ContainsWebProjects = false;
            Version = 0;
            _currentLineNumber = 0;
            _solutionConfigurations = new List<SolutionConfigurationInSolution>();
            _defaultConfigurationName = null;
            _defaultPlatformName = null;

            // Pool strings as we parse them, de-duplicating them in memory.
            StringPool pool = new();

            // the raw list of project configurations in solution configurations, to be processed after it's fully read in.
            Dictionary<ProjectConfigurationKey, string>? rawProjectConfigurationsEntries = null;

            ParseFileHeader();

            while (TryReadLine(out ReadOnlySpan<char> line))
            {
                if (line.StartsWith("Project(".AsSpan(), StringComparison.Ordinal))
                {
                    ParseProject(line, pool);
                }
                else if (line.StartsWith("GlobalSection(NestedProjects)".AsSpan(), StringComparison.Ordinal))
                {
                    ParseNestedProjects(pool);
                }
                else if (line.StartsWith("GlobalSection(SolutionConfigurationPlatforms)".AsSpan(), StringComparison.Ordinal))
                {
                    ParseSolutionConfigurations(pool);
                }
                else if (line.StartsWith("GlobalSection(ProjectConfigurationPlatforms)".AsSpan(), StringComparison.Ordinal))
                {
                    rawProjectConfigurationsEntries = ParseProjectConfigurations(pool);
                }
                else if (line.StartsWith("VisualStudioVersion".AsSpan(), StringComparison.Ordinal))
                {
                    _currentVisualStudioVersion = ParseVisualStudioVersion(line);
                }
                else
                {
                    // No other section types to process at this point, so just ignore the line
                    // and continue.
                }
            }

            if (_solutionFilter != null)
            {
                HashSet<string> projectPaths = new(_projectsInOrder.Count, s_pathComparer);

                foreach (ProjectInSolution project in _projectsInOrder)
                {
                    projectPaths.Add(FileUtilities.FixFilePath(project.RelativePath));
                }

                foreach (string project in _solutionFilter)
                {
                    if (!projectPaths.Contains(project))
                    {
                        ProjectFileErrorUtilities.ThrowInvalidProjectFile(
                            "SubCategoryForSolutionParsingErrors",
                            new BuildEventFileInfo(FileUtilities.GetFullPath(project, Path.GetDirectoryName(_solutionFile))),
                            "SolutionFilterFilterContainsProjectNotInSolution",
                            _solutionFilterFile,
                            project,
                            _solutionFile);
                    }
                }
            }

            if (rawProjectConfigurationsEntries != null)
            {
                ProcessProjectConfigurationSection(rawProjectConfigurationsEntries, pool);
            }

            // Cache the unique name of each project, and check that we don't have any duplicates.
            var projectsByUniqueName = new Dictionary<string, ProjectInSolution>(StringComparer.OrdinalIgnoreCase);
            var projectsByOriginalName = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (ProjectInSolution proj in _projectsInOrder)
            {
                // Find the unique name for the project.  This method also caches the unique name,
                // so it doesn't have to be recomputed later.
                string uniqueName = proj.GetUniqueProjectName();

                if (proj.ProjectType == SolutionProjectType.WebProject)
                {
                    // Examine port information and determine if we need to disambiguate similarly-named projects with different ports.
                    if (Uri.TryCreate(proj.RelativePath, UriKind.Absolute, out Uri? uri))
                    {
                        if (!uri.IsDefaultPort)
                        {
                            // If there are no other projects with the same name as this one, then we will keep this project's unique name, otherwise
                            // we will create a new unique name with the port added.
                            foreach (ProjectInSolution otherProj in _projectsInOrder)
                            {
                                if (ReferenceEquals(proj, otherProj))
                                {
                                    continue;
                                }

                                if (string.Equals(otherProj.ProjectName, proj.ProjectName, StringComparison.OrdinalIgnoreCase))
                                {
                                    uniqueName = $"{uniqueName}:{uri.Port}";
                                    proj.UpdateUniqueProjectName(uniqueName);
                                    break;
                                }
                            }
                        }
                    }
                }

                // Detect collision caused by unique name's normalization
                if (projectsByUniqueName.TryGetValue(uniqueName, out ProjectInSolution? project))
                {
                    // Did normalization occur in the current project?
                    if (uniqueName != proj.ProjectName)
                    {
                        // Generates a new unique name
                        string tempUniqueName = $"{uniqueName}_{proj.GetProjectGuidWithoutCurlyBrackets()}";
                        proj.UpdateUniqueProjectName(tempUniqueName);
                        uniqueName = tempUniqueName;
                    }
                    // Did normalization occur in a previous project?
                    else if (uniqueName != project.ProjectName)
                    {
                        // Generates a new unique name
                        string tempUniqueName = $"{uniqueName}_{project.GetProjectGuidWithoutCurlyBrackets()}";
                        project.UpdateUniqueProjectName(tempUniqueName);

                        projectsByUniqueName.Remove(uniqueName);
                        projectsByUniqueName.Add(tempUniqueName, project);
                    }
                }

                bool uniqueNameExists = projectsByUniqueName.ContainsKey(uniqueName);

                // Add the unique name (if it does not exist) to the dictionary 
                if (!uniqueNameExists)
                {
                    projectsByUniqueName.Add(uniqueName, proj);
                }

                bool didntAlreadyExist = !uniqueNameExists && projectsByOriginalName.Add(proj.GetOriginalProjectName());

                if (!didntAlreadyExist)
                {
                    ProjectFileErrorUtilities.ThrowInvalidProjectFile(
                        "SubCategoryForSolutionParsingErrors",
                        new BuildEventFileInfo(FullPath),
                        "SolutionParseDuplicateProject",
                        uniqueNameExists ? uniqueName : proj.ProjectName);
                }
            }
        } // ParseSolutionFile()

        /// <summary>
        /// This method searches the first two lines of the solution file opened by the specified
        /// StreamReader for the solution file header.  An exception is thrown if it is not found.
        /// 
        /// The solution file header has form <c>Microsoft Visual Studio Solution File, Format Version 9.00</c>.
        /// </summary>
        private void ParseFileHeader()
        {
            ErrorUtilities.VerifyThrow(SolutionReader != null, "ParseFileHeader(): reader is null!");

            const string slnFileHeaderNoVersion = "Microsoft Visual Studio Solution File, Format Version ";

            // Read the file header.  This can be on either of the first two lines.
            for (int i = 1; i <= 2; i++)
            {
                if (!TryReadLine(out ReadOnlySpan<char> line))
                {
                    // EOF
                    break;
                }

                if (line.StartsWith(slnFileHeaderNoVersion.AsSpan(), StringComparison.Ordinal))
                {
                    // Found it. Validate the version.
                    ValidateSolutionFileVersion(line.Slice(slnFileHeaderNoVersion.Length));
                    return;
                }
            }

            // Didn't find the header on either the first or second line, so the solution file
            // is invalid.
            ProjectFileErrorUtilities.ThrowInvalidProjectFile(
                "SubCategoryForSolutionParsingErrors",
                new BuildEventFileInfo(FullPath),
                "SolutionParseNoHeaderError");
        }

        /// <summary>
        /// Parses a <c>VisualStudioVersion</c> line. The caller must ensure to pass such a line.
        /// </summary>
        /// <remarks>
        /// These lines have form <c>VisualStudioVersion = 12.0.20311.0 VSPRO_PLATFORM</c>. This method
        /// attempts to parse the numeric version, ignoring any textual suffix. If parsing fails,
        /// returns <see langword="null"/>.
        /// </remarks>
        private static Version? ParseVisualStudioVersion(ReadOnlySpan<char> line)
        {
            if (TryParseNameValue(line, allowEmpty: false, allowEqualsInValue: false, out _, out ReadOnlySpan<char> value))
            {
                int spaceIndex = value.IndexOf(' ');

                if (spaceIndex != -1)
                {
                    // Exclude any textual suffix.
                    value = value.Slice(0, spaceIndex);
                }

                if (System.Version.TryParse(value.ToString(), out Version? version))
                {
                    return version;
                }
            }

            return null;
        }

        /// <summary>
        /// Parses the solution file version, assigns it to <see cref="Version"/>, and validates the version
        /// is within the supported range.
        /// </summary>
        /// <remarks>
        /// Throws if the <paramref name="versionString"/> cannot be parsed, or if the version is too low.
        /// If the version is too high, adds a comment to <see cref="SolutionParserComments"/>.
        /// </remarks>
        private void ValidateSolutionFileVersion(ReadOnlySpan<char> versionString)
        {
            if (!System.Version.TryParse(versionString.ToString(), out Version? version))
            {
                ProjectFileErrorUtilities.ThrowInvalidProjectFile(
                    "SubCategoryForSolutionParsingErrors",
                    new BuildEventFileInfo(FullPath, _currentLineNumber, 0),
                    "SolutionParseVersionMismatchError",
                    slnFileMinUpgradableVersion,
                    slnFileMaxVersion);
            }

            Version = version.Major;

            // Validate against our min & max
            if (Version < slnFileMinUpgradableVersion)
            {
                ProjectFileErrorUtilities.ThrowInvalidProjectFile(
                    "SubCategoryForSolutionParsingErrors",
                    new BuildEventFileInfo(FullPath, _currentLineNumber, 0),
                    "SolutionParseVersionMismatchError",
                    slnFileMinUpgradableVersion,
                    slnFileMaxVersion);
            }

            // If the solution file version is greater than the maximum one we will create a comment rather than warn
            // as users such as blend opening a dev10 project cannot do anything about it.
            if (Version > slnFileMaxVersion)
            {
                SolutionParserComments.Add(ResourceUtilities.FormatResourceStringStripCodeAndKeyword("UnrecognizedSolutionComment", Version));
            }
        }

        /// <summary>
        /// This method processes a "Project" section in the solution file opened by the specified
        /// StreamReader, and returns a populated ProjectInSolution instance, if successful.
        /// An exception is thrown if the solution file is invalid.
        ///
        /// The format of the parts of a Project section that we care about is as follows:
        /// <code>
        ///  Project("{Project type GUID}") = "Project name", "Relative path to project file", "{Project GUID}"
        ///      ProjectSection(ProjectDependencies) = postProject
        ///          {Parent project unique name} = {Parent project unique name}
        ///          ...
        ///      EndProjectSection
        ///  EndProject
        /// </code>
        /// </summary>
        private void ParseProject(ReadOnlySpan<char> firstLine, StringPool pool)
        {
            ErrorUtilities.VerifyThrow(!firstLine.IsEmpty, "ParseProject() got an empty firstLine!");
            ErrorUtilities.VerifyThrow(SolutionReader != null, "ParseProject() got a null reader!");

            ProjectInSolution proj = new(this);

            // Extract the important information from the first line.
            ParseFirstProjectLine(firstLine, proj, pool);

            // Search for project dependencies. Keeping reading lines until we either:
            // 1. reach the end of the file,
            // 2. see "ProjectSection(ProjectDependencies)" at the beginning of the line, or
            // 3. see "EndProject" at the beginning of the line.
            while (TryReadLine(out ReadOnlySpan<char> line))
            {
                // If we see an "EndProject", well ... that's the end of this project!
                if (line.Equals("EndProject".AsSpan(), StringComparison.Ordinal))
                {
                    break;
                }
                else if (line.StartsWith("ProjectSection(ProjectDependencies)".AsSpan(), StringComparison.Ordinal))
                {
                    // We have a ProjectDependencies section.  Each subsequent line should identify
                    // a dependency.
                    line = ReadRequiredLine();
                    while (!line.StartsWith("EndProjectSection".AsSpan(), StringComparison.Ordinal))
                    {
                        // This should be a dependency.  The GUID identifying the parent project should
                        // be both the property name and the property value.
                        if (!TryParseNameValue(line, allowEmpty: true, allowEqualsInValue: true, out ReadOnlySpan<char> propertyName, out _))
                        {
                            ProjectFileErrorUtilities.ThrowInvalidProjectFile(
                                "SubCategoryForSolutionParsingErrors",
                                new BuildEventFileInfo(FullPath, _currentLineNumber, 0),
                                "SolutionParseProjectDepGuidError",
                                proj.ProjectName);
                        }

                        proj.AddDependency(referencedProjectGuid: pool.Intern(propertyName));

                        line = ReadRequiredLine();
                    }
                }
                else if (line.StartsWith("ProjectSection(WebsiteProperties)".AsSpan(), StringComparison.Ordinal))
                {
                    // We have a WebsiteProperties section.  This section is present only in Venus
                    // projects, and contains properties that we'll need in order to call the
                    // AspNetCompiler task.
                    line = ReadRequiredLine();
                    while (!line.StartsWith("EndProjectSection".AsSpan(), StringComparison.Ordinal))
                    {
                        if (!TryParseNameValue(line, allowEmpty: true, allowEqualsInValue: true, out ReadOnlySpan<char> propertyName, out ReadOnlySpan<char> propertyValue))
                        {
                            ProjectFileErrorUtilities.ThrowInvalidProjectFile(
                                "SubCategoryForSolutionParsingErrors",
                                new BuildEventFileInfo(FullPath, _currentLineNumber, 0),
                                "SolutionParseWebProjectPropertiesError",
                                proj.ProjectName);
                        }

                        // TODO: Convert ParseAspNetCompilerProperty to work with ReadOnlySpan<char>
                        ParseAspNetCompilerProperty(proj, propertyName.ToString(), propertyValue.ToString());

                        line = ReadRequiredLine();
                    }
                }
                else if (line.StartsWith("Project(".AsSpan(), StringComparison.Ordinal))
                {
                    // Another Project spotted instead of EndProject for the current one - solution file is malformed.
                    string warning = ResourceUtilities.FormatResourceStringStripCodeAndKeyword(out _, out _, "Shared.InvalidProjectFile",
                        _solutionFile, proj.ProjectName);
                    SolutionParserWarnings.Add(warning);

                    // The line with new project is already read and we can't go one line back - we have no choice but to recursively parse spotted project.
                    ParseProject(firstLine: line, pool);

                    // We're not waiting for the EndProject for malformed project, so we carry on.
                    break;
                }
            }

            // Add the project to the collection.
            AddProjectToSolution(proj);

            // If the project is an etp project then parse the etp project file to get the projects contained in it.
            if (IsEtpProjectFile(proj.RelativePath))
            {
                ParseEtpProject(proj);
            }

            ReadOnlySpan<char> ReadRequiredLine()
            {
                if (!TryReadLine(out ReadOnlySpan<char> line))
                {
                    ProjectFileErrorUtilities.ThrowInvalidProjectFile(
                        "SubCategoryForSolutionParsingErrors",
                        new BuildEventFileInfo(FullPath),
                        "SolutionParseProjectEofError",
                        proj.ProjectName);
                }

                return line;
            }
        } // ParseProject()

        /// <summary>
        /// This method will parse a .etp project recursively and 
        /// add all the projects found to projects and projectsInOrder
        /// </summary>
        /// <param name="etpProj">ETP Project</param>
        internal void ParseEtpProject(ProjectInSolution etpProj)
        {
            var etpProjectDocument = new XmlDocument();
            // Get the full path to the .etp project file
            string fullPathToEtpProj = Path.Combine(SolutionFileDirectory!, etpProj.RelativePath);
            string? etpProjectRelativeDir = Path.GetDirectoryName(etpProj.RelativePath);
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
                var readerSettings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Ignore };

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
                XmlNodeList? referenceNodes = etpProjectDocument.DocumentElement!.SelectNodes("/EFPROJECT/GENERAL/References/Reference");
                // Do the right thing for each <Reference> element
                foreach (XmlNode referenceNode in referenceNodes!)
                {
                    // Get the relative path to the project file
                    string fileElementValue = referenceNode.SelectSingleNode("FILE")!.InnerText;
                    // If <FILE>  element is not present under <Reference> then we don't do anything.
                    if (fileElementValue != null)
                    {
                        // Create and populate a ProjectInSolution for the project
                        var proj = new ProjectInSolution(this)
                        {
                            RelativePath = Path.Combine(etpProjectRelativeDir!, fileElementValue)
                        };

                        // Verify the relative path specified in the .etp proj file
                        ValidateProjectRelativePath(proj);
                        proj.ProjectType = SolutionProjectType.EtpSubProject;
                        proj.ProjectName = proj.RelativePath;
                        XmlNode? projGuidNode = referenceNode.SelectSingleNode("GUIDPROJECTID");

                        // It is ok for a project to not have a guid inside an etp project.
                        // If a solution file contains a project without a guid it fails to 
                        // load in Everett. But if an etp project contains a project without 
                        // a guid it loads well in Everett and p2p references to/from this project
                        // are preserved. So we should make sure that we don’t error in this 
                        // situation while upgrading.
                        proj.ProjectGuid = projGuidNode?.InnerText ?? string.Empty;

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
                string warning = ResourceUtilities.FormatResourceStringStripCodeAndKeyword(out string errorCode, out _, "Shared.ProjectFileCouldNotBeLoaded",
                    etpProj.RelativePath, e.Message);
                SolutionParserWarnings.Add(warning);
                SolutionParserErrorCodes.Add(errorCode);
            }
            // handle errors in path resolution
            catch (NotSupportedException e)
            {
                // Log a warning 
                string warning = ResourceUtilities.FormatResourceStringStripCodeAndKeyword(out string errorCode, out _, "Shared.ProjectFileCouldNotBeLoaded",
                    etpProj.RelativePath, e.Message);
                SolutionParserWarnings.Add(warning);
                SolutionParserErrorCodes.Add(errorCode);
            }
            // handle errors in loading project file
            catch (IOException e)
            {
                // Log a warning 
                string warning = ResourceUtilities.FormatResourceStringStripCodeAndKeyword(out string errorCode, out _, "Shared.ProjectFileCouldNotBeLoaded",
                    etpProj.RelativePath, e.Message);
                SolutionParserWarnings.Add(warning);
                SolutionParserErrorCodes.Add(errorCode);
            }
            // handle errors in loading project file
            catch (UnauthorizedAccessException e)
            {
                // Log a warning 
                string warning = ResourceUtilities.FormatResourceStringStripCodeAndKeyword(out string errorCode, out _, "Shared.ProjectFileCouldNotBeLoaded",
                    etpProj.RelativePath, e.Message);
                SolutionParserWarnings.Add(warning);
                SolutionParserErrorCodes.Add(errorCode);
            }
            // handle XML parsing errors 
            catch (XmlException e)
            {
                // Log a warning 
                string warning = ResourceUtilities.FormatResourceStringStripCodeAndKeyword(out string errorCode, out _, "Shared.InvalidProjectFile",
                   etpProj.RelativePath, e.Message);
                SolutionParserWarnings.Add(warning);
                SolutionParserErrorCodes.Add(errorCode);
            }
        }

        /// <summary>
        /// Adds a given project to the project collections of this class
        /// </summary>
        /// <param name="proj">proj</param>
        private void AddProjectToSolution(ProjectInSolution proj)
        {
            if (!string.IsNullOrEmpty(proj.ProjectGuid))
            {
                _projects![proj.ProjectGuid] = proj;
            }
            _projectsInOrder!.Add(proj);
        }

        /// <summary>
        /// Checks whether a given project has a .etp extension.
        /// </summary>
        /// <param name="projectFile"></param>
        private static bool IsEtpProjectFile(string projectFile)
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
            if (proj.RelativePath!.IndexOfAny(s_invalidPathChars) != -1)
            {
                ProjectFileErrorUtilities.ThrowInvalidProjectFile(
                    "SubCategoryForSolutionParsingErrors",
                    new BuildEventFileInfo(FullPath, _currentLineNumber, 0),
                    "SolutionParseInvalidProjectFileNameCharacters",
                    proj.ProjectName,
                    proj.RelativePath);
            }

            // Verify the relative path is not empty string
            if (proj.RelativePath.Length == 0)
            {
                ProjectFileErrorUtilities.ThrowInvalidProjectFile(
                    "SubCategoryForSolutionParsingErrors",
                    new BuildEventFileInfo(FullPath, _currentLineNumber, 0),
                    "SolutionParseInvalidProjectFileNameEmpty",
                    proj.ProjectName);
            }
        }

        /// <summary>
        /// Takes a property name / value that comes from the SLN file for a Venus project, and
        /// stores it appropriately in our data structures.
        /// </summary>
        private static void ParseAspNetCompilerProperty(
            ProjectInSolution proj,
            string propertyName,
            string propertyValue)
        {
            // What we expect to find in the SLN file is something that looks like this:
            //
            // Project("{E24C65DC-7377-472B-9ABA-BC803B73C61A}") = "c:\...\myfirstwebsite\", "..\..\..\..\..\..\rajeev\temp\websites\myfirstwebsite", "{956CC04E-FD59-49A9-9099-96888CB6F366}"
            //     ProjectSection(WebsiteProperties) = preProject
            //       TargetFrameworkMoniker = ".NETFramework,Version%3Dv4.0"
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
                string aspNetPropertyName = ((propertyName.Length - indexOfFirstDot) > 0) ? propertyName.Substring(indexOfFirstDot + 1, propertyName.Length - indexOfFirstDot - 1) : "";

                // And the part after the <equals> sign is the property value (which was parsed out for us prior
                // to calling this method).
                propertyValue = TrimQuotes(propertyValue);

                // Grab the parameters for this specific configuration if they exist.
                object? aspNetCompilerParametersObject = proj.AspNetConfigurations[configurationName];
                AspNetCompilerParameters aspNetCompilerParameters;

                if (aspNetCompilerParametersObject == null)
                {
                    // If it didn't exist, create a new one.
                    aspNetCompilerParameters = new AspNetCompilerParameters
                    {
                        aspNetVirtualPath = string.Empty,
                        aspNetPhysicalPath = string.Empty,
                        aspNetTargetPath = string.Empty,
                        aspNetForce = string.Empty,
                        aspNetUpdateable = string.Empty,
                        aspNetDebug = string.Empty,
                        aspNetKeyFile = string.Empty,
                        aspNetKeyContainer = string.Empty,
                        aspNetDelaySign = string.Empty,
                        aspNetAPTCA = string.Empty,
                        aspNetFixedNames = String.Empty
                    };
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
                    string[] projectReferenceEntries = propertyValue.Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries);

                    foreach (string projectReferenceEntry in projectReferenceEntries)
                    {
                        int indexOfBar = projectReferenceEntry.IndexOf('|');

                        // indexOfBar could be -1 if we had semicolons in the file names, so skip entries that
                        // don't contain a guid. File names may not contain the '|' character
                        if (indexOfBar != -1)
                        {
                            int indexOfOpeningBrace = projectReferenceEntry.IndexOf('{');
                            if (indexOfOpeningBrace != -1)
                            {
                                int indexOfClosingBrace = projectReferenceEntry.IndexOf('}', indexOfOpeningBrace);
                                if (indexOfClosingBrace != -1)
                                {
                                    string referencedProjectGuid = projectReferenceEntry.Substring(
                                        indexOfOpeningBrace,
                                        indexOfClosingBrace - indexOfOpeningBrace + 1);

                                    proj.AddDependency(referencedProjectGuid);
                                    proj.ProjectReferences.Add(referencedProjectGuid);
                                }
                            }
                        }
                    }
                }
                else if (string.Equals(propertyName, "TargetFrameworkMoniker", StringComparison.OrdinalIgnoreCase))
                {
                    // Website project need to back support 3.5 MSBuild parser for the Blend (it is not move to .Net4.0 yet.)
                    // However, 3.5 version of Solution parser can't handle a equal sign in the value.
                    // The "=" in TargetFrameworkMoniker was escaped to "%3D" for Orcas
                    string targetFrameworkMoniker = TrimQuotes(propertyValue);
                    proj.TargetFrameworkMoniker = EscapingUtilities.UnescapeAll(targetFrameworkMoniker);
                }
            }

            static string TrimQuotes(string property)
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
        }

        internal static bool TryParseFirstProjectLine(
            ReadOnlySpan<char> line,
            StringPool pool,
            [NotNullWhen(returnValue: true)] out string? projectTypeGuid,
            [NotNullWhen(returnValue: true)] out string? projectName,
            [NotNullWhen(returnValue: true)] out string? relativePath,
            [NotNullWhen(returnValue: true)] out string? projectGuid)
        {
            //// Project("{9A19103F-16F7-4668-BE54-9A1E7A4F7556}") = "Microsoft.Build", "src\Build\Microsoft.Build.csproj", "{69BE05E2-CBDA-4D27-9733-44E12B0F5627}"

            if (!TrySkip(ref line, "Project(") ||
                !TryReadQuotedString(ref line, out projectTypeGuid, pool) ||
                !TrySkip(ref line, ")") ||
                !TrySkipDelimiter(ref line, '=') ||
                !TryReadQuotedString(ref line, out projectName) ||
                !TrySkipDelimiter(ref line, ',') ||
                !TryReadQuotedString(ref line, out relativePath) ||
                !TrySkipDelimiter(ref line, ',') ||
                !TryReadQuotedString(ref line, out projectGuid, pool) ||
                !line.IsEmpty)
            {
                projectTypeGuid = null;
                projectName = null;
                relativePath = null;
                projectGuid = null;
                return false;
            }

            return true;

            static bool TryReadQuotedString(
                ref ReadOnlySpan<char> line,
                [NotNullWhen(returnValue: true)] out string? value,
                StringPool? pool = null)
            {
                if (line.Length == 0 ||
                    line[0] != '"')
                {
                    value = null;
                    return false;
                }

                line = line.Slice(1);

                int quoteIndex = line.IndexOf('"');

                if (quoteIndex == -1)
                {
                    value = null;
                    return false;
                }

                ReadOnlySpan<char> valueSpan = line.Slice(0, quoteIndex).Trim();

                value = pool?.Intern(valueSpan) ?? valueSpan.ToString();
                line = line.Slice(quoteIndex + 1);
                return true;
            }

            static bool TrySkip(ref ReadOnlySpan<char> line, string value)
            {
                if (!line.StartsWith(value.AsSpan()))
                {
                    return false;
                }

                line = line.Slice(value.Length);
                return true;
            }

            static bool TrySkipDelimiter(ref ReadOnlySpan<char> line, char delimiter)
            {
                line = line.TrimStart();

                if (line.Length == 0 || line[0] != delimiter)
                {
                    return false;
                }

                line = line.Slice(1).TrimStart();
                return true;
            }
        }

        /// <summary>
        /// Parse the first line of a <c>Project</c> section of a solution file.
        /// </summary>
        /// <remarks>
        /// This line should look like:
        /// <code>
        /// Project("{Project type GUID}") = "Project name", "Relative path to project file", "{Project GUID}"
        /// Project("{9A19103F-16F7-4668-BE54-9A1E7A4F7556}") = "Microsoft.Build", "src\Build\Microsoft.Build.csproj", "{69BE05E2-CBDA-4D27-9733-44E12B0F5627}"
        /// </code>
        /// </remarks>
        internal void ParseFirstProjectLine(ReadOnlySpan<char> firstLine, ProjectInSolution proj, StringPool pool)
        {
            if (!TryParseFirstProjectLine(firstLine, pool, out string? projectTypeGuid, out string? projectName, out string? relativePath, out string? projectGuid))
            {
                ProjectFileErrorUtilities.ThrowInvalidProjectFile(
                    "SubCategoryForSolutionParsingErrors",
                    new BuildEventFileInfo(FullPath, _currentLineNumber, 0),
                    "SolutionParseProjectError");
            }

            proj.ProjectName = projectName;
            proj.RelativePath = relativePath;
            proj.ProjectGuid = projectGuid;

            // If the project name is empty (as in some bad solutions) set it to some generated generic value.
            // This allows us to at least generate reasonable target names etc. instead of crashing.
            if (string.IsNullOrEmpty(proj.ProjectName))
            {
                proj.ProjectName = "EmptyProjectName." + Guid.NewGuid();
            }

            // Validate project relative path
            ValidateProjectRelativePath(proj);

            // Figure out what type of project this is.
            if (string.Equals(projectTypeGuid, vbProjectGuid, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(projectTypeGuid, csProjectGuid, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(projectTypeGuid, cpsProjectGuid, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(projectTypeGuid, cpsCsProjectGuid, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(projectTypeGuid, cpsVbProjectGuid, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(projectTypeGuid, cpsFsProjectGuid, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(projectTypeGuid, fsProjectGuid, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(projectTypeGuid, dbProjectGuid, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(projectTypeGuid, vjProjectGuid, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(projectTypeGuid, synProjectGuid, StringComparison.OrdinalIgnoreCase))
            {
                proj.ProjectType = SolutionProjectType.KnownToBeMSBuildFormat;
            }
            else if (string.Equals(projectTypeGuid, sharedProjectGuid, StringComparison.OrdinalIgnoreCase))
            {
                proj.ProjectType = SolutionProjectType.SharedProject;
            }
            else if (string.Equals(projectTypeGuid, solutionFolderGuid, StringComparison.OrdinalIgnoreCase))
            {
                proj.ProjectType = SolutionProjectType.SolutionFolder;
            }
            // MSBuild format VC projects have the same project type guid as old style VC projects.
            // If it's not an old-style VC project, we'll assume it's MSBuild format
            else if (string.Equals(projectTypeGuid, vcProjectGuid, StringComparison.OrdinalIgnoreCase))
            {
                if (string.Equals(proj.Extension, ".vcproj", StringComparison.OrdinalIgnoreCase))
                {
                    if (!_parsingForConversionOnly)
                    {
                        ProjectFileErrorUtilities.ThrowInvalidProjectFile(new BuildEventFileInfo(FullPath), "ProjectUpgradeNeededToVcxProj", proj.RelativePath);
                    }
                    // otherwise, we're parsing this solution file because we want the P2P information during 
                    // conversion, and it's perfectly valid for an unconverted solution file to still contain .vcprojs
                }
                else
                {
                    proj.ProjectType = SolutionProjectType.KnownToBeMSBuildFormat;
                }
            }
            else if (string.Equals(projectTypeGuid, webProjectGuid, StringComparison.OrdinalIgnoreCase))
            {
                proj.ProjectType = SolutionProjectType.WebProject;
                ContainsWebProjects = true;
            }
            else if (string.Equals(projectTypeGuid, wdProjectGuid, StringComparison.OrdinalIgnoreCase))
            {
                proj.ProjectType = SolutionProjectType.WebDeploymentProject;
                ContainsWebDeploymentProjects = true;
            }
            else
            {
                proj.ProjectType = SolutionProjectType.Unknown;
            }
        }

        /// <summary>
        /// Read nested projects section.
        /// This is required to find a unique name for each project's target.
        /// </summary>
        internal void ParseNestedProjects(StringPool pool)
        {
            while (TryReadLine(out ReadOnlySpan<char> line))
            {
                if (line.Equals("EndGlobalSection".AsSpan(), StringComparison.Ordinal))
                {
                    break;
                }

                if (line.Length == 0 || line[0] == CommentStartChar)
                {
                    // Skip blank and comment lines.
                    continue;
                }

                if (!TryParseNameValue(line, allowEmpty: true, allowEqualsInValue: true, out ReadOnlySpan<char> propertyName, out ReadOnlySpan<char> propertyValue))
                {
                    ProjectFileErrorUtilities.ThrowInvalidProjectFile(
                        "SubCategoryForSolutionParsingErrors",
                        new BuildEventFileInfo(FullPath, _currentLineNumber, 0),
                        "SolutionParseNestedProjectError");
                }

                string projectGuid = pool.Intern(propertyName);
                string parentProjectGuid = pool.Intern(propertyValue);

                if (!_projects!.TryGetValue(projectGuid, out ProjectInSolution? proj))
                {
                    if (proj is null)
                    {
                        ProjectFileErrorUtilities.ThrowInvalidProjectFile(
                            "SubCategoryForSolutionParsingErrors",
                            new BuildEventFileInfo(FullPath, _currentLineNumber, 0),
                            "SolutionParseNestedProjectUndefinedError",
                            projectGuid,
                            parentProjectGuid);
                    }
                }

                proj.ParentProjectGuid = parentProjectGuid;
            }
        }

        /// <summary>
        /// Read solution configuration section.
        /// </summary>
        /// <remarks>
        /// A sample section:
        /// <code>
        /// GlobalSection(SolutionConfigurationPlatforms) = preSolution
        ///     Debug|Any CPU = Debug|Any CPU
        ///     Release|Any CPU = Release|Any CPU
        /// EndGlobalSection
        /// </code>
        /// </remarks>
        internal void ParseSolutionConfigurations(StringPool pool)
        {
            while (TryReadLine(out ReadOnlySpan<char> line))
            {
                if (line.Equals("EndGlobalSection".AsSpan(), StringComparison.Ordinal))
                {
                    break;
                }

                if (line.Length == 0 || line[0] == CommentStartChar)
                {
                    // Skip blank and comment lines.
                    continue;
                }

                // Parse the name/value pair.
                if (!TryParseNameValue(line, allowEmpty: true, allowEqualsInValue: false, out ReadOnlySpan<char> name, out ReadOnlySpan<char> value))
                {
                    ProjectFileErrorUtilities.ThrowInvalidProjectFile(
                        "SubCategoryForSolutionParsingErrors",
                        new BuildEventFileInfo(FullPath, _currentLineNumber, 0),
                        "SolutionParseInvalidSolutionConfigurationEntry",
                        line.ToString());
                }

                // Fixing bug 555577: Solution file can have description information, in which case we ignore.
                if (name.Equals("DESCRIPTION".AsSpan(), StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                // The name must equal the value. i.e. "Debug|Any CPU" == "Debug|Any CPU".
                if (!name.Equals(value, StringComparison.Ordinal))
                {
                    ProjectFileErrorUtilities.ThrowInvalidProjectFile(
                        "SubCategoryForSolutionParsingErrors",
                        new BuildEventFileInfo(FullPath, _currentLineNumber, 0),
                        "SolutionParseInvalidSolutionConfigurationEntry",
                        line.ToString());
                }

                (string configuration, string platform) = ParseConfigurationName(name, FullPath, _currentLineNumber, line, pool);

                _solutionConfigurations!.Add(new SolutionConfigurationInSolution(configuration, platform));
            }
        }

        internal static (string Configuration, string Platform) ParseConfigurationName(ReadOnlySpan<char> fullConfigurationName, string? projectPath, int lineNumber, ReadOnlySpan<char> containingString, StringPool pool)
        {
            if (!TryParseConfigurationPlatform(fullConfigurationName, isPlatformRequired: true, out ReadOnlySpan<char> configuration, out ReadOnlySpan<char> platform))
            {
                ProjectFileErrorUtilities.ThrowInvalidProjectFile(
                    "SubCategoryForSolutionParsingErrors",
                    new BuildEventFileInfo(projectPath, lineNumber, 0),
                    "SolutionParseInvalidSolutionConfigurationEntry",
                    containingString.ToString());
            }

            return (pool.Intern(configuration), pool.Intern(platform));
        }

        internal readonly struct ProjectConfigurationKey : IEquatable<ProjectConfigurationKey>
        {
            public string ProjectGuid { get; }

            public string Suffix { get; }

            public ProjectConfigurationKey(string projectGuid, string suffix)
            {
                ProjectGuid = projectGuid;
                Suffix = suffix;
            }

            public override int GetHashCode()
            {
                return StringComparer.OrdinalIgnoreCase.GetHashCode(ProjectGuid)
                     ^ StringComparer.Ordinal.GetHashCode(Suffix);
            }

            public bool Equals(ProjectConfigurationKey other)
            {
                return
                    string.Equals(ProjectGuid, other.ProjectGuid, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(Suffix, other.Suffix, StringComparison.Ordinal);
            }

            public override bool Equals(object? obj) => obj is ProjectConfigurationKey key && Equals(key);

            public override string ToString() => $"{ProjectGuid}.{Suffix}";
        }

        /// <summary>
        /// Read project configurations in solution configurations section.
        /// </summary>
        /// <remarks>
        /// A sample (incomplete) section:
        /// <code>
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
        /// </code>
        /// </remarks>
        /// <returns>An unprocessed dictionary of entries in this section.</returns>
        internal Dictionary<ProjectConfigurationKey, string> ParseProjectConfigurations(StringPool pool)
        {
            Dictionary<ProjectConfigurationKey, string> rawProjectConfigurationsEntries = new();

            while (TryReadLine(out ReadOnlySpan<char> line))
            {
                if (line.Equals("EndGlobalSection".AsSpan(), StringComparison.Ordinal))
                {
                    break;
                }

                if (line.Length == 0 || line[0] == CommentStartChar)
                {
                    // Skip blank and comment lines.
                    continue;
                }

                // {69BE05E2-CBDA-4D27-9733-44E12B0F5627}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
                // ^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^   ^^^^^^^^^^^^^
                //                           NAME                                       VALUE
                // ^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^ ^^^^^^^^^^^^^^^^^^^^^^^
                //                  GUID                           SUFFIX

                if (!TryParseNameValue(line, allowEmpty: true, allowEqualsInValue: false, out ReadOnlySpan<char> name, out ReadOnlySpan<char> value))
                {
                    ProjectFileErrorUtilities.ThrowInvalidProjectFile(
                        "SubCategoryForSolutionParsingErrors",
                        new BuildEventFileInfo(FullPath, _currentLineNumber, 0),
                        "SolutionParseInvalidProjectSolutionConfigurationEntry",
                        line.ToString());
                }

                int periodIndex = name.IndexOf('.');

                if (periodIndex != -1)
                {
                    ReadOnlySpan<char> guid = name.Slice(0, periodIndex);
                    ReadOnlySpan<char> suffix = name.Slice(periodIndex + 1);
                    ProjectConfigurationKey key = new(pool.Intern(guid), pool.Intern(suffix));

                    rawProjectConfigurationsEntries[key] = pool.Intern(value);
                }
            }

            return rawProjectConfigurationsEntries;
        }

        /// <summary>
        /// Read the project configuration information for every project in the solution, using pre-cached
        /// solution section data.
        /// </summary>
        /// <param name="rawProjectConfigurationsEntries">Cached data from the project configuration section</param>
        /// <param name="pool">Allows efficient deduplication of repeated strings.</param>
        internal void ProcessProjectConfigurationSection(Dictionary<ProjectConfigurationKey, string> rawProjectConfigurationsEntries, StringPool pool)
        {
            // Instead of parsing the data line by line, we parse it project by project, constructing the
            // entry name (e.g. "{A6F99D27-47B9-4EA4-BFC9-25157CBDC281}.Release|Any CPU.ActiveCfg") and retrieving its
            // value from the raw data. The reason for this is that the IDE does it this way, and as the result
            // the '.' character is allowed in configuration names although it technically separates different
            // parts of the entry name string. This could lead to ambiguous results if we tried to parse
            // the entry name instead of constructing it and looking it up. Although it's pretty unlikely that
            // this would ever be a problem, it's safer to do it the same way VS IDE does it.

            Dictionary<string, string> activeConfigSuffixes = new(StringComparer.Ordinal);
            Dictionary<string, string> build0Suffixes = new(StringComparer.Ordinal);

            foreach (ProjectInSolution project in _projectsInOrder!)
            {
                // Solution folders don't have configurations
                if (project.ProjectType != SolutionProjectType.SolutionFolder)
                {
                    foreach (SolutionConfigurationInSolution solutionConfiguration in _solutionConfigurations!)
                    {
                        // The "ActiveCfg" entry defines the active project configuration in the given solution configuration
                        // This entry must be present for every possible solution configuration/project combination.
                        ProjectConfigurationKey activeConfigKey = new(project.ProjectGuid, GetSuffix(activeConfigSuffixes, solutionConfiguration.FullName, ".ActiveCfg"));

                        // The "Build.0" entry tells us whether to build the project configuration in the given solution configuration.
                        // Technically, it specifies a configuration name of its own which seems to be a remnant of an initial,
                        // more flexible design of solution configurations (as well as the '.0' suffix - no higher values are ever used).
                        // The configuration name is not used, and the whole entry means "build the project configuration"
                        // if it's present in the solution file, and "don't build" if it's not.
                        ProjectConfigurationKey buildKey = new(project.ProjectGuid, GetSuffix(build0Suffixes, solutionConfiguration.FullName, ".Build.0"));

                        if (rawProjectConfigurationsEntries.TryGetValue(activeConfigKey, out string? configurationPlatform))
                        {
                            // Project configuration may not necessarily contain the platform part. Some projects support only the configuration part.
                            if (!TryParseConfigurationPlatform(configurationPlatform.AsSpan(), isPlatformRequired: false, out ReadOnlySpan<char> configuration, out ReadOnlySpan<char> platform))
                            {
                                ProjectFileErrorUtilities.ThrowInvalidProjectFile(
                                    "SubCategoryForSolutionParsingErrors",
                                    new BuildEventFileInfo(FullPath),
                                    "SolutionParseInvalidProjectSolutionConfigurationEntry",
                                    $"{activeConfigKey} = {configurationPlatform}");
                            }

                            ProjectConfigurationInSolution projectConfiguration = new(
                                pool.Intern(configuration),
                                pool.Intern(platform),
                                includeInBuild: rawProjectConfigurationsEntries.ContainsKey(buildKey));

                            project.SetProjectConfiguration(solutionConfiguration.FullName, projectConfiguration);
                        }
                    }
                }
            }

            static string GetSuffix(Dictionary<string, string> cache, string head, string tail)
            {
                if (!cache.TryGetValue(head, out string? value))
                {
                    value = cache[head] = head + tail;
                }

                return value;
            }
        }

        /// <summary>
        /// Gets the default configuration name for this solution. Usually it's Debug, unless it's not present
        /// in which case it's the first configuration name we find.
        /// </summary>
        public string GetDefaultConfigurationName()
        {
            // Have we done this already? Return the cached name
            if (_defaultConfigurationName != null)
            {
                return _defaultConfigurationName;
            }

            _defaultConfigurationName = string.Empty;

            // Pick the Debug configuration as default if present
            foreach (SolutionConfigurationInSolution solutionConfiguration in SolutionConfigurations)
            {
                if (string.Equals(solutionConfiguration.ConfigurationName, "Debug", StringComparison.OrdinalIgnoreCase))
                {
                    _defaultConfigurationName = solutionConfiguration.ConfigurationName;
                    break;
                }
            }

            // Failing that, just pick the first configuration name as default
            if ((_defaultConfigurationName.Length == 0) && (SolutionConfigurations.Count > 0))
            {
                _defaultConfigurationName = SolutionConfigurations[0].ConfigurationName;
            }

            return _defaultConfigurationName;
        }

        /// <summary>
        /// Gets the default platform name for this solution. Usually it's Mixed Platforms, unless it's not present
        /// in which case it's the first platform name we find.
        /// </summary>
        public string GetDefaultPlatformName()
        {
            // Have we done this already? Return the cached name
            if (_defaultPlatformName != null)
            {
                return _defaultPlatformName;
            }

            _defaultPlatformName = string.Empty;

            // Pick the Mixed Platforms platform as default if present
            foreach (SolutionConfigurationInSolution solutionConfiguration in SolutionConfigurations)
            {
                if (string.Equals(solutionConfiguration.PlatformName, "Mixed Platforms", StringComparison.OrdinalIgnoreCase))
                {
                    _defaultPlatformName = solutionConfiguration.PlatformName;
                    break;
                }
                // We would like this to be chosen if Mixed platforms does not exist.
                else if (string.Equals(solutionConfiguration.PlatformName, "Any CPU", StringComparison.OrdinalIgnoreCase))
                {
                    _defaultPlatformName = solutionConfiguration.PlatformName;
                }
            }

            // Failing that, just pick the first platform name as default
            if ((_defaultPlatformName.Length == 0) && (SolutionConfigurations.Count > 0))
            {
                _defaultPlatformName = SolutionConfigurations[0].PlatformName;
            }

            return _defaultPlatformName;
        }

        /// <summary>
        /// This method takes a string representing one of the project's unique names (guid), and
        /// returns the corresponding "friendly" name for this project.
        /// </summary>
        internal string? GetProjectUniqueNameByGuid(string projectGuid)
        {
            if (_projects!.TryGetValue(projectGuid, out ProjectInSolution? proj))
            {
                return proj.GetUniqueProjectName();
            }

            return null;
        }

        /// <summary>
        /// This method takes a string representing one of the project's unique names (guid), and
        /// returns the corresponding relative path to this project.
        /// </summary>
        internal string? GetProjectRelativePathByGuid(string projectGuid)
        {
            if (_projects!.TryGetValue(projectGuid, out ProjectInSolution? proj))
            {
                return proj.RelativePath;
            }

            return null;
        }

        internal static bool TryParseNameValue(ReadOnlySpan<char> input, bool allowEmpty, bool allowEqualsInValue, out ReadOnlySpan<char> name, out ReadOnlySpan<char> value)
        {
            int equalsIndex = input.IndexOf('=');

            if (equalsIndex == -1)
            {
                name = default;
                value = default;
                return false;
            }

            name = input.Slice(0, equalsIndex).Trim();
            value = input.Slice(equalsIndex + 1).Trim();

            if ((!allowEqualsInValue && value.IndexOf('=') != -1) ||
                (!allowEmpty && (name.Length == 0 || value.Length == 0)))
            {
                name = default;
                value = default;
                return false;
            }

            return true;
        }

        internal static bool TryParseConfigurationPlatform(ReadOnlySpan<char> input, bool isPlatformRequired, out ReadOnlySpan<char> configuration, out ReadOnlySpan<char> platform)
        {
            // "Debug|AnyCPU"
            int pipeIndex = input.IndexOf('|');

            if (pipeIndex == -1)
            {
                if (isPlatformRequired)
                {
                    configuration = default;
                    platform = default;
                    return false;
                }
                else
                {
                    configuration = input;
                    platform = ReadOnlySpan<char>.Empty;
                    return true;
                }
            }

            configuration = input.Slice(0, pipeIndex);
            platform = input.Slice(pipeIndex + 1);

            if (platform.IndexOf('|') != -1)
            {
                configuration = default;
                platform = default;
                return false;
            }

            return true;
        }

        #endregion
    } // class SolutionParser
} // namespace Microsoft.Build.Construction
