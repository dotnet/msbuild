// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Security;
using System.Text;
using System.Xml;

namespace Microsoft.Build.BuildEngine.Shared
{
    /// <remarks>
    /// An enumeration defining the different types of projects we might find in an SLN.
    /// </remarks>
    internal enum SolutionProjectType
    {
        Unknown,            // Everything else besides the below well-known project types.
        ManagedProject,     // C#, VB, and VJ# projects
        VCProject,          // VC projects, managed and unmanaged
        SolutionFolder,     // Not really a project, but persisted as such in the .SLN file.
        WebProject,         // Venus projects
        EtpSubProject       // Project inside an Enterprise Template project
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
    internal sealed class ProjectInSolution
    {
        #region Constants

        /// <summary>
        /// Characters that need to be cleansed from a project name.
        /// </summary>
        private static readonly char[] charsToCleanse = { '%', '$', '@', ';', '.', '(', ')', '\'' };

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

        private SolutionProjectType projectType;      // For example, ManagedProject, VCProject, WebProject, etc.
        private string projectName;          // For example, "WindowsApplication1"
        private string relativePath;         // Relative from .SLN file.  For example, "WindowsApplication1\WindowsApplication1.csproj"
        private string projectGuid;          // The unique Guid assigned to this project or SLN folder.
        private ArrayList dependencies;     // A list of strings representing the Guids of the dependent projects.
        private ArrayList projectReferences; // A list of strings representing the guids of referenced projects.
                                             // This is only used for VC/Venus projects
        private string parentProjectGuid;    // If this project (or SLN folder) is within a SLN folder, this is the Guid of the parent SLN folder.
        private string uniqueProjectName;    // For example, "MySlnFolder\MySubSlnFolder\WindowsApplication1"
        private Hashtable aspNetConfigurations;    // Key is configuration name, value is [struct] AspNetCompilerParameters
        private SolutionParser parentSolution; // The parent solution for this project
        private int dependencyLevel;         // the dependency level of this project. 0 means no dependencies on other projects.
        private bool isStaticLibrary;        // for VCProjects, is this project a static library?
        private bool childReferencesGathered; // Have we gathered the complete set of references for this project?

        /// <summary>
        /// The project configuration in given solution configuration
        /// K: full solution configuration name (cfg + platform)
        /// V: project configuration 
        /// </summary>
        private Dictionary<string, ProjectConfigurationInSolution> projectConfigurations;

        #endregion

        #region Constructors

        internal ProjectInSolution(SolutionParser solution)
        {
            projectType = SolutionProjectType.Unknown;
            projectName = null;
            relativePath = null;
            projectGuid = null;
            dependencies = new ArrayList();
            projectReferences = new ArrayList();
            parentProjectGuid = null;
            uniqueProjectName = null;
            parentSolution = solution;
            dependencyLevel = ProjectInSolution.DependencyLevelUnknown;
            isStaticLibrary = false;
            childReferencesGathered = false;

            // This hashtable stores a AspNetCompilerParameters struct for each configuration name supported.
            aspNetConfigurations = new Hashtable(StringComparer.OrdinalIgnoreCase);

            projectConfigurations = new Dictionary<string, ProjectConfigurationInSolution>(StringComparer.OrdinalIgnoreCase);
        }

        #endregion

        #region Properties

        internal SolutionProjectType ProjectType
        {
            get { return projectType; }
            set { projectType = value; }
        }

        internal string ProjectName
        {
            get { return projectName; }
            set { projectName = value; }
        }

        internal string RelativePath
        {
            get { return relativePath; }
            set { relativePath = value; }
        }

        /// <summary>
        /// Returns the absolute path for this project
        /// </summary>
        /// <returns></returns>
        /// <owner>LukaszG</owner>
        internal string AbsolutePath
        {
            get
            {
                return Path.Combine(this.ParentSolution.SolutionFileDirectory, this.RelativePath);
            }
        }

        internal string ProjectGuid
        {
            get { return projectGuid; }
            set { projectGuid = value; }
        }

        internal ArrayList Dependencies
        {
            get { return dependencies; }
        }

        internal ArrayList ProjectReferences
        {
            get { return projectReferences; }
        }

        internal string ParentProjectGuid
        {
            get { return parentProjectGuid; }
            set { parentProjectGuid = value; }
        }

        internal SolutionParser ParentSolution
        {
            get { return parentSolution; }
            set { parentSolution = value; }
        }

        internal Hashtable AspNetConfigurations
        {
            get { return aspNetConfigurations; }
            set { aspNetConfigurations = value; }
        }

        internal Dictionary<string, ProjectConfigurationInSolution> ProjectConfigurations
        {
            get { return this.projectConfigurations; }
        }

        internal int DependencyLevel
        {
            get { return this.dependencyLevel; }
            set { this.dependencyLevel = value; }
        }

        internal bool IsStaticLibrary
        {
            get { return this.isStaticLibrary; }
            set { this.isStaticLibrary = value; }
        }

        internal bool ChildReferencesGathered
        {
            get { return this.childReferencesGathered; }
            set { this.childReferencesGathered = value; }
        }

        #endregion

        #region Methods

        private bool checkedIfCanBeMSBuildProjectFile = false;
        private bool canBeMSBuildProjectFile;
        private string canBeMSBuildProjectFileErrorMessage;

