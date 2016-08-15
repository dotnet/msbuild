// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Text;
using System.Xml;

using Microsoft.Build.Framework;
using Microsoft.Build.BuildEngine.Shared;

namespace Microsoft.Build.BuildEngine
{
    // internal delegates to make unit testing the TaskRegistry support easier
    internal delegate string[] GetFiles(string path, string pattern);
    internal delegate XmlDocument LoadXmlFromPath(string path);

    /// <summary>
    /// Encapsulates all the state associated with a tools version. Each ToolsetState
    /// aggregates a Toolset, which contains that part of the state that is externally visible.
    /// </summary>
    internal class ToolsetState
    {
        #region Constructors
        /// <summary>
        /// Internal constructor
        /// </summary>
        /// <param name="engine"></param>
        /// <param name="toolset"></param>
        internal ToolsetState(Engine engine, Toolset toolset)
            : this(engine,
                   toolset,
                   new GetFiles(Directory.GetFiles),
                   new LoadXmlFromPath(ToolsetState.LoadXmlDocumentFromPath)
                  )
        {
        }

        /// <summary>
        /// Additional constructor to make unit testing the TaskRegistry support easier
        /// </summary>
        /// <remarks>
        /// Internal for unit test purposes only.
        /// </remarks>
        /// <param name="engine"></param>
        /// <param name="toolset"></param>
        /// <param name="getFiles"></param>
        /// <param name="loadXmlFromPath"></param>
        internal ToolsetState(Engine engine,
                         Toolset toolset,
                         GetFiles getFiles,
                         LoadXmlFromPath loadXmlFromPath
                        )
        {
            this.parentEngine = engine;
            this.loggingServices = engine.LoggingServices;

            ErrorUtilities.VerifyThrowArgumentNull(toolset, "toolset");
            this.toolset = toolset;

            this.getFiles = getFiles;
            this.loadXmlFromPath = loadXmlFromPath;
        }

        #endregion

        #region Properties
        /// <summary>
        /// Associated Toolset (version name, toolset path, optional associated properties)
        /// </summary>
        internal Toolset Toolset
        {
            get
            {
                return this.toolset;
            }
        }

        /// <summary>
        /// Tools version for this toolset
        /// </summary>
        internal string ToolsVersion
        {
            get
            {
                return this.toolset.ToolsVersion;
            }
        }

        /// <summary>
        /// Tools path for this toolset
        /// </summary>
        internal string ToolsPath
        {
            get
            {
                return this.toolset.ToolsPath;
            }
        }

        /// <summary>
        /// Wrapper for the Toolset property group
        /// </summary>
        internal BuildPropertyGroup BuildProperties
        {
            get 
            {
                return this.toolset.BuildProperties;
            }
        }


        #endregion

        #region Methods

        /// <summary>
        /// Used for validating the project (file) and its imported files against a designated schema.
        ///
        /// PERF NOTE: this property helps to delay creation of the ProjectSchemaValidationHandler object
        /// </summary>
        internal ProjectSchemaValidationHandler SchemaValidator(BuildEventContext buildEventContext)
        {
            if (schemaValidator == null)
            {
                schemaValidator = new ProjectSchemaValidationHandler(buildEventContext, loggingServices, toolset.ToolsPath);
            }

            return schemaValidator;
        }

        /// <summary>
        /// Return a task registry stub for the tasks in the *.tasks file for this toolset
        /// </summary>
        /// <param name="buildEventContext"></param>
        /// <returns></returns>
        internal ITaskRegistry GetTaskRegistry(BuildEventContext buildEventContext)
        {
            RegisterDefaultTasks(buildEventContext);
            return defaultTaskRegistry;
        }

        /// <summary>
        /// Sets the default task registry to the provided value.
        /// </summary>
        /// <param name="taskRegistry"></param>
        internal void SetTaskRegistry(ITaskRegistry taskRegistry)
        {
            ErrorUtilities.VerifyThrowArgumentNull(taskRegistry, "taskRegistry");
            defaultTasksRegistrationAttempted = true;
            defaultTaskRegistry = taskRegistry;
        }

