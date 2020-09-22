// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections;
using System.IO;
using System.Globalization;
using System.Reflection;
using System.Resources;
using System.Xml;
using System.Text;
using System.Security;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;
using System.Linq;

using Microsoft.Build.Construction;
using Microsoft.Build.Shared;
using ProjectLoadSettings = Microsoft.Build.BuildEngine.ProjectLoadSettings;
using OldProject = Microsoft.Build.BuildEngine.Project;
using OldEngine = Microsoft.Build.BuildEngine.Engine;

using InvalidProjectFileException = Microsoft.Build.Exceptions.InvalidProjectFileException;
using ProjectXmlUtilities = Microsoft.Build.Internal.ProjectXmlUtilities;

using error = Microsoft.Build.Shared.ErrorUtilities;
using Microsoft.Build.Evaluation;

namespace Microsoft.Build.Conversion
{
    /***************************************************************************
     *
     * An outline of the structure of a VS.NET 2002/2003 project file is shown
     * below:
     *
     * <VisualStudioProject>
     *    <CSHARP, VisualBasic, or VISUALJSHARP attributes>
     *      <Build>
     *          <Settings attributes>
     *              <Config Name="..." attributes>
     *                  <InteropRegistration attributes /> (.USER file only)
     *              </Config>
     *              <Config Name="..." attributes>
     *                  <InteropRegistration attributes /> (.USER file only)
     *              </Config>
     *              ...
     *          </Settings>
     *
     *          <References>
     *              <Reference
     *                  Name="alias"                    (required)
     *                  Private="True/False"
     *                  Guid="guid"                     (COM2 references only)
     *                  VersionMinor="minorversion"     (COM2 references only)
     *                  VersionMajor="majorversion"     (COM2 references only)
     *                  Lcid="lcid"                     (COM2 references only)
     *                  WrapperTool="wrappertool"       (COM2 references only)
     *                  Project="projectguid"           (Project references only)
     *                  Package="packageguid"           (Project references only)
     *                  AssemblyName="assembly"         (.NET references only)
     *                  HintPath="hintpath"             (.NET references only)
     *                  AssemblyFolderKey="asmfolder"   (.NET references only)
     *                  />
     *              <Reference .../>
     *              ...
     *          </References>
     *
     *          <Imports>
     *              <Import Namespace="namespace" />
     *              <Import Namespace="namespace" />
     *              ...
     *          </Imports>
     *      </Build>
     *
     *      <Files>
     *          <Include>
     *              <File
     *                  RelPath="project-relative path"
     *                  Link="path to actual file"      (Linked files only)
     *                  SubType="subtype"
     *                  BuildAction="buildaction"
     *                  DesignTime="true/false"
     *                  AutoGen="true/false"
     *                  Generator="generator"
     *                  CustomToolNamespace="customtoolns"
     *                  LastGenOutputs="lastgenoutputs"
     *                  DependentUpon="dependentfile"
     *                  />
     *              <Folder
     *                  RelPath="relpath"
     *                  WebReferences="true"        (Web reference folders only)
     *                  WebReferenceURL="url"       (Web references only)
     *                  UrlBehavior="urlbehavior"   (Web references only)
     *                  />
     *              ...
     *          </Include>
     *      </Files>
     *
     *      <StartupServices>
     *          <Service ID="id"/>
     *          <Service ID="id"/>
     *          ...
     *      </StartupServices>
     *
     *      <UserProperties attributes>
     *          random goop?
     *      </UserProperties>
     *
     *      <OtherProjectSettings attributes /> (.USER file only)
     *
     *    </CSHARP, /VisualBasic, or /VISUALJSHARP>
     *
     * </VisualStudioProject>
     *
     **************************************************************************/
    /// <summary>
    /// This class performs a project file format conversion from Visual Studio
    /// .NET 2002 or 2003 to MSBuild format (for Whidbey).
    /// </summary>
    /// <owner>rgoel</owner>
    public sealed class ProjectFileConverter
    {
        // The filename of the old VS7/Everett project file.
        private string oldProjectFile = null;

        // The target Whidbey project file for the conversion.
        private string newProjectFile = null;

        // Is the project file we're converting a .USER file?
        private bool isUserFile = false;

        // Is the conversion a minor upgrade operation?
        // Minor upgrade also means the converted project file can be opened in old VS as well, so we won't update the tools version.
        private bool isMinorUpgrade = false;

        // The object representing the destination XMake project.
        private ProjectRootElement xmakeProject = null;

        // This is the XMake object representing the global property group
        // in the destination project file.
        private ProjectPropertyGroupElement globalPropertyGroup = null;

        // The language for the project we're converting -- CSHARP, VisualBasic, VISUALJSHARP
        private string language = null;

        // This is the project instance GUID for the project we're converting
        // (only if it's the main project file -- this doesn't apply for .USER
        // files).
        private string projectGuid = null;

        // This is the fullpath to the solution file that contains this project
        // being converted.  When conversion is done from the IDE in-proc, this
        // information can be given to us unambiguously.  However, in the command-
        // line case, we may have to use a heuristic to search for the containing
        // SLN ourselves.
        private string solutionFile = null;

        // This is the object representing the VS solution named above.
        private SolutionFile solution = null;

        // The PreBuildEvent and PostBuildEvent properties are handled specially.
        private string preBuildEvent = null;
        private string postBuildEvent = null;

        // If we see any web references in the project, we must add some new properties to the
        // Whidbey project file, in order to force the proxy generation code to mimic the
        // Everett behavior.
        private bool newWebReferencePropertiesAdded = false;

        // If this is a VSD ( devices ) project, this is the platform retrieved.  It's needed in two places...
        private string platformForVSD = null;
        private string frameworkVersionForVSD = null;

        // Cache the assembly name (used for converting DocumentationFile property for VB)
        private string assemblyName = null;

        // Cache the output type (used for choosing the correct MyType for VB projects).
        private string outputType = null;

        // Whether or not System.Windows.Forms is present as a reference.
        private bool hasWindowsFormsReference = false;

        private bool isMyTypeAlreadySetInOriginalProject = false;

        // Internal collecction that collects the conversion warnings,
        // to be exposed through the ConversionWarnings property
        private ArrayList conversionWarnings = null;

        // A list of property names whose values we need to escape when converting to Whidbey.
        private Dictionary<string,string> propertiesToEscape = null;

        /// <summary>
        /// Default constructor.  We need a constructor that takes zero parameters,
        /// because this class needs to be instantiated from COM.
        /// </summary>
        /// <owner>rgoel</owner>
        public ProjectFileConverter
            (
            )
        {
            this.oldProjectFile = null;
            this.newProjectFile = null;
            this.isUserFile = false;
            this.solutionFile = null;

            Initialize();
        }

        /// <summary>
        /// The read/write accessor for the old project filename.  This must be
        /// set by the consumer before calling Convert().
        /// </summary>
        /// <owner>rgoel</owner>
        public string OldProjectFile
        {
            get
            {
                return oldProjectFile;
            }
            set
            {
                oldProjectFile = value;
            }
        }

        /// <summary>
        /// The read/write accessor for the new project filename.  This must be
        /// set by the consumer before calling Convert().
        /// </summary>
        /// <owner>rgoel</owner>
        public string NewProjectFile
        {
            get
            {
                return newProjectFile;
            }
            set
            {
                newProjectFile = value;
            }
        }

        /// <summary>
        /// The read/write accessor for the boolean which tells the converter
        /// whether the project file we're converting is a "main" project file
        /// or a .USER file.  Most of the conversion logic is identical for
        /// both types of files, but for example, one difference is that the
        /// "main" project file gets an &lt;Import&gt; tag inserted at the end.
        /// </summary>
        /// <owner>rgoel</owner>
        public bool IsUserFile
        {
            get
            {
                return isUserFile;
            }
            set
            {
                isUserFile = value;
            }
        }

        /// <summary>
        /// The read/write accessor for the solution file which contains this
        /// project being converted.  This is used to look up information about the
        /// project-to-project references.
        /// </summary>
        /// <owner>rgoel</owner>
        public string SolutionFile
        {
            get
            {
                return solutionFile;
            }
            set
            {
                solutionFile = Path.GetFullPath(value);
            }
        }

        /// <summary>
        /// Indicates if the last attempted conversion was skipped because the project is already in the latest format.
        /// This will always return false;
        /// </summary>
        /// <owner>SumedhK</owner>
        /// <value>true, if conversion was skipped</value>
        public bool ConversionSkippedBecauseProjectAlreadyConverted
        {
            get
            {
                return false;
            }
        }

        /// <summary>
        /// This property returns the list of warnings that were generated during the conversion
        /// </summary>
        /// <owner>faisalmo</owner>
        /// <value>true, if conversion was skipped</value>
        [SuppressMessage("Microsoft.Performance", "CA1819:PropertiesShouldNotReturnArrays", Justification = "Public interface that has shipped previously. ")]
        public string[] ConversionWarnings
        {
            get
            {
                return (string[]) conversionWarnings.ToArray(typeof(string));
            }
        }

        /// <summary>
        /// Is the conversion a minor upgrade operation?
        /// Minor upgrade also means the converted project file can be opened in old VS as well, so we won't update the tools version.
        /// </summary>
        public bool IsMinorUpgrade
        {
            get
            {
                return this.isMinorUpgrade;
            }

            set
            {
                this.isMinorUpgrade = value;
            }
        }

        /// <summary>
        /// This is the entry point method, which performs the project file format
        /// conversion.  This method will overwrite "newProjectFile" if it already
        /// exists, so the caller of this method should confirm with the user
        /// that that's what he really wants to do.
        /// </summary>
        /// <owner>rgoel</owner>
        public void Convert()
        {
            DoConvert();
        }

        /// <summary>
        /// This is the entry point method, which performs the project file format
        /// conversion.  This method will overwrite "newProjectFile" if it already
        /// exists, so the caller of this method should confirm with the user
        /// that that's what he really wants to do.
        /// </summary>
        /// <owner>rgoel</owner>
        [Obsolete("Use parameterless overload instead")]
        public void Convert(ProjectLoadSettings projectLoadSettings)
        {
            DoConvert();
        }

        /// <summary>
        /// This is the entry point method, which performs the project file format
        /// conversion.  This method will overwrite "newProjectFile" if it already
        /// exists, so the caller of this method should confirm with the user
        /// that that's what he really wants to do.
        /// </summary>
        /// <owner>rgoel</owner>
        [Obsolete("Use parameterless overload instead.")]
        public void Convert
            (
            string msbuildBinPath
            )
        {
            DoConvert();
        }

        /// <summary>
        /// Helper method to convert given an engine
        /// </summary>
        private void DoConvert()
        {
            // Make sure we were passed in non-empty source and destination project
            // file names.
            error.VerifyThrowArgument(!string.IsNullOrEmpty(this.oldProjectFile),
                "MissingOldProjectFile");
            error.VerifyThrowArgument(!string.IsNullOrEmpty(this.newProjectFile),
                "MissingNewProjectFile");

            ConvertInMemoryToMSBuildProject();

            this.xmakeProject.Save(newProjectFile);
        }

        /// <summary>
        /// Initialize all member variables to get ready for a conversion.
        /// </summary>
        /// <owner>RGoel</owner>
        private void Initialize()
        {
            this.xmakeProject = null;
            this.globalPropertyGroup = null;
            this.language = null;
            this.projectGuid = null;
            this.preBuildEvent = null;
            this.postBuildEvent = null;
            this.solution = null;
            this.newWebReferencePropertiesAdded = false;
            this.platformForVSD = null;
            this.assemblyName = null;
            this.outputType = null;
            this.hasWindowsFormsReference = false;
            this.isMyTypeAlreadySetInOriginalProject = false;
            this.conversionWarnings = new ArrayList();

            this.propertiesToEscape = new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
            this.propertiesToEscape.Add("ApplicationIcon", null);
            this.propertiesToEscape.Add("AssemblyKeyContainerName", null);
            this.propertiesToEscape.Add("AssemblyName", null);
            this.propertiesToEscape.Add("AssemblyOriginatorKeyFile", null);
            this.propertiesToEscape.Add("RootNamespace", null);
            this.propertiesToEscape.Add("StartupObject", null);
            this.propertiesToEscape.Add("ConfigurationOverrideFile", null);
            this.propertiesToEscape.Add("DocumentationFile", null);
            this.propertiesToEscape.Add("OutputPath", null);
        }

