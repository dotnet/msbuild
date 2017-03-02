// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Security;
using System.Text;
using System.Xml;

using XMakeAttributes = Microsoft.Build.Shared.XMakeAttributes;
using ProjectFileErrorUtilities = Microsoft.Build.Shared.ProjectFileErrorUtilities;
using BuildEventFileInfo = Microsoft.Build.Shared.BuildEventFileInfo;
using ErrorUtilities = Microsoft.Build.Shared.ErrorUtilities;
using System.Collections.ObjectModel;

namespace Microsoft.Build.Construction
{
    /// <remarks>
    /// An enumeration defining the different types of projects we might find in an SLN.
    /// </remarks>
    public enum SolutionProjectType
    {
        /// <summary>
        /// Everything else besides the below well-known project types.
        /// </summary>
        Unknown,
        /// <summary>
        /// C#, VB, and VJ# projects
        /// </summary>
        KnownToBeMSBuildFormat,
        /// <summary>
        /// Solution folders appear in the .sln file, but aren't buildable projects.
        /// </summary>
        SolutionFolder,
        /// <summary>
        /// ASP.NET projects
        /// </summary>
        WebProject,
        /// <summary>
        /// Web Deployment (.wdproj) projects
        /// </summary>
        WebDeploymentProject, //  MSBuildFormat, but Whidbey-era ones specify ProjectReferences differently
        /// <summary>
        /// Project inside an Enterprise Template project
        /// </summary>
        EtpSubProject
    }

    internal struct AspNetCompilerParameters
    {
        internal string aspNetVirtualPath;      // For Venus projects only, Virtual path for web
        internal string aspNetPhysicalPath;     // For Venus projects only, Physical path for web
        internal string aspNetTargetPath;       // For Venus projects only, Target for output files
        internal string aspNetForce;            // For Venus projects only, Force overwrite of target
        internal string aspNetUpdateable;       // For Venus projects only, compiled web application is updateable
        internal string aspNetDebug;            // For Venus projects only, generate symbols, etc.
        internal string aspNetKeyFile;          // For Venus projects only, strong name key file.
        internal string aspNetKeyContainer;     // For Venus projects only, strong name key container.
        internal string aspNetDelaySign;        // For Venus projects only, delay sign strong name.
        internal string aspNetAPTCA;            // For Venus projects only, AllowPartiallyTrustedCallers.
        internal string aspNetFixedNames;       // For Venus projects only, generate fixed assembly names.
    }

    /// <remarks>
    /// This class represents a project (or SLN folder) that is read in from a solution file.
    /// </remarks>
    public sealed class ProjectInSolution
    {
        #region Constants

        /// <summary>
        /// Characters that need to be cleansed from a project name.
        /// </summary>
        private static readonly char[] s_charsToCleanse = { '%', '$', '@', ';', '.', '(', ')', '\'' };

        /// <summary>
        /// Project names that need to be disambiguated when forming a target name
        /// </summary>
        internal static readonly string[] projectNamesToDisambiguate = { "Build", "Rebuild", "Clean", "Publish" };

        /// <summary>
        /// Character that will be used to replace 'unclean' ones.
        /// </summary>
        private const char cleanCharacter = '_';

        #endregion

        #region Member data

        private SolutionProjectType _projectType;      // For example, KnownToBeMSBuildFormat, VCProject, WebProject, etc.
        private string _projectName;          // For example, "WindowsApplication1"
        private string _relativePath;         // Relative from .SLN file.  For example, "WindowsApplication1\WindowsApplication1.csproj"
        private string _projectGuid;          // The unique Guid assigned to this project or SLN folder.
        private List<string> _dependencies;     // A list of strings representing the Guids of the dependent projects.
        private ArrayList _projectReferences; // A list of strings representing the guids of referenced projects.
                                              // This is only used for VC/Venus projects
        private string _parentProjectGuid;    // If this project (or SLN folder) is within a SLN folder, this is the Guid of the parent SLN folder.
        private string _uniqueProjectName;    // For example, "MySlnFolder\MySubSlnFolder\WindowsApplication1"
        private Hashtable _aspNetConfigurations;    // Key is configuration name, value is [struct] AspNetCompilerParameters
        private SolutionFile _parentSolution; // The parent solution for this project
        private string _targetFrameworkMoniker; // used for website projects, since they don't have a project file in which the
                                                // target framework is stored.  Defaults to .NETFX 3.5