        /// <summary>
        /// Looks at the project file node and determines (roughly) if the project file is in the MSBuild format.
        /// The results are cached in case this method is called multiple times.
        /// </summary>
        /// <param name="errorMessage">Detailed error message in case we encounter critical problems reading the file</param>
        /// <returns></returns>
        internal bool CanBeMSBuildProjectFile(out string errorMessage)
        {
            if (checkedIfCanBeMSBuildProjectFile)
            {
                errorMessage = canBeMSBuildProjectFileErrorMessage;
                return canBeMSBuildProjectFile;
            }

            checkedIfCanBeMSBuildProjectFile = true;
            canBeMSBuildProjectFile = false;
            errorMessage = null;

            try
            {
                // Load the project file and get the first node
                XmlDocument projectDocument = new XmlDocument();
                projectDocument.Load(this.AbsolutePath);

                XmlElement mainProjectElement = null;

                // The XML parser will guarantee that we only have one real root element,
                // but we need to find it amongst the other types of XmlNode at the root.
                foreach (XmlNode childNode in projectDocument.ChildNodes)
                {
                    if (XmlUtilities.IsXmlRootElement(childNode))
                    {
                        mainProjectElement = (XmlElement)childNode;
                        break;
                    }
                }

                if (mainProjectElement?.LocalName == "Project")
                {
                    if (String.Equals(mainProjectElement.NamespaceURI, XMakeAttributes.defaultXmlNamespace, StringComparison.OrdinalIgnoreCase))
                    {
                        canBeMSBuildProjectFile = true;
                        return canBeMSBuildProjectFile;
                    }
                }
            }
            // catch all sorts of exceptions - if we encounter any problems here, we just assume the project file is not
            // in the MSBuild format

            // handle errors in path resolution
            catch (SecurityException e)
            {
                canBeMSBuildProjectFileErrorMessage = e.Message;
            }
            // handle errors in path resolution
            catch (NotSupportedException e)
            {
                canBeMSBuildProjectFileErrorMessage = e.Message;
            }
            // handle errors in loading project file
            catch (IOException e)
            {
                canBeMSBuildProjectFileErrorMessage = e.Message;
            }
            // handle errors in loading project file
            catch (UnauthorizedAccessException e)
            {
                canBeMSBuildProjectFileErrorMessage = e.Message;
            }
            // handle XML parsing errors (when reading project file)
            // this is not critical, since the project file doesn't have to be in XML formal
            catch (XmlException)
            {
            }

            errorMessage = canBeMSBuildProjectFileErrorMessage;

            return canBeMSBuildProjectFile;
        }

        /// <summary>
        /// Find the unique name for this project, e.g. SolutionFolder\SubSolutionFolder\ProjectName
        /// </summary>
        /// <owner>RGoel</owner>
        internal string GetUniqueProjectName()
        {
            if (this.uniqueProjectName == null)
            {
                // EtpSubProject and Venus projects have names that are already unique.  No need to prepend the SLN folder.
                if ((this.ProjectType == SolutionProjectType.WebProject) || (this.ProjectType == SolutionProjectType.EtpSubProject))
                {
                    this.uniqueProjectName = CleanseProjectName(this.ProjectName);
                }
                else
                {
                    // This is "normal" project, which in this context means anything non-Venus and non-EtpSubProject.

                    // If this project has a parent SLN folder, first get the full unique name for the SLN folder,
                    // and tack on trailing backslash.
                    string uniqueName = String.Empty;

                    if (this.ParentProjectGuid != null)
                    {
                        ProjectInSolution proj = (ProjectInSolution)this.ParentSolution.ProjectsByGuid[this.ParentProjectGuid];

                        ProjectFileErrorUtilities.VerifyThrowInvalidProjectFile(proj != null, "SubCategoryForSolutionParsingErrors",
                            new BuildEventFileInfo(parentSolution.SolutionFile), "SolutionParseNestedProjectError");

                        uniqueName = proj.GetUniqueProjectName() + "\\";
                    }

                    // Now tack on our own project name, and cache it in the ProjectInSolution object for future quick access.
                    this.uniqueProjectName = CleanseProjectName(uniqueName + this.ProjectName);
                }
            }

            return this.uniqueProjectName;
        }

        /// <summary>
        /// Cleanse the project name, by replacing characters like '@', '$' with '_'
        /// </summary>
        /// <param name="projectName">The name to be cleansed</param>
        /// <returns>string</returns>
        /// <owner>KieranMo</owner>
        static private string CleanseProjectName(string projectName)
        {
            ErrorUtilities.VerifyThrow(projectName != null, "Null strings not allowed.");

            // If there are no special chars, just return the original string immediately.
            // Don't even instantiate the StringBuilder.
            int indexOfChar = projectName.IndexOfAny(charsToCleanse);
            if (indexOfChar == -1)
            {
                return projectName;
            }

            // This is where we're going to work on the final string to return to the caller.
            StringBuilder cleanProjectName = new StringBuilder(projectName);

            // Replace each unclean character with a clean one            
            foreach (char uncleanChar in charsToCleanse)
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
        /// <owner>KieranMo</owner>
        static internal string DisambiguateProjectTargetName(string uniqueProjectName)
        {
            // Test our unique project name against those names that collide with Solution
            // entry point targets
            foreach (string projectName in projectNamesToDisambiguate)
            {
                if (String.Equals(uniqueProjectName, projectName, StringComparison.OrdinalIgnoreCase))
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