        /// <summary>
        /// Method extracted strictly to make unit testing easier.
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        private static XmlDocument LoadXmlDocumentFromPath(string path)
        {
            XmlDocument xmlDocumentFromPath = new XmlDocument();
            xmlDocumentFromPath.Load(path);
            return xmlDocumentFromPath;
        }

        /// <summary>
        /// Used to load information about default MSBuild tasks i.e. tasks that do not need to be explicitly declared in projects
        /// with the &lt;UsingTask&gt; element. Default task information is read from special files, which are located in the same
        /// directory as the MSBuild binaries.
        /// </summary>
        /// <remarks>
        /// 1) a default tasks file needs the &lt;Project&gt; root tag in order to be well-formed
        /// 2) the XML declaration tag &lt;?xml ...&gt; is ignored
        /// 3) comment tags are always ignored regardless of their placement
        /// 4) the rest of the tags are expected to be &lt;UsingTask&gt; tags
        /// </remarks>
        private void RegisterDefaultTasks(BuildEventContext buildEventContext)
        {
            if (!defaultTasksRegistrationAttempted)
            {
                try
                {
                    this.defaultTaskRegistry = new TaskRegistry();

                    string[] defaultTasksFiles = { };

                    try
                    {
                        defaultTasksFiles = getFiles(toolset.ToolsPath, defaultTasksFilePattern);

                        if (defaultTasksFiles.Length == 0)
                        {
                            loggingServices.LogWarning( buildEventContext, new BuildEventFileInfo(/* this warning truly does not involve any file */ String.Empty),
                                "DefaultTasksFileLoadFailureWarning",
                                defaultTasksFilePattern, toolset.ToolsPath, String.Empty);
                        }
                    }
                    // handle security problems when finding the default tasks files
                    catch (UnauthorizedAccessException e)
                    {
                        loggingServices.LogWarning( buildEventContext, new BuildEventFileInfo(/* this warning truly does not involve any file */ String.Empty),
                            "DefaultTasksFileLoadFailureWarning",
                            defaultTasksFilePattern, toolset.ToolsPath, e.Message);
                    }
                    // handle problems when reading the default tasks files
                    catch (Exception e) // Catching Exception, but rethrowing unless it's an IO related exception.
                    {
                        if (ExceptionHandling.NotExpectedException(e))
                            throw;

                        loggingServices.LogWarning( buildEventContext, new BuildEventFileInfo(/* this warning truly does not involve any file */ String.Empty),
                            "DefaultTasksFileLoadFailureWarning",
                            defaultTasksFilePattern, toolset.ToolsPath, e.Message);
                    }

                    BuildPropertyGroup propertyBag = null;

                    foreach (string defaultTasksFile in defaultTasksFiles)
                    {
                        try
                        {
                            XmlDocument defaultTasks = loadXmlFromPath(defaultTasksFile);

                            // look for the first root tag that is not a comment or an XML declaration -- this should be the <Project> tag
                            // NOTE: the XML parser will guarantee there is only one real root element in the file
                            // but we need to find it amongst the other types of XmlNode at the root.
                            foreach (XmlNode topLevelNode in defaultTasks.ChildNodes)
                            {
                                if (XmlUtilities.IsXmlRootElement(topLevelNode))
                                {
                                    ProjectErrorUtilities.VerifyThrowInvalidProject(topLevelNode.LocalName == XMakeElements.project,
                                        topLevelNode, "UnrecognizedElement", topLevelNode.Name);

                                    ProjectErrorUtilities.VerifyThrowInvalidProject((topLevelNode.Prefix.Length == 0) && (String.Compare(topLevelNode.NamespaceURI, XMakeAttributes.defaultXmlNamespace, StringComparison.OrdinalIgnoreCase) == 0),
                                        topLevelNode, "ProjectMustBeInMSBuildXmlNamespace", XMakeAttributes.defaultXmlNamespace);

                                    // the <Project> tag can only the XML namespace -- no other attributes
                                    foreach (XmlAttribute projectAttribute in topLevelNode.Attributes)
                                    {
                                        ProjectXmlUtilities.VerifyThrowProjectInvalidAttribute(projectAttribute.Name == XMakeAttributes.xmlns, projectAttribute); 
                                    }

                                    // look at all the child tags of the <Project> root tag we found
                                    foreach (XmlNode usingTaskNode in topLevelNode.ChildNodes)
                                    {
                                        if (usingTaskNode.NodeType != XmlNodeType.Comment)
                                        {
                                            ProjectErrorUtilities.VerifyThrowInvalidProject(usingTaskNode.Name == XMakeElements.usingTask,
                                                usingTaskNode, "UnrecognizedElement", usingTaskNode.Name);

                                            // Initialize the property bag if it hasn't been already.
                                            if (propertyBag == null)
                                            {
                                                // Set the value of the MSBuildBinPath/ToolsPath properties.
                                                BuildPropertyGroup reservedPropertyBag = new BuildPropertyGroup();

                                                reservedPropertyBag.SetProperty(
                                                    new BuildProperty(ReservedPropertyNames.binPath, EscapingUtilities.Escape(toolset.ToolsPath),
                                                    PropertyType.ReservedProperty));

                                                reservedPropertyBag.SetProperty(
                                                    new BuildProperty(ReservedPropertyNames.toolsPath, EscapingUtilities.Escape(toolset.ToolsPath),
                                                    PropertyType.ReservedProperty));

                                                // Also set MSBuildAssemblyVersion so that the tasks file can tell between v4 and v12 MSBuild
                                                reservedPropertyBag.SetProperty(
                                                    new BuildProperty(ReservedPropertyNames.assemblyVersion, Constants.AssemblyVersion,
                                                    PropertyType.ReservedProperty));

                                                propertyBag = new BuildPropertyGroup();
                                                propertyBag.ImportInitialProperties(parentEngine.EnvironmentProperties, reservedPropertyBag, BuildProperties, parentEngine.GlobalProperties);
                                            }

                                            defaultTaskRegistry.RegisterTask(new UsingTask((XmlElement)usingTaskNode, true), new Expander(propertyBag), loggingServices, buildEventContext);
                                        }
                                    }

                                    break;
                                }
                            }
                        }
                        // handle security problems when loading the default tasks file
                        catch (UnauthorizedAccessException e)
                        {
                            loggingServices.LogError(buildEventContext, new BuildEventFileInfo(defaultTasksFile), "DefaultTasksFileFailure", e.Message);
                            break;
                        }
                        // handle problems when loading the default tasks file
                        catch (IOException e)
                        {
                            loggingServices.LogError(buildEventContext, new BuildEventFileInfo(defaultTasksFile), "DefaultTasksFileFailure", e.Message);
                            break;
                        }
                        // handle XML errors in the default tasks file
                        catch (XmlException e)
                        {
                            ProjectFileErrorUtilities.VerifyThrowInvalidProjectFile(false, new BuildEventFileInfo(e),
                                "DefaultTasksFileFailure", e.Message);
                        }
                    }
                }
                finally
                {
                    defaultTasksRegistrationAttempted = true;
                }
            }
        }
        #endregion

        #region Data
        // The parent Engine object used for logging
        private Engine parentEngine;

        // Logging service for posting messages
        private EngineLoggingServices loggingServices;

        // The settings for this toolset (version name, path, and properties)
        private Toolset toolset;

        // these files list all default tasks and task assemblies that do not need to be explicitly declared by projects
        private const string defaultTasksFilePattern = "*.tasks";

        // indicates if the default tasks file has already been scanned
        private bool defaultTasksRegistrationAttempted = false;

        // holds all the default tasks we know about and the assemblies they exist in
        private ITaskRegistry defaultTaskRegistry = null;

        // used for validating the project (file) and its imported files against a designated schema
        private ProjectSchemaValidationHandler schemaValidator;

        // private delegates to make unit testing the TaskRegistry support easier
        private GetFiles getFiles = null;
        private LoadXmlFromPath loadXmlFromPath = null;

        #endregion
    }
}