        /// <summary>
        /// The project configuration in given solution configuration
        /// K: full solution configuration name (cfg + platform)
        /// V: project configuration 
        /// </summary>
        private Dictionary<string, ProjectConfigurationInSolution> _projectConfigurations;

        #endregion

        #region Constructors

        internal ProjectInSolution(SolutionFile solution)
        {
            _projectType = SolutionProjectType.Unknown;
            _projectName = null;
            _relativePath = null;
            _projectGuid = null;
            _dependencies = new List<string>();
            _projectReferences = new ArrayList();
            _parentProjectGuid = null;
            _uniqueProjectName = null;
            _parentSolution = solution;

            // default to .NET Framework 3.5 if this is an old solution that doesn't explicitly say.
            _targetFrameworkMoniker = ".NETFramework,Version=v3.5";

            // This hashtable stores a AspNetCompilerParameters struct for each configuration name supported.
            _aspNetConfigurations = new Hashtable(StringComparer.OrdinalIgnoreCase);

            _projectConfigurations = new Dictionary<string, ProjectConfigurationInSolution>(StringComparer.OrdinalIgnoreCase);
        }

        #endregion

        #region Properties

        /// <summary>
        /// This project's name
        /// </summary>
        public string ProjectName
        {
            get { return _projectName; }
            internal set { _projectName = value; }
        }

        /// <summary>
        /// The path to this project file, relative to the solution location
        /// </summary>
        public string RelativePath
        {
            get { return _relativePath; }
            internal set { _relativePath = value; }
        }

        /// <summary>
        /// Returns the absolute path for this project
        /// </summary>
        public string AbsolutePath
        {
            get
            {
                return Path.Combine(this.ParentSolution.SolutionFileDirectory, this.RelativePath);
            }
        }

        /// <summary>
        /// The unique guid associated with this project, in "{XXXXXXXX-XXXX-XXXX-XXXX-XXXXXXXXXXXX}" form
        /// </summary>
        public string ProjectGuid
        {
            get { return _projectGuid; }
            internal set { _projectGuid = value; }
        }

        /// <summary>
        /// The guid, in "{XXXXXXXX-XXXX-XXXX-XXXX-XXXXXXXXXXXX}" form, of this project's 
        /// parent project, if any. 
        /// </summary>
        public string ParentProjectGuid
        {
            get { return _parentProjectGuid; }
            internal set { _parentProjectGuid = value; }
        }

        /// <summary>
        /// List of guids, in "{XXXXXXXX-XXXX-XXXX-XXXX-XXXXXXXXXXXX}" form, mapping to projects 
        /// that this project has a build order dependency on, as defined in the solution file. 
        /// </summary>
        public IReadOnlyList<string> Dependencies
        {
            get { return _dependencies.AsReadOnly(); }
        }

        /// <summary>
        /// Configurations for this project, keyed off the configuration's full name, e.g. "Debug|x86"
        /// </summary>
        public IReadOnlyDictionary<string, ProjectConfigurationInSolution> ProjectConfigurations
        {
            get { return new ReadOnlyDictionary<string, ProjectConfigurationInSolution>(_projectConfigurations); }
        }

        /// <summary>
        /// Extension of the project file, if any
        /// </summary>
        internal string Extension
        {
            get
            {
                return Path.GetExtension(_relativePath);
            }
        }

        /// <summary>
        /// This project's type.
        /// </summary>
        public SolutionProjectType ProjectType
        {
            get { return _projectType; }
            set { _projectType = value; }
        }