        /// <summary>
        /// This is the entry point method, which performs the project file format
        /// conversion.  This method will simply create a new XmlDocument
        /// in memory, instead of trying to write it to disk.
        /// </summary>
        /// <owner>RGoel</owner>
        private void ConvertInMemoryToMSBuildProject()
        {
            // Make sure we were passed in non-empty source and destination project
            // file names.
            error.VerifyThrowArgument(!string.IsNullOrEmpty(this.oldProjectFile),
                "MissingOldProjectFile");

            // Make sure the source project file exists.
            error.VerifyThrowArgument(File.Exists(oldProjectFile), "ProjectFileNotFound",
                oldProjectFile);

            Initialize();

            // Load the old project file as an XML document.
            XmlDocumentWithLocation oldProjectDocument = new XmlDocumentWithLocation();
            oldProjectDocument.PreserveWhitespace = true;
            TextReader oldProjectFileReader = new OldVSProjectFileReader(oldProjectFile);
            try
            {
                // We have our own custom XML reader to read in the old VS7/Everett project
                // file.  This is because the VS7/Everett project file format supported
                // having characters like <, >, &, etc. embedded inside XML attribute
                // values, but the default XmlTextReader won't handle this.
                using (XmlTextReader xmlReader = new XmlTextReader(oldProjectFileReader))
                {
                    xmlReader.DtdProcessing = DtdProcessing.Ignore;
                    oldProjectDocument.Load(xmlReader);
                }
            }
            catch (Exception e)
            {
                throw new InvalidProjectFileException(e.Message);
            }
            finally
            {
                oldProjectFileReader.Close();
            }

            // Get the top-level nodes from the XML.
            XmlNodeList rootNodes = oldProjectDocument.ChildNodes;
            XmlElementWithLocation visualStudioProjectElement = null;

            // The XML parser will guarantee that we only have one real root element,
            // but since XML comments may appear outside of the <VisualStudioProject> scope,
            // it's possible to get more than one child node.  Just find the first
            // non-comment node.  That should be the <VisualStudioProject> element.
            foreach(XmlNode childNode in rootNodes)
            {
                if ((childNode.NodeType != XmlNodeType.Comment) &&
                    (childNode.NodeType != XmlNodeType.XmlDeclaration) &&
                    (childNode.NodeType != XmlNodeType.Whitespace))
                {
                    visualStudioProjectElement = (XmlElementWithLocation) childNode;
                    break;
                }
            }

            IElementLocation oldProjectDocumentLocation = ElementLocation.Create(oldProjectDocument.FullPath, 1, 1);

            // Verify that we found a non-comment root node.
            ProjectErrorUtilities.VerifyThrowInvalidProject(visualStudioProjectElement != null,
                oldProjectDocumentLocation,
                "NoRootProjectElement", VSProjectElements.visualStudioProject);

            // If the root element is <Project>, then assume that this project is
            // already in XMake format.
            if (visualStudioProjectElement.Name == XMakeProjectStrings.project)
            {
                this.xmakeProject = ProjectRootElement.Open(oldProjectFile);

                // For Whidbey project just need to set the "ToolsVersion" attribute for the main project file
                // and remove imports like <Import Project="$(MSBuildBinPath)\Microsoft.WinFX.targets" />
                // because the Fidalgo stuff is part of .NET Framework 3.5

                // For upgraded workflow projects, the workflow targets need to reference the new v3.5 targets instead of v3.0 targets
                // this change is required to fix the msbuild break when building workflow rules.
                // e.g. before upgrade :<Import Project="$(MSBuildExtensionsPath)\Microsoft\Windows Workflow Foundation\v3.0\Workflow.Targets" />
                // after upgrade  <Import Project="$(MSBuildExtensionsPath)\Microsoft\Windows Workflow Foundation\v3.5\Workflow.Targets" />

                string oldToolsVersion = xmakeProject.ToolsVersion;

                xmakeProject.ToolsVersion = XMakeProjectStrings.toolsVersion;
                List<ProjectImportElement> listOfImportsToBeDeleted = new List<ProjectImportElement>();
                List<ProjectImportElement> listOfWFImportsToBeDeleted = new List<ProjectImportElement>();
                List<string> workflowImportsToAdd = new List<string>();
                string workflowTargetsBasePath = @"$(MSBuildExtensionsPath)\Microsoft\Windows Workflow Foundation\";
                string workflowOldWhidbeyTargetsPath = workflowTargetsBasePath + @"v3.0\";
                string workflowOldOrcasTargetsPath = workflowTargetsBasePath + @"v3.5\";
                string workflowNewTargetsPath = @"$(MSBuildToolsPath)\";
                bool removedWFWhidbeyTargets = false;
                bool changedProject = false;

                // Find matching imports but don't delete whilst enumerating else it will throw an error
                foreach (ProjectImportElement nextImport in xmakeProject.Imports)
                {
                    if (String.Equals(nextImport.Project, @"$(MSBuildBinPath)\Microsoft.WinFX.targets", StringComparison.OrdinalIgnoreCase))
                    {
                        listOfImportsToBeDeleted.Add(nextImport);
                    }

                    if (nextImport.Project.Contains(workflowOldWhidbeyTargetsPath))
                    {
                        listOfWFImportsToBeDeleted.Add(nextImport);
                        workflowImportsToAdd.Add(nextImport.Project.Replace(workflowOldWhidbeyTargetsPath, workflowNewTargetsPath));
                        removedWFWhidbeyTargets = true;
                    }
                    if (nextImport.Project.Contains(workflowOldOrcasTargetsPath))
                    {
                        listOfWFImportsToBeDeleted.Add(nextImport);
                        workflowImportsToAdd.Add(nextImport.Project.Replace(workflowOldOrcasTargetsPath, workflowNewTargetsPath));
                    }
                }

                // Now delete any matching imports
                foreach (ProjectImportElement importToDelete in listOfWFImportsToBeDeleted)
                {
                    this.xmakeProject.RemoveChild(importToDelete);
                    changedProject = true;
                }

                bool removedWinFXTargets = false;
                foreach (ProjectImportElement importToDelete in listOfImportsToBeDeleted)
                {
                    this.xmakeProject.RemoveChild(importToDelete);
                    removedWinFXTargets = true;
                    changedProject = true;
                }

                // If we removed WinFX targets this is a sparkle project and should use v3.0
                if (removedWinFXTargets)
                {
                    xmakeProject.AddProperty(XMakeProjectStrings.TargetFrameworkVersion, "v3.0");
                    changedProject = true;
                }

                //If we removed WFWhidbey imports, we should target this project to v3.0
                if (removedWFWhidbeyTargets)
                {
                    xmakeProject.AddProperty(XMakeProjectStrings.TargetFrameworkVersion, "v3.0");
                    changedProject = true;
                }

                // Re-add the workflow imports with the v4.0 targets.
                foreach (string workflowImportToAdd in workflowImportsToAdd)
                {
                    this.xmakeProject.AddImport(workflowImportToAdd);
                    changedProject = true;
                }

                // Find all the XAML files in the project and give them the custom attributes
                //   <Generator>MSBuild:Compile</Generator> (DevDiv Bugs bug 81222)
                //   <SubType>Designer</SubType> (DevDiv Bugs bug 82748)

                // Find all references to old VC project files (.vcproj extension) and change the
                // extension to .vcxproj instead.  NOTE: we assume that the actual .vcproj -> .vcxproj
                // conversion has already been / is being / will be done elsewhere.
                // Dev10 Bug 557388

                foreach (ProjectItemElement nextItem in xmakeProject.Items)
                {
                    if ((!nextItem.ItemType.Equals("Reference", StringComparison.OrdinalIgnoreCase)) &&
                        (nextItem.Include.Trim().EndsWith(".xaml", StringComparison.OrdinalIgnoreCase)))

                    {
                        if (!nextItem.Metadata.Any(m => String.Equals(m.Name, "Generator", StringComparison.OrdinalIgnoreCase)))
                        {
                            nextItem.AddMetadata("Generator", "MSBuild:Compile");
                            changedProject = true;
                        }

                        if (!nextItem.Metadata.Any(m => String.Equals(m.Name, "SubType", StringComparison.OrdinalIgnoreCase)))
                        {
                            nextItem.AddMetadata("SubType", "Designer");
                            changedProject = true;
                        }
                    }

                    if (String.Equals(nextItem.ItemType, "ProjectReference", StringComparison.OrdinalIgnoreCase) &&
                        nextItem.Include.Trim().EndsWith(".vcproj", StringComparison.OrdinalIgnoreCase))
                    {
                        nextItem.Include = Path.ChangeExtension(nextItem.Include, ".vcxproj");
                        changedProject = true;
                    }
                }

                // DevDiv Bugs bug 100701: if we removed the Microsoft.WinFX.targets import,
                // and if there is no ProjectTypeGuids property, add the WPF flavor GUID
                if (removedWinFXTargets)
                {
                    ProjectPropertyElement currentGuidsProperty = FindPropertyIfPresent(this.xmakeProject, XMakeProjectStrings.projectTypeGuids);
                    string newGuids = "{" + XMakeProjectStrings.wpfFlavorGuid + "}";
                    if (currentGuidsProperty == null || currentGuidsProperty.Value.Length == 0)
                    {
                        string currentGuids = String.Empty;

                        // To have a flavor GUID we need a base GUID.
                        if (oldProjectFile.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
                        {
                            currentGuids = "{" + XMakeProjectStrings.cSharpGuid + "}";
                        }
                        if (oldProjectFile.EndsWith(".vbproj", StringComparison.OrdinalIgnoreCase))
                        {
                            currentGuids = "{" + XMakeProjectStrings.visualBasicGuid + "}";
                        }
                        xmakeProject.AddProperty(XMakeProjectStrings.projectTypeGuids, newGuids + ";" + currentGuids);
                        changedProject = true;
                    }
                }

                // Fix up TargetFrameworkSubset
                changedProject = FixTargetFrameworkSubset() || changedProject;

                var hasFSharpSpecificConversions = FSharpSpecificConversions(true);

                changedProject = hasFSharpSpecificConversions || changedProject;
                changedProject = VBSpecificConversions() || changedProject;

                // Do asset compat repair for any project that was previously a TV < 12.0
                if (
                        String.IsNullOrEmpty(oldToolsVersion) ||
                        String.Equals(oldToolsVersion, "3.5", StringComparison.OrdinalIgnoreCase) ||
                        String.Equals(oldToolsVersion, "4.0", StringComparison.OrdinalIgnoreCase)
                    )
                {
                    changedProject = DoRepairForAssetCompat() || changedProject;
                }

                // Remove any default fully qualified Code Analysis paths.
                // DevDiv bug 63415
                changedProject = FixCodeAnalysisPaths() || changedProject;

                if (hasFSharpSpecificConversions && !String.IsNullOrEmpty(oldToolsVersion))
                {
                    // for Bug 609702:A ToolsVersion=12.0 F# project fails to load in VS 2012
                    // for F# project after upgrade we restore previous value of ToolsVersion so Dev11 still can load upgraded project
                    // however if old ToolsVersion as 3.5 - it will be upgraded to 4.0 to avoid any unexpected behavior in Dev10\Dev11
                    xmakeProject.ToolsVersion = String.Equals(oldToolsVersion, "3.5", StringComparison.OrdinalIgnoreCase) ? "4.0" : oldToolsVersion;
                }
                else if (this.isMinorUpgrade ||
                        (!changedProject &&
                         !String.IsNullOrEmpty(oldToolsVersion) &&
                         !String.Equals(oldToolsVersion, "3.5", StringComparison.OrdinalIgnoreCase))
                    )
                {
                    // If it's minor upgrade, or nothing changed and the project was already TV 4.0 or higher,
                    // set the ToolsVersion back to its old value. 
                    xmakeProject.ToolsVersion = oldToolsVersion;
                }
            }
            else
            {
                // OK, we have to start with a fresh project and assemble it
                this.xmakeProject = ProjectRootElement.Create();

                // This root node must be a <VisualStudioProject> node.
                ProjectErrorUtilities.VerifyThrowInvalidProject(visualStudioProjectElement.Name ==
                    VSProjectElements.visualStudioProject,
                    visualStudioProjectElement.Location, "UnrecognizedElement", visualStudioProjectElement.Name);

                // Set the "DefaultTargets" attribute for the main project file.
                if (!isUserFile)
                {
                    xmakeProject.DefaultTargets = XMakeProjectStrings.defaultTargets;
                }

                // Set the "ToolsVersion" attribute for the main project file.
                if (!isUserFile)
                {
                    xmakeProject.ToolsVersion = XMakeProjectStrings.toolsVersion;
                }

                // Process the <VisualStudioProject> element in the source project file,
                // adding the necessary stuff to the XMake project.
                this.ProcessVisualStudioProjectElement(visualStudioProjectElement);
            }
        }

        /// <summary>
        /// returns 'false' if there was no repair required
        /// else does a repair and returns 'true'
        /// </summary>
        /// <returns>bool</returns>
        private bool DoRepairForAssetCompat()
        {
            var toRepairImports = RequiresRepairForAssetCompat();

            if (toRepairImports == null || toRepairImports.Count() == 0)
            {
                // no need to repair
                return false;
            }

            foreach (var toRepairImport in toRepairImports)
            {
                RepairImportForAssetCompat(toRepairImport);
            }

            //
            // Add PropertyGroup with Conditions right before where the Imports occur
            //   <PropertyGroup>
            //     <VisualStudioVersion Condition="'$(VisualStudioVersion)' == ''">10.0</VisualStudioVersion>
            //     <VSToolsPath Condition="'$(VSToolsPath)' == ''">$(MSBuildExtensionsPath32)\Microsoft\VisualStudio\v$(VisualStudioVersion)</VSToolsPath>
            //   </PropertyGroup>
            //
            var vsToolsPathPropGroup = this.xmakeProject.CreatePropertyGroupElement();
            var firstAmongImports = this.xmakeProject.Imports.First();
            firstAmongImports.Parent.InsertBeforeChild(vsToolsPathPropGroup, firstAmongImports);

            var vsVersionProperty = this.xmakeProject.CreatePropertyElement(XMakeProjectStrings.visualStudioVersion);
            vsVersionProperty.Value = @"10.0";
            vsVersionProperty.Condition = @"'$(VisualStudioVersion)' == ''";
            vsToolsPathPropGroup.AppendChild(vsVersionProperty);

            var vsToolsPathProperty = this.xmakeProject.CreatePropertyElement(XMakeProjectStrings.vsToolsPath);
            vsToolsPathProperty.Value = @"$(MSBuildExtensionsPath32)\Microsoft\VisualStudio\v$(VisualStudioVersion)";
            vsToolsPathProperty.Condition = @"'$(VSToolsPath)' == ''";
            vsToolsPathPropGroup.AppendChild(vsToolsPathProperty);

            //
            // Add a conditional import to Microsoft.Common.props at the beginning of project
            // <Import Project="$(MSBuildExtensionsPath)\$(MSBuildToolsVersion)\Microsoft.Common.props" Condition="Exists('$(MSBuildExtensionsPath)\$(MSBuildToolsVersion)\Microsoft.Common.props')"/>
            //
            var newImportElement = this.xmakeProject.CreateImportElement(@"$(MSBuildExtensionsPath)\$(MSBuildToolsVersion)\Microsoft.Common.props");
            newImportElement.Condition = @"Exists('$(MSBuildExtensionsPath)\$(MSBuildToolsVersion)\Microsoft.Common.props')";
            this.xmakeProject.InsertBeforeChild(newImportElement, this.xmakeProject.FirstChild);

            return true;
        }

        /// <summary>
        /// Repairs the given import element
        /// Change Import to use $(VSToolsPath), with Condition using $(VSToolsPath)
        /// e.g. From: Import Project="$(MSBuildExtensionsPath32)\Microsoft\VisualStudio\v10.0\WebApplications\Microsoft.WebApplication.targets"
        ///        To: Import Project="$(MSBuildExtensionsPath32)\Microsoft\VisualStudio\v10.0\WebApplications\Microsoft.WebApplication.targets" Condition="false"
        ///            Import Project="$(VSToolsPath)\WebApplications\Microsoft.WebApplication.targets"
        /// $(VSToolsPath) will be defined elsewhere in this upgrade to be: $(MSBuildExtensionsPath32)\Microsoft\VisualStudio\v$(VisualStudioVersion)
        /// </summary>
        /// <param name="toRepairImport"></param>
        private void RepairImportForAssetCompat(ProjectImportElement toRepairImport)
        {
            // We shouldn't have this happen but check anyway:
            ErrorUtilities.VerifyThrowInternalNull(toRepairImport, nameof(toRepairImport));
            ErrorUtilities.VerifyThrow(!toRepairImport.Condition.Equals("false", StringComparison.OrdinalIgnoreCase), "RepairImportForAssetCompat should not receive imports with condition=false already");

            var newImportElement = this.xmakeProject.CreateImportElement(toRepairImport.Project);
            newImportElement.Condition = "false";
            newImportElement.Project = XMakeProjectStrings.toRepairPatternForAssetCompatV10 + ExtractImportTargetsString(newImportElement.Project);
            toRepairImport.Parent.InsertAfterChild(newImportElement, toRepairImport);

            toRepairImport.Project = @"$(VSToolsPath)\" + ExtractImportTargetsString(toRepairImport.Project);
            toRepairImport.Condition = @"'$(VSToolsPath)' != ''";
        }

        /// <summary>
        /// Extracts the actual targets imported without the repair -pattern path
        /// e.g. from: $(MSBuildExtensionsPath32)\Microsoft\VisualStudio\v10.0\WebApplications\Microsoft.WebApplication.targets
        /// we will extract: WebApplications\Microsoft.WebApplication.targets
        /// </summary>
        /// <param name="importProjectValue"></param>
        /// <returns></returns>
        private string ExtractImportTargetsString(string importProjectValue)
        {
            // For VS2005 Office Targets return: OfficeTools\Microsoft.VisualStudio.Tools.Office.targets
            if (importProjectValue.Equals(XMakeProjectStrings.officeTargetsVS2005Import, StringComparison.OrdinalIgnoreCase)
                || importProjectValue.Equals(XMakeProjectStrings.officeTargetsVS2005Import2, StringComparison.OrdinalIgnoreCase))
            {
                return XMakeProjectStrings.officeTargetsVS2005Repair;
            }

            string startString;

            if (importProjectValue.StartsWith(XMakeProjectStrings.toRepairPatternForAssetCompat, StringComparison.OrdinalIgnoreCase))
            {
                startString = XMakeProjectStrings.toRepairPatternForAssetCompat;
            }
            else
            {
                startString = XMakeProjectStrings.toRepairPatternForAssetCompatBeforeV10;
            }

            string result = importProjectValue.Remove(0, startString.Length);

            // Extract the version string
            Match m = Regex.Match(result, XMakeProjectStrings.repairHardCodedPathPattern);

            return result.Remove(0, m.Length);
        }

        /// <summary>
        /// Checks if repair is required
        /// </summary>
        /// <returns>bool</returns>
        private IEnumerable<ProjectImportElement> RequiresRepairForAssetCompat()
        {
            // check if the project has the to-repair pattern in the Imports
            // pattern: $(MSBuildExtensionsPath32)\Microsoft\VisualStudio\v10.0\
            var toRepairImports =  from import in xmakeProject.Imports
                                   where HasRepairPattern(import)
                                   select import;

            return toRepairImports;
        }

        /// <summary>
        /// Check if the Import element has a repair pattern:
        /// $(MSBuildExtensionsPath)\Microsoft\VisualStudio\vX.X
        /// or $(MSBuildExtensionsPath)\Microsoft.VisualStudio.OfficeTools.targets
        /// </summary>
        /// <param name="importElement"></param>
        /// <returns></returns>
        private bool HasRepairPattern(ProjectImportElement importElement)
        {
            bool bHasRepairPattern = false;

            // in case of an already repaired project the repair pattern will exist with Condition="false"
            if (!String.Equals(importElement.Condition, "false", StringComparison.OrdinalIgnoreCase))
            {
                if ((importElement.Project.StartsWith(XMakeProjectStrings.toRepairPatternForAssetCompat, StringComparison.OrdinalIgnoreCase))
                    || (importElement.Project.StartsWith(XMakeProjectStrings.toRepairPatternForAssetCompatBeforeV10, StringComparison.OrdinalIgnoreCase)))
                {
                    string startString;
                    if (importElement.Project.StartsWith(XMakeProjectStrings.toRepairPatternForAssetCompat, StringComparison.OrdinalIgnoreCase))
                    {
                        startString = XMakeProjectStrings.toRepairPatternForAssetCompat;
                    }
                    else
                    {
                        startString = XMakeProjectStrings.toRepairPatternForAssetCompatBeforeV10;
                    }

                    Match m = Regex.Match(importElement.Project.Substring(startString.Length), XMakeProjectStrings.repairHardCodedPathPattern);

                    if (m.Success)
                    {
                        bHasRepairPattern = true;
                    }
                }
                else
                {
                    // Check for VS2003/2005 Office Targets
                    // $(MSBuildExtensionsPath)\Microsoft.VisualStudio.OfficeTools.targets
                    if (importElement.Project.Equals(XMakeProjectStrings.officeTargetsVS2005Import, StringComparison.OrdinalIgnoreCase)
                        || importElement.Project.Equals(XMakeProjectStrings.officeTargetsVS2005Import2, StringComparison.OrdinalIgnoreCase))
                        bHasRepairPattern = true;
                }
            }

            return bHasRepairPattern;
        }

        /// <summary>
        /// Fixes <TargetFrameworkSubset/> properties in the project file.  This was the Orcas SP1 way of
        /// handling framework profiles, and that way is now incompatible with the VS 2010 way of handling
        /// profiles.
        /// </summary>
        /// <returns>true if changes were required, false otherwise</returns>
        private bool FixTargetFrameworkSubset()
        {
            bool changedProject = false;

            foreach (ProjectPropertyElement propertyElement in xmakeProject.Properties)
            {
                if (String.Equals(propertyElement.Name , XMakeProjectStrings.TargetFrameworkSubset, StringComparison.OrdinalIgnoreCase))
                {
                    // For the Client profile, which was the only profile supported in Orcas SP1, we want to replace 
                    // <TargetFrameworkSubset/> with <TargetFrameworkProfile/>.
                    if (String.Equals(propertyElement.Value, XMakeProjectStrings.ClientProfile, StringComparison.OrdinalIgnoreCase))
                    {
                        ProjectPropertyGroupElement parentGroup = (ProjectPropertyGroupElement) propertyElement.Parent;
                        parentGroup.SetProperty(XMakeProjectStrings.TargetFrameworkProfile, XMakeProjectStrings.ClientProfile);
                        changedProject = true;
                    }

                    // In all cases, <TargetFrameworkSubset/> is no longer supported.  If it comes from the project
                    // that we're converting, then we forcibly remove it.  If it comes from some import... the user is
                    // on their own.  
                    if (propertyElement.ContainingProject == xmakeProject)
                    {
                        propertyElement.Parent.RemoveChild(propertyElement);
                        changedProject = true;
                    }

                    break;
                }
            }

            return changedProject;
        }

        /// <summary>
        /// Performs conversions specific to F# projects (VS2008 CTP -> VS2012) and (VS2010 -> VS2012).
        /// This involves: changing the location of FSharp targets,
        /// and for 2008CTP, adding explicit mscorlib and FSharp.Core references.
        /// </summary>
        /// <param name="actuallyMakeChanges">if true, make the changes, otherwise, don't actually make any changes, but do report the return boolean as to whether you would make changes</param>
        /// <returns>true if anything was (would be) changed, false otherwise</returns>
        public bool FSharpSpecificConversions(bool actuallyMakeChanges)
        {
            // For FSharp projects, should import different location of FSharp targets
            const string fsharpFS10TargetsPath = @"$(MSBuildExtensionsPath)\FSharp\1.0\Microsoft.FSharp.Targets";
            const string fsharpFS10TargetsPath32 = @"$(MSBuildExtensionsPath32)\FSharp\1.0\Microsoft.FSharp.Targets";
            const string fsharpFS40TargetsPath = @"$(MSBuildExtensionsPath32)\..\Microsoft F#\v4.0\Microsoft.FSharp.Targets";
            const string fsharpFS45TargetsPath = @"$(MSBuildExtensionsPath32)\..\Microsoft SDKs\F#\3.0\Framework\v4.0\Microsoft.FSharp.Targets";
            const string fsharpPortableDev11TargetsPath = @"$(MSBuildExtensionsPath32)\..\Microsoft SDKs\F#\3.0\Framework\v4.0\Microsoft.Portable.FSharp.Targets";

            const string fsharpDev12PlusProperty = "FSharpTargetsPath";

            // Dev12+ projects import *.targets files using property
            const string fsharpDev12PlusImportsValue = @"$(" + fsharpDev12PlusProperty + ")";
            // Q: do we need to distinguish between different versions of F# for the same version of VS
            const string fsharpDev12PlusTargetsPath = @"$(MSBuildExtensionsPath32)\Microsoft\VisualStudio\v$(VisualStudioVersion)\FSharp\Microsoft.FSharp.Targets";
            const string fsharpDev12PlusPortableTargetsPath = @"$(MSBuildExtensionsPath32)\Microsoft\VisualStudio\v$(VisualStudioVersion)\FSharp\Microsoft.Portable.FSharp.Targets";

            bool isAtLeastDev10Project = false;

            ProjectImportElement fsharpTargetsFS10Import = null;
            ProjectImportElement fsharpTargetsFS40Import = null;
            ProjectImportElement fsharpTargetsFS45Import = null;
            ProjectImportElement fsharpTargetsDev12PlusImport = null;
            ProjectImportElement fsharpTargetsDev11PortableImport = null;

            if (!actuallyMakeChanges && this.xmakeProject == null)
            {
                // when coming down the actuallyMakeChanges==false code path (from the F# project system's UpgradeProject_CheckOnly method), we may not have loaded the Xml yet, so do that now
                this.xmakeProject = ProjectRootElement.Open(oldProjectFile);
            }

            // local function: string equality check using OrdinalIgnoreCase comparison
            Func<string, string, bool> equals = (s1, s2) => String.Equals(s1, s2, StringComparison.OrdinalIgnoreCase);

            // local function: wraps specified string value into Exists('value')
            Func<string, string> exists = s => string.Format(CultureInfo.InvariantCulture, "Exists('{0}')", s);

            // local function: 
            // Creates property group element containing one property fsharpDev12PlusProperty with value 'path'. 
            // If addCondition is true, property group will have Exists(path) condition
            Action<string, ProjectElementContainer> appendPropertyGroupForDev12PlusTargetsPath =
                (path, parent) =>
                {
                    var propGroup = xmakeProject.CreatePropertyGroupElement();
                    parent.AppendChild(propGroup);
                    var prop = xmakeProject.CreatePropertyElement(fsharpDev12PlusProperty);
                    prop.Value = path;
                    propGroup.AppendChild(prop);
                };

            foreach (ProjectImportElement importElement in xmakeProject.Imports)
            {
                if (equals(importElement.Project, fsharpFS10TargetsPath) || equals(importElement.Project, fsharpFS10TargetsPath32))
                {
                    fsharpTargetsFS10Import = importElement;
                    if (equals(@"!Exists('$(MSBuildToolsPath)\Microsoft.Build.Tasks.v4.0.dll')", fsharpTargetsFS10Import.Condition)
                        || equals(@"!Exists('$(MSBuildBinPath)\Microsoft.Build.Tasks.v4.0.dll')", fsharpTargetsFS10Import.Condition))
                    {
                        isAtLeastDev10Project = true;
                    }
                }
                else if (equals(importElement.Project, fsharpFS40TargetsPath))
                {
                    fsharpTargetsFS40Import = importElement;
                    isAtLeastDev10Project = true;
                }
                else if (equals(importElement.Project, fsharpFS45TargetsPath))
                {
                    fsharpTargetsFS45Import = importElement;
                    isAtLeastDev10Project = true;
                }
                else if (equals(importElement.Project, fsharpDev12PlusImportsValue))
                {
                    fsharpTargetsDev12PlusImport = importElement;
                    isAtLeastDev10Project = true;
                }
                else if (equals(importElement.Project, fsharpPortableDev11TargetsPath))
                {
                    fsharpTargetsDev11PortableImport = importElement;
                    isAtLeastDev10Project= true;
                }
            }

            if (fsharpTargetsDev12PlusImport != null)
            {
                // if project already contains version independent import - then assume it is already at least dev12 - do nothing
                return false;
            }

            // no other F# imports - do nothing
            if (fsharpTargetsFS10Import == null && fsharpTargetsFS40Import == null && fsharpTargetsFS45Import == null && fsharpTargetsDev11PortableImport == null)
                return false;

            if (!actuallyMakeChanges)
                return true;

            // both branches adds this elements to the project
            var chooseElement = xmakeProject.CreateChooseElement(); // (1)

            if (fsharpTargetsDev11PortableImport != null)
            {
                // Dev11 portable library
                // Expected fragment of the project file after upgrade
                //<Choose>
                //  <When Condition="'$(VisualStudioVersion)' == '11.0'"> (2)
                //    <PropertyGroup>
                //      <FSharpTargetsPath>$(MSBuildExtensionsPath32)\..\Microsoft SDKs\F#\3.0\Framework\v4.0\Microsoft.Portable.FSharp.Targets</FSharpTargetsPath>
                //    </PropertyGroup>
                //  </When>
                //  <Otherwise> (3)
                //    <PropertyGroup>
                //      <FSharpTargetsPath>$(MSBuildExtensionsPath32)\Microsoft\VisualStudio\v$(VisualStudioVersion)\FSharp\Microsoft.Portable.FSharp.Targets</FSharpTargetsPath>
                //    </PropertyGroup>
                //  </Otherwise>
                //</Choose>
                //<Import Project=""$(FSharpTargetsPath)"" Condition="Exists('$(FSharpTargetsPath)')"/>
                fsharpTargetsDev11PortableImport.Parent.InsertBeforeChild(chooseElement, fsharpTargetsDev11PortableImport);

                // portable libraries are supported since Dev11
                var whenVsVersionIsDev11 = xmakeProject.CreateWhenElement("'$(VisualStudioVersion)' == '11.0'"); // (2)
                chooseElement.AppendChild(whenVsVersionIsDev11);

                appendPropertyGroupForDev12PlusTargetsPath(fsharpPortableDev11TargetsPath, whenVsVersionIsDev11);

                var otherwiseIfVsVersionIsDev12Plus = xmakeProject.CreateOtherwiseElement(); // (3)
                chooseElement.AppendChild(otherwiseIfVsVersionIsDev12Plus);

                appendPropertyGroupForDev12PlusTargetsPath(fsharpDev12PlusPortableTargetsPath, otherwiseIfVsVersionIsDev12Plus);
            }
            else
            {
                // This is an FSharp project, and it does not already have a 4.5 import, and thus it needs repair.
                // one of these elements should be non-null, otherwise we'll exit based on the check above
                var someNonNullImportElement = fsharpTargetsFS10Import ?? fsharpTargetsFS40Import ?? fsharpTargetsFS45Import;

                someNonNullImportElement.Parent.InsertBeforeChild(chooseElement, someNonNullImportElement);

                // Expected fragment of the project file after upgrade 
                //<Choose>
                //  <When Condition="'$(VisualStudioVersion)' == '11.0'">
                //    <PropertyGroup>
                //      <FSharpTargetsPath>$(MSBuildExtensionsPath32)\..\Microsoft SDKs\F#\3.0\Framework\v4.0\Microsoft.FSharp.Targets</FSharpTargetsPath>
                //    </PropertyGroup>
                //  </When>
                //  <Otherwise>
                //    <PropertyGroup>
                //      <FSharpTargetsPath>$(MSBuildExtensionsPath32)\Microsoft\VisualStudio\v$(VisualStudioVersion)\FSharp\Microsoft.FSharp.Targets</FSharpTargetsPath>
                //    </PropertyGroup>
                //  </Otherwise>
                //</Choose>
                //<Import Project="$(FSharpTargetsPath)" Condition="Exists('$(FSharpTargetsPath)')" />           

                var whenVsVersionIsDev11 = xmakeProject.CreateWhenElement("'$(VisualStudioVersion)' == '11.0'");
                chooseElement.AppendChild(whenVsVersionIsDev11);
                {
                    appendPropertyGroupForDev12PlusTargetsPath(fsharpFS45TargetsPath, whenVsVersionIsDev11);
                }

                var otherwiseIfVsVersionIsDev12Plus = xmakeProject.CreateOtherwiseElement();
                chooseElement.AppendChild(otherwiseIfVsVersionIsDev12Plus);
                {
                    // Dev12+ projects - import target file based on property 'fsharpDev12PlusProperty'
                    appendPropertyGroupForDev12PlusTargetsPath(fsharpDev12PlusTargetsPath, otherwiseIfVsVersionIsDev12Plus);
                }
            }
            // add Dev12 specific Imports element
            var dev12PlusImportElement = xmakeProject.CreateImportElement(fsharpDev12PlusImportsValue);
            dev12PlusImportElement.Condition = exists(fsharpDev12PlusImportsValue);
            chooseElement.Parent.InsertAfterChild(dev12PlusImportElement, chooseElement);

            if (fsharpTargetsFS10Import != null)
                xmakeProject.RemoveChild(fsharpTargetsFS10Import);
            if (fsharpTargetsFS40Import != null)
                xmakeProject.RemoveChild(fsharpTargetsFS40Import);
            if (fsharpTargetsFS45Import != null)
                xmakeProject.RemoveChild(fsharpTargetsFS45Import);
            if (fsharpTargetsDev11PortableImport != null)
                xmakeProject.RemoveChild(fsharpTargetsDev11PortableImport);

            const string ReferenceItemType = "Reference";

            // find ItemGroup for Reference items
            ProjectItemGroupElement referencesItemGroup = xmakeProject.Items
                .Where(projectItem => projectItem.ItemType == ReferenceItemType && projectItem.Parent is ProjectItemGroupElement)
                .Select(projectItem => (ProjectItemGroupElement)projectItem.Parent)
                .FirstOrDefault();

            if (referencesItemGroup == null)
            {
                referencesItemGroup = this.xmakeProject.AddItemGroup();
            }

            var targetFrameworkVersionProperty = xmakeProject.Properties.FirstOrDefault(p => equals(p.Name, "TargetFrameworkVersion"));

            // fix FSharp.Core reference
            const string TargetFSharpCoreVersionProperty = "TargetFSharpCoreVersion";

            // by default import with minimal possible version
            const string DefaultFSharpCoreVersionFor40 = "4.3.0.0";
            const string DefaultFSharpCoreVersionFor20 = "2.3.0.0";
            const string DefaultPortableFSharpCoreVersion = "2.3.5.0";
            const string FSharpCoreName = "FSharp.Core";

            if (!isAtLeastDev10Project)
            {
                bool hasMscorlibReference = xmakeProject.Items.Any(projectItem => projectItem.ItemType == ReferenceItemType && equals(projectItem.Include, "mscorlib"));
                // It appears pre-dev10, so add explicit references to mscorlib
                if (!hasMscorlibReference)
                {
                    referencesItemGroup.AddItem(ReferenceItemType, "mscorlib");
                }
            }

            // try to find reference to FSharp.Core 
            ProjectItemElement fsharpCoreItem = null;
            foreach (var item in xmakeProject.Items.Where(x => x.ItemType == ReferenceItemType))
            {
                try
                {
                    var name = new AssemblyName(item.Include);
                    if (name.Name == FSharpCoreName)
                    {
                        fsharpCoreItem = item;
                        break;
                    }
                }
                catch (FileLoadException)
                {
                    // Include contains not AssemblyName but rather something else - not the case for F# projects
                }
            }

            const string Dev11PortableFSharpCoreLocation = @"$(MSBuildExtensionsPath32)\..\Reference Assemblies\Microsoft\FSharp\3.0\Runtime\.NETPortable\FSharp.Core.dll";
            const string Dev12PortableFSharpCoreLocationForDev11Projects = @"$(MSBuildExtensionsPath32)\..\Reference Assemblies\Microsoft\FSharp\.NETPortable\$(" + TargetFSharpCoreVersionProperty + @")\FSharp.Core.dll";
            const string HintPath = "HintPath";

            ProjectItemElement newFSharpCoreItem = null;
            string targetFSharpCoreVersionValue = null;

            var hintPathValue = fsharpCoreItem?.Metadata.FirstOrDefault(metadata => metadata.Name == HintPath);
            if (hintPathValue != null)
            {
                if (equals(hintPathValue.Value, Dev11PortableFSharpCoreLocation))
                {
                    // Reference to Dev11 portable library
                    newFSharpCoreItem = referencesItemGroup.AddItem(ReferenceItemType, FSharpCoreName);
                    newFSharpCoreItem.AddMetadata(HintPath, Dev12PortableFSharpCoreLocationForDev11Projects);

                    targetFSharpCoreVersionValue = DefaultPortableFSharpCoreVersion;
                }
            }
            else if (!isAtLeastDev10Project || fsharpCoreItem != null)
            {
                newFSharpCoreItem = referencesItemGroup.AddItem(ReferenceItemType, string.Format("FSharp.Core, Version=$({0}), Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a", TargetFSharpCoreVersionProperty));
                if (targetFrameworkVersionProperty == null || string.IsNullOrEmpty(targetFrameworkVersionProperty.Value) || !targetFrameworkVersionProperty.Value.StartsWith("v"))
                {
                    targetFSharpCoreVersionValue = DefaultFSharpCoreVersionFor40;
                }
                else
                {
                    var versionStr = targetFrameworkVersionProperty.Value.Substring(1); // strip 'v'
                    Version version;
                    targetFSharpCoreVersionValue =
                        Version.TryParse(versionStr, out version)
                        ? version.Major < 4 ? DefaultFSharpCoreVersionFor20 : DefaultFSharpCoreVersionFor40
                        : DefaultFSharpCoreVersionFor40;
                }
            }

            newFSharpCoreItem?.AddMetadata("Private", "True");

            const string MinimumVisualStudioVersionProperty = "MinimumVisualStudioVersion";
            var hasMinimumVSVersion = xmakeProject.Properties.Any(prop => prop.Name == MinimumVisualStudioVersionProperty);

            foreach(var group in xmakeProject.PropertyGroups)
            {
                // find first non-conditional property group to add TargetFSharpCoreVersion property
                if (string.IsNullOrEmpty(group.Condition))
                {
                    if (targetFSharpCoreVersionValue != null)
                    {
                        group.AddProperty(TargetFSharpCoreVersionProperty, targetFSharpCoreVersionValue);
                    }

                    if (!hasMinimumVSVersion)
                    {
                        var prop = group.AddProperty(MinimumVisualStudioVersionProperty, "11");
                        prop.Condition = "'$(" + MinimumVisualStudioVersionProperty + ")' == ''";
                    }

                    break;
                }
            }

            // new FSharp.Core was added - can delete the old reference
            if (newFSharpCoreItem != null && fsharpCoreItem != null)
            {
                fsharpCoreItem.Parent.RemoveChild(fsharpCoreItem);
            }

            return true;
        }

        /// <summary>
        /// Performs conversions specific to VB projects (VS2008 and VS2008 -> VS2010).
        /// This involves: Adding a set of nowarn settings to disable warnings added
        /// in VS2010 that break customers upgrading from previous releases.
        /// </summary>
        /// <returns>true if changes were required, false otherwise</returns>
        private bool VBSpecificConversions()
        {
            // Are we upgrading a VB project?
            // We are if the project file imports:
            //     "$(MSBuildToolsPath)\Microsoft.VisualBasic.targets" (VS2008)
            //     "$(MSBuildBinPath)\Microsoft.VisualBasic.targets"   (VS2005)

            bool vbProject = false;
            bool changedProject = false;

            foreach (var import in xmakeProject.Imports)
            {
                if (String.Equals(import.Project, XMakeProjectStrings.vbTargetsVS2008, StringComparison.OrdinalIgnoreCase) ||
                    String.Equals(import.Project, XMakeProjectStrings.vbTargetsVS2005, StringComparison.OrdinalIgnoreCase))
                {
                    vbProject = true;
                    break;
                }
            }

            // Not a VB project -> no work to do.
            if (!vbProject)
            {
                return changedProject;
            }

            // Any property group with a condition is of interest.
            // If we find it and it has "NoWarn" property, we add our warnings into it.
            // If it doesn't, we create new NoWarn property with the initial value
            foreach (var group in xmakeProject.PropertyGroups)
            {
                if (String.IsNullOrEmpty(group.Condition))
                {
                    continue;
                }

                string noWarn = null;
                foreach (var property in group.Properties)
                {
                    if (String.Equals(property.Name, XMakeProjectStrings.noWarn, StringComparison.OrdinalIgnoreCase))
                    {
                        noWarn = property.Value;
                        break;
                    }
                }

                if (String.IsNullOrWhiteSpace(noWarn))
                {
                    noWarn = String.Empty;
                }
                else
                {
                    noWarn = noWarn.Trim();
                }

                string originalNoWarnValue = noWarn;
                //
                // Split the no warning string and trim the results
                //
                string[] oldWarnings = noWarn.Split(',');
                for (var oi = 0; oi < oldWarnings.Length; oi++)
                {
                    oldWarnings[oi] = oldWarnings[oi].Trim();
                }

                //
                // Add the new warnings specific to Dev10: 42353,42354,42355
                // (if we don't have them already)
                //
                string[] newWarnings = new[] { "42353", "42354", "42355" };

                foreach (var newWarn in newWarnings)
                {
                    bool found = false;
                    foreach (var oldWarn in oldWarnings)
                    {
                        if (String.Equals(newWarn, oldWarn, StringComparison.OrdinalIgnoreCase))
                        {
                            found = true;
                            break;
                        }
                    }

                    if (!found)
                    {
                        // append the new warning
                        if (!String.IsNullOrEmpty(noWarn) && !noWarn.EndsWith(",", StringComparison.OrdinalIgnoreCase))
                        {
                            noWarn += ",";
                        }
                        noWarn += newWarn;
                    }
                }

                // Set the property value. If it doesn't exist, it will be added.
                if (!String.Equals(originalNoWarnValue, noWarn, StringComparison.OrdinalIgnoreCase))
                {
                    group.SetProperty(XMakeProjectStrings.noWarn, noWarn);
                    changedProject = true;
                }
            }

            return changedProject;
        }

        /// <summary>
        /// This is the entry point method, which performs the project file format
        /// conversion.  This method will simply create a new MSBuild Project object
        /// in memory, instead of trying to write it to disk.
        /// </summary>
        public ProjectRootElement ConvertInMemory()
        {
            ConvertInMemoryToMSBuildProject();

            return xmakeProject;
        }

        /// <summary>
        /// This is the entry point method, which performs the project file format
        /// conversion.  This method will simply create a new MSBuild Project object
        /// in memory, instead of trying to write it to disk.
        /// </summary>
        /// <returns></returns>
        /// <owner>rgoel</owner>
        [Obsolete("Use parameterless ConvertInMemory() method instead")]
        public OldProject ConvertInMemory
            (
                OldEngine engine
            )
        {
            return ConvertInMemory(engine, ProjectLoadSettings.None);
        }

        /// <summary>
        /// This is the entry point method, which performs the project file format
        /// conversion.  This method will simply create a new MSBuild Project object
        /// in memory, instead of trying to write it to disk.
        /// </summary>
        /// <returns></returns>
        /// <owner>rgoel</owner>
        [Obsolete("Use parameterless ConvertInMemory() method instead")]
        public OldProject ConvertInMemory
            (
                OldEngine engine,
                ProjectLoadSettings projectLoadSettings
            )
        {
            this.ConvertInMemoryToMSBuildProject();

            OldProject oldProject = new OldProject(engine);

            using (StringReader reader = new StringReader(xmakeProject.RawXml))
            {
                oldProject.Load(reader);
            }

            return oldProject;
        }

        /// <summary>
        /// Takes an XML element from an Everett project file, and loops through
        /// all its attributes.  For each attribute, it adds a new XMake property
        /// to the destination project file in the property group passed in.
        /// </summary>
        /// <owner>RGoel</owner>
        private void AddXMakePropertiesFromXMLAttributes
            (
            ProjectPropertyGroupElement propertyGroup,
            XmlElement          xmlElement
            )
        {
            error.VerifyThrow(propertyGroup != null, "Expected valid ProjectPropertyElementGroup to add properties to.");

            foreach (XmlAttribute xmlAttribute in xmlElement.Attributes)
            {
                // Add this as a property to the MSBuild project file.  If the property is one of those
                // that contains an identifier or a path, we must escape it to treat it as a literal.
                string value = xmlAttribute.Value;
                if (this.propertiesToEscape.ContainsKey(xmlAttribute.Name))
                {
                    value = ProjectCollection.Escape(value);
                }

                propertyGroup.AddProperty(xmlAttribute.Name, value);
            }
        }

        /// <summary>
        /// Processes the &lt;VisualStudioProject&gt; XML element, and everything
        /// within it.  As it is doing this, it will add stuff to the xmakeProject.
        /// </summary>
        /// <owner>RGoel</owner>
        private void ProcessVisualStudioProjectElement
            (
            XmlElementWithLocation      visualStudioProjectElement
            )
        {
            // Make sure this is the <VisualStudioProject> element.
            error.VerifyThrow((visualStudioProjectElement?.Name == VSProjectElements.visualStudioProject),
                "Expected <VisualStudioProject> element.");

            // Make sure the caller has given us a valid xmakeProject object.
            error.VerifyThrow(xmakeProject != null, "Expected valid XMake project object.");

            // This is just about better error reporting.  Detect if the user tried
            // to convert a VC++ or some other type of project, and give a more friendly
            // error message.
            string projectType = visualStudioProjectElement.GetAttribute(VSProjectAttributes.projectType);
            ProjectErrorUtilities.VerifyThrowInvalidProject(string.IsNullOrEmpty(projectType),
                visualStudioProjectElement.Location, "ProjectTypeCannotBeConverted", projectType);

            // Make sure the <VisualStudioProject> tag doesn't have any attributes.
            ProjectErrorUtilities.VerifyThrowInvalidProject(!visualStudioProjectElement.HasAttributes,
                visualStudioProjectElement.Location, "NoAttributesExpected",
                VSProjectElements.visualStudioProject);

            bool languageFound = false;

            // Loop through all the direct children of the <VisualStudioProject> element.
            foreach(XmlNode visualStudioProjectChildNode in visualStudioProjectElement)
            {
                // Handle XML comments under the <VisualStudioProject> node (just ignore them)
                if ((visualStudioProjectChildNode.NodeType == XmlNodeType.Comment) ||
                    (visualStudioProjectChildNode.NodeType == XmlNodeType.Whitespace))
                {
                    continue;
                }

                if (visualStudioProjectChildNode.NodeType == XmlNodeType.Element)
                {
                    XmlElementWithLocation visualStudioProjectChildElement = (XmlElementWithLocation)visualStudioProjectChildNode;

                    switch (visualStudioProjectChildElement.Name)
                    {
                        // See if we have a <CSHARP>, <VisualBasic>, or <VISUALJSHARP> element.
                        case VSProjectElements.cSharp:
                        case VSProjectElements.visualJSharp:
                        case VSProjectElements.visualBasic:
                        case VSProjectElements.ECSharp:
                        case VSProjectElements.EVisualBasic:
                            // Make sure this is the first language node we're encountering.
                            ProjectErrorUtilities.VerifyThrowInvalidProject(!languageFound, visualStudioProjectChildElement.Location,
                                "MultipleLanguageNodesNotAllowed", VSProjectElements.visualStudioProject);

                            languageFound = true;
                            this.language = visualStudioProjectChildNode.Name;
                            this.ProcessLanguageElement((XmlElementWithLocation)visualStudioProjectChildElement);
                            break;

                        default:
                            ProjectErrorUtilities.VerifyThrowInvalidProject(false, visualStudioProjectChildElement.Location,
                                "UnrecognizedChildElement", visualStudioProjectChildElement.Name,
                                VSProjectElements.visualStudioProject);
                            break;
                    }
                }
                else
                {
                    ProjectXmlUtilities.ThrowProjectInvalidChildElement(visualStudioProjectChildNode.Name, visualStudioProjectElement.Name, visualStudioProjectElement.Location);
                }
            }
        }

        /// <summary>
        /// Processes the language (e.g. &lt;CSHARP&gt;) XML element, and everything
        /// within it.  As it is doing this, it will add stuff to the xmakeProject.
        /// </summary>
        /// <owner>RGoel</owner>
        private void ProcessLanguageElement
            (
            XmlElementWithLocation      languageElement
            )
        {
            // Make sure we have a valid XML element to process.
            error.VerifyThrow(languageElement != null, "Expected valid XML language element.");

            // Make sure the caller has given us a valid xmakeProject object.
            error.VerifyThrow(xmakeProject != null, "Expected valid XMake project object.");

            // Get the project instance GUID for this project file.  It is required for
            // the main project file, but not for the .USER file.
            this.projectGuid = languageElement.GetAttribute(VSProjectAttributes.projectGuid);
            ProjectErrorUtilities.VerifyThrowInvalidProject((this.projectGuid != null) || (this.isUserFile),
                languageElement.Location, "MissingAttribute", languageElement.Name, VSProjectAttributes.projectGuid);

            // Get the project type for this project file.  We only support "Local".  We do not
            // convert web projects -- that's Venus's job.
            string projectType = languageElement.GetAttribute(VSProjectAttributes.projectType);
            ProjectErrorUtilities.VerifyThrowInvalidProject(string.IsNullOrEmpty(projectType) ||
                (String.Compare(projectType, VSProjectAttributes.local, StringComparison.OrdinalIgnoreCase) == 0),
                languageElement.Location, "ProjectTypeCannotBeConverted", projectType);

            // All of the attributes on the language tag get converted to XMake
            // properties.  A couple exceptions ... for the "ProductVersion"
            // and "SchemaVersion" properties, we don't just copy the previous
            // value; we actually set it to 8.0.##### and 2.0 respectively.
            // In addition, we also add a default value for the "Configuration"
            // property.  For example,
            // -----------------------------------------------------------------------
            // Everett format:
            // ===============
            //    <CSHARP
            //        ProjectType = "Local"
            //        ProductVersion = "7.10.2284"
            //        SchemaVersion = "1.0"
            //        ProjectGuid = "{71F4C768-901B-4027-BD9D-378665D6C0B2}"
            //    >
            //        ...
            //        ...
            //        ...
            //    </CSHARP>
            // -----------------------------------------------------------------------
            // XMake format:
            // =============
            //    <PropertyGroup>
            //        <ProjectType>Local</ProjectType>
            //        <ProductVersion>8.0.31031</ProductVersion>
            //        <SchemaVersion>2.0</SchemaVersion>
            //        <ProjectGuid>{71F4C768-901B-4027-BD9D-378665D6C0B2}</ProjectGuid>
            //        <Configuration Condition = " '$(Configuration)' == '' ">Debug</Configuration>
            //    </PropertyGroup>
            // -----------------------------------------------------------------------
            // For Dev11, we are removing "ProductVersion" and "SchemaVersion" from all
            // project templates. Thus, eliminated writing these tags from this method.
            // -----------------------------------------------------------------------

            string originalMyType = languageElement.GetAttribute(XMakeProjectStrings.myType);
            if (!string.IsNullOrEmpty(originalMyType))
            {
                // Flag the fact that the Everett project already had a MyType property in there,
                // so we don't try to override it later.
                this.isMyTypeAlreadySetInOriginalProject = true;
            }

            // Copy over all the other properties.
            this.globalPropertyGroup = xmakeProject.AddPropertyGroup();
            this.AddXMakePropertiesFromXMLAttributes(this.globalPropertyGroup, languageElement);

            // Add the "Configuration" property.  Put a condition on it so it only gets
            // set to the default if the user doesn't have an environment variable called
            // "Configuration".  The final XML looks something like this:
            //        <Configuration Condition = " '$(Configuration)' == '' ">Debug</Configuration>
            ProjectPropertyElement configurationProperty = this.globalPropertyGroup.AddProperty(
                XMakeProjectStrings.configuration, XMakeProjectStrings.defaultConfiguration);
            configurationProperty.Condition = XMakeProjectStrings.configurationPrefix +
                XMakeProjectStrings.configurationSuffix;

            // Add the "Platform" property.  Put a condition on it so it only gets
            // set to the default if the user doesn't have an environment variable called
            // "Platform".  The final XML looks something like this:
            //        <Property Platform = "AnyCPU" Condition = " '$(Platform)' == '' " />
            // Platform of course depends on the language we are dealing with - J# in whidbey supports only x86
            string platform = (this.language != VSProjectElements.visualJSharp)
                ? XMakeProjectStrings.defaultPlatform
                : XMakeProjectStrings.x86Platform;
            ProjectPropertyElement platformProperty = this.globalPropertyGroup.AddProperty(
                XMakeProjectStrings.platform, platform);
            platformProperty.Condition = XMakeProjectStrings.platformPrefix +
                XMakeProjectStrings.platformSuffix;

            bool isTriumphProject = false;

            // For SDE projects, we need to add a special <ProjectTypeGuids> property to
            // the project file.  This will contain the project types for both the
            // flavor and the main language project type.  In addition, SDE projects
            // need to have the host process disabled.
            if (!this.isUserFile)
            {
                if (languageElement.Name == VSProjectElements.ECSharp)
                {
                    this.globalPropertyGroup.AddProperty ( XMakeProjectStrings.projectTypeGuids,
                                                              "{" +
                                                              XMakeProjectStrings.VSDCSProjectTypeGuid +
                                                              "};{" +
                                                              XMakeProjectStrings.cSharpGuid +
                                                              "}" );
                    string visualStudioProjectExtensions =  GetProjectExtensionsString(XMakeProjectStrings.visualStudio);
                    visualStudioProjectExtensions += XMakeProjectStrings.disableCSHostProc;
                    SetProjectExtensionsString(XMakeProjectStrings.visualStudio, visualStudioProjectExtensions);
                }
                else if (languageElement.Name == VSProjectElements.EVisualBasic)
                {
                    this.globalPropertyGroup.AddProperty ( XMakeProjectStrings.projectTypeGuids,
                                                              "{" +
                                                              XMakeProjectStrings.VSDVBProjectTypeGuid +
                                                              "};{" +
                                                              XMakeProjectStrings.visualBasicGuid +
                                                              "}" );
                    string visualStudioProjectExtensions = GetProjectExtensionsString(XMakeProjectStrings.visualStudio);
                    visualStudioProjectExtensions += XMakeProjectStrings.disableVBHostProc;
                    SetProjectExtensionsString(XMakeProjectStrings.visualStudio, visualStudioProjectExtensions);
                }
            }

            // Loop through all the direct child elements of the language element.
            foreach(XmlNode languageChildNode in languageElement)
            {
                // Handle XML comments under the the language node (just ignore them).
                if ((languageChildNode.NodeType == XmlNodeType.Comment) ||
                    (languageChildNode.NodeType == XmlNodeType.Whitespace))
                {
                    continue;
                }

                if (languageChildNode.NodeType == XmlNodeType.Element)
                {
                    XmlElementWithLocation languageChildElement = (XmlElementWithLocation)languageChildNode;

                    switch (languageChildElement.Name)
                    {
                        // The <Build> element.
                        case VSProjectElements.build:
                            this.ProcessBuildElement((XmlElementWithLocation)languageChildElement);
                            break;

                        case VSProjectElements.files:
                            this.ProcessFilesElement((XmlElementWithLocation)languageChildElement);
                            break;

                        case VSProjectElements.startupServices:
                            this.ProcessStartupServicesElement((XmlElementWithLocation)languageChildElement);
                            break;

                        case VSProjectElements.userProperties:
                            this.ProcessUserPropertiesElement((XmlElementWithLocation)languageChildElement, out isTriumphProject);
                            break;

                        case VSProjectElements.otherProjectSettings:
                            this.ProcessOtherProjectSettingsElement((XmlElementWithLocation)languageChildElement);
                            break;

                        default:
                            ProjectErrorUtilities.VerifyThrowInvalidProject(false, languageChildElement.Location,
                                "UnrecognizedChildElement", languageChildNode.Name,
                                languageElement.Name);
                            break;
                    }
                }
                else
                {
                    ProjectXmlUtilities.ThrowProjectInvalidChildElement(languageChildNode.Name, languageElement.Name, languageElement.Location);
                }
            }

            AddFinalPropertiesAndImports(languageElement, isTriumphProject);
        }

        /// <summary>
        /// Adds any last-minute additional properties such as FileUpgradeFlags and MyType,
        /// and also adds in the necessary Import tags.
        /// </summary>
        /// <param name="languageElement"></param>
        /// <param name="isTriumphProject"></param>
        private void AddFinalPropertiesAndImports(XmlElementWithLocation languageElement, bool isTriumphProject)
        {
            // For the main project file only, add a line at the end of the new XMake
            // project file to import the appropriate .TARGETS file.
            if (!this.isUserFile)
            {
                // We set a property called "FileUpgradeFlags", so that for command-line conversions,
                // if this project is ever loaded into the IDE, the file upgrade (.VB code, etc.) will kick in.
                // The "20" means SxS upgrade.  For IDE conversions, the project system will itself set
                // this property immediately after the MSBuild conversion returns, so this value will
                // be overwritten.
                this.globalPropertyGroup.AddProperty(XMakeProjectStrings.fileUpgradeFlags, "20");

                // VisualBasic projects need MyType set.
                if
                (
                    (
                        (this.language == VSProjectElements.visualBasic) ||
                        (
                            (this.language == VSProjectElements.EVisualBasic) &&
                            (this.frameworkVersionForVSD == XMakeProjectStrings.vTwo)
                        )
                    ) &&
                    (!this.isMyTypeAlreadySetInOriginalProject) &&
                    !isTriumphProject        // Doesn't apply to Triumph->Trinity conversions.
                )
                {
                    if (!string.IsNullOrEmpty(this.outputType))
                    {
                        if (String.Equals(this.outputType, XMakeProjectStrings.winExe, StringComparison.OrdinalIgnoreCase))
                        {
                            if (this.hasWindowsFormsReference)
                            {
                                // Only applies if there's a System.Windows.Forms reference.
                                this.globalPropertyGroup.AddProperty(XMakeProjectStrings.myType, XMakeProjectStrings.windowsFormsWithCustomSubMain);
                            }
                            else
                            {
                                this.globalPropertyGroup.AddProperty(XMakeProjectStrings.myType, XMakeProjectStrings.console);
                            }
                        }
                        else if (String.Equals(this.outputType, XMakeProjectStrings.exe, StringComparison.OrdinalIgnoreCase))
                        {
                            this.globalPropertyGroup.AddProperty(XMakeProjectStrings.myType, XMakeProjectStrings.console);
                        }
                        else if (String.Equals(this.outputType, XMakeProjectStrings.library, StringComparison.OrdinalIgnoreCase))
                        {
                            this.globalPropertyGroup.AddProperty(XMakeProjectStrings.myType, XMakeProjectStrings.windows);
                        }
                    }
                }
                else if (this.language == VSProjectElements.EVisualBasic)
                {
                    // For Devices, we always want a MyType of "Empty," as projects
                    //   are converted into v1 .NETCF, which doesn't support My.NET
                    this.globalPropertyGroup.AddProperty(XMakeProjectStrings.myType, XMakeProjectStrings.empty);
                }

                // We need to handle the SDE scenarios for C# and VB
                if (languageElement.Name == VSProjectElements.ECSharp)
                {
                    xmakeProject.AddImport(XMakeProjectStrings.importPrefix + XMakeProjectStrings.SDECSTargets);
                }
                else if (languageElement.Name == VSProjectElements.EVisualBasic)
                {
                    xmakeProject.AddImport(XMakeProjectStrings.importPrefix + XMakeProjectStrings.SDEVBTargets);
                }
                else if (languageElement.Name == VSProjectElements.cSharp)
                {
                    xmakeProject.AddImport(XMakeProjectStrings.importPrefix + XMakeProjectStrings.targetsFilenamePrefix + XMakeProjectStrings.csharpTargets + XMakeProjectStrings.importSuffix);
                }
                else if (languageElement.Name == VSProjectElements.visualBasic)
                {
                    xmakeProject.AddImport(XMakeProjectStrings.importPrefix + XMakeProjectStrings.targetsFilenamePrefix + XMakeProjectStrings.visualBasicTargets + XMakeProjectStrings.importSuffix);
                }
                else if (languageElement.Name == VSProjectElements.visualJSharp)
                {
                    xmakeProject.AddImport(XMakeProjectStrings.importPrefix + XMakeProjectStrings.targetsFilenamePrefix + XMakeProjectStrings.visualJSharpTargets + XMakeProjectStrings.importSuffix);
                }
                else
                {
                    xmakeProject.AddImport(XMakeProjectStrings.importPrefix + XMakeProjectStrings.targetsFilenamePrefix + languageElement.Name + XMakeProjectStrings.importSuffix);
                }

                // [ancrider] VSTO project migration will handle the import target changes.
                //if (isTriumphProject)
                //{
                //    xmakeProject.AddImport(XMakeProjectStrings.triumphImport, null);
                //}

                // Also add the PreBuildEvent and PostBuildEvent properties to the end
                // of the project file.  The reason is that they can contain embedded
                // macros that are defined in the .TARGETS file that was imported
                // above.
                if ((this.preBuildEvent != null) || (this.postBuildEvent != null))
                {
                    // In this case, we specifically need the property group at the end, so we can't just call AddPropertyGroup(..),
                    // but instead must do it ourselves
                    ProjectPropertyGroupElement preAndPostBuildEvents = xmakeProject.CreatePropertyGroupElement();
                    xmakeProject.AppendChild(preAndPostBuildEvents);

                    // Add the "PreBuildEvent" property.
                    if (this.preBuildEvent != null)
                    {
                        // We must escape the percent-sign in order to handle cases like
                        // "echo %DEBUGGER%".  We don't want MSBuild to treat the "%DE" as
                        // an escaped character.
                        preAndPostBuildEvents.AddProperty(VSProjectAttributes.preBuildEvent,
                            this.preBuildEvent.Replace("%", "%25"));
                    }

                    // Add the "PostBuildEvent" property.
                    if (this.postBuildEvent != null)
                    {
                        // We must escape the percent-sign in order to handle cases like
                        // "echo %DEBUGGER%".  We don't want MSBuild to treat the "%DE" as
                        // an escaped character.
                        preAndPostBuildEvents.AddProperty(VSProjectAttributes.postBuildEvent,
                            this.postBuildEvent.Replace("%", "%25"));
                    }
                }
            }
        }

        /// <summary>
        /// Processes the &lt;Build&gt; element, and everything within it.  As it is
        /// doing this, it will add stuff to the xmakeProject.
        /// </summary>
        /// <owner>RGoel</owner>
        private void ProcessBuildElement
            (
            XmlElementWithLocation      buildElement
            )
        {
            // Make sure this is the <Build> element.
            error.VerifyThrow((buildElement?.Name == VSProjectElements.build), "Expected <Build> element.");

            // Make sure the caller has given us a valid xmakeProject object.
            error.VerifyThrow(xmakeProject != null, "Expected valid XMake project object.");

            // Make sure the caller has given us a valid globalPropertyGroup object.
            error.VerifyThrow(globalPropertyGroup != null, "Expected valid global ProjectPropertyElementGroup.");

            // The <Build> element should not have any attributes on it.
            ProjectErrorUtilities.VerifyThrowInvalidProject(!buildElement.HasAttributes, buildElement.Location,
                "NoAttributesExpected", VSProjectElements.build);

            // Loop through all the direct child elements of the <Build> element.
            foreach(XmlNode buildChildNode in buildElement)
            {
                // Handle XML comments under the the <Build> node (just ignore them).
                if ((buildChildNode.NodeType == XmlNodeType.Comment) ||
                    (buildChildNode.NodeType == XmlNodeType.Whitespace))
                {
                    continue;
                }

                if (buildChildNode.NodeType == XmlNodeType.Element)
                {
                    XmlElementWithLocation buildChildElement = (XmlElementWithLocation)buildChildNode;
                    switch (buildChildElement.Name)
                    {
                        // The <Settings> element.
                        case VSProjectElements.settings:
                            this.ProcessSettingsElement((XmlElementWithLocation)buildChildElement);
                            break;

                        // The <References> element.
                        case VSProjectElements.references:
                            this.ProcessReferencesElement((XmlElementWithLocation)buildChildElement);
                            break;

                        // The <Imports> element.
                        case VSProjectElements.imports:
                            this.ProcessImportsElement((XmlElementWithLocation)buildChildElement);
                            break;

                        default:
                            ProjectErrorUtilities.VerifyThrowInvalidProject(false, buildChildElement.Location,
                                "UnrecognizedChildElement", buildChildNode.Name,
                                VSProjectElements.build);
                            break;
                    }
                }
                else
                {
                    ProjectXmlUtilities.ThrowProjectInvalidChildElement(buildChildNode.Name, buildElement.Name, buildElement.Location);
                }
            }
        }

        /// <summary>
        /// Processes the &lt;Settings&gt; element, and everything within it.  As it is
        /// doing this, it will add stuff to the xmakeProject.
        /// </summary>
        /// <owner>RGoel</owner>
        private void ProcessSettingsElement
            (
            XmlElementWithLocation      settingsElement
            )
        {
            // Make sure this is the <Settings> element.
            error.VerifyThrow((settingsElement?.Name == VSProjectElements.settings),
                "Expected <Settings> element.");

            // Make sure the caller has given us a valid xmakeProject object.
            error.VerifyThrow(xmakeProject != null, "Expected valid XMake project object.");

            // Make sure the caller has given us a valid globalPropertyGroup object.
            error.VerifyThrow(globalPropertyGroup != null, "Expected valid global ProjectPropertyElementGroup.");

            // All of the attributes on the <Settings> tag get converted to XMake
            // properties, except for PreBuildEvent and PostBuildEvent.  For example,
            // -----------------------------------------------------------------------
            // Everett format:
            // ===============
            //      <Settings
            //          ApplicationIcon = ""
            //          AssemblyKeyContainerName = ""
            //          AssemblyName = "XMakeBuildEngine"
            //          AssemblyOriginatorKeyFile = ""
            //          DefaultClientScript = "JScript"
            //          DefaultHTMLPageLayout = "Grid"
            //          DefaultTargetSchema = "IE50"
            //          DelaySign = "false"
            //          OutputType = "Library"
            //          PreBuildEvent = ""
            //          PostBuildEvent = "..\..\PostBuildEvent.bat"
            //          RootNamespace = "XMakeBuildEngine"
            //          RunPostBuildEvent = "OnBuildSuccess"
            //          StartupObject = ""
            //      >
            //          ...
            //          ...
            //          ...
            //      </Settings>
            // -----------------------------------------------------------------------
            // XMake format:
            // =============
            //    <PropertyGroup>
            //        <ApplicationIcon></ApplicationIcon>
            //        <AssemblyKeyContainerName></AssemblyKeyContainerName>
            //        <AssemblyName>XMakeBuildEngine</AssemblyName>
            //        <AssemblyOriginatorKeyFile></AssemblyOriginatorKeyFile>
            //        <DefaultClientScript>JScript</DefaultClientScript>
            //        <DefaultHTMLPageLayout>Grid</DefaultHTMLPageLayout>
            //        <DefaultTargetSchema>IE50</DefaultTargetSchema>
            //        <DelaySign>false</DelaySign>
            //        <OutputType>Library</OutputType>
            //        <RootNamespace>XMakeBuildEngine</RootNamespace>
            //        <RunPostBuildEvent>OnBuildSuccess</RunPostBuildEvent>
            //        <StartupObject></StartupObject>
            //    </PropertyGroup>
            // -----------------------------------------------------------------------

            // The "PreBuildEvent" and "PostBuildEvent" properties need to be handled
            // specially.  These can contain references to predefined macros, such
            // as "$(ProjectDir)".  But these get defined in Microsoft.CSharp.targets, so the
            // "PreBuildEvent" and "PostBuildEvent" properties need to get added to
            // the project file *after* the <Import> for Microsoft.CSharp.targets.  For now,
            // just save the values of these two properties.
            this.preBuildEvent = settingsElement.GetAttribute(VSProjectAttributes.preBuildEvent);
            settingsElement.RemoveAttribute(VSProjectAttributes.preBuildEvent);
            this.postBuildEvent = settingsElement.GetAttribute(VSProjectAttributes.postBuildEvent);
            settingsElement.RemoveAttribute(VSProjectAttributes.postBuildEvent);

            // cache the assembly name in case its needed to upgrade the
            // documentation file property)
            this.assemblyName = settingsElement.GetAttribute(VSProjectAttributes.assemblyName);

            // cache the output type.
            this.outputType = settingsElement.GetAttribute(XMakeProjectStrings.outputType);

            // Take care of copying all the other normal properties.
            this.AddXMakePropertiesFromXMLAttributes(this.globalPropertyGroup, settingsElement);

            // Loop through all the direct child elements of the <Build> element.
            foreach(XmlNode settingsChildNode in settingsElement)
            {
                // Handle XML comments under the the <Settings> node (just ignore them).
                if ((settingsChildNode.NodeType == XmlNodeType.Comment) ||
                    (settingsChildNode.NodeType == XmlNodeType.Whitespace))
                {
                    continue;
                }

                if (settingsChildNode.NodeType == XmlNodeType.Element)
                {
                    XmlElementWithLocation settingsChildElement = (XmlElementWithLocation)settingsChildNode;
                    switch (settingsChildElement.Name)
                    {
                        // The <Config> element.
                        case VSProjectElements.config:
                            this.ProcessConfigElement(settingsChildElement);
                            break;

                        // In the case of a VSD project, the <Platform> element
                        case VSProjectElements.platform:
                            this.ProcessPlatformElement(settingsChildElement);
                            break;

                        default:
                            ProjectErrorUtilities.VerifyThrowInvalidProject(false, settingsChildElement.Location,
                                "UnrecognizedChildElement", settingsChildElement.Name,
                                VSProjectElements.settings);
                            break;
                    }
                }
                else
                {
                    ProjectXmlUtilities.ThrowProjectInvalidChildElement(settingsChildNode.Name, settingsElement.Name, settingsElement.Location);
                }
            }
        }

        /// <summary>
        /// Processes the &lt;Config&gt; element, and everything within it.  As it is
        /// doing this, it will add stuff to the xmakeProject, including new
        /// configuration-specific property groups.
        /// </summary>
        /// <owner>RGoel</owner>
        private void ProcessConfigElement
            (
            XmlElementWithLocation      configElement
            )
        {
            // Make sure this is the <Config> element.
            error.VerifyThrow((configElement?.Name == VSProjectElements.config),
                "Expected <Config> element.");

            // Make sure the caller has given us a valid xmakeProject object.
            error.VerifyThrow(xmakeProject != null, "Expected valid XMake project object.");

            // All of the attributes on the <Config> tag get converted to XMake
            // properties, except for the "Name" attribute which becomes part of
            // the "Condition" on the <PropertyGroup>.  For example,
            // -----------------------------------------------------------------------
            // Everett format:
            // ===============
            //    <Config
            //        Name = "Debug"
            //        AllowUnsafeBlocks = "false"
            //        BaseAddress = "285212672"
            //        CheckForOverflowUnderflow = "false"
            //        ConfigurationOverrideFile = ""
            //        DefineConstants = "DEBUG;TRACE"
            //        DocumentationFile = ""
            //        DebugSymbols = "true"
            //        FileAlignment = "4096"
            //        IncrementalBuild = "true"
            //        NoStdLib = "false"
            //        NoWarn = ""
            //        Optimize = "false"
            //        OutputPath = "bin\Debug\"
            //        RegisterForComInterop = "false"
            //        RemoveIntegerChecks = "false"
            //        TreatWarningsAsErrors = "true"
            //        WarningLevel = "4"
            //    />
            // -----------------------------------------------------------------------
            // XMake format:
            // =============
            //    <PropertyGroup Condition=" '$(Configuration)|$(Platform)' == 'Debug|AnyCPU' ">
            //        <AllowUnsafeBlocks>false</AllowUnsafeBlocks>
            //        <BaseAddress>285212672</BaseAddress>
            //        <CheckForOverflowUnderflow>false</CheckForOverflowUnderflow>
            //        <ConfigurationOverrideFile></ConfigurationOverrideFile>
            //        <DefineConstants>DEBUG;TRACE</DefineConstants>
            //        <DocumentationFile></DocumentationFile>
            //        <DebugSymbols>true</DebugSymbols>
            //        <FileAlignment>4096</FileAlignment>
            //        <NoStdLib>false</NoStdLib>
            //        <NoWarn></NoWarn>
            //        <Optimize>false</Optimize>
            //        <OutputPath>bin\Debug\</OutputPath>
            //        <RegisterForComInterop>false</RegisterForComInterop>
            //        <RemoveIntegerChecks>false</RemoveIntegerChecks>
            //        <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
            //        <WarningLevel>4</WarningLevel>
            //    </PropertyGroup>
            // -----------------------------------------------------------------------

            // Get the "Name" attribute of the <Config> element.
            string configName = configElement.GetAttribute(VSProjectAttributes.name);
            ProjectErrorUtilities.VerifyThrowInvalidProject(!string.IsNullOrEmpty(configName),
                configElement.Location, "MissingAttribute", VSProjectElements.config, VSProjectAttributes.name);

            // In the case of VSD projects, the "Name" attribute will have a pipe in it,
            // followed by the device platform.  This last part needs to be removed,
            // leaving just the config name.
            if ( ( this.language == VSProjectElements.ECSharp ) ||
                 ( this.language == VSProjectElements.EVisualBasic ) )
            {
                int pipeLocation = configName.IndexOf ( '|' );
                if ( pipeLocation != -1 )
                {
                    configName = configName.Remove ( pipeLocation,
                                                     configName.Length - pipeLocation );
                }
            }

            // Remove the "Name" attribute from the <Config> element, so it doesn't get
            // added as an XMake property.
            configElement.RemoveAttribute(VSProjectAttributes.name);

            // PPD@31111: J# Only: We need to remove the AdditionalOptions attribute
            // (and note it down) before we create the property group out of the configElement
            string additionalOptionsValue = null;
            if (VSProjectElements.visualJSharp == this.language)
            {
                additionalOptionsValue = configElement.GetAttribute(VSProjectAttributes.additionalOptions);
                // Dont bother about getting a null value for additionalOptionsValue
                // GetAttribute return String.Empty if the attribute is not present
                configElement.RemoveAttribute(VSProjectAttributes.additionalOptions);
            }

            // Create a new property group, and add all of the XML attributes as XMake
            // properties.
            ProjectPropertyGroupElement configPropertyGroup = xmakeProject.AddPropertyGroup();

            // Process OutputPath attribute separately to ensure it contains trailing backslash
            string outputPath = configElement.GetAttribute(VSProjectAttributes.outputPath);
            if (!string.IsNullOrEmpty(outputPath))
            {
                if (outputPath[outputPath.Length-1] != Path.DirectorySeparatorChar)
                    outputPath += Path.DirectorySeparatorChar;

                configElement.RemoveAttribute(VSProjectAttributes.outputPath);
                configPropertyGroup.AddProperty(VSProjectAttributes.outputPath, ProjectCollection.Escape(outputPath));
            }

            // If the "SelectedDevice" or "DeploymentPlatform" attributes exist in the per-user
            //   project file, we should get rid of them.
            string selectedDevice = configElement.GetAttribute ( VSProjectAttributes.selectedDevice );
            if (isUserFile && (selectedDevice?.Length > 0))
            {
                configElement.RemoveAttribute ( VSProjectAttributes.selectedDevice );
            }

            string deploymentPlatform = configElement.GetAttribute ( VSProjectAttributes.deploymentPlatform );
            if (isUserFile && (deploymentPlatform?.Length > 0))
            {
                configElement.RemoveAttribute ( VSProjectAttributes.deploymentPlatform );
            }

            // Get rid of the "IncrementalBuild" attribute
            string incrementalBuild = configElement.GetAttribute ( VSProjectAttributes.incrementalBuild );
            if (!string.IsNullOrEmpty(incrementalBuild))
            {
                configElement.RemoveAttribute ( VSProjectAttributes.incrementalBuild );
            }

            // VSWhidbey bug 261464.  For VB projects migrated from VS7/Everett, the VB team would
            // like to enable XML documentation by default (this feature was unavailable to VB users
            // in VS7/Everett. To enable for VB, set the DocumentationFile property to <assemblyname>.xml
            if ((!this.isUserFile) && (VSProjectElements.visualBasic == this.language))
            {
                string documentationFile = this.assemblyName + XMakeProjectStrings.xmlFileExtension;
                configPropertyGroup.AddProperty(VSProjectAttributes.documentationFile, ProjectCollection.Escape(documentationFile));
            }

            // process the rest of Config attributes
            this.AddXMakePropertiesFromXMLAttributes(configPropertyGroup, configElement);

            // PPD@31111: J# Only: We now need to parse the additionalOptionsValue for properties and
            // add the individual properties to configPropertyGroup.
            // This needs to be done after the AddXMakePropertiesFromXMLAttributes call above since
            // an property defined in the AdditionalOptions takes precedence.
            if (VSProjectElements.visualJSharp == this.language)
            {
                AdditionalOptionsParser addnlOptParser = new AdditionalOptionsParser();
                addnlOptParser.ProcessAdditionalOptions(additionalOptionsValue, configPropertyGroup);
            }

            // VSWhidbey bug 302946.  For VB projects migrated from VS7/Everett, the VB team would
            // like to disable the following new warnings for Whidbey:  42016,42017,42018,42019,42032
            // New projects created in Whidbey already have these warnings disabled by default.
            if ((!this.isUserFile) && (VSProjectElements.visualBasic == this.language))
            {
                configPropertyGroup.AddProperty(XMakeProjectStrings.noWarn, XMakeProjectStrings.disabledVBWarnings);
            }

            // VSWhidbey bug 472064.  For all projects that are converted, if "DebugSymbols" is set for a
            // particular platform/configuration, we set a "DebugType" property if and only if "DebugType" property
            // is not already there.  DebugType is set to "full" for DebugSymbols=true, DebugType is set to "none"
            // if DebugSymbols=false, and we don't do anything if DebugSymbols is not present in the source project.
            if (!this.isUserFile)
            {
                string debugType = configElement.GetAttribute(VSProjectAttributes.debugType);
                if (String.IsNullOrEmpty(debugType))
                {
                    string debugSymbols = configElement.GetAttribute(XMakeProjectStrings.debugSymbols);
                    if (  String.Equals ( debugSymbols, "true", StringComparison.OrdinalIgnoreCase ) )
                    {
                        configPropertyGroup.AddProperty(VSProjectAttributes.debugType, VSProjectAttributes.debugTypeFull);
                    }
                    else if ( String.Equals(debugSymbols, "false", StringComparison.OrdinalIgnoreCase) )
                    {
                        configPropertyGroup.AddProperty(VSProjectAttributes.debugType, VSProjectAttributes.debugTypeNone);
                    }
                }
            }

            // VSWhidbey bug 472064.  For all VC# projects that are converted, we add an ErrorReport
            // property, always set to "prompt"
            if ( !this.isUserFile && this.language == VSProjectElements.cSharp )
            {
                configPropertyGroup.AddProperty(VSProjectAttributes.errorReport, VSProjectAttributes.errorReportPrompt);
            }

            // Platform of course depends on the language we are dealing with - J# in whidbey supports only x86
            string platform = (this.language != VSProjectElements.visualJSharp)
                ? XMakeProjectStrings.defaultPlatform
                : XMakeProjectStrings.x86Platform;

            // Add the "Condition" to the new <PropertyGroup>.
            configPropertyGroup.Condition = XMakeProjectStrings.configplatformPrefix +
                ProjectCollection.Escape(configName) + XMakeProjectStrings.configplatformSeparator +
                ProjectCollection.Escape(platform) + XMakeProjectStrings.configplatformSuffix;

            // Loop through all the direct child elements of the <Config> element.
            foreach(XmlNode configChildNode in configElement)
            {
                // Handle XML comments under the the <Config> node (just ignore them).
                if ((configChildNode.NodeType == XmlNodeType.Comment) ||
                    (configChildNode.NodeType == XmlNodeType.Whitespace))
                {
                    continue;
                }

                if (configChildNode.NodeType == XmlNodeType.Element)
                {
                    XmlElementWithLocation configChildElement = (XmlElementWithLocation)configChildNode;
                    switch (configChildElement.Name)
                    {
                        // The <InteropRegistration> element.
                        case VSProjectElements.interopRegistration:
                            this.ProcessInteropRegistrationElement((XmlElementWithLocation)configChildElement, configPropertyGroup);
                            break;

                        default:
                            ProjectErrorUtilities.VerifyThrowInvalidProject(false, configChildElement.Location,
                                "UnrecognizedChildElement", configChildElement.Name,
                                VSProjectElements.config);
                            break;
                    }
                }
                else
                {
                    ProjectXmlUtilities.ThrowProjectInvalidChildElement(configChildNode.Name, configElement.Name, configElement.Location);
                }
            }
        }

        /// <summary>
        /// Processes the &lt;Platform&gt; element, and everything within it.  As it is
        /// doing this, it will add stuff to the xmakeProject, including new
        /// configuration-specific property groups.
        /// </summary>
        /// <owner>BCham</owner>
        private void ProcessPlatformElement
            (
            XmlElementWithLocation      platformElement
            )
        {
            if ( !IsUserFile )
            {
                // Make sure this is the <Platform> element.
                error.VerifyThrow((platformElement?.Name == VSProjectElements.platform),
                    "Expected <Platform> element.");

                // Make sure the caller has given us a valid xmakeProject object.
                error.VerifyThrow(xmakeProject != null, "Expected valid XMake project object.");

                // The platform listed in the <Platform> element will be the platform
                // used for the Whidbey project.
                // -----------------------------------------------------------------------
                // Everett format:
                // ===============
                //    <Platform Name = "Pocket PC" />
                // -----------------------------------------------------------------------
                // XMake format:
                // =============
                //    <PropertyGroup>
                //      <Property PlatformFamilyName="PocketPC"/>
                //      <Property PlatformID="3C41C503-53EF-4c2a-8DD4-A8217CAD115E"/>
                //    </PropertyGroup>
                // -----------------------------------------------------------------------

                // Get the "Name" attribute of the <Platform> element.
                platformForVSD = platformElement.GetAttribute(VSProjectAttributes.name);
                ProjectErrorUtilities.VerifyThrowInvalidProject(!string.IsNullOrEmpty(platformForVSD),
                    platformElement.Location, "MissingAttribute", VSProjectElements.platform, VSProjectAttributes.name);

                // Create a new property group, and add all of the XML attributes as XMake
                // properties.
                ProjectPropertyGroupElement platformPropertyGroup = xmakeProject.AddPropertyGroup();

                string platformID;
                string platformFamily;

                frameworkVersionForVSD = XMakeProjectStrings.vOne;

                switch ( platformForVSD )
                {
                    case VSProjectElements.PocketPC:
                        platformID = "3C41C503-53EF-4c2a-8DD4-A8217CAD115E";
                        platformFamily = "PocketPC";
                        break;

                    case VSProjectElements.Smartphone:
                        platformID = "4DE813A2-67E0-4a00-945C-3188240A8243";
                        platformFamily = "Smartphone";
                        break;

                    case VSProjectElements.WindowsCE:
                    default:

                        // If we're dealing with a platform other than the three that Everett ships with, we'll assign it as Windows CE

                        platformID = "E2BECB1F-8C8C-41ba-B736-9BE7D946A398";
                        platformFamily = "WindowsCE";

                        // We don't ship with a v1.0 WindowsCE platform.  Default to v2.0 instead.

                        frameworkVersionForVSD = XMakeProjectStrings.vTwo;
                        break;
                }

                // Add the properties for PlatformID and PlatformFamilyName

                platformPropertyGroup.AddProperty(XMakeProjectStrings.platformID, platformID);
                platformPropertyGroup.AddProperty(XMakeProjectStrings.platformFamilyName, platformFamily);

                // Since we're here, we know this is a VSD project.  Therefore, let's
                //   add a property for the deployment target path.  Note, we only need a suffix.
                //   The prefix will be defaulted to based on the selected device.

                platformPropertyGroup.AddProperty(XMakeProjectStrings.deployTargetSuffix, "$(AssemblyName)" );

                // And, we should also set the Target Framework version.  For
                //   VSD projects, we want to stay with v1.0

                platformPropertyGroup.AddProperty(XMakeProjectStrings.TargetFrameworkVersion, frameworkVersionForVSD);
            }
        }

        /// <summary>
        /// Processes the &lt;InteropRegistration&gt; element, and everything within it.
        /// As it is doing this, it will add extra properties to the configuration's
        /// property group.
        /// </summary>
        /// <owner>RGoel</owner>
        private void ProcessInteropRegistrationElement
            (
            XmlElementWithLocation      interopRegistrationElement,
            ProjectPropertyGroupElement configPropertyGroup
            )
        {
            // Make sure this is the <InteropRegistration> element.
            error.VerifyThrow((interopRegistrationElement?.Name == VSProjectElements.interopRegistration),
                "Expected <InteropRegistration> element.");

            // Make sure we've been given a valid configuration property group.
            error.VerifyThrow(configPropertyGroup != null,
                "Expected configuration's property group.");

            // All of the attributes on the <InteropRegistration> tag get converted to XMake
            // properties.  For example,
            // -----------------------------------------------------------------------
            // Everett format:
            // ===============
            //    <Config
            //        ...
            //        ...
            //        ... <all other configuration properties>
            //        ...
            //        ...
            //    >
            //        <InteropRegistration
            //            RegisteredComClassic = "true"
            //            RegisteredOutput = "Project1.dll"
            //            RegisteredTypeLib = "Project1.tlb"
            //        />
            //    </Config>
            // -----------------------------------------------------------------------
            // XMake format:
            // =============
            //    <PropertyGroup Condition=" '$(Configuration)' == 'Debug' ">
            //        ...
            //        ...
            //        ... <all other configuration properties>
            //        ...
            //        ...
            //        <RegisteredComClassic>true</RegisteredComClassic>
            //        <RegisteredOutput>Project1.dll</RegisteredOutput>
            //        <RegisteredTypeLib>Project1.tlb</RegisteredTypeLib>
            //    </PropertyGroup>
            // -----------------------------------------------------------------------
            this.AddXMakePropertiesFromXMLAttributes(configPropertyGroup, interopRegistrationElement);

            // There should be no children of the <InteropRegistration> element.
            ProjectXmlUtilities.VerifyThrowProjectNoChildElements(interopRegistrationElement);
        }

        /// <summary>
        /// Processes the &lt;References&gt; element, and everything within it.  As it is
        /// doing this, it will add reference items to a new ProjectItemGroupElement.
        /// </summary>
        /// <owner>RGoel</owner>
        private void ProcessReferencesElement
            (
            XmlElementWithLocation      referencesElement
            )
        {
            // Make sure this is the <References> element.
            error.VerifyThrow((referencesElement?.Name == VSProjectElements.references),
                "Expected <References> element.");

            // Make sure the caller has given us a valid xmakeProject object.
            error.VerifyThrow(xmakeProject != null, "Expected valid XMake project object.");

            // The <References> tag should have no attributes.
            ProjectErrorUtilities.VerifyThrowInvalidProject(!referencesElement.HasAttributes, referencesElement.Location,
                "NoAttributesExpected", VSProjectElements.references);

            // Before we begin processing the individual references, let's make sure
            // we have an SLN file, so we can go look up the project-to-project references.
            // If the caller has not provided us with an SLN file, or if the SLN provided
            // doesn't actually exist on disk yet (which can happen in VS IDE scenarios because
            // the SLN is only in-memory and hasn't been saved yet), then we search for the
            // SLN using a heuristic.
            if (this.solutionFile == null || !File.Exists(this.solutionFile))
            {
                this.solutionFile = null;
                this.SearchForSolutionFile();
            }
            else
            {
                // We've been given a valid SLN file that exists on disk, so just parse
                // it.
                this.solution = new SolutionFile();
                this.solution.FullPath = this.solutionFile;
                this.solution.ParseSolutionFileForConversion();
                this.conversionWarnings.AddRange(this.solution.SolutionParserWarnings);
            }

            ProjectItemGroupElement referencesItemGroup = null;

            // Loop through all the direct child elements of the <References> element.
            foreach(XmlNode referencesChildNode in referencesElement)
            {
                // Handle XML comments under the the <References> node (just ignore them).
                if ((referencesChildNode.NodeType == XmlNodeType.Comment) ||
                    (referencesChildNode.NodeType == XmlNodeType.Whitespace))
                {
                    continue;
                }

                if (referencesChildNode.NodeType == XmlNodeType.Element)
                {
                    XmlElementWithLocation referencesChildElement = (XmlElementWithLocation)referencesChildNode;
                    switch (referencesChildElement.Name)
                    {
                        // The <Reference> element.
                        case VSProjectElements.reference:
                            if (referencesItemGroup == null)
                            {
                                referencesItemGroup = xmakeProject.AddItemGroup();
                            }

                            this.ProcessReferenceElement(referencesChildElement, referencesItemGroup);
                            break;

                        default:
                            ProjectErrorUtilities.VerifyThrowInvalidProject(false, referencesChildElement.Location,
                                "UnrecognizedChildElement", referencesChildElement.Name,
                                VSProjectElements.references);
                            break;
                    }
                }
                else
                {
                    ProjectXmlUtilities.ThrowProjectInvalidChildElement(referencesChildNode.Name, referencesElement.Name, referencesElement.Location);
                }
            }
        }

        /// <summary>
        /// Processes the &lt;Reference&gt; element, and add an appropriate reference
        /// items to the referencesItemGroup.
        /// </summary>
        /// <owner>RGoel</owner>
        private void ProcessReferenceElement
            (
            XmlElementWithLocation      referenceElement,
            ProjectItemGroupElement referencesItemGroup
            )
        {
            // Make sure this is the <Reference> element.
            error.VerifyThrow((referenceElement?.Name == VSProjectElements.reference),
                "Expected <Reference> element.");

            // Make sure the caller has already created an ProjectItemGroupElement for us to
            // put the new items in.
            error.VerifyThrow(referencesItemGroup != null, "Received null ProjectItemGroupElement");

            // Before we do anything else, look for the "Platform" attribute.
            //   If it's available, we need to remove it, and if it ends in
            //   "-Designer", we need to disregard this reference entirely.

            string platform = referenceElement.GetAttribute(VSProjectAttributes.platform);
            if (!string.IsNullOrEmpty(platform))
            {
                if (platform.IndexOf("-Designer", 0, platform.Length, StringComparison.Ordinal) != -1)
                {
                    return;
                }

                referenceElement.RemoveAttribute ( VSProjectAttributes.platform );
            }

            ProjectItemElement newReferenceItem;

            // Get the "Name" attribute.  This is a required attribute in the VS7/
            // Everett format.
            string referenceName = referenceElement.GetAttribute(VSProjectAttributes.name);
            ProjectErrorUtilities.VerifyThrowInvalidProject(!string.IsNullOrEmpty(referenceName),
                referenceElement.Location, "MissingAttribute", VSProjectAttributes.name, VSProjectElements.reference);

            // Before we go any further, we must special-case some assemblies for VSD projects.

            if ((this.language == VSProjectElements.ECSharp) ||
                   (this.language == VSProjectElements.EVisualBasic))
            {
                if ( ( this.frameworkVersionForVSD == XMakeProjectStrings.vTwo ) &&
                     ( String.Equals ( referenceName, VSProjectElements.SystemDataCommon, StringComparison.OrdinalIgnoreCase ) ) )
                {
                    // We need to remove all references to "System.Data.Common" for VSD projects only.
                    //   Note : We only want to do this for projects that will be updated to v2.0
                    //          System.Data.Common is still valid for v1.0 upgraded projects.
                    return;
                }
                else if ( String.Equals ( referenceName, VSProjectElements.SystemSR, StringComparison.OrdinalIgnoreCase ) )
                {
                    // We always want to remove all references to "System.SR"
                    return;
                }
            }

            if ( ( this.language == VSProjectElements.EVisualBasic ) &&
                 ( String.Equals ( referenceName, VSProjectElements.MSCorLib, StringComparison.OrdinalIgnoreCase ) ) )
            {
                // We also want to get rid of all 'mscorlib' references for VB projects only.
                return;
            }

            // We need to find out what type of reference this is -- a .NET assembly
            // reference, a COM reference, or a project reference.  In the XMake format,
            // we use separate item types for each of these.

            // See if there's a "Guid" attribute on the <Reference> element.  If so,
            // it's a classic COM reference.
            string comReferenceGuid = referenceElement.GetAttribute(VSProjectAttributes.guid);

            // See if there's a "Project" guid attribute.  If so, it's a project
            // reference.
            string referencedProjectGuid = referenceElement.GetAttribute(VSProjectAttributes.project);

            if (!string.IsNullOrEmpty(comReferenceGuid) &&
                (comReferenceGuid != "{00000000-0000-0000-0000-000000000000}"))
            {
                newReferenceItem = ConvertClassicComReference(referenceElement, referencesItemGroup, referenceName);
            }
            else if (!string.IsNullOrEmpty(referencedProjectGuid))
            {
                newReferenceItem = ConvertProjectToProjectReference(referenceElement, referencesItemGroup, referenceName, ref referencedProjectGuid);
            }
            else
            {
                newReferenceItem = ConvertAssemblyReference(referenceElement, referencesItemGroup, referenceName);
            }

            // Add all the rest of the attributes on the <Reference> element to the new
            // XMake item.
            foreach (XmlAttribute referenceAttribute in referenceElement.Attributes)
            {
                newReferenceItem.AddMetadata(referenceAttribute.Name, ProjectCollection.Escape(referenceAttribute.Value));
            }

            // There should be no children of the <Reference> element.
            ProjectXmlUtilities.VerifyThrowProjectNoChildElements(referenceElement);
        }

        /// <summary>
        /// Given an element corresponding to a COM reference, create the appropriate element in the new project
        /// </summary>
        /// <param name="referenceElement"></param>
        /// <param name="referencesItemGroup"></param>
        /// <param name="referenceName"></param>
        /// <returns></returns>
        private static ProjectItemElement ConvertClassicComReference(XmlElementWithLocation referenceElement, ProjectItemGroupElement referencesItemGroup, string referenceName)
        {
            // This is a classic COM reference.

            // This gets added as a new XMake item of type "COMReference".
            // The "Include" attribute will contain the reference name,
            // and all the other attributes remain the same.  For example,
            // -----------------------------------------------------------------------
            // Everett format:
            // ===============
            //    <Reference
            //        Name = "UTILITIESLib"
            //        Guid = "{0EF79DA1-6555-11D2-A889-00AA006C2A9A}"
            //        VersionMajor = "1"
            //        VersionMinor = "0"
            //        Lcid = "0"
            //        WrapperTool = "tlbimp"
            //    />
            // -----------------------------------------------------------------------
            // XMake format:
            // =============
            //    <COMReference Include = "UTILITIESLib">
            //          <Guid>{0EF79DA1-6555-11D2-A889-00AA006C2A9A}</Guid>
            //          <VersionMajor>1</VersionMajor>
            //          <VersionMinor>0</VersionMinor>
            //          <Lcid>0</Lcid>
            //          <WrapperTool>tlbimp</WrapperTool>
            //    </COMReference>
            // -----------------------------------------------------------------------

            // Remove the "Name" attribute so we don't add it again at the end.
            referenceElement.RemoveAttribute(VSProjectAttributes.name);

            // Add a new item to XMake of type "COMReference".
            return referencesItemGroup.AddItem(XMakeProjectStrings.comReference, ProjectCollection.Escape(referenceName));
        }

        /// <summary>
        /// Given an element corresponding to a P2P reference, create the appropriate element in the new project
        /// </summary>
        /// <param name="referenceElement"></param>
        /// <param name="referencesItemGroup"></param>
        /// <param name="referenceName"></param>
        /// <param name="referencedProjectGuid"></param>
        /// <returns></returns>
        private ProjectItemElement ConvertProjectToProjectReference(XmlElementWithLocation referenceElement, ProjectItemGroupElement referencesItemGroup, string referenceName, ref string referencedProjectGuid)
        {
            // This is a project-to-project reference.
            // This gets added as a new XMake item of type "ProjectReference".
            // The "Include" attribute should be the relative path from the
            // current project to the referenced project file.  For example,
            // -----------------------------------------------------------------------
            // Everett format:
            // ===============
            //    <Reference
            //        Name = "XMakeTasks"
            //        Project = "{44342961-78F4-4F98-AFD6-720DA6E648A2}"
            //        Package = "{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}"
            //    />
            // -----------------------------------------------------------------------
            // XMake format:
            // =============
            //    <ProjectReference Include = "..\XMakeTasks\XMakeTasks.csproj">
            //          <Name>XMakeTasks</Name>
            //          <Project>{44342961-78F4-4F98-AFD6-720DA6E648A2}</Project>
            //          <Package>{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}</Package>
            //    </ProjectReference>
            // -----------------------------------------------------------------------

            // Apparently, sometimes project reference guids contain additional goo with relative project path.
            // Just strip it off. The project system does the same thing, and by doing this early we make
            // sure that we have the correct guid attribute in the project file and ResolveNonMSBuildReferences
            // does not complain about invalid characters there which causes all the references to fail to resolve.
            int barIndex = referencedProjectGuid.IndexOf('|');
            if (barIndex != -1)
            {
                referencedProjectGuid = referencedProjectGuid.Remove(barIndex);
                referenceElement.SetAttribute(VSProjectAttributes.project, referencedProjectGuid);
            }

            string pathToReferencedProject = this.GetRelativePathToReferencedProject(referencedProjectGuid);

            if (pathToReferencedProject != null)
            {
                // For VSD Projects, we want to transform all Everett ( .csdproj & .vbdproj ) project 2 project references into
                // Whidbey ( .csproj & .vbproj ) references.
                if (String.Equals(Path.GetExtension(pathToReferencedProject),
                                        XMakeProjectStrings.csdprojFileExtension,
                                        StringComparison.OrdinalIgnoreCase))
                {
                    pathToReferencedProject = Path.ChangeExtension(pathToReferencedProject, XMakeProjectStrings.csprojFileExtension);
                }
                else if (String.Equals(Path.GetExtension(pathToReferencedProject),
                                             XMakeProjectStrings.vbdprojFileExtension,
                                             StringComparison.OrdinalIgnoreCase))
                {
                    pathToReferencedProject = Path.ChangeExtension(pathToReferencedProject, XMakeProjectStrings.vbprojFileExtension);
                }
            }

            // Add a new item to XMake of type "ProjectReference".  If we were able to find
            // the relative path to the project, use it for the "Include", otherwise just use
            // the project name.
            string value = pathToReferencedProject ?? referenceName;
            return referencesItemGroup.AddItem(XMakeProjectStrings.projectReference, ProjectCollection.Escape(value));
        }

        /// <summary>
        /// Given an element corresponding to a .NET Assembly reference, create the appropriate element in the new project
        /// </summary>
        /// <param name="referenceElement"></param>
        /// <param name="referencesItemGroup"></param>
        /// <param name="referenceName"></param>
        /// <returns></returns>
        private ProjectItemElement ConvertAssemblyReference(XmlElementWithLocation referenceElement, ProjectItemGroupElement referencesItemGroup, string referenceName)
        {
            // This is a regular .NET assembly reference.

            // This gets added as a new XMake item of type "Reference".  The "Include"
            // attribute is the assembly name, and all the other attributes remain
            // the same.  For example,
            // -----------------------------------------------------------------------
            // Everett format:
            // ===============
            //    <Reference
            //        Name = "System.Xml"
            //        AssemblyName = "System.Xml"
            //        HintPath = "..\..\binaries\x86chk\bin\i386\System.Xml.dll"
            //    />
            // -----------------------------------------------------------------------
            // XMake format:
            // =============
            //    <Reference Include="System.Xml">
            //          <Name>System.Xml</Name>
            //          <HintPath>..\..\binaries\x86chk\bin\i386\System.Xml.dll</HintPath>
            //    </Reference>
            // -----------------------------------------------------------------------

            // Get the "AssemblyName" attribute.  If not found, just use the value from the
            // "Name" attribute.  This is what the project loading code does in VS.
            string assemblyName = referenceElement.GetAttribute(VSProjectAttributes.assemblyName);
            if (string.IsNullOrEmpty(assemblyName))
            {
                assemblyName = referenceName;
            }
            else
            {
                // Remove the "AssemblyName" attribute so we don't add it again at
                // the end.
                referenceElement.RemoveAttribute(VSProjectAttributes.assemblyName);
            }

            // MyType should only be added when System.Windows.Forms is present. If this
            // reference is seen, then set a flag so we can later add MyType.
            if (String.Equals("System.Windows.Forms", assemblyName, StringComparison.OrdinalIgnoreCase))
            {
                hasWindowsFormsReference = true;
            }

            // Remove hint paths that we think are to RTM or Everett framework assemblies
            string hintPath = referenceElement.GetAttribute(VSProjectAttributes.hintPath);
            if (hintPath != null)
            {
                hintPath = hintPath.ToUpper(CultureInfo.InvariantCulture);
                if (hintPath.IndexOf(LegacyFrameworkPaths.RTMFrameworkPath, StringComparison.Ordinal) != -1 ||
                    hintPath.IndexOf(LegacyFrameworkPaths.EverettFrameworkPath, StringComparison.Ordinal) != -1 ||
                    hintPath.IndexOf(LegacyFrameworkPaths.JSharpRTMFrameworkPath, StringComparison.Ordinal) != -1)
                {
                    referenceElement.RemoveAttribute(VSProjectAttributes.hintPath);
                }
            }

            return referencesItemGroup.AddItem(XMakeProjectStrings.reference, ProjectCollection.Escape(assemblyName));
        }

        /// <summary>
        /// To convert project-to-project references correctly, we need some data
        /// out of the solution file.  If we weren't given a solution file, then
        /// we search the project's directory and every parent directory all the
        /// way up to the root for the corresponding SLN file.
        /// </summary>
        /// <owner>RGoel</owner>
        private void SearchForSolutionFile
            (
            )
        {
            error.VerifyThrow(this.solutionFile == null, "Solution file already passed in!");
            error.VerifyThrow(this.projectGuid != null, "Need project Guid to find solution file.");

            // Start by looking for a solution file in the directory of the original project file.
            DirectoryInfo searchDirectory = new DirectoryInfo(Path.GetDirectoryName(Path.GetFullPath(this.oldProjectFile)));

            while (searchDirectory != null)
            {
                // Get a list of all the .SLN files in the current search directory.
                FileInfo[] slnFiles = searchDirectory.GetFiles("*.sln");

                // Open each .SLN file and parse it.  We're searching for a .SLN
                // file that contains the current project that we're converting.
                foreach (FileInfo slnFile in slnFiles)
                {
                    // Check that the extension really is ".SLN", because the above call to
                    // GetFiles will also return files such as blah.SLN1 and bloo.SLN2.
                    if (String.Equals(".sln", slnFile.Extension, StringComparison.OrdinalIgnoreCase))
                    {
                        // Parse the .SLN file.
                        SolutionFile solutionParser = new SolutionFile();
                        solutionParser.FullPath = slnFile.FullName;

                        try
                        {
                            solutionParser.ParseSolutionFile();
                            this.conversionWarnings.AddRange(solutionParser.SolutionParserWarnings);

                            // Determine if our current project guid (for the project we're converting)
                            // is listed in the .SLN file.
                            if (solutionParser.GetProjectUniqueNameByGuid(this.projectGuid) != null)
                            {
                                // If we found our project listed, then this is the solution we will
                                // use to help us convert the project-to-project references.
                                this.solutionFile = slnFile.FullName;
                                this.solution = solutionParser;
                                return;
                            }
                        }
                        catch (InvalidProjectFileException)
                        {
                            // If the SLN wasn't valid, that's fine ... just skip it, and
                            // move on to the next one.
                        }
                    }
                }

                // Go up one directory, and search there.  Stop when we hit the root.
                searchDirectory = searchDirectory.Parent;
            }

            // If we don't find a solution file that contains our project, that's okay...
            // we can still proceed.  It just means that the converted project-to-project
            // references won't have the relative path to the referenced project.  This
            // may prevent command-line builds from being fully functional, but it's
            // not the end of the world.
        }

        /// <summary>
        /// Given a 'from' path and a 'to' path, compose a relative path from 'from'
        /// to 'to'.
        /// </summary>
        /// <owner>jomof</owner>
        internal static string RelativePathTo(string from, string to)
        {
            error.VerifyThrow(from.IndexOf("*", StringComparison.Ordinal) == -1, "Bug: RelativePathTo can't handle wild cards.");
            error.VerifyThrow(to.IndexOf("*", StringComparison.Ordinal) == -1, "Bug: RelativePathTo can't handle wild cards.");
            from = Path.GetFullPath(from);
            to = Path.GetFullPath(to);

            Uri uriFrom = new Uri(from);
            Uri uriTo = new Uri(to);
            Uri relative = uriFrom.MakeRelativeUri(uriTo);
            string result = Uri.UnescapeDataString(relative.ToString());

            // The URI class returns forward slashes instead of backslashes.  Replace
            // them now, and return the final path.
            return result.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
        }

        /// <summary>
        /// Given a project guid for a referenced project, this method computes
        /// the relative path to that referenced project file from the current
        /// project file that is being converted.  In order to do this, we need
        /// some information out of the SLN file.
        /// </summary>
        /// <owner>RGoel</owner>
        private string GetRelativePathToReferencedProject
            (
            string referencedProjectGuid
            )
        {
            error.VerifyThrow(referencedProjectGuid != null, "Need valid project guid.");

            // If we don't have a pointer to the SLN file that contains this project,
            // then we really have no hope of finding the relative path to the
            // referenced project.
            if (this.solution == null)
            {
                // Log a warning that indicates that the complete solution file was not used
                string warning = ResourceUtilities.FormatString(
                    AssemblyResources.GetString("CouldNotFindCompleteSolutionFile"),
                    referencedProjectGuid);
                conversionWarnings.Add(warning);
                return null;
            }

            // Find the referenced project guid in the SLN file.
            string relativePathFromSolutionToReferencedProject =
                this.solution.GetProjectRelativePathByGuid(referencedProjectGuid);

            if (relativePathFromSolutionToReferencedProject == null)
            {
                // If the referenced project does not exist in the solution, we can't
                // get its relative path.  This is not a conversion error; it just means
                // the converted project file won't have the relative path to the
                // referenced project.  This may prevent some command-line build
                // scenarios from working completely.
                string warning = ResourceUtilities.FormatString(
                    AssemblyResources.GetString("ProjectNotListingInSolutionFile"),
                    referencedProjectGuid, this.solution.FullPath);
                conversionWarnings.Add(warning);
                return null;
            }

            if (relativePathFromSolutionToReferencedProject.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            {
                // We've discovered a P2P reference to a web project. This feature is no
                // longer supported in Whidbey.  We will remove the
                // P2P reference to the web project and issue a warning into the upgrade log
                // telling the user he needs to fix this up himself.
                string warning = ResourceUtilities.FormatString(
                    AssemblyResources.GetString("UnsupportedProjectToProjectWebReference"),
                    relativePathFromSolutionToReferencedProject);
                conversionWarnings.Add(warning);
                return null;
            }

            // Get the full path to the referenced project.  Compute this by combining
            // the full path to the SLN file with the relative path specified within
            // the SLN file.
            string fullPathToReferencedProject = Path.Combine(
                Path.GetDirectoryName(this.solutionFile),
                relativePathFromSolutionToReferencedProject);

            // Now compute the relative path from the current project to the referenced
            // project.
            return RelativePathTo(this.oldProjectFile, fullPathToReferencedProject);
        }

        /// <summary>
        /// Processes the &lt;Imports&gt; element, and everything within it.  As it is
        /// doing this, it will add "Import" items to a new ProjectItemGroupElement.
        /// </summary>
        /// <owner>RGoel</owner>
        private void ProcessImportsElement
            (
            XmlElementWithLocation      importsElement
            )
        {
            // Make sure this is the <Imports> element.
            error.VerifyThrow((importsElement?.Name == VSProjectElements.imports),
                "Expected <Imports> element.");

            // Make sure the caller gave us a valid xmakeProject to stuff
            // our new items into.
            error.VerifyThrow(xmakeProject != null, "Expected valid xmake project object.");

            // The <Imports> tag should have no attributes.
            ProjectErrorUtilities.VerifyThrowInvalidProject(!importsElement.HasAttributes, importsElement.Location,
                "NoAttributesExpected", VSProjectElements.imports);

            ProjectItemGroupElement importsItemGroup = null;

            // Loop through all the direct child elements of the <Imports> element.
            foreach(XmlNode importsChildNode in importsElement)
            {
                // Handle XML comments under the the <Imports> node (just ignore them).
                if ((importsChildNode.NodeType == XmlNodeType.Comment) ||
                    (importsChildNode.NodeType == XmlNodeType.Whitespace))
                {
                    continue;
                }

                if (importsChildNode.NodeType == XmlNodeType.Element)
                {
                    XmlElementWithLocation importsChildElement = (XmlElementWithLocation)importsChildNode;
                    switch (importsChildNode.Name)
                    {
                        // The <Import> element.
                        case VSProjectElements.import:
                            if (importsItemGroup == null)
                            {
                                importsItemGroup = xmakeProject.AddItemGroup();
                            }

                            this.ProcessImportElement((XmlElementWithLocation)importsChildElement, importsItemGroup);
                            break;

                        default:
                            ProjectErrorUtilities.VerifyThrowInvalidProject(false, importsChildElement.Location,
                                "UnrecognizedChildElement", importsChildElement.Name,
                                VSProjectElements.imports);
                            break;
                    }
                }
                else
                {
                    ProjectXmlUtilities.ThrowProjectInvalidChildElement(importsChildNode.Name, importsElement.Name, importsElement.Location);
                }
            }
        }

        /// <summary>
        /// Processes the &lt;Import&gt; element, and add an appropriate reference
        /// items to the importsItemGroup.
        /// </summary>
        /// <owner>RGoel</owner>
        private void ProcessImportElement
            (
            XmlElementWithLocation importElement,
            ProjectItemGroupElement importsItemGroup
            )
        {
            // Make sure this is the <Import> element.
            error.VerifyThrow((importElement?.Name == VSProjectElements.import),
                "Expected <Import> element.");

            // Make sure the caller has already created an ProjectItemGroupElement for us to
            // put the new items in.
            error.VerifyThrow(importsItemGroup != null, "Received null ProjectItemGroupElement");

            // Get the required "Namespace" attribute.
            string importNamespace = importElement.GetAttribute(VSProjectAttributes.importNamespace);
            ProjectErrorUtilities.VerifyThrowInvalidProject(!string.IsNullOrEmpty(importNamespace),
                importElement.Location, "MissingAttribute", VSProjectAttributes.importNamespace, VSProjectElements.import);
            // Remove the "Namespace" attribute, so it doesn't show up in our loop later.
            importElement.RemoveAttribute(VSProjectAttributes.importNamespace);

            // The <Import> element gets converted to XMake as an item of type "Import".
            // The "Namespace" attribute becomes the "Include" for the new item.  For
            // example,
            // -----------------------------------------------------------------------
            // Everett format:
            // ===============
            //    <Import Namespace = "System.Collections" />
            // -----------------------------------------------------------------------
            // XMake format:
            // =============
            //    <Import Include="System.Collections" />
            // -----------------------------------------------------------------------
            importsItemGroup.AddItem(XMakeProjectStrings.import, ProjectCollection.Escape(importNamespace));

            // There should be no other attributes on the <Import> element (besides
            // "Namespace" which we already took care of).  But loop through them
            // anyway, so we can emit a useful error message.
            foreach (XmlAttributeWithLocation importAttribute in importElement.Attributes)
            {
                ProjectErrorUtilities.VerifyThrowInvalidProject(false, importAttribute.Location, "UnrecognizedAttribute",
                    importAttribute.Name, VSProjectElements.import);
            }

            // There should be no children of the <Import> element.
            ProjectXmlUtilities.VerifyThrowProjectNoChildElements(importElement);
        }

        /// <summary>
        /// Processes the &lt;Files&gt; element, and everything within it.  As it is
        /// doing this, it will add the appropriate items to a new ProjectItemGroupElement.
        /// </summary>
        /// <owner>RGoel</owner>
        private void ProcessFilesElement
            (
            XmlElementWithLocation      filesElement
            )
        {
            // Make sure this is the <Files> element.
            error.VerifyThrow((filesElement?.Name == VSProjectElements.files),
                "Expected <Files> element.");

            // Make sure the caller gave us a valid xmakeProject to stuff
            // our new items into.
            error.VerifyThrow(xmakeProject != null, "Expected valid xmake project object.");

            // The <Files> tag should have no attributes.
            ProjectErrorUtilities.VerifyThrowInvalidProject(!filesElement.HasAttributes, filesElement.Location,
                "NoAttributesExpected", VSProjectElements.files);

            // Loop through all the direct child elements of the <Files> element.
            foreach(XmlNode filesChildNode in filesElement)
            {
                // Handle XML comments under the the <Files> node (just ignore them).
                if ((filesChildNode.NodeType == XmlNodeType.Comment) ||
                    (filesChildNode.NodeType == XmlNodeType.Whitespace))
                {
                    continue;
                }

                if (filesChildNode.NodeType == XmlNodeType.Element)
                {
                    XmlElementWithLocation filesChildElement = (XmlElementWithLocation)filesChildNode;
                    switch (filesChildNode.Name)
                    {
                        // The <Include> element.
                        case VSProjectElements.include:
                            this.ProcessIncludeElement(filesChildElement);
                            break;

                        // The <Exclude> element.  Actually, the <Exclude> element is not supported
                        // by VS.  That is, VS completely ignores anything under the <Exclude>
                        // element.  Yet, some really old project files have this tag in there,
                        // even though it doesn't do anything.  So let's at least not fail if
                        // the project file contains this.
                        case VSProjectElements.exclude:
                            string warning = AssemblyResources.GetString("ExcludeFoundInProject");
                            conversionWarnings.Add(warning);
                            break;

                        default:
                            ProjectErrorUtilities.VerifyThrowInvalidProject(false, filesChildElement.Location,
                                "UnrecognizedChildElement", filesChildNode.Name,
                                VSProjectElements.files);
                            break;
                    }
                }
                else
                {
                    ProjectXmlUtilities.ThrowProjectInvalidChildElement(filesChildNode.Name, filesElement.Name, filesElement.Location);
                }
            }
        }

        /// <summary>
        /// Processes the &lt;Include&gt; element, and everything within it.  As it is
        /// doing this, it will add the appropriate items to a new ProjectItemGroupElement.
        /// </summary>
        /// <owner>RGoel</owner>
        private void ProcessIncludeElement
            (
            XmlElementWithLocation      includeElement
            )
        {
            // Make sure this is the <Include> element.
            error.VerifyThrow((includeElement?.Name == VSProjectElements.include),
                "Expected <Include> element.");

            // Make sure the caller gave us a valid xmakeProject to stuff
            // our new items into.
            error.VerifyThrow(xmakeProject != null, "Expected valid xmake project object.");

            // The <Include> tag should have no attributes.
            ProjectErrorUtilities.VerifyThrowInvalidProject(!includeElement.HasAttributes, includeElement.Location,
                "NoAttributesExpected", VSProjectElements.include);

            ProjectItemGroupElement filesItemGroup = null;

            // Loop through all the direct child elements of the <Include> element.
            foreach(XmlNode includeChildNode in includeElement)
            {
                // Handle XML comments under the the <Include> node (just ignore them).
                if ((includeChildNode.NodeType == XmlNodeType.Comment) ||
                    (includeChildNode.NodeType == XmlNodeType.Whitespace))
                {
                    continue;
                }

                if (includeChildNode.NodeType == XmlNodeType.Element)
                {
                    if (filesItemGroup == null)
                    {
                        filesItemGroup = xmakeProject.AddItemGroup();
                    }

                    XmlElementWithLocation includeChildElement = (XmlElementWithLocation)includeChildNode;
                    switch (includeChildElement.Name)
                    {
                        // The <File> element.
                        case VSProjectElements.file:
                            this.ProcessFileElement(includeChildElement, filesItemGroup);
                            break;

                        // The <Folder> element.
                        case VSProjectElements.folder:
                            this.ProcessFolderElement(includeChildElement, filesItemGroup);
                            break;

                        default:
                            ProjectErrorUtilities.VerifyThrowInvalidProject(false, includeChildElement.Location,
                                "UnrecognizedChildElement", includeChildNode.Name,
                                VSProjectElements.include);
                            break;
                    }
                }
                else
                {
                    ProjectXmlUtilities.ThrowProjectInvalidChildElement(includeChildNode.Name, includeElement.Name, includeElement.Location);
                }
            }
        }

        /// <summary>
        /// Processes the &lt;File&gt; element, and adds an appropriate item to the
        /// filesItemGroup.
        /// </summary>
        /// <owner>RGoel</owner>
        private void ProcessFileElement
            (
            XmlElementWithLocation      fileElement,
            ProjectItemGroupElement filesItemGroup
            )
        {
            // Make sure this is the <File> element.
            error.VerifyThrow((fileElement?.Name == VSProjectElements.file),
                "Expected <File> element.");

            // Make sure the caller has already created an ProjectItemGroupElement for us to
            // put the new items in.
            error.VerifyThrow(filesItemGroup != null, "Received null ProjectItemGroupElement");

            // Get the required "RelPath" attribute.
            string relPath = fileElement.GetAttribute(VSProjectAttributes.relPath);
            ProjectErrorUtilities.VerifyThrowInvalidProject(!string.IsNullOrEmpty(relPath),
                fileElement.Location, "MissingAttribute", VSProjectAttributes.relPath, VSProjectElements.file);
            // Remove the "RelPath" attribute, so we don't end up adding it twice.
            fileElement.RemoveAttribute(VSProjectAttributes.relPath);

            // Get the "Link" attribute.  This is for linked items only.
            string linkPath = fileElement.GetAttribute(VSProjectAttributes.link);
            // Remove the "Link" attribute, so we don't end up adding it twice.
            fileElement.RemoveAttribute(VSProjectAttributes.link);

            // Get the "BuildAction" attribute.  If it doesn't exist, figure out
            // what the build action is based on the file extension.  This is
            // what the project loading code does in VS.
            string buildAction = fileElement.GetAttribute(VSProjectAttributes.buildAction);
            if (string.IsNullOrEmpty(buildAction))
            {
                buildAction = VSProjectAttributes.buildActionNone;
            }
            // Remove the "BuildAction" attribute, so we don't end up adding it twice.
            fileElement.RemoveAttribute(VSProjectAttributes.buildAction);

            ProjectItemElement newFileItem;

            // Bug Whidbey #248965. If a .resx file is completely empty, do not include a reference
            // to it in the upgraded project file.
            if (!
                (String.Equals(Path.GetExtension(relPath), ".resx", StringComparison.OrdinalIgnoreCase)
                 && IsFilePresentButEmpty(relPath, linkPath))
               )
            {
                // Add the new item to XMake.
                if (string.IsNullOrEmpty(linkPath))
                {
                    // Normal item.

                    // The <File> element gets converted to XMake as a new item, where
                    // the item type is the BuildAction, and the "Include" contains
                    // the relative path to the item.  For
                    // example,
                    // -----------------------------------------------------------------------
                    // Everett format:
                    // ===============
                    //    <File
                    //        RelPath = "Properties\PropertyGroupCollection.cs"
                    //        SubType = "Code"
                    //        BuildAction = "Compile"
                    //    />
                    // -----------------------------------------------------------------------
                    // XMake format:
                    // =============
                    //    <Compile Include = "Properties\PropertyGroupCollection.cs">
                    //          <SubType>Code</SubType>
                    //    </Compile>
                    // -----------------------------------------------------------------------
                    newFileItem = filesItemGroup.AddItem(buildAction, ProjectCollection.Escape(relPath));
                }
                else
                {
                    // Linked item.

                    // The <File> element gets converted to XMake as a new item, where
                    // the item type is the BuildAction, the "Include" contains
                    // the physical relative path to the item, and the non-XMake "Link"
                    // attribute contains the project-relative path for item (for display
                    // purposes in the Solution Explorer).  For example,
                    // -----------------------------------------------------------------------
                    // Everett format:
                    // ===============
                    //    <File
                    //        RelPath = "Properties\PropertyGroupCollection.cs"
                    //        Link = "c:\Rajeev\External\PropertyGroupCollection.cs"
                    //        SubType = "Code"
                    //        BuildAction = "Compile"
                    //    />
                    // -----------------------------------------------------------------------
                    // XMake format:
                    // =============
                    //    <Compile Include = "c:\Rajeev\External\PropertyGroupCollection.cs">
                    //          <Link>Properties\PropertyGroupCollection.cs</Link>
                    //          <SubType>Code</SubType>
                    //    </Compile>
                    // -----------------------------------------------------------------------
                    newFileItem = filesItemGroup.AddItem(buildAction, ProjectCollection.Escape(linkPath));
                    newFileItem.AddMetadata(XMakeProjectStrings.link, ProjectCollection.Escape(relPath));
                }

                // Add all the rest of the attributes on the <File> element to the new
                // XMake item.
                foreach (XmlAttribute fileAttribute in fileElement.Attributes)
                {
                    newFileItem.AddMetadata(fileAttribute.Name, ProjectCollection.Escape(fileAttribute.Value));
                }

                // If this is a VSD(devices) project and we're dealing with a content file,
                // mark it to copy if newer.
                if ( ((this.language == VSProjectElements.ECSharp) ||
                         (this.language == VSProjectElements.EVisualBasic)) &&
                     ( String.Equals ( buildAction, XMakeProjectStrings.content, StringComparison.OrdinalIgnoreCase ) ) )
                {
                    newFileItem.AddMetadata ( XMakeProjectStrings.copytooutput,
                                              XMakeProjectStrings.preservenewest );
                }
            }
            else
            {
                string warning = ResourceUtilities.FormatString(
                    AssemblyResources.GetString("EmptyResxRemoved"),
                    relPath);
                conversionWarnings.Add(warning);
            }

            // There should be no children of the <File> element.
            ProjectXmlUtilities.VerifyThrowProjectNoChildElements(fileElement);
        }

        /// <summary>
        /// Checks whether a file has content. If it is empty, returns true.
        /// If file does not exist we may be waiting for it to download asynchronously
        /// via source control so return false to leave it in the project
        /// </summary>
        /// <owner>danmose</owner>
        private bool IsFilePresentButEmpty(string relPath, string linkPath)
        {
            // relpath is the filename
            // linkPath, if it exists, is the relative path from the project, or the absolute full path
            string path;
            if (string.IsNullOrEmpty(linkPath))
            {
                path = Path.Combine(Path.GetDirectoryName(oldProjectFile), relPath);
            }
            else
            {
                if (Path.IsPathRooted(linkPath)) // absolute
                {
                    path = linkPath;
                }
                else // relative
                {
                    path = Path.Combine(Path.GetDirectoryName(oldProjectFile), linkPath);
                }
            }

            if (!File.Exists(path))
            {
                // File does not exist - may be waiting to be download asynchronously via source control
                // so return false to leave it in the project
                return false;
            }

            long length;
            try
            {
                FileInfo fi = new FileInfo(path);
                length = fi.Length;
            }
            catch (Exception e) // Catching Exception, but rethrowing unless it's a well-known exception.
            {
                if (ExceptionHandling.NotExpectedException(e))
                    throw;
                // if we can't say for sure it's empty, play safe and return false
                return false;
            }

            return length == 0;
        }

        /// <summary>
        /// Processes the &lt;Folder&gt; element, and adds an appropriate item to the
        /// filesItemGroup.
        /// </summary>
        /// <owner>RGoel</owner>
        private void ProcessFolderElement
            (
            XmlElementWithLocation      folderElement,
            ProjectItemGroupElement filesItemGroup
            )
        {
            // Make sure this is the <Folder> element.
            error.VerifyThrow((folderElement?.Name == VSProjectElements.folder),
                "Expected <Folder> element.");

            // Make sure the caller has already created an ProjectItemGroupElement for us to
            // put the new items in.
            error.VerifyThrow(filesItemGroup != null, "Received null ProjectItemGroupElement");

            // Get the required "RelPath" attribute.
            string relPath = folderElement.GetAttribute(VSProjectAttributes.relPath);
            ProjectErrorUtilities.VerifyThrowInvalidProject(!string.IsNullOrEmpty(relPath),
                folderElement.Location, "MissingAttribute", VSProjectAttributes.relPath, VSProjectElements.folder);
            // Remove the "RelPath" attribute, so we don't end up adding it twice.
            folderElement.RemoveAttribute(VSProjectAttributes.relPath);

            // We need to find out what type of folder this is -- a web references
            // folder, a web reference URL, or just an empty project folder.

            // See if there's a "WebReferences" attribute on the <Folder> element.  If so,
            // and the value is set to "True", then it's a web reference folder.
            string webReferences = folderElement.GetAttribute(VSProjectAttributes.webReferences);
            // Remove the "WebReferences" attribute, so we don't end up adding it twice.
            folderElement.RemoveAttribute(VSProjectAttributes.webReferences);

            // See if there's a "WebReferenceURL" attribute.  If so, it's a web reference
            // URL.
            string webReferenceUrl = folderElement.GetAttribute(VSProjectAttributes.webReferenceUrl);
            // Remove the "WebReferenceURL" attribute, so we don't end up adding it twice.
            folderElement.RemoveAttribute(VSProjectAttributes.webReferenceUrl);

            ProjectItemElement newFolderItem;

            if ((webReferences != null) && (String.Equals(webReferences, "true", StringComparison.OrdinalIgnoreCase)))
            {
                // This is a web reference folder.

                // The <Folder> element gets converted to XMake as an item of type
                // "WebReferences".  The "Include" will contain the relative path.
                // For example,
                // -----------------------------------------------------------------------
                // Everett format:
                // ===============
                //    <Folder
                //        RelPath = "Web References\"
                //        WebReferences = "TRUE"
                //    />
                // -----------------------------------------------------------------------
                // XMake format:
                // =============
                //    <WebReferences Include = "Web References\" />
                // -----------------------------------------------------------------------
                newFolderItem = filesItemGroup.AddItem(XMakeProjectStrings.webReferences,
                    ProjectCollection.Escape(relPath));
            }
            else if (!string.IsNullOrEmpty(webReferenceUrl))
            {
                // This is an actual web reference URL.

                // The <Folder> element gets converted to XMake as an item of type
                // "WebReferenceURL".  The "Include" will contain the URL.
                // For example,
                // -----------------------------------------------------------------------
                // Everett format:
                // ===============
                //    <Folder
                //        RelPath = "Web References\mobileakipman\"
                //        WebReferenceUrl = "http://mobileakipman/HelloName/service1.asmx"
                //        UrlBehavior = "Static"
                //    />
                // -----------------------------------------------------------------------
                // XMake format:
                // =============
                //    <WebReferenceUrl Include="http://mobileakipman/HelloName/service1.asmx">
                //          <RelPath>Web References\mobileakipman\</RelPath>
                //          <UrlBehavior>Static</UrlBehavior>
                //    </WebReferenceUrl>
                // -----------------------------------------------------------------------
                newFolderItem = filesItemGroup.AddItem(XMakeProjectStrings.webReferenceUrl,
                    ProjectCollection.Escape(webReferenceUrl));
                newFolderItem.AddMetadata(XMakeProjectStrings.relPath, ProjectCollection.Escape(relPath));

                // Whidbey projects have some new properties to control the behavior of the
                // proxy generation.  For projects migrated from Everett, we want to force
                // the proxy generation to mimic the Everett behavior, so that people's projects
                // still work the same as they did in Everett.  (These properties did not
                // exist in Everett.)  See spec at:
                // http://devdiv/SpecTool/Documents/Whidbey/VSCore/Solution%20Project%20Build/FeatureSpecs/Project-WebReferences.doc
                if (!this.newWebReferencePropertiesAdded)
                {
                    this.globalPropertyGroup.AddProperty(XMakeProjectStrings.webRefEnableProperties,
                        (this.language == VSProjectElements.visualJSharp) ? "false" : "true");
                    this.globalPropertyGroup.AddProperty(XMakeProjectStrings.webRefEnableSqlTypes, "false");
                    this.globalPropertyGroup.AddProperty(XMakeProjectStrings.webRefEnableLegacyEventing, "true");

                    this.newWebReferencePropertiesAdded = true;
                }
            }
            else
            {
                // This is just a project folder that happens not to have any files in it.

                // The <Folder> element gets converted to XMake as an item of type "Folder".
                // However, we do need to remove the trailing backslash, because XMake
                // interprets that as a recursion (bug # 58591).  For example,
                // -----------------------------------------------------------------------
                // Everett format:
                // ===============
                //    <Folder
                //        RelPath = "MyEmptyProjectFolder\"
                //    />
                // -----------------------------------------------------------------------
                // XMake format:
                // =============
                //    <Folder Include="MyEmptyProjectFolder" />
                // -----------------------------------------------------------------------

                // Remove the trailing backslash.  XMake interprets trailing backslashes
                // as a recursive wildcard.  This will be fixed in M2 -- bug # 58591
                if (relPath.EndsWith("\\", StringComparison.Ordinal))
                {
                    relPath = relPath.Remove(relPath.Length - 1, 1);
                }

                newFolderItem = filesItemGroup.AddItem(XMakeProjectStrings.folder,
                    ProjectCollection.Escape(relPath));
            }

            // Add all the rest of the attributes on the <Folder> element to the new
            // XMake item.
            foreach (XmlAttribute folderAttribute in folderElement.Attributes)
            {
                newFolderItem.AddMetadata(folderAttribute.Name, ProjectCollection.Escape(folderAttribute.Value));
            }

            // There should be no children of the <Folder> element.
            ProjectXmlUtilities.VerifyThrowProjectNoChildElements(folderElement);
        }

        /// <summary>
        /// Processes the &lt;StartupServices&gt; element, and everything within it.  As
        /// it is doing this, it will add new "StartupService" items to a new ProjectItemGroupElement.
        /// </summary>
        /// <owner>RGoel</owner>
        private void ProcessStartupServicesElement
            (
            XmlElementWithLocation      startupServicesElement
            )
        {
            // Make sure this is the <StartupServices> element.
            error.VerifyThrow((startupServicesElement?.Name == VSProjectElements.startupServices),
                "Expected <StartupServices> element.");

            // Make sure the caller gave us a valid xmakeProject to stuff
            // our new items into.
            error.VerifyThrow(xmakeProject != null, "Expected valid xmake project object.");

            // The <StartupServices> tag should have no attributes.
            ProjectErrorUtilities.VerifyThrowInvalidProject(!startupServicesElement.HasAttributes, startupServicesElement.Location,
                "NoAttributesExpected", VSProjectElements.startupServices);

            ProjectItemGroupElement startupServicesItemGroup = null;

            // Loop through all the direct child elements of the <StartupServices> element.
            foreach(XmlNode startupServicesChildNode in startupServicesElement)
            {
                // Handle XML comments under the the <StartupServices> node (just ignore them).
                if ((startupServicesChildNode.NodeType == XmlNodeType.Comment) ||
                    (startupServicesChildNode.NodeType == XmlNodeType.Whitespace))
                {
                    continue;
                }

                if (startupServicesChildNode.NodeType == XmlNodeType.Element)
                {
                    XmlElementWithLocation startupServicesChildElement = (XmlElementWithLocation)startupServicesChildNode;
                    switch (startupServicesChildElement.Name)
                    {
                        // The <Service> element.
                        case VSProjectElements.service:
                            if (startupServicesItemGroup == null)
                            {
                                startupServicesItemGroup = xmakeProject.AddItemGroup();
                            }

                            this.ProcessServiceElement(startupServicesChildElement, startupServicesItemGroup);
                            break;

                        default:
                            ProjectErrorUtilities.VerifyThrowInvalidProject(false, startupServicesChildElement.Location,
                                "UnrecognizedChildElement", startupServicesChildNode.Name,
                                VSProjectElements.startupServices);
                            break;
                    }
                }
                else
                {
                    ProjectXmlUtilities.ThrowProjectInvalidChildElement(startupServicesChildNode.Name, startupServicesElement.Name, startupServicesElement.Location);
                }
            }
        }

        /// <summary>
        /// Processes the &lt;Service&gt; element, and add an appropriate reference
        /// items to the startupServicesItemGroup.
        /// </summary>
        /// <owner>RGoel</owner>
        private void ProcessServiceElement
            (
            XmlElementWithLocation      serviceElement,
            ProjectItemGroupElement startupServicesItemGroup
            )
        {
            // Make sure this is the <Service> element.
            error.VerifyThrow((serviceElement?.Name == VSProjectElements.service),
                "Expected <Service> element.");

            // Make sure the caller has already created an ProjectItemGroupElement for us to
            // put the new items in.
            error.VerifyThrow(startupServicesItemGroup != null, "Received null ProjectItemGroupElement");

            // Get the required "ID" attribute.
            string id = serviceElement.GetAttribute(VSProjectAttributes.id);
            ProjectErrorUtilities.VerifyThrowInvalidProject(!string.IsNullOrEmpty(id), serviceElement.Location,
                "MissingAttribute", VSProjectAttributes.id, VSProjectElements.service);
            // Remove the "ID" attribute, so it doesn't show up in our loop later.
            serviceElement.RemoveAttribute(VSProjectAttributes.id);

            // The <Service> element gets converted to XMake as an item of type "Service".
            // The "ID" attribute becomes the "Include" for the new item.  For
            // example,
            // -----------------------------------------------------------------------
            // Everett format:
            // ===============
            //    <Service ID = "ABCD1234-78F4-4F98-AFD6-720DA6E648A2" />
            // -----------------------------------------------------------------------
            // XMake format:
            // =============
            //    <Service Include="ABCD1234-78F4-4F98-AFD6-720DA6E648A2" />
            // -----------------------------------------------------------------------
            startupServicesItemGroup.AddItem(XMakeProjectStrings.service, ProjectCollection.Escape(id));

            // There should be no other attributes on the <Service> element (besides
            // "ID" which we already took care of).  But loop through them
            // anyway, so we can emit a useful error message.
            foreach (XmlAttributeWithLocation serviceAttribute in serviceElement.Attributes)
            {
                ProjectErrorUtilities.VerifyThrowInvalidProject(false, serviceAttribute.Location, "UnrecognizedAttribute",
                    serviceAttribute.Name, VSProjectElements.service);
            }

            // There should be no children of the <Service> element.
            ProjectXmlUtilities.VerifyThrowProjectNoChildElements(serviceElement);
        }

        /// <summary>
        /// Processes the &lt;OtherProjectSettings&gt; element, and everything within it.
        /// As it is doing this, it will add stuff to the globalPropertyGroup.
        /// </summary>
        /// <owner>RGoel</owner>
        private void ProcessOtherProjectSettingsElement
            (
            XmlElementWithLocation      otherProjectSettingsElement
            )
        {
            // Make sure this is the <OtherProjectSettings> element.
            error.VerifyThrow((otherProjectSettingsElement?.Name == VSProjectElements.otherProjectSettings),
                "Expected <Settings> element.");

            // Make sure the caller gave us a valid globalPropertyGroup to stuff
            // our properties into.
            error.VerifyThrow(globalPropertyGroup != null, "Expected valid global ProjectPropertyElementGroup.");

            // All of the attributes on the <Settings> tag get converted to XMake
            // properties.  For example,
            // -----------------------------------------------------------------------
            // Everett format:
            // ===============
            //    <OtherProjectSettings
            //        CopyProjectDestinationFolder = ""
            //        CopyProjectUncPath = ""
            //        CopyProjectOption = "0"
            //        ProjectView = "ProjectFiles"
            //        ProjectTrust = "0"
            //    />
            // -----------------------------------------------------------------------
            // XMake format:
            // =============
            //    <PropertyGroup>
            //        <CopyProjectDestinationFolder></CopyProjectDestinationFolder>
            //        <CopyProjectUncPath></CopyProjectUncPath>
            //        <CopyProjectOption>0</CopyProjectOption>
            //        <ProjectView>ProjectFiles</ProjectView>
            //        <ProjectTrust>0</ProjectTrust>
            //    </PropertyGroup>
            // -----------------------------------------------------------------------
            this.AddXMakePropertiesFromXMLAttributes(this.globalPropertyGroup, otherProjectSettingsElement);

            // There should be no children of the <OtherProjectSettings> element.
            ProjectXmlUtilities.VerifyThrowProjectNoChildElements(otherProjectSettingsElement);
        }

        /// <summary>
        /// Processes the &lt;UserProperties&gt; element, and everything within it.
        /// Basically, this element and its contents end up going verbatim into
        /// the &lt;ProjectExtensions&gt; section of the newly converted MSBuild project
        /// file.
        ///
        /// The one slight exception is that we do something special for Trinity
        /// conversion.  Specifically, if we detect that this is a White Rabbit
        /// project, we add the flavor GUID for Triumph.
        /// </summary>
        /// <param name="userPropertiesElement"></param>
        /// <param name="isTriumphProject"></param>
        /// <owner>rgoel</owner>
        private void ProcessUserPropertiesElement
            (
            XmlElementWithLocation      userPropertiesElement,
            out bool                    isTriumphProject
            )
        {
            // Make sure this is the <UserProperties> element.
            error.VerifyThrow((userPropertiesElement?.Name == VSProjectElements.userProperties),
                "Expected <UserProperties> element.");

            isTriumphProject = false;

            // All of the <UserProperties> node goes into the <ProjectExtensions> section
            // verbatim.  The one exception is that if we detect a White Rabbit project,
            // then we add the flavor GUID for Triumph.  For example,
            // -----------------------------------------------------------------------
            // Everett format:
            // ===============
            //    <UserProperties
            //        OfficeDocumentPath = ".\ExcelProject41.xls"
            //        OfficeDocumentType = "XLS"
            //        OfficeProject = "true"
            //        blun="1"
            //        bloo="2"
            //        blee="3"
            //    />
            // -----------------------------------------------------------------------
            // XMake format:
            // =============
            //  <Project>
            //
            //      <PropertyGroup>
            //          ...
            //          <ProjectTypeGuids>{BAA0C2D2-18E2-41B9-852F-F413020CAA33};{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}</ProjectTypeGuids>
            //          ...
            //      </PropertyGroup>
            //      ...
            //      <ProjectExtensions>
            //
            //          <VisualStudio>
            //
            //              <UserProperties
            //                  OfficeDocumentPath = ".\ExcelProject41.xls"
            //                  OfficeDocumentType = "XLS"
            //                  OfficeProject = "true"
            //                  blun="1"
            //                  bloo="2"
            //                  blee="3"
            //              />
            //
            //              <CommonProperties>
            //                  <FL_B3B1084D_C66C_4F4C_B279_5C1BA6092AFB>
            //                      <FL_FAE04EC0_301F_11D3_BF4B_00C04F79EFBC />
            //                  </FL_B3B1084D_C66C_4F4C_B279_5C1BA6092AFB>
            //              </CommonProperties>
            //
            //          </VisualStudio>
            //
            //      </ProjectExtensions>
            //
            //  </Project>
            // -----------------------------------------------------------------------

            string visualStudioProjectExtensions = GetProjectExtensionsString(XMakeProjectStrings.visualStudio);
            visualStudioProjectExtensions += userPropertiesElement.OuterXml;

            // If there are any attributes on the <UserProperties> element that indicate that
            // this was a White Rabbit project, add the Triumph flavor GUID.
            if (
                (userPropertiesElement.Attributes[VSProjectAttributes.officeDocumentPath] != null) ||
                (userPropertiesElement.Attributes[VSProjectAttributes.officeDocumentType] != null) ||
                (userPropertiesElement.Attributes[VSProjectAttributes.officeProject] != null)
               )
            {
                isTriumphProject = true;

                // We need the language-specific Guid as well.
                string languageGuid = String.Empty;

                if (this.language == VSProjectElements.cSharp)
                {
                    languageGuid = XMakeProjectStrings.cSharpGuid;
                }
                else if (this.language == VSProjectElements.visualBasic)
                {
                    languageGuid = XMakeProjectStrings.visualBasicGuid;
                }
                else if (this.language == VSProjectElements.visualJSharp)
                {
                    languageGuid = XMakeProjectStrings.visualJSharpGuid;
                }
                else
                {
                    error.VerifyThrow(false, "This project is not recognized as one of the following 3 languages:  C#, VB, VJ#");
                }

                // Add a new global property called ProjectTypeGuids.
                this.globalPropertyGroup.AddProperty(XMakeProjectStrings.projectTypeGuids,
                    "{" + XMakeProjectStrings.triumphProjectTypeGuid + "};{" + languageGuid + "}");

                // Add the Office document as a "None" item in the converted project file.
                XmlAttribute officeDocumentPathAttribute = userPropertiesElement.Attributes[VSProjectAttributes.officeDocumentPath];
                if (officeDocumentPathAttribute != null)
                {
                    string officeDocumentPath = officeDocumentPathAttribute.Value;
                    if (!string.IsNullOrEmpty(officeDocumentPath))
                    {
                        string projectFileDirectory = Path.GetDirectoryName(Path.GetFullPath(this.oldProjectFile));
                        string officeDocumentFullPath = Path.GetFullPath(Path.Combine(projectFileDirectory, officeDocumentPath));

                        // If the office document is in the project directory ...
                        if (String.Equals(projectFileDirectory, Path.GetDirectoryName(officeDocumentFullPath), StringComparison.OrdinalIgnoreCase))
                        {
                            // If the office document actually exists on disk ...
                            if (File.Exists(officeDocumentFullPath))
                            {
                                // Add the office document as a "None" item to the converted project.
                                ProjectItemGroupElement officeDocumentItemGroup = this.xmakeProject.AddItemGroup();
                                officeDocumentItemGroup.AddItem("None", ProjectCollection.Escape(officeDocumentPath));
                            }
                        }
                    }
                }
            }

            SetProjectExtensionsString(XMakeProjectStrings.visualStudio, visualStudioProjectExtensions);
        }

        /// <summary>
        /// Fix hard-coded fully qualified paths in Code Analysis properties.
        ///
        /// Due to a bug in Whidbey configuration cloning, some Code Analysis
        /// properties in Whidbey project files contain fully qualified paths.
        /// They need to be detected and removed during project conversion so
        /// that Code Analysis will work on converted Whidbey projects.
        /// </summary>
        /// <owner>duanek</owner>
        /// <returns>true if changes were required, false otherwise</returns>
        // -----------------------------------------------------------------------
        // XMake format:
        // =============
        //  <Project>
        //      ...
        //      <PropertyGroup>
        //          ...
        //          <CodeAnalysisRuleAssemblies>C:\Program Files\Microsoft Visual Studio 8\Team Tools\Static Analysis Tools\FxCop\\rules</CodeAnalysisRuleAssemblies>
        //          ...
        //      </PropertyGroup>
        //      ...
        //  </Project>
        // -----------------------------------------------------------------------
        private bool FixCodeAnalysisPaths()
        {
            bool changedProject = false;

            // Iterate over all <PropertyGroup> nodes
            // Look for a <CodeAnalysisRuleAssemblies> node
            foreach (ProjectPropertyElement ProjectPropertyElement in xmakeProject.Properties)
            {
                if (ProjectPropertyElement.Name == XMakeProjectStrings.codeAnalysisRuleAssemblies)
                {
                    // We do not want to blindly remove this property since it
                    // is valid for the user to modify it in the project file.

                    // The default value in Microsoft.CodeAnalysis.Targets
                    // is a rooted path ending in "FxCop\\rules".
                    if (Path.IsPathRooted(ProjectPropertyElement.Value))
                    {
                        if (ProjectPropertyElement.Value.EndsWith(@"FxCop\\rules", StringComparison.Ordinal))
                        {
                            ProjectPropertyElement.Parent.RemoveChild(ProjectPropertyElement);
                            changedProject = true;
                        }
                    }

                    break;
                }
            }

            return changedProject;
        }

        /// <summary>
        /// Find the first property with the provided name in the ProjectRootElement.
        /// If none is found, returns null.
        /// </summary>
        private ProjectPropertyElement FindPropertyIfPresent(ProjectRootElement project, string name)
        {
            foreach (ProjectPropertyElement property in project.Properties)
            {
                if (String.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    return property;
                }
            }

            return null;
        }

        /// <summary>
        /// Get the project extensions string with a particular ID,
        /// or empty string if it does not exist or there is no project extensions at all
        /// </summary>
        private string GetProjectExtensionsString(string id)
        {
            ProjectExtensionsElement element = xmakeProject.ProjectExtensions;

            return (element == null) ? String.Empty : element[id];
        }

        /// <summary>
        /// Set a project extensions string with the provided Id,
        /// even if there is no project extensions tag at present
        /// </summary>
        private void SetProjectExtensionsString(string id, string content)
        {
            ProjectExtensionsElement element = xmakeProject.ProjectExtensions;

            if (element == null)
            {
                element = xmakeProject.CreateProjectExtensionsElement();
                xmakeProject.AppendChild(element);
            }

            element[id] = content;
        }
    }
}