        /// <summary>
        /// Only applies to websites -- for other project types, references are 
        /// either specified as Dependencies above, or as ProjectReferences in the
        /// project file, which the solution doesn't have insight into. 
        /// </summary>
        internal ArrayList ProjectReferences
        {
            get { return _projectReferences; }
        }

        internal SolutionFile ParentSolution
        {
            get { return _parentSolution; }
            set { _parentSolution = value; }
        }

        internal Hashtable AspNetConfigurations
        {
            get { return _aspNetConfigurations; }
            set { _aspNetConfigurations = value; }
        }

        internal string TargetFrameworkMoniker
        {
            get { return _targetFrameworkMoniker; }
            set { _targetFrameworkMoniker = value; }
        }

        #endregion

        #region Methods

        private bool _checkedIfCanBeMSBuildProjectFile = false;
        private bool _canBeMSBuildProjectFile;
        private string _canBeMSBuildProjectFileErrorMessage;

        /// <summary>
        /// Add the guid of a referenced project to our dependencies list.
        /// </summary>
        internal void AddDependency(string referencedProjectGuid)
        {
            _dependencies.Add(referencedProjectGuid);
        }

        /// <summary>
        /// Set the requested project configuration. 
        /// </summary>
        internal void SetProjectConfiguration(string configurationName, ProjectConfigurationInSolution configuration)
        {
            _projectConfigurations[configurationName] = configuration;
        }

        /// <summary>
        /// Looks at the project file node and determines (roughly) if the project file is in the MSBuild format.
        /// The results are cached in case this method is called multiple times.
        /// </summary>
        /// <param name="errorMessage">Detailed error message in case we encounter critical problems reading the file</param>
        /// <returns></returns>
        internal bool CanBeMSBuildProjectFile(out string errorMessage)
        {
            if (_checkedIfCanBeMSBuildProjectFile)
            {
                errorMessage = _canBeMSBuildProjectFileErrorMessage;
                return _canBeMSBuildProjectFile;
            }

            _checkedIfCanBeMSBuildProjectFile = true;
            _canBeMSBuildProjectFile = false;
            errorMessage = null;

            try
            {
                // Read project thru a XmlReader with proper setting to avoid DTD processing
                XmlReaderSettings xrSettings = new XmlReaderSettings();
                xrSettings.DtdProcessing = DtdProcessing.Ignore;

                XmlDocument projectDocument = new XmlDocument();

                using (XmlReader xmlReader = XmlReader.Create(this.AbsolutePath, xrSettings))
                {
                    // Load the project file and get the first node    
                    projectDocument.Load(xmlReader);
                }

                XmlElement mainProjectElement = null;

                // The XML parser will guarantee that we only have one real root element,
                // but we need to find it amongst the other types of XmlNode at the root.
                foreach (XmlNode childNode in projectDocument.ChildNodes)
                {
                    if (childNode.NodeType == XmlNodeType.Element)
                    {
                        mainProjectElement = (XmlElement)childNode;
                        break;
                    }
                }

                if (mainProjectElement != null && mainProjectElement.LocalName == "Project")
                {
                    _canBeMSBuildProjectFile = true;
                    return _canBeMSBuildProjectFile;
                }
            }
            // catch all sorts of exceptions - if we encounter any problems here, we just assume the project file is not
            // in the MSBuild format

            // handle errors in path resolution
            catch (SecurityException e)
            {
                _canBeMSBuildProjectFileErrorMessage = e.Message;
            }
            // handle errors in path resolution
            catch (NotSupportedException e)
            {
                _canBeMSBuildProjectFileErrorMessage = e.Message;
            }
            // handle errors in loading project file
            catch (IOException e)
            {
                _canBeMSBuildProjectFileErrorMessage = e.Message;
            }
            // handle errors in loading project file
            catch (UnauthorizedAccessException e)
            {
                _canBeMSBuildProjectFileErrorMessage = e.Message;
            }
            // handle XML parsing errors (when reading project file)
            // this is not critical, since the project file doesn't have to be in XML formal
            catch (XmlException)
            {
            }

            errorMessage = _canBeMSBuildProjectFileErrorMessage;

            return _canBeMSBuildProjectFile;
        }

        /// <summary>
        /// Find the unique name for this project, e.g. SolutionFolder\SubSolutionFolder\ProjectName
        /// </summary>
        internal string GetUniqueProjectName()
        {
            if (_uniqueProjectName == null)
            {
                // EtpSubProject and Venus projects have names that are already unique.  No need to prepend the SLN folder.
                if ((this.ProjectType == SolutionProjectType.WebProject) || (this.ProjectType == SolutionProjectType.EtpSubProject))
                {
                    _uniqueProjectName = CleanseProjectName(this.ProjectName);
                }
                else
                {
                    // This is "normal" project, which in this context means anything non-Venus and non-EtpSubProject.

                    // If this project has a parent SLN folder, first get the full unique name for the SLN folder,
                    // and tack on trailing backslash.
                    string uniqueName = String.Empty;

                    if (this.ParentProjectGuid != null)
                    {
                        ProjectInSolution proj;
                        if (!this.ParentSolution.ProjectsByGuid.TryGetValue(this.ParentProjectGuid, out proj))
                        {
                            ProjectFileErrorUtilities.VerifyThrowInvalidProjectFile(proj != null, "SubCategoryForSolutionParsingErrors",
                                new BuildEventFileInfo(_parentSolution.FullPath), "SolutionParseNestedProjectError");
                        }

                        uniqueName = proj.GetUniqueProjectName() + "\\";
                    }

                    // Now tack on our own project name, and cache it in the ProjectInSolution object for future quick access.
                    _uniqueProjectName = CleanseProjectName(uniqueName + this.ProjectName);
                }
            }

            return _uniqueProjectName;
        }

        /// <summary>
        /// Changes the unique name of the project.
        /// </summary>
        internal void UpdateUniqueProjectName(string newUniqueName)
        {
            ErrorUtilities.VerifyThrowArgumentLength(newUniqueName, "newUniqueName");

            _uniqueProjectName = newUniqueName;
        }

        /// <summary>
        /// Cleanse the project name, by replacing characters like '@', '$' with '_'
        /// </summary>
        /// <param name="projectName">The name to be cleansed</param>
        /// <returns>string</returns>
        static private string CleanseProjectName(string projectName)
        {
            ErrorUtilities.VerifyThrow(projectName != null, "Null strings not allowed.");

            // If there are no special chars, just return the original string immediately.
            // Don't even instantiate the StringBuilder.
            int indexOfChar = projectName.IndexOfAny(s_charsToCleanse);
            if (indexOfChar == -1)
            {
                return projectName;
            }

            // This is where we're going to work on the final string to return to the caller.
            StringBuilder cleanProjectName = new StringBuilder(projectName);

            // Replace each unclean character with a clean one            
            foreach (char uncleanChar in s_charsToCleanse)
            {
                cleanProjectName.Replace(uncleanChar, cleanCharacter);
            }

            return cleanProjectName.ToString();
        }

        /// <summary>
        /// If the unique project name provided collides with one of the standard Solution project
        /// entry point targets (Build, Rebuild, Clean, Publish), then disambiguate it by prepending the string "Solution:"
        /// </summary>
        /// <param name="uniqueProjectName">The unique name for the project</param>
        /// <returns>string</returns>
        static internal string DisambiguateProjectTargetName(string uniqueProjectName)
        {
            // Test our unique project name against those names that collide with Solution
            // entry point targets
            foreach (string projectName in projectNamesToDisambiguate)
            {
                if (String.Compare(uniqueProjectName, projectName, StringComparison.OrdinalIgnoreCase) == 0)
                {
                    // Prepend "Solution:" so that the collision is resolved, but the
                    // log of the solution project still looks reasonable.
                    return "Solution:" + uniqueProjectName;
                }
            }

            return uniqueProjectName;
        }

        #endregion

        #region Constants

        internal const int DependencyLevelUnknown = -1;
        internal const int DependencyLevelBeingDetermined = -2;

        #endregion
    }
}
