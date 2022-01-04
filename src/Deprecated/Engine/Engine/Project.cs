// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Xml;
using System.Security;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Text;
using System.Globalization;
#if (!STANDALONEBUILD)
using Microsoft.Internal.Performance;
#if MSBUILDENABLEVSPROFILING 
using Microsoft.VisualStudio.Profiler;
#endif
#endif

using Microsoft.Build.Framework;
using Microsoft.Build.BuildEngine.Shared;

using error = Microsoft.Build.BuildEngine.Shared.ErrorUtilities;

namespace Microsoft.Build.BuildEngine
{
    /// <summary>
    /// The position of a property to be set inside a project file.
    /// </summary>
    public enum PropertyPosition
    {
        /// <summary>
        /// Replace existing compatible property if present.
        /// Otherwise, if possible, create a new property in an existing compatible property group.
        /// If necessary, create a new compatible property group right after the last one in the project.
        /// </summary>
        UseExistingOrCreateAfterLastPropertyGroup = 0,

        /// <summary>
        /// Replace existing compatible property if present.
        /// Otherwise, create the property after the last imported project.
        /// </summary>
        UseExistingOrCreateAfterLastImport = 1
    };

    /// <summary>
    /// Whether we are in the first (properties) pass, or the second (items) pass.
    /// </summary>
    internal enum ProcessingPass
    {
        /// <summary>
        /// First pass (evaluating properties)
        /// </summary>
        Pass1,
        /// <summary>
        /// Second pass (evaluating items)
        /// </summary>
        Pass2
    };

    /// <summary>
    /// This class represents an MSBuild project.  It is a container for items,
    /// properties, and targets.  It can load in project content from in-memory
    /// XML or from an XML file, and it can save to an XML file, preserving
    /// most whitespace and all XML comments.
    ///
    /// All Project objects must be associated with an Engine object, in order
    /// to get at the loggers and other shared information.  Also, when doing
    /// a "build", the Engine needs to keep track of which projects are currently
    /// building.
    /// </summary>
    [Obsolete("This class has been deprecated. Please use Microsoft.Build.Evaluation.Project from the Microsoft.Build assembly instead.")]
    public class Project
    {
        #region Member Data

        // The parent Engine object for this project.
        private Engine                      parentEngine;

        // Event location contextual information for the current project instance
        private BuildEventContext                projectBuildEventContext;

        // We need to know if the projectContextId which was generated during the
        // instantiation of the project instance has been used. If the Id has been used,
        // which would make the value true, we will then generate a new projectContextId 
        // on the next project started event
        private bool haveUsedInitialProjectContextId;

        // A unique ID for this project object that can be used to distinguish projects that
        // have the same file path but different global properties
        private int projectId;

        // indicates if the project (file) and all its imported files are validated against a schema
        private bool                        isValidated;

        // the schema against which to validate the project (file) and all its imported files
        private string                      schemaFile;

        // The XML document for the main project file.
        private XmlDocument                 mainProjectEntireContents;

        // The <project> XML node of the main project file which was loaded
        // (as opposed to any imported project files).
        private XmlElement                  mainProjectElement;

        // The default targets, specified as an optional attribute on the
        // <Project> element.
        private string[]                    defaultTargetNames;

        // The names of the targets specified in the InitialTargets attributes
        // on the <Project> node.  We separate the ones in the main project from
        // the ones in the imported projects to make the object model more sensible.
        // These ArrayLists contain strings.
        private ArrayList                   initialTargetNamesInMainProject;
        private ArrayList                   initialTargetNamesInImportedProjects;

        // The fully qualified path to the project file for this project.
        // If the project was loaded from in-memory XML, this will empty string.
        private string                      fullFileName;

        // This is the directory containing the project file
        private string                      projectDirectory;

        // The set of global properties for this project.  For example, global
        // properties may be set at the command-line via the /p: switch.
        private BuildPropertyGroup               globalProperties;

        // The set of properties coming from environment variables.
        private BuildPropertyGroup               environmentProperties;

        // The set of XMake reserved properties for this project (e.g.,
        // XMakeProjectName).
        private BuildPropertyGroup               reservedProperties;

        // The raw persisted <PropertyGroup>'s in the project file.
        private BuildPropertyGroupCollection     rawPropertyGroups;

        // The set of all <Target>s in the project file after they've been evaluated
        // and made useful for end-users.
        private TargetCollection            targets;

        // The name of the first target in the main project. This is used as the default
        // target when one isn't specified in some other way.
        private string                      nameOfFirstTarget = null;

        // The final set of evaluated and expanded property values for this
        // project, which includes all global properties, environment properties,
        // reserved properties, and properties in the main project file and
        // any imported project files - taking into account property precedence
        // rules, of course.
        internal BuildPropertyGroup              evaluatedProperties;

        // This hash table keeps track of the properties that are referenced inside
        // any "Condition" attribute in the entire project, and for each such
        // property, we keep the list of string values that the property is
        // being tested against.  This is how the IDE gets at the list of
        // configurations for a project.
        private Hashtable                   conditionedPropertiesTable;

        // The raw persisted <ItemGroup>'s in the project file.
        private BuildItemGroupCollection         rawItemGroups;

        // The raw collection of all grouping elements (ItemGroup, BuildPropertyGroup, and Choose.
        private GroupingCollection          rawGroups;

        // Each entry in this contains an BuildItemGroup.  Each of these
        // ItemGroups represents a list of "BuildItem" objects all of the same item type.
        // The key for this Hashtable is a string identifying the item type.
        // This table is what is actually used to feed items into the build process
        // (the tasks).  It represents true reality, and therefore it gets
        // re-computed every time something in the project changes (like a property
        // value for example).
        internal Hashtable                  evaluatedItemsByName;

        // All the item definitions in the logical project, including imports.
        // (Imported definitions don't need to be distinguished, since we don't have OM support
        // for item definitions.)
        private ItemDefinitionLibrary itemDefinitionLibrary;

        /// <summary>
        /// A single virtual BuildItemGroup containing all the items in the project, after
        /// wildcard and property expansion.  This list is what is actually used to
        /// feed items into the build process (the tasks).  It represents true reality,
        /// and therefore it gets re-computed every time something in the project changes
        /// (like a property value for example).
        /// </summary>
        internal BuildItemGroup                  evaluatedItems;

        // This is a Hashtable of ItemGroups.  Each BuildItemGroup contains a list of Items
        // of a the same type.  The key for the Hashtable is the "item type" string.
        // The difference between this hashtable and "evaluatedItemsByType" is that
        // this one ignores all "Condition"s on the items.  So, for example,
        // if an item is declared to be active only in the Debug configuration, it
        // will show up in this list regardless of the current setting for the
        // "Configuration" property.  Furthermore, this list never gets re-computed
        // due to changes in global properties, project properties, other items, etc.
        // This is important for IDE scenarios, because the IDE is going to ask for
        // this list of items and store pointers to them in its own data structures.
        // Therefore, this list of items must survive for the entire IDE session, in
        // case the IDE comes back to MSBuild saying ... "here's this BuildItem you gave
        // me a while ago, please delete it for me now".  There are a couple things
        // that might cause this list to get re-computed, such as adding a new <Import>
        // tag to the project ... however, the Whidbey VisualStudio IDE has no way
        // of doing this, so we're safe there.  The data in this table does *not*
        // get used for "build" purposes, since it is not entirely accurate and up-to-
        // date.  It is only to be consumed by the IDE for purposes of displaying
        // items in the Solution Explorer (for example).
        internal Hashtable                  evaluatedItemsByNameIgnoringCondition;

        // A single BuildItemGroup containing all the items in the project, after
        // wildcard and property expansion, but ignoring "Condition"s.  So, for example,
        // if an item is declared to be active only in the Debug configuration, it
        // will show up in this list regardless of the current setting for the
        // "Configuration" property.  Furthermore, this list never gets re-computed
        // due to changes in global properties, project properties, other items, etc.
        // This is important for IDE scenarios, because the IDE is going to ask for
        // this list of items and store pointers to them in its own data structures.
        // Therefore, this list of items must survive for the entire IDE session, in
        // case the IDE comes back to MSBuild saying ... "here's this BuildItem you gave
        // me a while ago, please delete it for me now".  There are a couple things
        // that might cause this list to get re-computed, such as adding a new <Import>
        // tag to the project ... however, the Whidbey VisualStudio IDE has no way
        // of doing this, so we're safe there.  The data in this table does *not*
        // get used for "build" purposes, since it is not entirely accurate and up-to-
        // date.  It is only to be consumed by the IDE for purposes of displaying
        // items in the Solution Explorer (for example).
        internal BuildItemGroup                  evaluatedItemsIgnoringCondition;

        // This array list contains the ordered list of <UsingTask> elements in
        // the project file and all the imported files.  When we
        // object model for these things has been worked out, there should
        // be an actual class here that we define, rather than an array list.
        private UsingTaskCollection         usingTasks;

        // holds all the tasks the project knows about and the assemblies they exist in
        // NOTE: tasks and their assemblies are declared in a project with the <UsingTask> tag
        private ITaskRegistry                taskRegistry;

        // The <ProjectExtensions> XML node if there is one.
        private XmlNode                     projectExtensionsNode;

        // This hash table simply keeps track of the list of imported project
        // files so that we can detect a circular import to prevent infinite
        // recursion.
        private ImportCollection            imports;

        // Tells us if anything has changed that would require us to re-read and re-
        // process the XML.  The main scenario here would be the addition or removal
        // of an <Import> tag.
        private bool                        dirtyNeedToReprocessXml;

        // Tells us if anything in this project has changed since the last time
        // we evaluated all our properties and items.  That could include all sorts
        // of things like properties (including global properties), items, etc.
        // Note: The only reason this is internal is for unit tests.
        private bool                       dirtyNeedToReevaluate;

        // Tells us whether we need to re-evaluate the conditions on global warnings and errors.
        internal bool                        dirtyNeedToReevaluateGlobalWarningsAndErrors;

        // Tells us if anything in this project has changed since the last time
        // we either loaded or saved the project file to disk.  That could include all sorts
        // of things like properties (including global properties), items, etc.
        private bool                        dirtyNeedToSaveProjectFile;

        // Tells us the timestamp of when the project was last touched in a way
        // that would require it to need to be saved.
        private DateTime                    timeOfLastDirty;

        // Tells us whether the project is currently in the "reset" state, meaning
        // that all of the targets are marked NotStarted.
        private bool                        isReset;

        // This variable indicates whether a particular project was loaded by the host
        // (e.g., the IDE) and therefore needs to be kept around permanently, or whether
        // this is just a project that is only being built and thus can be discarded
        // when the build is complete.  The default is "true", and any Project instantiated
        // directly by the host will always have a value of "true", because there's no
        // way for the host to change it.  The only entity that should every be changing
        // this to "false" is the Engine itself because it knows that we're just building
        // this project and don't need to keep it around for design-time scenarios.
        private bool                        isLoadedByHost;

        // This controls whether or not the building of targets/tasks is enabled for this
        // project.  This is for security purposes in case a host wants to closely
        // control which projects it allows to run targets/tasks.
        private BuildEnabledSetting buildEnabled;

        /// 0 means not building; >=1 means building.
        // The refcount may be greater than 1 because the MSBuild task may call back in to
        // cause the project to be built again.
        private int buildingCount = 0;

        // The MSBuild ToolsVersion associated with the project file
        private string toolsVersion = null;

        /// true if the ToolsVersion of this project was overridden; false otherwise.
        private bool overridingToolsVersion = false;

        // Whether when we read ToolsVersion="4.0" or greater on the <Project> tag, we treat it as "4.0".
        // See explanation in DefaultToolsVersion property.
        private bool treatinghigherToolsVersionsAs40;

        // Set to true if the client wants this project to be unloaded
        private bool needToUnloadProject = false;

        // The load settings for this project
        private ProjectLoadSettings loadSettings = ProjectLoadSettings.None;

        /// <summary>
        /// Items need the project directory in order to evaluate their built-in
        /// metadata (like "%(FullPath)") when their itemspec is relative. We store this
        /// here in thread-local-storage because we cannot modify the public constructors
        /// to require it, and also it can change during the life of a BuildItem
        /// (when the item is passed to another project).
        /// This is also used when evaluating conditions.
        /// </summary>
        [ThreadStatic]
        private static string perThreadProjectDirectory;

        #endregion

        private enum BuildEnabledSetting
        {
            BuildEnabled,
            BuildDisabled,
            UseParentEngineSetting
        }

        #region Constructors

        /// <summary>
        /// Creates an instance of this class for the given engine, specifying a tools version to
        /// use during builds of this project.
        /// </summary>
        /// <owner>RGoel</owner>
        /// <param name="engine">Engine that will build this project. May be null if the global engine is expected.</param>
        /// <param name="toolsVersion">Tools version to use during builds of this project instance. May be null,
        /// in which case we will use the value in the Project's ToolsVersion attribute, or else the engine
        /// default value.</param>
        public Project
        (
            Engine engine,
            string toolsVersion
        )
        {
#if MSBUILDENABLEVSPROFILING 
            try
            {
                DataCollection.CommentMarkProfile(8808, "Construct Project Using Old OM - Start");
#endif 
#if (!STANDALONEBUILD)
            using (new CodeMarkerStartEnd(CodeMarkerEvent.perfMSBuildProjectConstructBegin, CodeMarkerEvent.perfMSBuildProjectConstructEnd))
#endif
            {
                if (engine == null)
                {
                    engine = Engine.GlobalEngine;
                }

                this.parentEngine = engine;
                this.projectId = parentEngine.GetNextProjectId();
                this.projectBuildEventContext = new BuildEventContext(parentEngine.NodeId, BuildEventContext.InvalidTargetId, parentEngine.GetNextProjectId(), BuildEventContext.InvalidTaskId);

                this.isLoadedByHost = true;
                this.buildEnabled = BuildEnabledSetting.UseParentEngineSetting;

                this.isValidated = false;

                // Create a new XML document and add a <Project> element.  This way, the
                // project is always in a valid state from the beginning, and now somebody
                // can start programmatically adding stuff to the <Project>.
                this.mainProjectEntireContents = new XmlDocument();
                this.mainProjectElement = mainProjectEntireContents.CreateElement(XMakeElements.project, XMakeAttributes.defaultXmlNamespace);
                this.mainProjectEntireContents.AppendChild(mainProjectElement);

                // initialize all case-insensitive hash-tables
                this.conditionedPropertiesTable = new Hashtable(StringComparer.OrdinalIgnoreCase);
                this.evaluatedItemsByName = new Hashtable(StringComparer.OrdinalIgnoreCase);
                this.evaluatedItemsByNameIgnoringCondition = new Hashtable(StringComparer.OrdinalIgnoreCase);

                // Create the group collection.  All collection elements are stored here.
                this.rawGroups = new GroupingCollection(null /* null parent means this is the master collection */);

                // Initialize all property-related objects.
                // (see above for initialization of this.conditionedPropertiesTable)
                this.globalProperties = null;
                this.environmentProperties = null;
                this.reservedProperties = null;
                // We still create the rawPropertyGroups collection, but
                // it's just a facade over rawGroups
                this.rawPropertyGroups = new BuildPropertyGroupCollection(this.rawGroups);
                this.evaluatedProperties = new BuildPropertyGroup();

                // Initialize all item-related objects.
                // (see above for initialization of this.evaluatedItemsByName and this.evaluatedItemsByNameIgnoringCondition
                // We still create the rawItemGroups collection, but it's just a facade over rawGroups
                this.rawItemGroups = new BuildItemGroupCollection(this.rawGroups);
                this.evaluatedItems = new BuildItemGroup();
                this.evaluatedItemsIgnoringCondition = new BuildItemGroup();

                this.itemDefinitionLibrary = new ItemDefinitionLibrary(this);

                // Initialize all target- and task-related objects.
                this.usingTasks = new UsingTaskCollection();
                this.imports = new ImportCollection(this);
                this.taskRegistry = new TaskRegistry();
                this.targets = new TargetCollection(this);

                // Initialize the default targets, initial targets, and project file name.
                this.defaultTargetNames = new string[0];
                this.initialTargetNamesInMainProject = new ArrayList();
                this.initialTargetNamesInImportedProjects = new ArrayList();
                this.FullFileName = String.Empty;
                this.projectDirectory = String.Empty;

                this.projectExtensionsNode = null;

                // If the toolsVersion is null, we will use the value specified in
                // the Project element's ToolsVersion attribute, or else the default if that
                // attribute is not present.
                if (toolsVersion != null)
                {
                    this.ToolsVersion = toolsVersion;
                }

                this.MarkProjectAsDirtyForReprocessXml();
                // The project doesn't really need to be saved yet; there's nothing in it!
                this.dirtyNeedToSaveProjectFile = false;
                this.IsReset = false;

                // Grab some initial properties from the Engine.
                // Global properties and reserved properties need to be cloned, because
                // different projects may have different sets of properties or values
                // for these.  Environment properties don't have to be cloned, because
                // the environment is captured once at engine instantiation, and
                // shared by all projects thereafter.
                this.GlobalProperties = this.parentEngine.GlobalProperties;
                this.EnvironmentProperties = this.parentEngine.EnvironmentProperties;
            }
#if MSBUILDENABLEVSPROFILING 
            }
            finally
            {
                DataCollection.CommentMarkProfile(8809, "Construct Project Using Old OM - End");
            }
#endif
        }

        /// <summary>
        /// Creates an instance of this class for the given engine.
        /// </summary>
        /// <param name="engine">Engine that will build this project.</param>
        public Project
        (
            Engine engine
        )
            : this(engine, null)
        {
        }

        /// <summary>
        /// This default constructor creates a new Project object associated with
        /// the global Engine object.
        /// </summary>
        /// <owner>RGoel</owner>
        public Project
            (
            )
            : this(null)
        {
        }

        #endregion

        #region Properties

        /// <summary>
        /// The directory of this project. This is needed for evaluating conditions,
        /// and for evaluating itemspecs. It's easier to share it via TLS than to access
        /// it directly from every item.
        /// </summary>
        internal static string PerThreadProjectDirectory
        {
            get { return perThreadProjectDirectory; }
            set { perThreadProjectDirectory = value; }
        }

        /// <summary>
        /// The one and only item definition library for this project.
        /// </summary>
        internal ItemDefinitionLibrary ItemDefinitionLibrary
        {
            get { return itemDefinitionLibrary; }
            set { itemDefinitionLibrary = value; }
        }

        //Have we used the initial project context Id that was created when the project was instantiated
        internal bool HaveUsedInitialProjectContextId
        {
            get
            {
                return haveUsedInitialProjectContextId;
            }

            set
            {
                haveUsedInitialProjectContextId = value;
            }
        }

        // A unique ID for this project object that can be used to distinguish projects that
        // have the same file path but different global properties
        internal int Id
        {
            get
            {
                return projectId;
            }
        }

        /// <summary>
        /// Returns the table of evaluated items by type.
        /// </summary>
        /// <owner>DavidLe</owner>
        internal Hashtable EvaluatedItemsByName
        {
            get
            {
                return this.evaluatedItemsByName;
            }
        }

        /// <summary>
        /// Gets or sets the fully qualified path + filename of the project file. This could be empty-string if the project
        /// doesn't have a file associated with it -- for example, if we were given the XML in memory.
        /// </summary>
        /// <owner>RGoel</owner>
        /// <value>The full path of the project file.</value>
        public string FullFileName
        {
            get
            {
                return fullFileName;
            }

            set
            {
                if (this.IsLoadedByHost)
                {
                    this.ParentEngine.OnRenameProject(this, this.fullFileName, value);
                }

                fullFileName = value;
                this.SetProjectFileReservedProperties();

                this.MarkProjectAsDirtyForReevaluation();
            }
        }

        /// <summary>
        /// Read-write accessor for the "DefaultTargets" attribute of the
        /// &lt;Project&gt; element.  This is passed in and out as a semicolon-separated
        /// list of target names.
        /// </summary>
        /// <owner>RGoel</owner>
        public string DefaultTargets
        {
            get
            {
                return String.Join("; ", this.defaultTargetNames);
            }

            set
            {
                ErrorUtilities.VerifyThrowArgumentNull(value, "value");

                // PERF NOTE: we create this property bag each time this accessor is called because this is not a very common
                // operation -- it's better to recreate this property bag than keep it around all the time hogging memory
                BuildPropertyGroup initialProperties = new BuildPropertyGroup();
                initialProperties.ImportInitialProperties(EnvironmentProperties, ReservedProperties, Toolset.BuildProperties, GlobalProperties);

                // allow "DefaultTargets" to only reference env. vars., command-line properties and reserved properties
                SetDefaultTargets(value, initialProperties);
                mainProjectElement.SetAttribute(XMakeAttributes.defaultTargets, value);
                this.MarkProjectAsDirty();
            }
        }

        /// <summary>
        /// Returns the array of actual target names that will be built by default. First choice is
        /// the defaultTargets attribute on the Project node, if not present we fall back to the first target
        /// in the project file. Return value is null if there are no targets in the project file.
        /// </summary>
        /// <owner>LukaszG</owner>
        internal string[] DefaultBuildTargets
        {
            get
            {
                // If there is a default target name, then use it. Otherwise, just
                // pick the first target in the main project.
                if (this.defaultTargetNames.Length != 0)
                {
                    return this.defaultTargetNames;
                }
                else if (this.nameOfFirstTarget != null)
                {
                    return new string[] { this.nameOfFirstTarget };
                }

                return null;
            }
        }

        /// <summary>
        /// Read-write accessor for the "InitialTargets" attribute of the
        /// &lt;Project&gt; element.  This is passed in and out as a semicolon-separated
        /// list of target names.  The "get" returns all of the initial targets in both
        /// the main project and all imported projects (after property expansion).  The
        /// "set" only sets the initial targets for the main project.
        /// </summary>
        /// <owner>RGoel</owner>
        public string InitialTargets
        {
            get
            {
                // Return the concatenation of the initial target names from the main project and the ones from 
                // all the imported projects.  Join target names together with semicolons in between.
                return String.Join("; ", (string[]) this.CombinedInitialTargetNames.ToArray(typeof(string)));
            }

            set
            {
                ErrorUtilities.VerifyThrowArgumentNull(value, "value");

                // PERF NOTE: we create this property bag each time this accessor is called because this is not a very common
                // operation -- it's better to recreate this property bag than keep it around all the time hogging memory
                BuildPropertyGroup initialProperties = new BuildPropertyGroup();
                initialProperties.ImportInitialProperties(EnvironmentProperties, ReservedProperties, Toolset.BuildProperties, GlobalProperties);

                this.initialTargetNamesInMainProject.Clear();
                this.initialTargetNamesInMainProject.AddRange((new Expander(initialProperties)).ExpandAllIntoStringListLeaveEscaped(value, null));
                mainProjectElement.SetAttribute(XMakeAttributes.initialTargets, value);
                this.MarkProjectAsDirty();
            }
        }

        /// <summary>
        /// Returns an ArrayList containing strings which are all of the target names that are considerd
        /// "initial targets" -- those targets that get run every time before any other targets.
        /// </summary>
        /// <owner>RGoel</owner>
        private ArrayList CombinedInitialTargetNames
        {
            get
            {
                ArrayList combinedInitialTargetNames = new ArrayList(this.initialTargetNamesInMainProject.Count +
                    this.initialTargetNamesInImportedProjects.Count);

                combinedInitialTargetNames.AddRange(this.initialTargetNamesInMainProject);
                combinedInitialTargetNames.AddRange(this.initialTargetNamesInImportedProjects);

                return combinedInitialTargetNames;
            }
        }

        /// <summary>
        /// Gets the parent engine object.
        /// </summary>
        /// <owner>RGoel</owner>
        /// <value>Engine object.</value>
        public Engine ParentEngine
        {
            get
            {
                ErrorUtilities.VerifyThrowInvalidOperation(parentEngine != null, "ProjectInvalidUnloaded");

                return parentEngine;
            }
        }

        /// <summary>
        /// This property indicates whether a particular project was loaded by the host
        /// (e.g., the IDE) and therefore needs to be kept around permanently, or whether
        /// this is just a project that is only being built and thus can be discarded
        /// when the build is complete.  The default is "true", and any Project instantiated
        /// directly by the host will always have a value of "true", because there's no
        /// way for the host to change it.  The only entity that should every be changing
        /// this to "false" is the Engine itself because it knows that we're just building
        /// this project and don't need to keep it around for design-time scenarios.
        /// </summary>
        /// <owner>RGoel</owner>
        internal bool IsLoadedByHost
        {
            get
            {
                return this.isLoadedByHost;
            }
            set
            {
                this.isLoadedByHost = value;
            }
        }

        /// <summary>
        /// Indicates if the project (file) is to be validated against a schema.
        /// </summary>
        /// <owner>SumedhK</owner>
        public bool IsValidated
        {
            get
            {
                return isValidated;
            }

            set
            {
                isValidated = value;
            }
        }

        /// <summary>
        /// Is this project in the process of building?
        /// </summary>
        /// <owner>JomoF</owner>
        internal bool IsBuilding
        {
            get
            {
                return buildingCount>0;
            }
        }

        /// <summary>
        /// The schema against which the project (file) and all its imported files are validated.
        /// </summary>
        /// <owner>SumedhK</owner>
        public string SchemaFile
        {
            get
            {
                return schemaFile;
            }

            set
            {
                // NOTE: null is ok, in which case we'll validate against the default schema
                schemaFile = value;
            }
        }

        /// <summary>
        /// This controls whether or not the building of targets/tasks is enabled for this
        /// project.  This is for security purposes in case a host wants to closely
        /// control which projects it allows to run targets/tasks.  By default, for a newly
        /// created project, we will use whatever setting is in the parent engine.
        /// </summary>
        /// <owner>RGoel</owner>
        public bool BuildEnabled
        {
            get
            {
                switch (this.buildEnabled)
                {
                    case BuildEnabledSetting.BuildEnabled:
                        return true;

                    case BuildEnabledSetting.BuildDisabled:
                        return false;

                    case BuildEnabledSetting.UseParentEngineSetting:
                        return this.ParentEngine.BuildEnabled;

                    default:
                        ErrorUtilities.VerifyThrow(false, "How did this.buildEnabled get a bogus value?");
                        return false;
                }
            }

            set
            {
                this.buildEnabled = value ? BuildEnabledSetting.BuildEnabled : BuildEnabledSetting.BuildDisabled;
            }
        }

        /// <summary>
        /// When gotten, returns the effective tools version being used by this project.
        /// If the tools version is being overridden, the overriding value will be the effective tools version.
        /// Otherwise, if there is a ToolsVersion attribute on the Project element, that is the effective tools version.
        /// Otherwise, the default tools version of the parent engine is the effective tools version.
        ///
        /// When set, overrides the current tools version of this project with the provided value.
        ///
        /// NOTE: This is distinct to the ToolsVersion attribute, if any, on the Project element.
        /// To get and set the ToolsVersion attribute on the Project element use the Project.DefaultToolsVersion
        /// property.
        /// </summary>
        public string ToolsVersion
        {
            get
            {
                // We could have a toolsversion already (either from the Project element, or 
                // from an externally set override value). If not, read it off the Project
                // element now
                if (!OverridingToolsVersion)
                {
                    return DefaultToolsVersion;
                }

                return this.toolsVersion;
            }
            internal set
            {
                error.VerifyThrowArgumentLength(value, "value");

                // Make sure the tools version we're trying to set is recognized by the engine
                error.VerifyThrowInvalidOperation(this.ParentEngine.ToolsetStateMap.ContainsKey(value), "UnrecognizedToolsVersion", value);

                this.toolsVersion = value;
                this.overridingToolsVersion = true;

                MarkProjectAsDirtyForReprocessXml();
            }
        }

        /// <summary>
        /// Returns true if the ToolsVersion of this project is being overridden; false otherwise.
        /// </summary>
        /// <owner>JeffCal</owner>
        internal bool OverridingToolsVersion
        {
            get
            {
                return this.overridingToolsVersion;
            }
        }

        /// <summary>
        /// Public read-write accessor for the ToolsVersion xml attribute found on the
        /// &lt;Project /&gt; element.  If this attribute is not present on the &lt;Project/&gt;
        /// element, getting the value will return the default tools version of the parent Engine.
        ///
        /// NOTE: This value is distinct from the effective tools version used during a build,
        /// as that value may be overridden during construction of the Project instance or
        /// by setting the Project.ToolsVersion property. Setting this attribute value will not change the
        /// effective tools version if it has been overridden. To change the effective tools version,
        /// set the Project.ToolsVersion property.
        /// </summary>
        public string DefaultToolsVersion
        {
            get
            {
                string toolsVersionAttribute = null;

                if (ProjectElement.HasAttribute(XMakeAttributes.toolsVersion))
                {
                    toolsVersionAttribute = ProjectElement.GetAttribute(XMakeAttributes.toolsVersion);

                    // hosts may need to treat toolsversions later than 4.0 as 4.0
                    // given them that ability through an environment variable
                    if (Environment.GetEnvironmentVariable("MSBUILDTREATHIGHERTOOLSVERSIONASCURRENT") == "1")
                    {
                        Version toolsVersionAsVersion;

                        if (Version.TryParse(toolsVersionAttribute, out toolsVersionAsVersion))
                        {
                            // This is higher than an FX 4.0 normal toolsversion
                            // Therefore we need to enter best effort mode
                            // and present a toolsversion 4.0
                            if (toolsVersionAsVersion.Major >= 4 && toolsVersionAsVersion.Minor > 0)
                            {
                                toolsVersionAttribute = "4.0";
                                treatinghigherToolsVersionsAs40 = true;
                            }
                        }
                    }

                    // If the toolset specified in the project is not present
                    // then we'll use the current version, i.e. "4.0"
                    if (!this.ParentEngine.ToolsetStateMap.ContainsKey(toolsVersionAttribute))
                    {
                        toolsVersionAttribute = "4.0";
                        treatinghigherToolsVersionsAs40 = true;
                    }
                }

                return String.IsNullOrEmpty(toolsVersionAttribute) ? ParentEngine.DefaultToolsVersion
                                                                   : toolsVersionAttribute;
            }
            set
            {
                // We intentionally don't check that this is a known tools version value: it can be anything,
                // because the host might want to persist this project and use it later when the tools
                // version is actually valid
                ProjectElement.SetAttribute(XMakeAttributes.toolsVersion, value);

                if (!overridingToolsVersion)
                {
                    this.toolsVersion = DefaultToolsVersion;
                }

                MarkProjectAsDirtyForReprocessXml();
            }
        }

        /// <summary>
        /// Public read  accessor to determine if the Project file has the ToolsVersion xml attribute
        /// e.g. &lt;Project ToolsVersion="3.5"/&gt; . This is different to knowing the inherited
        /// value and allows us to spot Whidbey (VS 8.0) projects.
        /// </summary>
        public bool HasToolsVersionAttribute
        {
            get
            {
                return ProjectElement.HasAttribute(XMakeAttributes.toolsVersion);
            }
        }

        /// <summary>
        /// This private property is here for convenience so that the error checking needn't be duplicated throughout
        /// the project object.
        /// </summary>
        private ToolsetState Toolset
        {
            get
            {
                // Check that we actually have a valid tools version at this point. It's possible we don't, if for example,
                // we're using a tools version off the project attribute, which is allowed to have any value you like,
                // unless you are actually trying to build or load the project.
                ProjectFileErrorUtilities.VerifyThrowInvalidProjectFile(this.ParentEngine.ToolsetStateMap.ContainsKey(ToolsVersion),
                    new BuildEventFileInfo(FullFileName), "UnrecognizedToolsVersion", ToolsVersion);

                return this.ParentEngine.ToolsetStateMap[ToolsVersion];
            }
        }

        /// <summary>
        /// The project's task registry.
        /// </summary>
        internal ITaskRegistry TaskRegistry
        {
            get
            {
                return taskRegistry;
            }
            set
            {
                ErrorUtilities.VerifyThrowArgumentNull(value, "value");
                taskRegistry = value;
            }
        }

        /// <summary>
        /// The project directory where the project file is in, this can be empty if the project is constructed in memory and does
        /// not come from a file location
        /// </summary>
        internal string ProjectDirectory
        {
            get
            {
                return projectDirectory;
            }
        }

        /// <summary>
        /// Read-write accessor for the project's global properties collection.
        /// To set or modify global properties, a caller can hand us an entire
        /// new BuildPropertyGroup here, or can simply modify the properties in the
        /// BuildPropertyGroup that is already here.  Global properties are those
        /// defined via the "/p:" switch on the MSBuild.exe command-line, or
        /// properties like "Configuration" set by the IDE prior to invoking MSBuild.
        /// </summary>
        /// <owner>RGoel</owner>
        public BuildPropertyGroup GlobalProperties
        {
            get
            {
                // Lazy creation
                if (this.globalProperties == null)
                {
                    this.globalProperties = new BuildPropertyGroup(this);
                }

                return this.globalProperties;
            }

            set
            {
                error.VerifyThrowArgumentNull(value, "value");

                // Any time a global property changes (which could happen
                // without even calling this "set" accessor), we need to reprocess
                // the entire project file from the beginning, reading in all
                // the XML again.  This is mainly because global properties can
                // be referenced inside an <Import> tag, and thus a change in a
                // global property could result in an entirely different imported
                // project file.
                //
                // However, we know that most project files (particularly VS-generated
                // ones) don't use property references inside the <Import> tag, and
                // it's a pretty big perf hit to reprocess all the XML, so we've decided
                // for now to just take a shortcut and only re-evaluate the project file
                // instead of re-processing it.
                //
                // By the way, since normal project properties can also be referenced
                // inside an <Import> tag, we actually also have the same exact issue for
                // normal properties.  Again, we intentionally choose to be slightly
                // incorrect, so that we don't take the perf hit of re-processing all
                // the XML every time any property value changes.

                // Unhook the old globalProperties from this project.
                globalProperties?.ClearParentProject();

                globalProperties = value.Clone(true);

                // Mark the new globalProperties as belonging to this project.
                globalProperties.ParentProject = this;
                this.MarkProjectAsDirtyForReevaluation();
            }
        }

        /// <summary>
        /// Read-write internal accessor for the property group containing
        /// environment variables.
        /// </summary>
        /// <owner>RGoel</owner>
        internal BuildPropertyGroup EnvironmentProperties
        {
            get
            {
                if (this.environmentProperties == null)
                {
                    this.environmentProperties = new BuildPropertyGroup();
                    // We don't hook up the environment properties with a ParentProject,
                    // because multiple projects will be sharing the same environment
                    // block.
                }

                return this.environmentProperties;
            }

            set
            {
                this.environmentProperties = value;
            }
        }

        /// <summary>
        /// Read-only internal accessor for the property group containing
        /// MSBuild reserved properties (like "MSBuildProjectName", for example).
        /// </summary>
        /// <owner>RGoel</owner>
        internal BuildPropertyGroup ReservedProperties
        {
            get
            {
                // Lazy creation
                if (this.reservedProperties == null)
                {
                    this.reservedProperties = new BuildPropertyGroup(this);
                }

                return this.reservedProperties;
            }
        }

        /// <summary>
        /// Read-only accessor for the final set of evaluated properties for
        /// this project.  This takes into account all conditions and property
        /// expansions, and gives back a single linear collection of project-level
        /// properties, which includes global properties, environment variable
        /// properties, reserved properties, and normal/imported properties.
        /// Through this collection, the caller can modify any normal
        /// properties, and the changes will be reflected in the project file
        /// when it is saved again.  However, adding or deleting properties
        /// from this collection will not impact the project.
        ///
        /// PERF WARNING: cloning a BuildPropertyGroup can be very expensive -- use
        /// only when a copy of the entire property bag is strictly necessary
        /// </summary>
        /// <owner>RGoel</owner>
        public BuildPropertyGroup EvaluatedProperties
        {
            get
            {
                this.RefreshProjectIfDirty();
                return this.evaluatedProperties.Clone(false /* shallow clone */);
            }
        }

        /// <summary>
        /// Get the event context information for this project instance
        /// </summary>
        internal BuildEventContext ProjectBuildEventContext
        {
            get
            {
                return projectBuildEventContext;
            }

            set
            {
                projectBuildEventContext = value;
            }
        }

        /// <summary>
        /// Read-only accessor for the final collection of evaluated items, taking
        /// into account all conditions and property expansions.  Through this
        /// collection, the caller can modify any of the items present, and it
        /// will be reflected in the project file the next time it is saved.
        /// However, adding or deleting items from this collection will not impact
        /// the project.
        /// </summary>
        /// <owner>RGoel</owner>
        public BuildItemGroup EvaluatedItems
        {
            get
            {
                this.RefreshProjectIfDirty();
                return evaluatedItems.Clone(false);
            }
        }

        /// <summary>
        /// Read-only accessor for the collection of evaluated items, taking into
        /// account property expansions and wildcards, but ignoring "Condition"s.
        /// This way, an IDE can display all items regardless of whether they're
        /// relevant for a particular build flavor or not.  Through this
        /// collection, the caller can modify any of the items present, and it
        /// will be reflected in the project file the next time it is saved.
        /// However, adding or deleting items from this collection will not impact
        /// the project.
        ///
        /// See the comments for the "evaluatedItemsIgnoringCondition" member
        /// variable up above.
        /// </summary>
        /// <owner>RGoel</owner>
        public BuildItemGroup EvaluatedItemsIgnoringCondition
        {
            get
            {
                if (this.dirtyNeedToReprocessXml)
                {
                    this.ProcessMainProjectElement();
                }

                return evaluatedItemsIgnoringCondition;
            }
        }

        /// <summary>
        /// Read-only accessor for the raw property groups of this project.
        /// This is essentially a reflection of the data in the XML for this
        /// project's properties as well as any &lt;Import&gt;'d projects.
        /// </summary>
        /// <owner>RGoel</owner>
        public BuildPropertyGroupCollection PropertyGroups
        {
            get
            {
                error.VerifyThrow(this.rawPropertyGroups != null,
                    "Project object not initialized.  rawPropertyGroups is null.");
                return this.rawPropertyGroups;
            }
        }

        /// <summary>
        /// Read-only accessor for the target groups of this project.
        /// </summary>
        /// <owner>RGoel</owner>
        public TargetCollection Targets
        {
            get
            {
                error.VerifyThrow(this.targets != null,
                    "Project object not initialized.  targets is null.");
                return this.targets;
            }
        }

        /// <summary>
        /// Read-only accessor for the UsingTask elements of this project.
        /// </summary>
        /// <owner>LukaszG</owner>
        public UsingTaskCollection UsingTasks
        {
            get
            {
                ErrorUtilities.VerifyThrow(this.usingTasks != null, "Project object not initialized. usingTasks is null.");
                return this.usingTasks;
            }
        }

        /// <summary>
        /// Read-only accessor for the imported projects of this project
        /// </summary>
        /// <owner>LukaszG</owner>
        public ImportCollection Imports
        {
            get
            {
                ErrorUtilities.VerifyThrow(this.imports != null, "Project object not initialized. imports is null.");
                return this.imports;
            }
        }

        /// <summary>
        /// Read-only accessor for the raw item groups of this project.
        /// This is essentially a reflection of the data in the XML for this
        /// project's items as well as any &lt;Import&gt;'d projects.
        /// </summary>
        /// <owner>RGoel</owner>
        public BuildItemGroupCollection ItemGroups
        {
            get
            {
                error.VerifyThrow(this.rawItemGroups != null,
                    "Project object not initialized.  rawItemGroups is null.");
                return this.rawItemGroups;
            }
        }

        /// <summary>
        /// Read-only accessor for the string of Xml representing this project.
        /// Used for verification in unit testing.
        /// </summary>
        /// <owner>RGoel</owner>
        public string Xml
        {
            get
            {
                using (StringWriter stringWriter = new StringWriter(CultureInfo.InvariantCulture))
                {
                    this.Save((TextWriter) stringWriter);
                    return stringWriter.ToString();
                }
            }
        }

        /// <summary>
        /// Read-only accessor for the XmlDocument representing this project.
        /// Used for verification in unit testing.
        /// </summary>
        /// <owner>RGoel</owner>
        internal XmlDocument XmlDocument
        {
            get
            {
                return this.mainProjectEntireContents;
            }
        }

        /// <summary>
        /// Read-only accessor for main &lt;Project&gt; element.
        /// </summary>
        /// <value></value>
        /// <owner>RGoel</owner>
        internal XmlElement ProjectElement
        {
            get
            {
                return this.mainProjectElement;
            }
        }

        /// <summary>
        /// Is this project currently in a reset state in terms of the build?  That is,
        /// is it ready to be built?  A project that is reset means that all of the
        /// targets are marked "NotStarted", and there are no output items or output
        /// properties present in the evaluated lists.
        /// </summary>
        /// <remarks>
        /// This accessor is really just here for unit-testing purposes only.
        /// </remarks>
        /// <owner>RGoel</owner>
        internal bool IsReset
        {
            get
            {
                return this.isReset;
            }

            set
            {
                this.isReset = value;
            }
        }

        /// <summary>
        /// Read-only accessor for conditioned properties table.
        /// </summary>
        /// <value></value>
        /// <owner>DavidLe</owner>
        internal Hashtable ConditionedProperties
        {
            get
            {
                return this.conditionedPropertiesTable;
            }
        }

        /// <summary>
        /// Tells you whether this project file is dirty such that it would need
        /// to get saved to disk.
        /// </summary>
        /// <owner>RGoel</owner>
        public bool IsDirty
        {
            get
            {
                return this.dirtyNeedToSaveProjectFile;
            }
        }

        /// <summary>
        /// Tells you whether this project file is dirty such that it would need
        /// to get reevaluated.
        /// </summary>
        internal bool IsDirtyNeedToReevaluate
        {
            get { return this.dirtyNeedToReevaluate; }
        }

        /// <summary>
        /// Returns the timestamp of when the project was last touched in a way
        /// that would require it to need to be saved.
        /// </summary>
        /// <value>The DateTime object indicating when project was dirtied.</value>
        /// <owner>RGoel</owner>
        public DateTime TimeOfLastDirty
        {
            get
            {
                return this.timeOfLastDirty;
            }
        }

        /// <summary>
        /// Returns the project file's ?xml node, or null if it's not present
        /// </summary>
        /// <owner>LukaszG</owner>
        private XmlDeclaration XmlDeclarationNode
        {
            get
            {
                if (mainProjectEntireContents?.HasChildNodes == true)
                {
                    return mainProjectEntireContents.FirstChild as XmlDeclaration;
                }
                else
                {
                    return null;
                }
            }
        }

        /// <summary>
        /// Internal method for getting the project file encoding. When we have the managed vsproject assembly, this should be made public.
        /// </summary>
        public Encoding Encoding
        {
            get
            {
                // If encoding is unknown (that is, no ?xml node is present), we default to UTF8
                Encoding encoding = Encoding.UTF8;

                XmlDeclaration xmlDeclarationNode = this.XmlDeclarationNode;

                if (xmlDeclarationNode != null)
                {
                    string encodingName = xmlDeclarationNode.Encoding;

                    if (encodingName.Length > 0)
                    {
                        encoding = Encoding.GetEncoding(encodingName);
                    }
                }

                return encoding;
            }
        }

        /// <summary>
        /// Load settings for this project
        /// </summary>
        internal ProjectLoadSettings LoadSettings
        {
            get
            {
                return this.loadSettings;
            }
        }

        #endregion

        /// <summary>
        /// Returns a single evaluated property value.
        /// Call this to retrieve a few properties. If you need to retrieve many properties
        /// use EvaluatedProperty accessor.
        /// </summary>
        /// <param name="propertyName">Name of the property to retrieve.</param>
        /// <returns>The property value.</returns>
        public string GetEvaluatedProperty(string propertyName)
        {
            this.RefreshProjectIfDirty();

            BuildProperty property = this.evaluatedProperties[propertyName];

            // Project system needs to know the difference between a property not existing,
            // a property that is set to empty string.
            return property?.FinalValue;
        }

        /// <summary>
        /// Sets the project's default targets from the given list of semi-colon-separated target names after expanding all
        /// embedded properties in the list.
        /// </summary>
        /// <owner>SumedhK</owner>
        /// <param name="defaultTargetsList"></param>
        /// <param name="propertyBag"></param>
        private void SetDefaultTargets(string defaultTargetsList, BuildPropertyGroup propertyBag)
        {
            Expander propertyExpander = new Expander(propertyBag);

            this.defaultTargetNames = propertyExpander.ExpandAllIntoStringListLeaveEscaped(defaultTargetsList, null).ToArray();

            BuildProperty defaultTargetsProperty = new BuildProperty(ReservedPropertyNames.projectDefaultTargets,
                                                           propertyExpander.ExpandAllIntoStringLeaveEscaped(defaultTargetsList, null),
                                                           PropertyType.ReservedProperty);

            this.ReservedProperties.SetProperty(defaultTargetsProperty);

            // we also need to push this property directly into the evaluatedProperties bucket
            // since this property was computed "late", i.e. after the initial evaluation.
            this.evaluatedProperties.SetProperty(defaultTargetsProperty);
        }

        /// <summary>
        /// Determines whether a project file can be considered equivalent to this Project, taking into account
        /// the set of global properties and the tools version (if any) that that project file
        /// is going to be built with.
        /// </summary>
        /// <param name="projectFullPath"></param>
        /// <param name="projectGlobalProperties"></param>
        /// <param name="projectToolsVersion">May be null, indicating the value from the project attribute, or the global default, should be used</param>
        /// <returns></returns>
        internal bool IsEquivalentToProject(string projectFullPath, BuildPropertyGroup projectGlobalProperties, string projectToolsVersion)
        {
            if (!String.Equals(projectFullPath, this.FullFileName, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (projectToolsVersion == null)
            {
                // There's not going to be a tools version specified for the other project, so it's going
                // to use the one on its Project tag (or the global default). How can we figure that out
                // without loading it? Well we know that we have the same file path as the other project,
                // so we can just look at our own Project tag!
                projectToolsVersion = this.DefaultToolsVersion;
            }

            return String.Equals(ToolsVersion, projectToolsVersion, StringComparison.OrdinalIgnoreCase)
                && this.GlobalProperties.IsEquivalent(projectGlobalProperties);
        }

        /// <summary>
        /// For internal use only by the Engine object when it lets go of a project.
        /// </summary>
        /// <owner>RGoel</owner>
        internal void ClearParentEngine
            (
            )
        {
            this.parentEngine = null;
        }

        /// <summary>
        /// This forces a re-evaluation of the project the next time somebody
        /// calls EvaluatedProperties or EvaluatedItems.  It is also a signal
        /// that the project file is dirty and needs to be saved to disk.
        /// </summary>
        /// <owner>RGoel</owner>
        public void MarkProjectAsDirty
            (
            )
        {
            this.MarkProjectAsDirtyForReevaluation();
            this.MarkProjectAsDirtyForSave();
        }

        /// <summary>
        /// This forces a re-evaluation of the project the next time somebody
        /// calls EvaluatedProperties or EvaluatedItems.
        /// </summary>
        /// <owner>RGoel</owner>
        internal void MarkProjectAsDirtyForReevaluation
            (
            )
        {
            this.dirtyNeedToReevaluate = true;
            this.dirtyNeedToReevaluateGlobalWarningsAndErrors = true;
        }

        /// <summary>
        /// This marks a project as needing to be saved to disk.
        /// </summary>
        /// <owner>RGoel</owner>
        internal void MarkProjectAsDirtyForSave
            (
            )
        {
            this.dirtyNeedToSaveProjectFile = true;
            this.timeOfLastDirty = DateTime.Now;
        }

        /// <summary>
        /// Indicates to the project that on the next build, we actually need to walk the
        /// entire XML structure from scratch.  It's pretty rare that this is required.
        /// Examples include changes to &lt;Import&gt; or &lt;Target&gt; tags.  These kinds of changes
        /// can require us to re-compute some of our data structures, and in some cases,
        /// there's no easy way to do it, except to walk the XML again.
        /// </summary>
        /// <owner>RGoel</owner>
        internal void MarkProjectAsDirtyForReprocessXml
            (
            )
        {
            this.MarkProjectAsDirty ();
            this.dirtyNeedToReprocessXml = true;
        }

        /// <summary>
        /// This returns a list of possible values for a particular property.  It
        /// gathers this list by looking at all of the "Condition" attributes
        /// in the project file.
        /// </summary>
        /// <param name="propertyName"></param>
        /// <returns></returns>
        /// <owner>RGoel</owner>
        public string[] GetConditionedPropertyValues
        (
            string propertyName
        )
        {
            this.RefreshProjectIfDirty();
            StringCollection propertyValues = (StringCollection)this.conditionedPropertiesTable[propertyName];

            if (propertyValues == null)
            {
                return new string[0];
            }
            else
            {
                // We need to transform the StringCollection into an array, so COM
                // clients can access it.
                string[] returnArray = new string[propertyValues.Count];

                int i = 0;
                foreach (string propertyValue in propertyValues)
                {
                    // Data leaving the engine, so time to unescape.
                    returnArray[i++] = EscapingUtilities.UnescapeAll(propertyValue);
                }

                return returnArray;
            }
        }

        /// <summary>
        /// Retrieves a group of evaluated items of a particular item type.
        /// </summary>
        /// <owner>RGoel</owner>
        /// <param name="itemName"></param>
        /// <returns>items of requested type</returns>
        public BuildItemGroup GetEvaluatedItemsByName
        (
            string itemName
        )
        {
            this.RefreshProjectIfDirty();

            BuildItemGroup itemsByName = (BuildItemGroup)evaluatedItemsByName[itemName];

            if (itemsByName == null)
            {
                return new BuildItemGroup();
            }
            else
            {
                return itemsByName.Clone(false);
            }
        }

        /// <summary>
        /// Retrieves a group of evaluated items of a particular item type. This is really just about the items that are persisted
        /// in the project file, ignoring all "Condition"s, so that an IDE can display all items regardless of whether they're
        /// relevant for a particular build flavor or not.
        /// </summary>
        /// <owner>RGoel</owner>
        /// <remarks>See the comments for the "evaluatedItemsByNameIgnoringCondition" member variable up above.</remarks>
        /// <param name="itemName"></param>
        /// <returns>items of requested type</returns>
        public BuildItemGroup GetEvaluatedItemsByNameIgnoringCondition
        (
            string itemName
        )
        {
            if (this.dirtyNeedToReprocessXml)
            {
                this.ProcessMainProjectElement();
            }

            BuildItemGroup itemsByName = (BuildItemGroup)evaluatedItemsByNameIgnoringCondition[itemName];

            if (itemsByName == null)
            {
                itemsByName = new BuildItemGroup();
            }

            return itemsByName;
        }

        /// <summary>
        /// Prepares the MSBuildToolsPath and MSBuildBinPath reserved properties
        /// </summary>
        private void ProcessToolsVersionDependentProperties()
        {
            // Add the XMakeBinPath property, and set its value to the full path of
            // where this assembly is currently running from.
            this.ReservedProperties.SetProperty(new BuildProperty(ReservedPropertyNames.binPath,
                EscapingUtilities.Escape(this.Toolset.ToolsPath),
                PropertyType.ReservedProperty));

            this.ReservedProperties.SetProperty(new BuildProperty(ReservedPropertyNames.toolsPath,
                EscapingUtilities.Escape(this.Toolset.ToolsPath),
                PropertyType.ReservedProperty));

            this.ReservedProperties.SetProperty(new BuildProperty(ReservedPropertyNames.toolsVersion,
                EscapingUtilities.Escape(ToolsVersion),
                PropertyType.ReservedProperty));
        }

        /// <summary>
        /// Sets the filename for this project, and sets the appropriate MSBuild
        /// reserved properties accordingly.
        /// </summary>
        /// <owner>rgoel</owner>
        private void SetProjectFileReservedProperties
            (
            )
        {
            this.ReservedProperties.SetProperty(new BuildProperty(ReservedPropertyNames.startupDirectory,
                    EscapingUtilities.Escape(parentEngine.StartupDirectory), PropertyType.ReservedProperty));

            this.ReservedProperties.SetProperty(new BuildProperty(ReservedPropertyNames.buildNodeCount,
                    parentEngine.EngineCpuCount.ToString(CultureInfo.CurrentCulture), PropertyType.ReservedProperty));

            this.ReservedProperties.SetProperty(new BuildProperty(ReservedPropertyNames.programFiles32,
                    FrameworkLocationHelper.programFiles32, PropertyType.ReservedProperty));

            this.ReservedProperties.SetProperty(new BuildProperty(ReservedPropertyNames.assemblyVersion,
                    Constants.AssemblyVersion, PropertyType.ReservedProperty));

            if (this.fullFileName.Length == 0)
            {
                // If we don't have a filename for this project, then we can't set all
                // the reserved properties related to the project file.  However, we
                // still need to set the MSBuildProjectDirectory property because this
                // is actually used by us to set the current directory before starting
                // the build, and after each task finishes.  So, here we set
                // MSBuildProjectDirectory = <current directory>.
                this.ReservedProperties.SetProperty(new BuildProperty(ReservedPropertyNames.projectDirectory,
                    EscapingUtilities.Escape(Directory.GetCurrentDirectory()), PropertyType.ReservedProperty));
            }
            else
            {
                FileInfo projectFileInfo = new FileInfo(this.fullFileName);

                string directoryName = projectFileInfo.DirectoryName;

                this.ReservedProperties.SetProperty(new BuildProperty(ReservedPropertyNames.projectDirectory,
                    EscapingUtilities.Escape(directoryName), PropertyType.ReservedProperty));

                this.ReservedProperties.SetProperty(new BuildProperty(ReservedPropertyNames.projectFile,
                    EscapingUtilities.Escape(projectFileInfo.Name), PropertyType.ReservedProperty));

                this.ReservedProperties.SetProperty(new BuildProperty(ReservedPropertyNames.projectExtension,
                    EscapingUtilities.Escape(projectFileInfo.Extension), PropertyType.ReservedProperty));

                this.ReservedProperties.SetProperty(new BuildProperty(ReservedPropertyNames.projectFullPath,
                    EscapingUtilities.Escape(projectFileInfo.FullName), PropertyType.ReservedProperty));

                this.ReservedProperties.SetProperty(new BuildProperty(ReservedPropertyNames.projectName,
                    EscapingUtilities.Escape(Path.GetFileNameWithoutExtension(this.fullFileName)), PropertyType.ReservedProperty));

                int rootLength = Path.GetPathRoot(directoryName).Length;
                string projectDirectoryNoRoot = directoryName.Substring(rootLength);
                projectDirectoryNoRoot = FileUtilities.EnsureNoTrailingSlash(projectDirectoryNoRoot);
                projectDirectoryNoRoot = FileUtilities.EnsureNoLeadingSlash(projectDirectoryNoRoot);

                this.ReservedProperties.SetProperty(new BuildProperty(ReservedPropertyNames.projectDirectoryNoRoot,
                    EscapingUtilities.Escape(projectDirectoryNoRoot), PropertyType.ReservedProperty));
            }

            this.projectDirectory = this.ReservedProperties[ReservedPropertyNames.projectDirectory].FinalValue;
        }

        /// <summary>
        /// Resets the state of each target in this project back to "NotStarted",
        /// so that a subsequent build will actually build those targets again.
        /// </summary>
        /// <owner>rgoel</owner>
        public void ResetBuildStatus
            (
            )
        {
            if (!this.IsReset)
            {
                foreach (Target target in this.targets)
                {
                    target.ResetBuildStatus();
                }

                // get rid of all intermediate (virtual) properties that were output by tasks, and restore any original properties
                // that were overridden by those task properties
                this.evaluatedProperties.RevertAllOutputProperties();

                // Delete all intermediate (virtual) items.
                this.evaluatedItems.RemoveAllIntermediateItems();

                foreach (BuildItemGroup itemGroup in this.evaluatedItemsByName.Values)
                {
                    itemGroup.RemoveAllIntermediateItems();
                }

                this.IsReset = true;
            }
        }

        /// <summary>
        /// Reads in the contents of this project from a project XML file on disk.
        /// </summary>
        /// <exception cref="InvalidProjectFileException"></exception>
        public void Load
        (
            string projectFileName
        )
        {
            Load(projectFileName, ProjectLoadSettings.None);
        }

        /// <summary>
        /// Reads in the contents of this project from a project XML file on disk.
        /// </summary>
        /// <exception cref="InvalidProjectFileException"></exception>
        public void Load
        (
            string projectFileName,
            ProjectLoadSettings projectLoadSettings
        )
        {
            Load(projectFileName, projectBuildEventContext, projectLoadSettings);
        }

        /// <summary>
        /// Reads in the contents of this project from a project XML file on disk.
        /// </summary>
        /// <exception cref="InvalidProjectFileException"></exception>
        internal void Load
        (
            string projectFileName,
            BuildEventContext buildEventContext,
            ProjectLoadSettings projectLoadSettings
        )
        {
            ErrorUtilities.VerifyThrowArgumentNull(projectFileName, nameof(projectFileName));
            ErrorUtilities.VerifyThrowArgument(projectFileName.Length > 0, "EmptyProjectFileName");
            ErrorUtilities.VerifyThrowArgument(File.Exists(projectFileName), "ProjectFileNotFound", projectFileName);

#if (!STANDALONEBUILD)
            using (new CodeMarkerStartEnd(CodeMarkerEvent.perfMSBuildProjectLoadFromFileBegin, CodeMarkerEvent.perfMSBuildProjectLoadFromFileEnd))
#endif
            {
                string projectFullFileName = Path.GetFullPath(projectFileName);

                try
                {
#if MSBUILDENABLEVSPROFILING 
                string beginProjectLoad = String.Format(CultureInfo.CurrentCulture, "Load Project {0} Using Old OM - Start", projectFullFileName);
                DataCollection.CommentMarkProfile(8806, beginProjectLoad);
#endif
                    XmlDocument projectDocument = null;
                    if (IsSolutionFilename(projectFileName))
                    {
                        SolutionParser sp = new SolutionParser();

                        sp.SolutionFile = projectFileName;
                        sp.ParseSolutionFile();

                        // Log any comments from the solution parser
                        if (sp.SolutionParserComments.Count > 0)
                        {
                            foreach (string comment in sp.SolutionParserComments)
                            {
                                ParentEngine.LoggingServices.LogCommentFromText(buildEventContext, MessageImportance.Low, comment);
                            }
                        }

                        // Pass the toolsVersion of this project through, which will be not null if there was a /tv:nn switch
                        // Although we only get an XmlDocument, not a Project object back, it's still needed
                        // to determine which <UsingTask> tags to put in, whether to put a ToolsVersion parameter
                        // on <MSBuild> task tags, and what MSBuildToolsPath to use when scanning child projects
                        // for dependency information.
                        SolutionWrapperProject.Generate(sp, this, toolsVersion, buildEventContext);
                    }
                    else if (IsVCProjFilename(projectFileName))
                    {
                        ProjectFileErrorUtilities.VerifyThrowInvalidProjectFile(false, new BuildEventFileInfo(projectFileName), "ProjectUpgradeNeededToVcxProj", projectFileName);
                    }
                    else
                    {
                        projectDocument = new XmlDocument();
                        // XmlDocument.Load() may throw an XmlException
                        projectDocument.Load(projectFileName);
                    }

                    // Setting the FullFileName causes this project to be "registered" with
                    // the engine.  (Well, okay, "registered" is the wrong word ... but the
                    // engine starts keeping close track of this project in its tables.)
                    // We want to avoid this until we're sure that the XML is valid and
                    // the document can be read in.  Bug VSWhidbey 415236.
                    this.FullFileName = projectFullFileName;

                    if (!IsSolutionFilename(projectFileName))
                    {
                        InternalLoadFromXmlDocument(projectDocument, projectLoadSettings);
                    }

                    // This project just came off the disk, so it is certainly not dirty yet.
                    this.dirtyNeedToSaveProjectFile = false;
                }
                // handle errors in project syntax
                catch (InvalidProjectFileException e)
                {
                    ParentEngine.LoggingServices.LogInvalidProjectFileError(buildEventContext, e);
                    throw;
                }
                // handle errors in path resolution
                catch (SecurityException e)
                {
                    ParentEngine.LoggingServices.LogError(buildEventContext, new BuildEventFileInfo(FullFileName), "InvalidProjectFile", e.Message);

                    ProjectFileErrorUtilities.VerifyThrowInvalidProjectFile(false, new BuildEventFileInfo(FullFileName),
                        "InvalidProjectFile", e.Message);
                }
                // handle errors in path resolution
                catch (NotSupportedException e)
                {
                    ParentEngine.LoggingServices.LogError(buildEventContext, new BuildEventFileInfo(FullFileName), "InvalidProjectFile", e.Message);

                    ProjectFileErrorUtilities.VerifyThrowInvalidProjectFile(false, new BuildEventFileInfo(FullFileName),
                        "InvalidProjectFile", e.Message);
                }
                // handle errors in loading project file
                catch (IOException e)
                {
                    ParentEngine.LoggingServices.LogError(buildEventContext, new BuildEventFileInfo(FullFileName), "InvalidProjectFile", e.Message);

                    ProjectFileErrorUtilities.VerifyThrowInvalidProjectFile(false, new BuildEventFileInfo(FullFileName),
                        "InvalidProjectFile", e.Message);
                }
                // handle errors in loading project file
                catch (UnauthorizedAccessException e)
                {
                    ParentEngine.LoggingServices.LogError(buildEventContext, new BuildEventFileInfo(FullFileName), "InvalidProjectFile", e.Message);
                    ProjectFileErrorUtilities.VerifyThrowInvalidProjectFile(false, new BuildEventFileInfo(FullFileName),
                        "InvalidProjectFile", e.Message);
                }
                // handle XML parsing errors (when reading project file)
                catch (XmlException e)
                {
                    BuildEventFileInfo fileInfo = new BuildEventFileInfo(e);

                    ParentEngine.LoggingServices.LogError(buildEventContext, fileInfo, "InvalidProjectFile", e.Message);

                    ProjectFileErrorUtilities.VerifyThrowInvalidProjectFile(false, fileInfo,
                        "InvalidProjectFile", e.Message);
                }
                finally
                {
                    // Flush the logging queue
                    ParentEngine.LoggingServices.ProcessPostedLoggingEvents();
#if MSBUILDENABLEVSPROFILING 
                DataCollection.CommentMarkProfile(8807, "Load Project Using Old OM - End");
#endif
                }
            }
        }

        /// <summary>
        /// Reads in the contents of this project from a string containing the Xml contents.
        /// </summary>
        /// <exception cref="InvalidProjectFileException"></exception>
        public void Load
        (
            TextReader textReader
        )
        {
            Load(textReader, ProjectLoadSettings.None);
        }

        /// <summary>
        /// Reads in the contents of this project from a string containing the Xml contents.
        /// </summary>
        /// <exception cref="InvalidProjectFileException"></exception>
        public void Load
        (
            TextReader textReader,
            ProjectLoadSettings projectLoadSettings
        )
        {
            ErrorUtilities.VerifyThrowArgumentNull(textReader, nameof(textReader));

            try
            {
                XmlDocument projectDocument = new XmlDocument();
                // XmlDocument.Load() may throw an XmlException
                projectDocument.Load(textReader);
                InternalLoadFromXmlDocument(projectDocument, projectLoadSettings);

                // This means that as far as we know, this project hasn't been saved to disk yet.
                this.dirtyNeedToSaveProjectFile = true;
            }
            // handle errors in project syntax
            catch (InvalidProjectFileException e)
            {
                ParentEngine.LoggingServices.LogInvalidProjectFileError(projectBuildEventContext, e);
                throw;
            }
            // handle XML parsing errors (when reading XML contents)
            catch (XmlException e)
            {
                BuildEventFileInfo fileInfo = new BuildEventFileInfo(e);

                ParentEngine.LoggingServices.LogError(projectBuildEventContext, fileInfo, "InvalidProjectFile", e.Message);

                ProjectFileErrorUtilities.VerifyThrowInvalidProjectFile(false, fileInfo,
                    "InvalidProjectFile", e.Message);
            }
        }

        /// <summary>
        /// Reads in the contents of this project from a string containing the Xml contents.
        /// </summary>
        /// <exception cref="InvalidProjectFileException"></exception>
        public void LoadXml
        (
            string projectXml
        )
        {
            LoadXml(projectXml, ProjectLoadSettings.None);
        }

        /// <summary>
        /// Reads in the contents of this project from a string containing the Xml contents.
        /// </summary>
        /// <exception cref="InvalidProjectFileException"></exception>
        public void LoadXml
        (
            string projectXml,
            ProjectLoadSettings projectLoadSettings
        )
        {
            ErrorUtilities.VerifyThrowArgumentNull(projectXml, nameof(projectXml));

            try
            {
                XmlDocument projectDocument = new XmlDocument();
                // XmlDocument.Load() may throw an XmlException
                projectDocument.LoadXml(projectXml);

                InternalLoadFromXmlDocument(projectDocument, projectLoadSettings);

                // This means that as far as we know, this project hasn't been saved to disk yet.
                this.dirtyNeedToSaveProjectFile = true;
            }
            // handle errors in project syntax
            catch (InvalidProjectFileException e)
            {
                ParentEngine.LoggingServices.LogInvalidProjectFileError(projectBuildEventContext, e);
                throw;
            }
            // handle XML parsing errors (when reading XML contents)
            catch (XmlException e)
            {
                BuildEventFileInfo fileInfo = new BuildEventFileInfo(e);

                ParentEngine.LoggingServices.LogError(projectBuildEventContext, null,fileInfo, "InvalidProjectFile", e.Message);

                ProjectFileErrorUtilities.VerifyThrowInvalidProjectFile(false, fileInfo,
                    "InvalidProjectFile", e.Message);
            }
        }

        /// <summary>
        /// Reads in the contents of this project from an in-memory XmlDocument handed to us.
        /// </summary>
        /// <exception cref="InvalidProjectFileException"></exception>
        internal void LoadFromXmlDocument
            (
            XmlDocument projectXml,
            BuildEventContext buildEventContext,
            ProjectLoadSettings projectLoadSettings
            )
        {
            ErrorUtilities.VerifyThrowArgumentNull(projectXml, nameof(projectXml));

            try
            {
                InternalLoadFromXmlDocument(projectXml, projectLoadSettings);
            }
            // handle errors in project syntax
            catch (InvalidProjectFileException e)
            {
                ParentEngine.LoggingServices.LogInvalidProjectFileError(buildEventContext, e);
                throw;
            }
        }

        /// <summary>
        /// Reads in the contents of this project from an in-memory XmlDocument.
        /// </summary>
        /// <remarks>This method throws exceptions -- it is the responsibility of the caller to handle them.</remarks>
        /// <exception cref="InvalidProjectFileException"></exception>
        private void InternalLoadFromXmlDocument(XmlDocument projectXml, ProjectLoadSettings projectLoadSettings)
        {
            try
            {
                ErrorUtilities.VerifyThrow(projectXml != null, "Need project XML.");

                this.loadSettings = projectLoadSettings;
                this.mainProjectEntireContents = projectXml;

                // Get the top-level nodes from the XML.
                XmlNodeList projectFileNodes = mainProjectEntireContents.ChildNodes;

                // The XML parser will guarantee that we only have one real root element,
                // but we need to find it amongst the other types of XmlNode at the root.
                foreach (XmlNode childNode in projectFileNodes)
                {
                    if (XmlUtilities.IsXmlRootElement(childNode))
                    {
                        this.mainProjectElement = (XmlElement)childNode;
                        break;
                    }
                }

                // Verify that we found a non-comment root node
                ProjectErrorUtilities.VerifyThrowInvalidProject(this.mainProjectElement != null,
                    this.mainProjectEntireContents,
                    "NoRootProjectElement", XMakeElements.project);

                // If we have a <VisualStudioProject> node, tell the user they must upgrade the project
                ProjectErrorUtilities.VerifyThrowInvalidProject(mainProjectElement.LocalName != XMakeElements.visualStudioProject,
                    mainProjectElement, "ProjectUpgradeNeeded");

                // This node must be a <Project> node.
                ProjectErrorUtilities.VerifyThrowInvalidProject(this.mainProjectElement.LocalName == XMakeElements.project,
                    this.mainProjectElement, "UnrecognizedElement", this.mainProjectElement.Name);

                ProjectErrorUtilities.VerifyThrowInvalidProject((mainProjectElement.Prefix.Length == 0) && (String.Equals(mainProjectElement.NamespaceURI, XMakeAttributes.defaultXmlNamespace, StringComparison.OrdinalIgnoreCase)),
                    mainProjectElement, "ProjectMustBeInMSBuildXmlNamespace", XMakeAttributes.defaultXmlNamespace);

                MarkProjectAsDirtyForReprocessXml();
                this.RefreshProjectIfDirty();
            }
            catch (InvalidProjectFileException)
            {
                // Make sure the engine doesn't keep bad projects around
                if (this.IsLoadedByHost)
                {
                    Engine rememberParentEngine = this.ParentEngine;
                    this.ParentEngine.UnloadProject(this);
                    this.parentEngine = rememberParentEngine;
                }
                throw;
            }
        }

        /// <summary>
        /// Saves the current contents of the project to an XML project file on disk.
        /// This method will NOT add the ?xml node if it's not already present
        /// </summary>
        /// <param name="projectFileName"></param>
        /// <owner>RGoel</owner>
        public void Save
            (
            string projectFileName
            )
        {
            Save(projectFileName, this.Encoding);
        }

        /// <summary>
        /// Saves the current contents of the project to an XML project file on
        /// disk using the supplied encoding.
        /// </summary>
        /// <param name="projectFileName"></param>
        /// <param name="encoding"></param>
        /// <owner>LukaszG</owner>
        public void Save
            (
            string projectFileName,
            Encoding encoding
            )
        {
#if (!STANDALONEBUILD)
            using (new CodeMarkerStartEnd(CodeMarkerEvent.perfMSBuildProjectSaveToFileBegin, CodeMarkerEvent.perfMSBuildProjectSaveToFileEnd))
#endif
            {
#if MSBUILDENABLEVSPROFILING 
            try
            {
                string beginProjectSave = String.Format(CultureInfo.CurrentCulture, "Save Project {0} Using Old OM - Start", projectFileName);
                DataCollection.CommentMarkProfile(8810, beginProjectSave);
#endif

                // HIGHCHAR: Project.SaveToFileWithEncoding accepts encoding from caller.
                using (ProjectWriter projectWriter = new ProjectWriter(projectFileName, encoding))
                {
                    projectWriter.Initialize(mainProjectEntireContents, XmlDeclarationNode);
                    mainProjectEntireContents.Save(projectWriter);
                }

                // Update the project filename/path if it has changed.
                string newFullProjectFilePath = Path.GetFullPath(projectFileName);
                if (!String.Equals(newFullProjectFilePath, this.FullFileName, StringComparison.OrdinalIgnoreCase))
                {
                    this.FullFileName = newFullProjectFilePath;
                }

                // reset the dirty flag
                dirtyNeedToSaveProjectFile = false;
#if MSBUILDENABLEVSPROFILING 
            }
            finally
            {
                string endProjectSave = String.Format(CultureInfo.CurrentCulture, "Save Project {0} Using Old OM - End", projectFileName);
                DataCollection.CommentMarkProfile(8810, endProjectSave);
            }
#endif
            }
        }

        /// <summary>
        /// Saves the current contents of the project to a TextWriter object.
        /// </summary>
        /// <param name="textWriter"></param>
        /// <owner>RGoel</owner>
        public void Save
            (
            TextWriter textWriter
            )
        {
            using (ProjectWriter projectWriter = new ProjectWriter(textWriter))
            {
                projectWriter.Initialize(mainProjectEntireContents, XmlDeclarationNode);
                mainProjectEntireContents.Save(projectWriter);
            }
        }

        /// <summary>
        /// Adds a new &lt;PropertyGroup&gt; element to the project, and returns the
        /// corresponding BuildPropertyGroup object which can then be populated with
        /// properties.
        /// </summary>
        /// <param name="insertAtEndOfProject"></param>
        /// <returns></returns>
        /// <owner>RGoel</owner>
        public BuildPropertyGroup AddNewPropertyGroup
        (
            bool insertAtEndOfProject
        )
        {
            BuildPropertyGroup newPropertyGroup = new BuildPropertyGroup
                (
                this,
                this.mainProjectEntireContents,
                false /* Not imported */
                );

            if (insertAtEndOfProject)
            {
                this.mainProjectElement.AppendChild(newPropertyGroup.PropertyGroupElement);
                this.rawPropertyGroups.InsertAtEnd(newPropertyGroup);
            }
            else
            {
                // We add the new property group just after the last property group in the
                // main project file.  If there are currently no property groups in the main
                // project file, we add this one to the very beginning of the project file.
                BuildPropertyGroup lastLocalPropertyGroup = this.rawPropertyGroups.LastLocalPropertyGroup;
                if (lastLocalPropertyGroup != null)
                {
                    this.mainProjectElement.InsertAfter(newPropertyGroup.PropertyGroupElement,
                        lastLocalPropertyGroup.PropertyGroupElement);
                    this.rawPropertyGroups.InsertAfter(newPropertyGroup, lastLocalPropertyGroup);
                }
                else
                {
                    this.mainProjectElement.PrependChild(newPropertyGroup.PropertyGroupElement);
                    this.rawPropertyGroups.InsertAtBeginning(newPropertyGroup);
                }
            }

            this.MarkProjectAsDirty();

            return newPropertyGroup;
        }

        /// <summary>
        /// Adds a new &lt;PropertyGroup&gt; element to the project, and returns the
        /// corresponding BuildPropertyGroup object which can then be populated with
        /// properties.
        /// </summary>
        /// <owner>DavidLe</owner>
        /// <param name="importedFilename"></param>
        /// <param name="condition"></param>
        private BuildPropertyGroup AddNewImportedPropertyGroup
        (
            string importedFilename,
            string condition
        )
        {
            BuildPropertyGroup newPropertyGroup = new BuildPropertyGroup
                (
                this,
                importedFilename,
                condition
                );

            if (this.imports[importedFilename] != null)
            {
                this.rawGroups.InsertAfter(newPropertyGroup, this.imports[importedFilename]);
            }
            else
            {
                this.rawPropertyGroups.InsertAtBeginning(newPropertyGroup);
            }

            return newPropertyGroup;
        }

        /// <summary>
        /// Sets (or adds) a property to the project at a sensible location.
        /// </summary>
        /// <param name="propertyName"></param>
        /// <param name="propertyValue"></param>
        /// <owner>RGoel</owner>
        public void SetProperty
        (
            string propertyName,
            string propertyValue
        )
        {
            this.SetProperty(propertyName, propertyValue, null);
        }

        /// <summary>
        /// This method is called from the IDE to set a particular property at
        /// the project level.  The IDE doesn't care which property group it's
        /// in, as long as it gets set.  This method will search the existing
        /// property groups for a property with this name.  If found, it will
        /// change the value in place.  Otherwise, it will either add a new
        /// property to that property group, or possibly even add a new property
        /// group to the project.
        ///
        /// This method also takes the "Condition" string for the property group
        /// that the IDE wants this property placed under.
        /// </summary>
        /// <owner>RGoel, DavidLe</owner>
        /// <param name="propertyName"></param>
        /// <param name="propertyValue"></param>
        /// <param name="condition"></param>
        public void SetProperty
        (
            string propertyName,
            string propertyValue,
            string condition
        )
        {
            SetProperty
            (
                propertyName,
                propertyValue,
                condition,
                PropertyPosition.UseExistingOrCreateAfterLastPropertyGroup
            );
        }

        /// <summary>
        /// Sets the value of a property that comes from an imported project.
        /// Updates the current project (the one this method is called on) with
        /// a property that has no Xml behind it, and updates the imported project
        /// with a real backed property.
        /// </summary>
        /// <owner>DavidLe</owner>
        /// <param name="propertyName"></param>
        /// <param name="propertyValue"></param>
        /// <param name="condition"></param>
        /// <param name="importProject"></param>
        public void SetImportedProperty
        (
            string propertyName,
            string propertyValue,
            string condition,
            Project importProject
        )
        {
            SetImportedProperty
            (
                propertyName,
                propertyValue,
                condition,
                importProject,
                PropertyPosition.UseExistingOrCreateAfterLastPropertyGroup
            );
        }

        /// <summary>
        /// Set a property at a particular position inside the project file.
        /// The property will be in a group that has the specified condition.
        /// If necessary, a new property or property group will be created.
        /// </summary>
        /// <param name="propertyName">Property name.</param>
        /// <param name="propertyValue">Property value.</param>
        /// <param name="condition">The condition for this property.</param>
        /// <param name="position">Specifies the position within the project file for the property.</param>
        /// <owner>RGoel</owner>
        public void SetProperty
        (
            string propertyName,
            string propertyValue,
            string condition,
            PropertyPosition position
        )
        {
            SetPropertyAtHelper(propertyName, propertyValue, condition, /* importedProperty */ false, null, position);
        }

        /// <summary>
        /// Sets a property, and optionally escapes it so that it will be treated as a literal
        /// value despite any special characters that may be in it.
        /// </summary>
        /// <param name="propertyName"></param>
        /// <param name="propertyValue"></param>
        /// <param name="condition"></param>
        /// <param name="position"></param>
        /// <param name="treatPropertyValueAsLiteral"></param>
        /// <owner>RGoel</owner>
        public void SetProperty
            (
            string propertyName,
            string propertyValue,
            string condition,
            PropertyPosition position,
            bool treatPropertyValueAsLiteral
            )
        {
            this.SetProperty(propertyName,
                treatPropertyValueAsLiteral ? EscapingUtilities.Escape(propertyValue) : propertyValue,
                condition, position);
        }

        /// <summary>
        /// Set a property at a particular position inside an imported project file.
        /// The property will be in a group that has the specified condition.
        /// If necessary, a new property or property group will be created.
        /// </summary>
        /// <param name="propertyName">Property name.</param>
        /// <param name="propertyValue">Property value.</param>
        /// <param name="condition">The condition for this property.</param>
        /// <param name="importedProject">Specifies the project the property is imported from.</param>
        /// <param name="position">Specifies the position within the project file for the property.</param>
        /// <owner>DavidLe</owner>
        public void SetImportedProperty
        (
            string propertyName,
            string propertyValue,
            string condition,
            Project importedProject,
            PropertyPosition position
        )
        {
            SetPropertyAtHelper(propertyName, propertyValue, condition, /* importedProperty */ true, importedProject, position);
            importedProject.SetPropertyAtHelper(propertyName, propertyValue, condition, /* importedProperty */ false, null, position);
        }

        /// <summary>
        /// Set a property at a particular position inside an imported project file.
        /// The property will be in a group that has the specified condition.
        /// If necessary, a new property or property group will be created.
        /// </summary>
        /// <param name="propertyName">Property name.</param>
        /// <param name="propertyValue">Property value.</param>
        /// <param name="condition">The condition for this property.</param>
        /// <param name="importedProject">Specifies the project the property is imported from.</param>
        /// <param name="position">Specifies the position within the project file for the property.</param>
        /// <param name="treatPropertyValueAsLiteral"></param>
        /// <owner>RGoel</owner>
        public void SetImportedProperty
            (
            string propertyName,
            string propertyValue,
            string condition,
            Project importedProject,
            PropertyPosition position,
            bool treatPropertyValueAsLiteral
            )
        {
            this.SetImportedProperty(propertyName,
                treatPropertyValueAsLiteral ? EscapingUtilities.Escape(propertyValue) : propertyValue,
                condition, importedProject, position);
        }

        /// <summary>
        /// Set a property at a particular position inside the project file.
        /// The property will be in a group that has the specified condition.
        /// If necessary, a new property or property group will be created.
        /// </summary>
        /// <param name="propertyName">Property name.</param>
        /// <param name="propertyValue">Property value.</param>
        /// <param name="condition">The condition for this property.</param>
        /// <param name="importedProperty">Is the property an imported property.</param>
        /// <param name="importedProject">The project from which the property is imported, if it is an imported property.</param>
        /// <param name="position">Specifies the position within the project file for the property.</param>
        /// <owner>RGoel, JomoF, DavidLe</owner>
        internal void SetPropertyAtHelper
        (
            string propertyName,
            string propertyValue,
            string condition,
            bool importedProperty,
            Project importedProject,
            PropertyPosition position
        )
        {
            // Property name must be non-empty.
            error.VerifyThrowArgumentLength(propertyName, nameof(propertyName));

            // Property value must be non-null.
            error.VerifyThrowArgument(propertyValue != null,
                "CannotSetPropertyToNull");

            // Condition can be null, but that's the same as empty condition.
            if (condition == null)
            {
                condition = String.Empty;
            }

            // Is this an "after import" position?
            bool afterImportPosition = (position == PropertyPosition.UseExistingOrCreateAfterLastImport);

            BuildPropertyGroup matchingPropertyGroup = null;
            BuildProperty matchingProperty = null;

            string importedFilename = null;
            if (importedProperty)
            {
                importedFilename = importedProject.FullFileName;
            }

            // Find a matching property and\or property group.
            FindMatchingPropertyPosition(propertyName, condition, afterImportPosition, importedProperty, importedFilename, ref matchingPropertyGroup, ref matchingProperty);

            // If we found the property already in the project file, just change its value.
            if (matchingProperty != null)
            {
                matchingProperty.SetValue(propertyValue);
            }
            else
            {
                // Otherwise, add a new property to the last matching property group we
                // found.  If we didn't find any matching property groups, create a new
                // one.
                if (matchingPropertyGroup == null)
                {
                    if (importedProperty)
                    {
                        matchingPropertyGroup = this.AddNewImportedPropertyGroup(importedFilename, condition);
                    }
                    else
                    {
                        matchingPropertyGroup = this.AddNewPropertyGroup(afterImportPosition);
                        matchingPropertyGroup.Condition = condition;
                    }
                }

                if (importedProperty)
                {
                    matchingPropertyGroup.AddNewImportedProperty(propertyName, propertyValue, importedProject);
                }
                else
                {
                    matchingPropertyGroup.AddNewProperty(propertyName, propertyValue);
                }
            }
        }

        /// <summary>
        /// This method will attempt to find an existing property group and property that matches the requirements.
        /// If no property is found then matchingProperty will be null.
        /// If no property group is found then matchingPropertyGroup will be null.
        /// </summary>
        /// <param name="propertyName">The name of the property to match.</param>
        /// <param name="condition">The condition on the property to match.</param>
        /// <param name="matchOnlyAfterImport">If true, then the matching property must be after the last import.</param>
        /// <param name="importedPropertyGroup">Is the BuildPropertyGroup imported or not.</param>
        /// <param name="importedFilename">Name of the imported project (if BuildPropertyGroup is imported).</param>
        /// <param name="matchingPropertyGroup">Receives the matching property group. Null if none found.</param>
        /// <param name="matchingProperty">Receives the matching property. Null if none found.</param>
        /// <owner>RGoel</owner>
        private void FindMatchingPropertyPosition
        (
            string propertyName,
            string condition,
            bool matchOnlyAfterImport,
            bool importedPropertyGroup,
            string importedFilename,
            ref BuildPropertyGroup matchingPropertyGroup,
            ref BuildProperty matchingProperty
        )
        {
            // Search all of our existing (persisted) PropertyGroups for one
            // that is both local to the main project file, and has a "Condition"
            // matching the string that was passed in.
            foreach (BuildPropertyGroup propertyGroup in this.PropertyGroups)
            {
                // If property groups after import requested then reset any
                // current state when a new import is encountered.
                if (!importedPropertyGroup && matchOnlyAfterImport && propertyGroup.IsImported)
                {
                    matchingPropertyGroup = null;
                    matchingProperty = null;
                }

                if (propertyGroup.IsImported == importedPropertyGroup &&
                    (String.Equals(propertyGroup.Condition.Trim(), condition.Trim(), StringComparison.OrdinalIgnoreCase)) &&
                    (!importedPropertyGroup || (importedPropertyGroup && (String.Equals(propertyGroup.ImportedFromFilename, importedFilename, StringComparison.OrdinalIgnoreCase)))))
                {
                    if (matchingPropertyGroup == null)
                    {
                        // We found a matching property group.  Our current heuristic is
                        // that we always stick the property into the *first* property group
                        // that matches the requirements.  (That's the reason for the
                        // "if matchingPropertyGroup==null" above.)
                        matchingPropertyGroup = propertyGroup;
                    }

                    // Now loop through the property group, and search for the given
                    // property.
                    foreach (BuildProperty property in propertyGroup)
                    {
                        if (String.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase))
                        {
                            matchingProperty = property;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Removes all &lt;PropertyGroup&gt;'s from the main project file, but doesn't
        /// touch anything in any of the imported project files.
        /// </summary>
        /// <owner>RGoel</owner>
        public void RemoveAllPropertyGroups
            (
            )
        {
            this.rawGroups.RemoveAllPropertyGroups();
        }

        /// <summary>
        /// Removes all &lt;PropertyGroup&gt;'s from the main project file that have a
        /// specific "Condition".  This will not remove any property groups from
        /// imported project files.
        /// </summary>
        /// <param name="matchCondition">Condition on the PropertyGroups</param>
        /// <param name="includeImportedPropertyGroups"></param>
        /// <owner>RGoel</owner>
        public void RemovePropertyGroupsWithMatchingCondition
        (
            string matchCondition,
            bool includeImportedPropertyGroups
        )
        {
            this.rawGroups.RemoveAllPropertyGroupsByCondition(matchCondition, includeImportedPropertyGroups);
        }

        /// <summary>
        /// Removes all &lt;PropertyGroup&gt;'s from the main project file that have a
        /// specific "Condition".  This will not remove any property groups from
        /// imported project files.
        /// </summary>
        /// <param name="matchCondition">Condition on the PropertyGroups</param>
        /// <owner>RGoel</owner>
        public void RemovePropertyGroupsWithMatchingCondition
        (
            string matchCondition
        )
        {
            RemovePropertyGroupsWithMatchingCondition(matchCondition, false /* do not include imported constructs */);
        }

        /// <summary>
        /// Removes a &lt;PropertyGroup&gt; from the main project file.
        /// </summary>
        /// <param name="propertyGroupToRemove"></param>
        /// <owner>RGoel</owner>
        public void RemovePropertyGroup
        (
            BuildPropertyGroup propertyGroupToRemove
        )
        {
            error.VerifyThrowArgumentNull(propertyGroupToRemove, nameof(propertyGroupToRemove));

            // Confirm that it's not an imported property group.
            error.VerifyThrowInvalidOperation(!propertyGroupToRemove.IsImported,
                "CannotModifyImportedProjects");

            // Confirm that it's actually a persisted BuildPropertyGroup in the current project.
            error.VerifyThrowInvalidOperation(
                (propertyGroupToRemove.ParentProject == this) && (propertyGroupToRemove.PropertyGroupElement != null),
                "IncorrectObjectAssociation", "BuildPropertyGroup", "Project");

            // Clear out the children of the property group.
            propertyGroupToRemove.Clear();

            XmlElement parentElement = propertyGroupToRemove.ParentElement;
            ErrorUtilities.VerifyThrow(parentElement != null, "Why doesn't this PG have a parent XML element?");
            parentElement.RemoveChild(propertyGroupToRemove.PropertyGroupElement);

            ErrorUtilities.VerifyThrow(propertyGroupToRemove.ParentCollection != null, "Why doesn't this PG have a parent collection?");
            propertyGroupToRemove.ParentCollection.RemovePropertyGroup(propertyGroupToRemove);

            propertyGroupToRemove.ClearParentProject();

            this.MarkProjectAsDirty();
        }

        /// <summary>
        /// Removes a &lt;PropertyGroup&gt; from the main project file.
        /// </summary>
        /// <param name="propertyGroupToRemove"></param>
        public void RemoveImportedPropertyGroup
        (
            BuildPropertyGroup propertyGroupToRemove
        )
        {
            error.VerifyThrowArgumentNull(propertyGroupToRemove, nameof(propertyGroupToRemove));

            // Confirm that it's actually a persisted BuildPropertyGroup in the current project.
            error.VerifyThrowInvalidOperation(
                (propertyGroupToRemove.ParentProject == this) && (propertyGroupToRemove.PropertyGroupElement != null),
                "IncorrectObjectAssociation", "BuildPropertyGroup", "Project");

            // Clear out the children of the property group.
            propertyGroupToRemove.ClearImportedPropertyGroup();

            ErrorUtilities.VerifyThrow(propertyGroupToRemove.ParentCollection != null, "Why doesn't this PG have a parent collection?");
            propertyGroupToRemove.ParentCollection.RemovePropertyGroup(propertyGroupToRemove);

            propertyGroupToRemove.ClearParentProject();

            this.MarkProjectAsDirtyForReevaluation();
        }

        /// <summary>
        /// Adds a new &lt;ItemGroup&gt; element to the project, and returns the
        /// corresponding BuildItemGroup object which can then be populated with
        /// items or anything else that might belong inside an &lt;ItemGroup&gt;.
        /// </summary>
        /// <returns></returns>
        /// <owner>RGoel</owner>
        public BuildItemGroup AddNewItemGroup
            (
            )
        {
            BuildItemGroup newItemGroup = new BuildItemGroup
                (
                this.mainProjectEntireContents,
                false, /* Not imported */
                this /*parent project*/
                );

            // We normally add the new BuildItemGroup just after the last existing BuildItemGroup
            // in the main project file.
            BuildItemGroup lastLocalItemGroup = this.rawItemGroups.LastLocalItemGroup;
            if (lastLocalItemGroup != null)
            {
                this.mainProjectElement.InsertAfter(newItemGroup.ItemGroupElement,
                    lastLocalItemGroup.ItemGroupElement);
                this.rawItemGroups.InsertAfter(newItemGroup, lastLocalItemGroup);
            }
            else
            {
                // If there are currently no ItemGroups in the main project file,
                // then we check two other things ... are there any PropertyGroups
                // in the main project file, and are there are any ItemGroups at all
                // -- either local or imported.
                BuildPropertyGroup lastLocalPropertyGroup = this.rawPropertyGroups.LastLocalPropertyGroup;
                if ((this.rawItemGroups.Count == 0) && (lastLocalPropertyGroup != null))
                {
                    // There are no ItemGroups at all -- either imported or local.
                    // And there is at least one BuildPropertyGroup in the main project file.
                    // So, in this case, we add the BuildItemGroup just after the last
                    // BuildPropertyGroup in the main project file.

                    // We don't want to do this if there are some imported ItemGroups,
                    // because then our ordering would get screwed up.
                    this.mainProjectElement.InsertAfter(newItemGroup.ItemGroupElement,
                        lastLocalPropertyGroup.PropertyGroupElement);
                }
                else
                {
                    // If there are no local PropertyGroups, or we have imported
                    // ItemGroups, then do the safe thing and just stick this new
                    // BuildItemGroup on to the very end of the project.
                    this.mainProjectElement.AppendChild(newItemGroup.ItemGroupElement);
                }

                // Add the new BuildItemGroup to the very end of our collection.  This should
                // be the correct location relative to the other ItemGroups.
                this.rawItemGroups.InsertAtEnd(newItemGroup);
            }

            this.MarkProjectAsDirty();

            return newItemGroup;
        }

        /// <summary>
        /// Adds a new item to the project, and optionally escapes the Include value so it's treated as a literal value.
        /// </summary>
        /// <param name="itemName"></param>
        /// <param name="itemInclude"></param>
        /// <param name="treatItemIncludeAsLiteral"></param>
        /// <returns></returns>
        /// <owner>RGoel</owner>
        public BuildItem AddNewItem
            (
            string itemName,
            string itemInclude,
            bool treatItemIncludeAsLiteral
            )
        {
            return this.AddNewItem(itemName, treatItemIncludeAsLiteral ? EscapingUtilities.Escape(itemInclude) : itemInclude);
        }

        /// <summary>
        /// Called from the IDE to add a new item of a particular type to the project file. This method tries to add the new item
        /// near the other items of the same type.
        /// </summary>
        /// <owner>RGoel</owner>
        /// <param name="itemName">The name of the item list this item belongs to.</param>
        /// <param name="itemInclude">The value of the item's <c>Include</c> attribute i.e. the item-spec</param>
        /// <returns>The new item after evaluation.</returns>
        public BuildItem AddNewItem
            (
            string itemName,
            string itemInclude
            )
        {
            ErrorUtilities.VerifyThrowArgumentLength(itemName, nameof(itemName));
            ErrorUtilities.VerifyThrowArgumentLength(itemInclude, nameof(itemInclude));

            BuildItemGroup matchingItemGroup = null;

            // Search all of our existing (persisted) ItemGroups for one that is:
            //      1.)  local to the main project file
            //      2.)  a top-level BuildItemGroup, as opposed to a nested BuildItemGroup.
            //      3.)  has no "Condition"
            //      4.)  contains at least one item of the same type as the new item being added.
            foreach (BuildItemGroup itemGroup in this.rawItemGroups)
            {
                if (
                    (!itemGroup.IsImported) &&
                    (itemGroup.Condition.Length == 0)
                )
                {
                    // Now loop through the Items in the BuildItemGroup, and see if there's one of
                    // the same type as the new item being added.
                    foreach (BuildItem originalItem in itemGroup)
                    {
                        if ( String.Equals( originalItem.Name, itemName, StringComparison.OrdinalIgnoreCase))
                        {
                            // If the new item that the user is trying to add is already covered by 
                            // a wildcard in an existing item of the project, then there's really
                            // no need to physically touch the project file.  As long as the new item
                            // is on disk, the next reevaluation will automatically pick it up.  When
                            // customers employ the use of wildcards in their project files, and then
                            // they add new items through the IDE, they would much prefer that the IDE
                            // does not touch their project files.
                            if (originalItem.NewItemSpecMatchesExistingWildcard(itemInclude))
                            {
                                BuildItem tempNewItem = new BuildItem(itemName, itemInclude);
                                tempNewItem.SetEvaluatedItemSpecEscaped(itemInclude);
                                tempNewItem.SetFinalItemSpecEscaped((new Expander(evaluatedProperties)).ExpandAllIntoStringLeaveEscaped(itemInclude, null));

                                // We didn't touch the project XML, but we still need to add the new
                                // item to the appropriate data structures, and we need to have something
                                // to hand back to the project system so it can modify the new item
                                // later if needed.
                                BuildItem newItem = BuildItem.CreateClonedParentedItem(tempNewItem, originalItem);

                                AddToItemListByNameIgnoringCondition(newItem);

                                // Set up the other half of the parent/child relationship.
                                newItem.ParentPersistedItem.ChildItems.AddItem(newItem);

                                // Don't bother adding to item lists by name, as we're going to have to evaluate the project as a whole later anyway

                                // We haven't actually changed the XML for the project, because we're 
                                // just piggybacking onto an existing item that was a wildcard.  However,
                                // we should reevaluate on the next build.
                                this.MarkProjectAsDirtyForReevaluation();

                                return newItem;
                            }

                            matchingItemGroup = itemGroup;
                            break;
                        }
                    }
                }
            }

            // If we didn't find a matching BuildItemGroup, create a new one.
            if (matchingItemGroup == null)
            {
                matchingItemGroup = this.AddNewItemGroup();
            }

            // Add the new item to the appropriate place within the BuildItemGroup.  This
            // will attempt to keep items of the same type physically contiguous.
            BuildItem itemToAdd = matchingItemGroup.AddNewItem(itemName, itemInclude);

            // Since we're re-evaluating the project, clear out the previous list of child items
            // for each persisted item tag.
            itemToAdd.ChildItems.Clear();

            // Add this new item into the appropriate evaluated item tables for this project.
            BuildItemGroup items = BuildItemGroup.ExpandItemIntoItems(ProjectDirectory, itemToAdd, new Expander(evaluatedProperties, evaluatedItemsByName), false /* do not expand metadata */);

            foreach (BuildItem item in items)
            {
                BuildItem newItem = BuildItem.CreateClonedParentedItem(item, itemToAdd);

                AddToItemListByNameIgnoringCondition(newItem);

                // Set up the other half of the parent/child relationship.
                newItem.ParentPersistedItem.ChildItems.AddItem(newItem);

                // Don't bother adding to item lists by name, as we're going to have to evaluate the project as a whole later anyway
            }

            this.MarkProjectAsDirty();

            // Return the *evaluated* item to the caller. This way he can ask for evaluated item metadata, etc.
            // It also makes it consistent, because the IDE at project-load asks for all evaluated items,
            // and caches those pointers. We know the IDE is going to cache this pointer as well, so we
            // should give back an evaluated item here as well.
            return (itemToAdd.ChildItems.Count > 0) ? itemToAdd.ChildItems[0] : null;
        }

        /// <summary>
        /// Removes all &lt;ItemGroup&gt;'s from the main project file, but doesn't
        /// touch anything in any of the imported project files.
        /// </summary>
        /// <owner>RGoel</owner>
        public void RemoveAllItemGroups
            (
            )
        {
            this.rawGroups.RemoveAllItemGroups();
        }

        /// <summary>
        /// Removes all &lt;ItemGroup&gt;'s from the main project file that have a
        /// specific "Condition".  This will not remove any item groups from
        /// imported project files.
        /// </summary>
        /// <param name="matchCondition"></param>
        /// <owner>RGoel</owner>
        public void RemoveItemGroupsWithMatchingCondition
        (
            string matchCondition
        )
        {
            this.rawGroups.RemoveAllItemGroupsByCondition(matchCondition);
        }

        /// <summary>
        /// Removes a &lt;ItemGroup&gt; from the main project file.
        /// </summary>
        /// <param name="itemGroupToRemove"></param>
        /// <owner>RGoel</owner>
        public void RemoveItemGroup
        (
            BuildItemGroup itemGroupToRemove
        )
        {
            error.VerifyThrowArgumentNull(itemGroupToRemove, nameof(itemGroupToRemove));

            // Confirm that it's not an imported item group.
            error.VerifyThrowInvalidOperation(!itemGroupToRemove.IsImported,
                "CannotModifyImportedProjects");

            // Confirm that it's actually a persisted BuildItemGroup in the current project.
            error.VerifyThrowInvalidOperation(
                (itemGroupToRemove.ParentProject == this) && (itemGroupToRemove.ItemGroupElement != null),
                "IncorrectObjectAssociation", "BuildItemGroup", "Project");

            // Clear out the children of the BuildItemGroup.
            itemGroupToRemove.Clear();

            XmlElement parentElement = itemGroupToRemove.ParentElement;
            ErrorUtilities.VerifyThrow(parentElement != null, "Why doesn't this IG have a parent XML element?");
            parentElement.RemoveChild(itemGroupToRemove.ItemGroupElement);

            // Remove the item group from our collection.
            ErrorUtilities.VerifyThrow(itemGroupToRemove.ParentCollection != null, "Why doesn't this IG have a parent collection?");
            itemGroupToRemove.ParentCollection.RemoveItemGroup(itemGroupToRemove);

            itemGroupToRemove.ClearParentProject();
            this.MarkProjectAsDirty();
        }

        /// <summary>
        /// Removes all items of a particular type from the main project file.
        /// </summary>
        /// <param name="itemName"></param>
        /// <owner>RGoel</owner>
        public void RemoveItemsByName
        (
            string itemName
        )
        {
            this.rawGroups.RemoveItemsByName(itemName);
        }

        /// <summary>
        /// Removes an item from the main project file.
        /// </summary>
        /// <param name="itemToRemove"></param>
        /// <owner>RGoel</owner>
        public void RemoveItem
        (
            BuildItem itemToRemove
        )
        {
            error.VerifyThrowArgumentNull(itemToRemove, nameof(itemToRemove));

            // Confirm that it's not an imported item.
            error.VerifyThrowInvalidOperation(!itemToRemove.IsImported, "CannotModifyImportedProjects");
            BuildItemGroup parentItemGroup;

            if (itemToRemove.ParentPersistedItem == null)
            {
                // This is either a persisted item that's actually declared in the project file,
                // or it's some kind of intermediate virtual item.

                // If the item doesn't have a parent BuildItemGroup associated with it, then it
                // must not be a persisted item that's actually declared in the project file.
                parentItemGroup = itemToRemove.ParentPersistedItemGroup;
                error.VerifyThrowInvalidOperation (parentItemGroup != null, "ObjectIsNotInProject");
            }
            else
            {
                // This is an evaluated item that came from a persisted item tag declared in
                // the project file.

                // If the item tag produced more than one evaluated item, then it's time to
                // split up the item tag into several new item tags.
                itemToRemove.SplitChildItemIfNecessary();

                error.VerifyThrow(itemToRemove.ParentPersistedItem != null, "No parent BuildItem for item to be removed.");
                itemToRemove = itemToRemove.ParentPersistedItem;

                error.VerifyThrow(itemToRemove.ParentPersistedItemGroup != null,
                    "No parent BuildItemGroup for item to be removed.");
                parentItemGroup = itemToRemove.ParentPersistedItemGroup;
            }

            parentItemGroup.RemoveItem(itemToRemove);

            if (parentItemGroup.Count == 0)
            {
                this.RemoveItemGroup(parentItemGroup);
            }

            this.MarkProjectAsDirty ();
        }

        /// <summary>
        /// Adds a new &lt;Import&gt; element to the end of the project.
        /// </summary>
        /// <param name="projectFile"></param>
        /// <param name="condition"></param>
        /// <owner>RGoel</owner>
        public void AddNewImport
        (
            string projectFile,
            string condition
        )
        {
            imports.AddNewImport(projectFile, condition);
        }

        /// <summary>
        /// Helper for AddNewUsingTaskFromAssemblyName and AddNewUsingTaskFromAssemblyFile
        /// </summary>
        /// <param name="taskName"></param>
        /// <param name="assembly"></param>
        /// <param name="assemblyFile"></param>
        private void AddNewUsingTaskHelper(string taskName, string assembly, bool assemblyFile)
        {
            XmlElement newUsingTaskElement = this.mainProjectEntireContents.CreateElement(XMakeElements.usingTask, XMakeAttributes.defaultXmlNamespace);
            this.mainProjectElement.AppendChild(newUsingTaskElement);

            newUsingTaskElement.SetAttribute(XMakeAttributes.taskName, taskName);

            if (assemblyFile)
            {
                newUsingTaskElement.SetAttribute(XMakeAttributes.assemblyFile, assembly);
            }
            else
            {
                newUsingTaskElement.SetAttribute(XMakeAttributes.assemblyName, assembly);
            }

            this.MarkProjectAsDirtyForReprocessXml();
        }

        /// <summary>
        /// Adds a new &lt;UsingTask&gt; element to the end of the project
        /// </summary>
        /// <param name="taskName"></param>
        /// <param name="assemblyName"></param>
        /// <owner>LukaszG</owner>
        public void AddNewUsingTaskFromAssemblyName(string taskName, string assemblyName)
        {
            AddNewUsingTaskHelper(taskName, assemblyName, false /* use assembly name */);
        }

        /// <summary>
        /// Adds a new &lt;UsingTask&gt; element to the end of the project
        /// </summary>
        /// <param name="taskName"></param>
        /// <param name="assemblyFile"></param>
        /// <owner>LukaszG</owner>
        public void AddNewUsingTaskFromAssemblyFile(string taskName, string assemblyFile)
        {
            AddNewUsingTaskHelper(taskName, assemblyFile, true /* use assembly file */);
        }

        /// <summary>
        /// Sets the project extensions string.
        /// </summary>
        /// <owner>JomoF</owner>
        /// <param name="id"></param>
        /// <param name="content"></param>
        public void SetProjectExtensions(string id, string content)
        {
            // Lazily create the extensions node if it doesn't exist.
            if (projectExtensionsNode == null)
            {
                // No need to create the node if there wouldn't be anything to set.
                if (content.Length == 0)
                {
                    return;
                }

                projectExtensionsNode = mainProjectEntireContents.CreateElement(XMakeElements.projectExtensions, XMakeAttributes.defaultXmlNamespace);
                mainProjectElement.AppendChild(projectExtensionsNode);
            }

            // Look in the extensions node and see if there is a child that matches
            XmlElement idElement = (XmlElement) projectExtensionsNode[id];

            // Found anything?
            if (idElement == null)
            {
                idElement = mainProjectEntireContents.CreateElement(id, XMakeAttributes.defaultXmlNamespace);
                projectExtensionsNode.AppendChild(idElement);
            }

            // Now there should be an idElement, set its InnerXml to be xmlText.
            idElement.InnerXml = content;

            // We don't need to re-evaluate anything (so don't call MarkProjectAsDirty),
            // but the project file still needs to be saved to disk.
            this.MarkProjectAsDirtyForSave();
        }

        /// <summary>
        /// Returns the project extensions string for the given ID.
        /// </summary>
        /// <owner>JomoF</owner>
        /// <param name="id"></param>
        /// <returns>String value of specified ID.</returns>
        public string GetProjectExtensions(string id)
        {
            if (projectExtensionsNode == null)
            {
                return String.Empty;
            }

            // Look in the extensions node and see if there is a child that matches
            XmlElement idElement = (XmlElement)projectExtensionsNode[id];

            // Found anything?
            if (idElement == null)
            {
                // No, so return "".
                return String.Empty;
            }

            // Now there should be an idElement, return its InnerXml.
            // HACK: remove the xmlns attribute, because the IDE's not expecting that
            return Utilities.RemoveXmlNamespace(idElement.InnerXml);
        }

        /// <summary>
        /// Builds the default targets in this project.
        /// </summary>
        /// <returns></returns>
        /// <owner>RGoel</owner>
        public bool Build
            (
            )
        {
            return this.ParentEngine.BuildProject(this, null, null, BuildSettings.None);
        }

        /// <summary>
        /// Builds the specified target in this project.
        /// </summary>
        /// <param name="targetName"></param>
        /// <returns></returns>
        /// <owner>JomoF</owner>
        public bool Build
            (
            string targetName
            )
        {
            return this.ParentEngine.BuildProject(this, (targetName == null) ? null : new string[] {targetName},
                null, BuildSettings.None);
        }

        /// <summary>
        /// Builds the specified list of targets in this project.
        /// </summary>
        /// <remarks>
        /// This is the public method that host IDEs can call to build a project.
        /// It just turns around and calls "BuildProject" on the engine object.
        /// All builds must go through the engine object, because it needs to
        /// keep track of the projects that are currently in progress, so that
        /// we don't end up in infinite loops when we have circular project-to-
        /// project dependencies.
        /// </remarks>
        /// <param name="targetNames"></param>
        /// <returns></returns>
        /// <owner>RGoel</owner>
        public bool Build
            (
            string[] targetNames    // can be null to build the default targets
            )
        {
            return this.ParentEngine.BuildProject(this, targetNames, null, BuildSettings.None);
        }

        /// <summary>
        /// Builds the specified list of targets in this project, and returns the target outputs.
        /// </summary>
        /// <remarks>
        /// This is the public method that host IDEs can call to build a project.
        /// It just turns around and calls "BuildProject" on the engine object.
        /// All builds must go through the engine object, because it needs to
        /// keep track of the projects that are currently in progress, so that
        /// we don't end up in infinite loops when we have circular project-to-
        /// project dependencies.
        /// </remarks>
        /// <param name="targetNames"></param>
        /// <param name="targetOutputs"></param>
        /// <returns></returns>
        /// <owner>RGoel</owner>
        public bool Build
            (
            string[] targetNames,    // can be null to build the default targets
            IDictionary targetOutputs       // can be null if outputs are not needed
            )
        {
            return this.ParentEngine.BuildProject(this, targetNames, targetOutputs, BuildSettings.None);
        }

        /// <summary>
        /// Builds the specified list of targets in this project using the specified
        /// flags, and returns the target outputs.
        /// </summary>
        /// <remarks>
        /// This is the public method that host IDEs can call to build a project.
        /// It just turns around and calls "BuildProject" on the engine object.
        /// All builds must go through the engine object, because it needs to
        /// keep track of the projects that are currently in progress, so that
        /// we don't end up in infinite loops when we have circular project-to-
        /// project dependencies.
        /// </remarks>
        /// <param name="targetNames"></param>
        /// <param name="targetOutputs"></param>
        /// <param name="buildFlags"></param>
        /// <returns></returns>
        /// <owner>RGoel</owner>
        public bool Build
            (
            string[] targetNames,    // can be null to build the default targets
            IDictionary targetOutputs,      // can be null if outputs are not needed
            BuildSettings buildFlags
            )
        {
            return this.ParentEngine.BuildProject(this, targetNames, targetOutputs, buildFlags);
        }

        /// <summary>
        /// This internal method actually performs the build of the specified targets
        /// in the project.  If no targets are specified, then we build the
        /// "defaultTargets" as specified in the attribute of the &lt;Project&gt; element
        /// in the XML.
        /// </summary>
        /// <param name="buildRequest"></param>
        internal void BuildInternal
        (
            BuildRequest buildRequest
        )
        {
            // First, make sure that this project has its targets/tasks enabled.  They may have been disabled
            // by the host for security reasons.
            if (!this.BuildEnabled)
            {
                this.ParentEngine.LoggingServices.LogError(buildRequest.ParentBuildEventContext, new BuildEventFileInfo(FullFileName), "SecurityProjectBuildDisabled");
                buildRequest.BuildCompleted = true;

                if (buildRequest.HandleId != EngineCallback.invalidEngineHandle)
                {
                    ParentEngine.Router.PostDoneNotice(buildRequest);
                }
            }
            else
            {
                ProjectBuildState buildContext = InitializeForBuildingTargets(buildRequest);
                if (buildContext != null)
                {
                    ContinueBuild(buildContext, null);
                }
            }
        }

        internal void ContinueBuild(ProjectBuildState buildContext, TaskExecutionContext taskExecutionContext)
        {
            if (Engine.debugMode)
            {
                Console.WriteLine("Project continue build :" + buildContext.BuildRequest.ProjectFileName + " Handle " + buildContext.BuildRequest.HandleId + " State " + buildContext.CurrentBuildContextState +
                                   " current target " + buildContext.NameOfTargetInProgress + " blocking target " + buildContext.NameOfBlockingTarget);
            }
            bool exitedDueToError = true;
            try
            {
                if (buildContext.CurrentBuildContextState == ProjectBuildState.BuildContextState.BuildingCurrentTarget)
                {
                    // Execute the next appropriate operation for this target
                    ErrorUtilities.VerifyThrow( taskExecutionContext != null, "Task context should be non-null");
                    taskExecutionContext.ParentTarget.ContinueBuild(taskExecutionContext.BuildContext, taskExecutionContext);
                }
                else if (buildContext.CurrentBuildContextState == ProjectBuildState.BuildContextState.StartingFirstTarget)
                {
                    // Start the first target of the build request
                    buildContext.CurrentBuildContextState = ProjectBuildState.BuildContextState.BuildingCurrentTarget;
                    GetTargetForName(buildContext.NameOfTargetInProgress).Build(buildContext);
                }
                else if (buildContext.CurrentBuildContextState == ProjectBuildState.BuildContextState.CycleDetected)
                {
                    ErrorUtilities.VerifyThrow(
                        taskExecutionContext?.ParentTarget != null,
                        "Unexpected task context. Should not be null");
                    // Check that the target is in progress
                    ErrorUtilities.VerifyThrow(
                        taskExecutionContext.ParentTarget.TargetBuildState == Target.BuildState.InProgress,
                        "The target forming the cycle should not be complete");
                    // Throw invalid project exeception
                    ProjectErrorUtilities.VerifyThrowInvalidProject
                        (false, taskExecutionContext.ParentTarget.TargetElement,
                         "CircularDependency", taskExecutionContext.ParentTarget.Name);
                }

                CalculateNextActionForProjectContext(buildContext);

                exitedDueToError = false;

                if (Engine.debugMode)
                {
                    Console.WriteLine("Project after continue build :" + buildContext.BuildRequest.ProjectFileName + " Handle " + buildContext.BuildRequest.HandleId + " State " + buildContext.CurrentBuildContextState +
                                      " current target " + buildContext.NameOfTargetInProgress + " blocking target " + buildContext.NameOfBlockingTarget);
                }
            }
            catch (InvalidProjectFileException e)
            {
                // Make sure the Invalid Project error gets logged *before* ProjectFinished.  Otherwise,
                // the log is confusing.
                this.ParentEngine.LoggingServices.LogInvalidProjectFileError(buildContext.ProjectBuildEventContext, e);
            }
            finally
            {
                if ( (exitedDueToError || buildContext.BuildComplete) &&
                     buildContext.CurrentBuildContextState != ProjectBuildState.BuildContextState.RequestFilled)
                {
                    // If the target that threw an exception is being built due to an
                    // dependson or onerror relationship, it is necessary to make sure 
                    // the buildrequests waiting on targets below it get notified of the failure. In single
                    // threaded mode there is only a single outstanding request so this issue is avoided.
                    if (exitedDueToError)
                    {
                        buildContext.RecordBuildException();

                        if (buildContext.NameOfBlockingTarget != null)
                        {
                            while (buildContext.NameOfBlockingTarget != null)
                            {
                                Target blockingTarget = GetTargetForName(buildContext.NameOfBlockingTarget);
                                if (blockingTarget.ExecutionState?.BuildingRequiredTargets == true)
                                {
                                    blockingTarget.ContinueBuild(buildContext, null);
                                }

                                buildContext.RemoveBlockingTarget();
                            }
                            Target inprogressTarget = GetTargetForName(buildContext.NameOfTargetInProgress);
                            if (inprogressTarget.ExecutionState?.BuildingRequiredTargets == true)
                            {
                                inprogressTarget.ContinueBuild(buildContext, null);
                            }
                        }

                        buildContext.CurrentBuildContextState = ProjectBuildState.BuildContextState.BuildComplete;
                    }

                    this.buildingCount--;

                    if (buildContext.BuildRequest.FireProjectStartedFinishedEvents)
                    {
                        ParentEngine.LoggingServices.LogProjectFinished(buildContext.ProjectBuildEventContext, FullFileName, buildContext.BuildResult);
                    }

                    // Notify targets in other projects that are waiting on us via IBuildEngine
                    // interface (via MSBuild and CallTarget tasks).
                    if (buildContext.BuildRequest.IsGeneratedRequest)
                    {
                        if (Engine.debugMode)
                        {
                            Console.WriteLine("Notifying about " + buildContext.BuildRequest.ProjectFileName +
                                              " about " + buildContext.TargetNamesToBuild[0] + " on node " + buildContext.BuildRequest.NodeIndex +
                                              " HandleId " + buildContext.BuildRequest.HandleId + " ReqID " +
                                              buildContext.BuildRequest.RequestId);
                        }
                        ParentEngine.Router.PostDoneNotice(buildContext.BuildRequest);
                    }

                    // Don't try to unload projects loaded by the host
                    if (this.buildingCount == 0 && this.needToUnloadProject && !this.IsLoadedByHost)
                    {
                        parentEngine.UnloadProject(this, false /* unload only this project version */);
                    }

                    buildContext.CurrentBuildContextState = ProjectBuildState.BuildContextState.RequestFilled;
                }
            }
        }

        internal void CalculateNextActionForProjectContext(ProjectBuildState buildContext)
        {
            // If the build request has been already complete 
            if (buildContext.CurrentBuildContextState == ProjectBuildState.BuildContextState.RequestFilled)
            {
                return;
            }

            // In case the first step of the target failed, the target is empty or needs another target
            // to be build, it is necessary to recalculate the next action. The loop below is broken as
            // soon as a target completes or a target requests a task to be executed.
            bool recalculateAction = true;

            while (recalculateAction)
            {
                recalculateAction = false;

                // Check if there is a dependent target
                Target currentTarget;
                if (buildContext.NameOfBlockingTarget != null)
                {
                    currentTarget = GetTargetForName(buildContext.NameOfBlockingTarget);

                    if (buildContext.CurrentBuildContextState == ProjectBuildState.BuildContextState.StartingBlockingTarget)
                    {
                        buildContext.CurrentBuildContextState = ProjectBuildState.BuildContextState.BuildingCurrentTarget;
                        ExecuteNextActionForProjectContext(buildContext, true);
                        recalculateAction = true;
                    }
                    else if (currentTarget.TargetBuildState != Target.BuildState.InProgress)
                    {
                        if (buildContext.CurrentBuildContextState == ProjectBuildState.BuildContextState.WaitingForTarget)
                        {
                            // Get target outputs before moving to the next target
                            currentTarget.Build(buildContext);
                        }

                        buildContext.CurrentBuildContextState = ProjectBuildState.BuildContextState.BuildingCurrentTarget;
                        buildContext.RemoveBlockingTarget();
                        ExecuteNextActionForProjectContext(buildContext, false);
                        recalculateAction = true;
                    }
                }
                else
                {
                    currentTarget = GetTargetForName(buildContext.NameOfTargetInProgress);
                    if (currentTarget.TargetBuildState != Target.BuildState.InProgress)
                    {
                        if (buildContext.CurrentBuildContextState == ProjectBuildState.BuildContextState.WaitingForTarget)
                        {
                            // Get target outputs before moving to the next target
                            currentTarget.Build(buildContext);
                            buildContext.CurrentBuildContextState = ProjectBuildState.BuildContextState.BuildingCurrentTarget;
                        }
                        if (currentTarget.TargetBuildState == Target.BuildState.CompletedUnsuccessfully)
                        {
                            // Abort the request and notify everyone
                            buildContext.RecordBuildCompletion(false);
                        }
                        else
                        {
                            // Check if there are no more targets to run
                            if (buildContext.GetNextTarget() == null)
                            {
                                // The request is complete 
                                buildContext.RecordBuildCompletion(true);
                            }
                            else
                            {
                                // Move to the next target in the request
                                ExecuteNextActionForProjectContext(buildContext, true);
                                recalculateAction = true;
                            }
                        }
                    }
                }
            }
        }

        private void ExecuteNextActionForProjectContext(ProjectBuildState buildContext, bool initialCall)
        {
            Target nextTarget;
            if (buildContext.NameOfBlockingTarget != null)
            {
                // Notify the next target in depends on/on error stack
                nextTarget = GetTargetForName(buildContext.NameOfBlockingTarget);
            }
            else
            {
                nextTarget = GetTargetForName(buildContext.NameOfTargetInProgress);
            }

            // Build the target.  Note that this could throw an InvalidProjectFileException, in which
            // case we want to make sure and still log the ProjectFinished event with completedSuccessfully=false.
            if (initialCall)
            {
                nextTarget.Build(buildContext);
            }
            else
            {
                nextTarget.ContinueBuild(buildContext, null);
            }
        }

        private Target GetTargetForName(string name)
        {
            string targetNameToBuildUnescaped = EscapingUtilities.UnescapeAll(name);

            // Find the appropriate Target object based on the target name.
            Target target = targets[targetNameToBuildUnescaped];

            // If we couldn't find a target with that name, it's an error.
            // (Or should we just continue anyway?  This might be useful
            // for pre-build and post-build steps.)
            ProjectFileErrorUtilities.VerifyThrowInvalidProjectFile(target != null,
                new BuildEventFileInfo(this.FullFileName), "TargetDoesNotExist", targetNameToBuildUnescaped);
            return target;
        }

        private ProjectBuildState InitializeForBuildingTargets(BuildRequest buildRequest)
        {
            ProjectBuildState buildContext = null;

            string[] targetNamesToBuild = buildRequest.TargetNames;

            // Initialize to the parent requests project context id
            int projectContextId = buildRequest.ParentBuildEventContext.ProjectContextId;

            BuildEventContext buildEventContext = null;

            // Determine if a project started event is required to be fired, if so we may need a new projectContextId
             if (buildRequest.FireProjectStartedFinishedEvents)
             {
                 //If we have not already used the context from the project yet, lets use that as our first event context
                 if (!haveUsedInitialProjectContextId)
                 {
                     buildEventContext = projectBuildEventContext;
                     haveUsedInitialProjectContextId = true;
                 }
                 else // We are going to need a new Project context Id and a new buildEventContext
                 {
                     projectContextId = parentEngine.GetNextProjectId();
                 }
             }

             if (buildEventContext == null)
             {
                 buildEventContext = new BuildEventContext
                                   (
                                       projectBuildEventContext.NodeId,
                                       projectBuildEventContext.TargetId,
                                       projectContextId,
                                       projectBuildEventContext.TaskId
                                    );
             }

            bool exitedDueToError = true;

            try
            {
                // Refreshing (reevaluating) a project may end up calling ResetBuildStatus which will mark
                // IsReset=true.  This is legitimate because when a project is being reevaluated, we want
                // to be explicit in saying that any targets that had run previously are no longer valid,
                // and we must rebuild them on the next build.
                this.RefreshProjectIfDirty();

                // Only log the project started event after making sure the project is reevaluated if necessary,
                // otherwise we could log stale item/property information.
                if (!ParentEngine.LoggingServices.OnlyLogCriticalEvents && buildRequest.FireProjectStartedFinishedEvents)
                {
                    string joinedTargetNamesToBuild = null;
                    if (targetNamesToBuild?.Length > 0)
                    {
                        joinedTargetNamesToBuild = EscapingUtilities.UnescapeAll(String.Join(";", targetNamesToBuild));
                    }

                    // Flag the start of the project build.
                    //
                    // This also passes all the current properties/items and their values. The logger might want to use the
                    // object it gets from this event to see updated property/item values later in the build: so be 
                    // careful to use the original "evaluatedProperties" and "evaluatedItems" table, not the clone 
                    // "EvaluatedProperties" or "EvaluatedItems" table. It's fine to pass these live tables, because we're 
                    // wrapping them in read-only proxies.
                    BuildPropertyGroup propertyGroupForStartedEvent = this.evaluatedProperties;

                    // If we are on the child process we need to fiure out which properties we need to serialize
                    if (ParentEngine.Router.ChildMode)
                    {
                        // Initially set it to empty so that we do not serialize all properties if we are on a child node
                        propertyGroupForStartedEvent = new BuildPropertyGroup();

                        // Get the list of properties to serialize to the parent node
                        string[] propertyListToSerialize = parentEngine.PropertyListToSerialize;
                        if (propertyListToSerialize?.Length > 0)
                        {
                            foreach (string propertyToGet in propertyListToSerialize)
                            {
                                BuildProperty property = this.evaluatedProperties[propertyToGet];

                                //property can be null if propertyToGet does not exist
                                if (property != null)
                                {
                                    propertyGroupForStartedEvent.SetProperty(property);
                                }
                            }
                        }
                    }

                    BuildPropertyGroupProxy propertiesProxy = new BuildPropertyGroupProxy(propertyGroupForStartedEvent);
                    BuildItemGroupProxy itemsProxy = new BuildItemGroupProxy(this.evaluatedItems);

                    ParentEngine.LoggingServices.LogProjectStarted(this.projectId, buildRequest.ParentBuildEventContext, buildEventContext, FullFileName, joinedTargetNamesToBuild, propertiesProxy, itemsProxy);

                    // See comment on DefaultToolsVersion setter.
                    if (treatinghigherToolsVersionsAs40)
                    {
                        ParentEngine.LoggingServices.LogComment(buildEventContext, MessageImportance.High, "TreatingHigherToolsVersionAs40", DefaultToolsVersion);
                    }

                    ParentEngine.LoggingServices.LogComment(buildEventContext, MessageImportance.Low, "ToolsVersionInEffectForBuild", ToolsVersion);
                }

                // Incrementing the building count. A single project may be building more than once at a time
                // because of callbacks by the MSBuild task.
                this.buildingCount++;

                // Because we are about to build some targets, we are no longer going to be in the "reset"
                // state.
                this.IsReset = false;

                // This is an ArrayList of strings, where each string is the name of a target that
                // we need to build.  We start out by populating it with the list of targets specified
                // in the "InitialTargets" attribute of the <Project> node.
                ArrayList completeListOfTargetNamesToBuild = this.CombinedInitialTargetNames;

                if (buildRequest.UseResultsCache)
                {
                    buildRequest.InitialTargets = string.Join(";", (string[])completeListOfTargetNamesToBuild.ToArray(typeof(string)));
                    buildRequest.DefaultTargets = (this.DefaultBuildTargets != null) ? string.Join(";", this.DefaultBuildTargets) : string.Empty;
                    buildRequest.ProjectId = this.projectId;
                }

                // If no targets were passed in, use the "defaultTargets" from the
                // project file.
                if ((targetNamesToBuild == null) || (targetNamesToBuild.Length == 0))
                {
                    string[] defaultTargetsToBuild = this.DefaultBuildTargets;

                    // There wasn't at least one target in the project, then we have
                    // a problem.
                    ProjectFileErrorUtilities.VerifyThrowInvalidProjectFile(defaultTargetsToBuild != null,
                        new BuildEventFileInfo(this.FullFileName), "NoTargetSpecified");

                    completeListOfTargetNamesToBuild.AddRange(defaultTargetsToBuild);
                }
                else
                {
                    completeListOfTargetNamesToBuild.AddRange(targetNamesToBuild);
                }

                // Check if the client requests that the project should be unloaded once it is completed
                needToUnloadProject = buildRequest.UnloadProjectsOnCompletion || needToUnloadProject;
                buildContext = new ProjectBuildState(buildRequest, completeListOfTargetNamesToBuild, buildEventContext);
                exitedDueToError = false;
            }
            catch (InvalidProjectFileException e)
            {
                // Make sure the Invalid Project error gets logged *before* ProjectFinished.  Otherwise,
                // the log is confusing.
                this.ParentEngine.LoggingServices.LogInvalidProjectFileError(buildEventContext, e);
                // Mark the build request as completed and failed. The build context is either not created
                // or discarded
                buildRequest.BuildCompleted = true;
                buildRequest.BuildSucceeded = false;
                buildContext = null;
            }
            finally
            {
                if (exitedDueToError)
                {
                    this.buildingCount--;

                    if (buildRequest.FireProjectStartedFinishedEvents)
                    {
                        ParentEngine.LoggingServices.LogProjectFinished(
                            buildEventContext,
                            FullFileName,
                            false);
                    }
                }
            }
            return buildContext;
        }

        /// <summary>
        /// Checks the dirty flags and calls the necessary methods to update the
        /// necessary data structures, etc.
        /// </summary>
        /// <owner>RGoel</owner>
        private void RefreshProjectIfDirty
            (
            )
        {
            if (this.dirtyNeedToReprocessXml)
            {
                this.ProcessMainProjectElement();
            }
            else if (this.dirtyNeedToReevaluate)
            {
                this.EvaluateProject(false /*not currently loading*/);
            }
        }

        /// <summary>
        /// Process the attributes and all the children of the &lt;Project&gt; tag.
        /// This basically just parses through the XML and instantiates the
        /// appropriate internal objects.  It doesn't actually do any evaluation
        /// or building.
        /// </summary>
        /// <owner>RGoel</owner>
        private void ProcessMainProjectElement
            (
            )
        {
            // Make sure the <Project> node has been given to us.
            error.VerifyThrow(mainProjectElement != null,
                "Need an XML node representing the <project> element.");

            // Make sure this really is the <Project> node.
            ProjectXmlUtilities.VerifyThrowElementName(mainProjectElement, XMakeElements.project);

            // Technically, this belongs in ProcessProjectAttributes. However, ToolsVersion
            // affects strategic reserved properties, so it's better to process it before anything else happens
            ProcessToolsVersionDependentProperties();

            if (IsValidated)
            {
                // Validate the project schema. If we have a file, then validate that
                // because we need the proper line numbers. If this is an anonymous project
                // then just validate the XML.
                if (mainProjectElement.OwnerDocument.BaseURI.Length != 0)
                {
                    this.Toolset.SchemaValidator(projectBuildEventContext).VerifyProjectFileSchema(FullFileName, SchemaFile);
                }
                else
                {
                    this.Toolset.SchemaValidator(projectBuildEventContext).VerifyProjectSchema(mainProjectEntireContents.InnerXml, SchemaFile);
                }
            }

            this.evaluatedItemsByNameIgnoringCondition.Clear();
            this.evaluatedItemsIgnoringCondition.Clear();
            this.targets.Clear();
            this.nameOfFirstTarget = null;
            this.defaultTargetNames = new string[0];
            this.initialTargetNamesInImportedProjects.Clear();
            this.initialTargetNamesInMainProject.Clear();

            this.rawGroups.Clear();
            this.conditionedPropertiesTable.Clear();
            this.usingTasks.Clear();
            this.imports.Clear();
            this.projectExtensionsNode = null;

            // Attributes on the <Project> element and in the <Import> tags can
            // make use of global properties, reserved properties, and environment
            // variables ... so we need to set these up early.
            this.evaluatedProperties.Clear();
            this.evaluatedProperties.ImportInitialProperties(this.EnvironmentProperties, this.ReservedProperties, this.Toolset.BuildProperties, this.GlobalProperties);

            // Process the attributes of the <project> element.
            ProcessProjectAttributes(this.mainProjectElement, false);

            // Figure out where the project is located
            this.projectDirectory = !string.IsNullOrEmpty(this.fullFileName) ?
                Path.GetDirectoryName(this.fullFileName) : Directory.GetCurrentDirectory();

            // Process the child elements of the <Project> element, instantiating
            // internal objects for each of them.
            ProcessProjectChildren(this.mainProjectElement, this.ProjectDirectory, false);

            this.EvaluateProject(true /*currently loading*/);
        }

        /// <summary>
        /// Deal with all of the attributes on the &lt;Project&gt; element of the
        /// XML project file.
        /// </summary>
        /// <param name="projectElement"></param>
        /// <param name="importedProject"></param>
        /// <owner>RGoel</owner>
        private void ProcessProjectAttributes
        (
            XmlElement  projectElement,
            bool        importedProject
        )
        {
            // Make sure the <Project> node has been given to us.
            error.VerifyThrow(projectElement != null,
            "Need an XML node representing the <project> element.");

            // Make sure this really is the <Project> node.
            ProjectXmlUtilities.VerifyThrowElementName(projectElement, XMakeElements.project);

            // Loop through the list of attributes on the <Project> element.
            //
            // NOTE: The "ToolsVersion" attribute is not processed here as you might expect it would be;
            // it's handled in ProcessToolsVersionDependentProperties() instead.
            foreach (XmlAttribute projectAttribute in projectElement.Attributes)
            {
                switch (projectAttribute.Name)
                {
                    // The "xmlns" attribute points us at the XSD file which describes the
                    // schema for the project file.  We should use the XSD to validate
                    // the format of the project file XML.
                    case XMakeAttributes.xmlns:
                        break;

                    // "MSBuildVersion" attribute is deprecated -- log a warning and ignore it
                    case XMakeAttributes.msbuildVersion:
                        ParentEngine.LoggingServices.LogWarning(projectBuildEventContext, Utilities.CreateBuildEventFileInfo(projectAttribute, FullFileName),
                            "MSBuildVersionAttributeDeprecated");
                        break;

                    // The "DefaultTargets" attribute is the target that we would build
                    // if no specific target was given to us by the caller (as part of
                    // the engine parameters passed in to Engine.BuildProject).
                    case XMakeAttributes.defaultTargets:
                        // We take only the first "DefaultTargets" attribute that we see in the chain
                        // of imports.  So if the main project file has a DefaultTargets defined, we
                        // always take that one.  If it doesn't, then we might take one from one of
                        // the imported files.
                        if ((defaultTargetNames == null) || (defaultTargetNames.Length == 0))
                        {
                            // NOTE: at this time, evaluatedProperties only contains env. vars., command-line properties and
                            // reserved properties, and that is all the "DefaultTargets" attribute is allowed to reference
                            SetDefaultTargets(projectAttribute.Value, evaluatedProperties);
                        }
                        break;

                    // The "InitialTargets" attribute defines the target that we will always build
                    // before building any other target.
                    case XMakeAttributes.initialTargets:
                        // allow "InitialTargets" to only reference env. vars., command-line properties and reserved properties
                        List<string> initialTargetsList = (new Expander(evaluatedProperties)).ExpandAllIntoStringListLeaveEscaped(projectAttribute.Value, projectAttribute);
                        if (importedProject)
                        {
                            this.initialTargetNamesInImportedProjects.AddRange(initialTargetsList);
                        }
                        else
                        {
                            this.initialTargetNamesInMainProject.AddRange(initialTargetsList);
                        }
                        break;

                    // We've come across an attribute in the <Project> element that we
                    // don't recognize.  This is okay; just ignore it.  There are many
                    // attributes that can be present in the root element
                    // that it is not our job to interpret.  The XML parser takes care of
                    // these automatically.
                    default:
                        break;
                }
            }
        }

        /// <summary>
        /// Process each of the direct children beneath the &gt;Project&lt; element.
        /// These include things like &lt;PropertyGroup&gt;, &lt;ItemGroup&gt;, &lt;Target&gt;, etc.
        /// This method is simply capturing the data in the form of our own
        /// internal objects.  It is not actually evaluating any of the properties
        /// or other data.
        /// </summary>
        /// <param name="projectElement"></param>
        /// <param name="projectDirectoryLocation"></param>
        /// <param name="importedProject"></param>
        /// <owner>RGoel</owner>
        private void ProcessProjectChildren
        (
            XmlElement projectElement,
            string projectDirectoryLocation,
            bool importedProject
        )
        {
            // Make sure the <Project> node has been given to us.
            error.VerifyThrow(projectElement != null,
                "Need an XML node representing the <project> element.");

            // Make sure this really is the <Project> node.
            ProjectXmlUtilities.VerifyThrowElementName(projectElement, XMakeElements.project);

            // Loop through all the direct children of the <project> element.
            // This verifies all the XML is legitimate, and creates ordered lists of objects
            // representing the top-level nodes (itemgroup, choose, etc.)
            // As this progresses, the Chooses and PropertyGroups are evaluated, so that conditions
            // on Imports involving properties can be evaluated too, because we need to know whether to
            // follow the imports.
            // All this comprises "Pass 1".
            List<XmlElement> childElements = ProjectXmlUtilities.GetValidChildElements(projectElement);

            string currentPerThreadProjectDirectory = Project.PerThreadProjectDirectory;

            try
            {
                // Make the correct project directory available. This is needed because it is 
                // used for evaluating "exists" in conditional expressions, for example on <Import> elements.
                Project.PerThreadProjectDirectory = ProjectDirectory;

                foreach (XmlElement childElement in childElements)
                {
                    switch (childElement.Name)
                    {
                        // Process the <ItemDefinitionGroup> element.
                        case XMakeElements.itemDefinitionGroup:
                            itemDefinitionLibrary.Add(childElement);
                            break;

                        // Process the <ItemGroup> element.
                        case XMakeElements.itemGroup:
                            BuildItemGroup newItemGroup = new BuildItemGroup(childElement, importedProject, /*parent project*/ this);
                            this.rawItemGroups.InsertAtEnd(newItemGroup);
                            break;

                    // Process the <PropertyGroup> element.
                    case XMakeElements.propertyGroup:
                        BuildPropertyGroup newPropertyGroup = new BuildPropertyGroup(this, childElement, importedProject);
                        newPropertyGroup.EnsureNoReservedProperties();
                        this.rawPropertyGroups.InsertAtEnd(newPropertyGroup);
                        // PropertyGroups/Chooses are evaluated immediately during this scan, as they're needed to figure out whether
                        // we include Imports.
                        newPropertyGroup.Evaluate(this.evaluatedProperties, this.conditionedPropertiesTable, ProcessingPass.Pass1);
                        break;

                        // Process the <Choose> element.
                        case XMakeElements.choose:
                            Choose newChoose = new Choose(this, this.rawGroups, childElement, importedProject, 0 /* not nested in another <Choose> */);

                            this.rawGroups.InsertAtEnd(newChoose);
                            // PropertyGroups/Chooses are evaluated immediately during this scan, as they're needed to figure out whether
                            // we include Imports.
                            newChoose.Evaluate(this.evaluatedProperties, false, true, this.conditionedPropertiesTable, ProcessingPass.Pass1);
                            break;

                        // Process the <Target> element.
                        case XMakeElements.target:
                            XmlElement targetElement = childElement;
                            Target newTarget = new Target(targetElement, this, importedProject);

                            // If a target with this name already exists, log a low priority message.
                            if (!ParentEngine.LoggingServices.OnlyLogCriticalEvents)
                            {
                                if (targets.Exists(newTarget.Name))
                                {
                                    ParentEngine.LoggingServices.LogComment(projectBuildEventContext, "OverridingTarget",
                                         targets[newTarget.Name].Name, targets[newTarget.Name].ProjectFileOfTargetElement,
                                         newTarget.Name, newTarget.ProjectFileOfTargetElement);
                                }
                            }

                            this.targets.AddOverrideTarget(newTarget);

                            if (this.nameOfFirstTarget == null)
                            {
                                this.nameOfFirstTarget = targetElement.GetAttribute(XMakeAttributes.name);
                            }
                            break;

                        // Process the <UsingTask> element.
                        case XMakeElements.usingTask:
                            UsingTask usingTask = new UsingTask(childElement, importedProject);
                            this.usingTasks.Add(usingTask);
                            break;

                        // Process the <ProjectExtensions> element.
                        case XMakeElements.projectExtensions:
                            if (!importedProject)
                            {
                                ProjectErrorUtilities.VerifyThrowInvalidProject(this.projectExtensionsNode == null, childElement,
                                    "DuplicateProjectExtensions");
                                this.projectExtensionsNode = childElement;

                                // No attributes are legal on this element
                                ProjectXmlUtilities.VerifyThrowProjectNoAttributes(childElement);
                            }
                            break;

                        // Process the <Error>, <Warning>, and <Message> elements
                        case XMakeElements.error:
                        case XMakeElements.warning:
                        case XMakeElements.message:
                            ProjectErrorUtilities.VerifyThrowInvalidProject(false, childElement, "ErrorWarningMessageNotSupported", childElement.Name);
                            break;

                        case XMakeElements.importGroup:
                            foreach (XmlElement importGroupChild in childElement.ChildNodes)
                            {
                                switch(importGroupChild.Name)
                                {
                                    case XMakeElements.import:
                                        ProcessImportElement(importGroupChild, projectDirectoryLocation, importedProject);
                                        break;
                                    default:
                                        ProjectXmlUtilities.ThrowProjectInvalidChildElement(importGroupChild);
                                        break;
                                }
                            }
                            break;

                        // Process the <Import> element.
                        case XMakeElements.import:
                            ProcessImportElement(childElement, projectDirectoryLocation, importedProject);
                            break;

                        default:
                            // We've encounted an unknown child element beneath <project>.
                            ProjectXmlUtilities.ThrowProjectInvalidChildElement(childElement);
                            break;
                    }
                }
            }
            finally
            {
                // Reset back to the original value
                Project.PerThreadProjectDirectory = currentPerThreadProjectDirectory;
            }
        }

        /// <summary>
        /// Process the &lt;Import&gt; element by loading the child project file, and processing its &lt;Project&gt; element. In a
        /// given main project, the same file cannot be imported twice -- this is to prevent circular imports.
        /// </summary>
        /// <owner>RGoel</owner>
        /// <param name="importElement"></param>
        /// <param name="projectDirectoryLocation"></param>
        /// <param name="importedProject"></param>
        private void ProcessImportElement
        (
            XmlElement  importElement,
            string      projectDirectoryLocation,
            bool        importedProject
        )
        {
            Import temp = new Import(importElement, this, importedProject);

            if (temp.ConditionAttribute != null)
            {
                // Do not expand properties or items before passing in the value of the
                // condition attribute to EvaluateCondition, otherwise special characters
                // inside the property values can really confuse the condition parser.
                if (!Utilities.EvaluateCondition(temp.Condition, temp.ConditionAttribute,
                    new Expander(this.evaluatedProperties), this.conditionedPropertiesTable,
                    ParserOptions.AllowProperties, ParentEngine.LoggingServices, projectBuildEventContext))
                {
                    return;
                }
            }

            // If we got this far, we expect the "Project" attribute to have a reasonable
            // value, so process it now.

            // Expand any $(propertyname) references inside the "Project" attribute value.
            string expandedImportedFilename = (new Expander(this.evaluatedProperties)).ExpandAllIntoStringLeaveEscaped(temp.ProjectPath, temp.ProjectPathAttribute);

            // Expand any wildcards
            string[] importedFilenames = EngineFileUtilities.GetFileListEscaped(projectDirectoryLocation, expandedImportedFilename);

            for (int i = 0; i < importedFilenames.Length; i++)
            {
                string importedFilename = EscapingUtilities.UnescapeAll(importedFilenames[i]);     
                ProjectErrorUtilities.VerifyThrowInvalidProject(!string.IsNullOrEmpty(importedFilename),
                    importElement, "MissingRequiredAttribute",
                    XMakeAttributes.project, XMakeElements.import);

                Import import = new Import(importElement, this, importedProject);

                try
                {
                    if (!string.IsNullOrEmpty(projectDirectoryLocation))
                    {
                        import.SetEvaluatedProjectPath(Path.GetFullPath(Path.Combine(projectDirectoryLocation, importedFilename)));
                    }
                    else
                    {
                        import.SetEvaluatedProjectPath(Path.GetFullPath(importedFilename));
                    }
                }
                catch (Exception e) // Catching Exception, but rethrowing unless it's an IO related exception.
                {
                    if (ExceptionHandling.NotExpectedException(e))
                        throw;

                    ProjectErrorUtilities.VerifyThrowInvalidProject(false, importElement, "InvalidAttributeValueWithException", importedFilename, XMakeAttributes.project, XMakeElements.import, e.Message);
                }

                XmlDocument importedDocument = LoadImportedProject(import);

                if (importedDocument != null)
                {
                    this.rawGroups.InsertAtEnd(import);

                    // Get the top-level nodes from the XML.
                    XmlNodeList importedFileNodes = importedDocument.ChildNodes;

                    // The XML parser will guarantee that we only have one real root element,
                    // but we need to find it amongst the other types of XmlNode at the root.
                    foreach (XmlNode importedChildNode in importedFileNodes)
                    {
                        if (XmlUtilities.IsXmlRootElement(importedChildNode))
                        {
                            // Save the current directory, so we can restore it back later.
                            string currentDirectory = Directory.GetCurrentDirectory();

                            // If we have a <VisualStudioProject> node, tell the user they must upgrade the project
                            ProjectErrorUtilities.VerifyThrowInvalidProject(importedChildNode.LocalName != XMakeElements.visualStudioProject,
                                importedChildNode, "ProjectUpgradeNeeded");

                            // This node must be a <Project> node.
                            ProjectErrorUtilities.VerifyThrowInvalidProject(importedChildNode.LocalName == XMakeElements.project,
                                importedChildNode, "UnrecognizedElement", importedChildNode.Name);

                            ProjectErrorUtilities.VerifyThrowInvalidProject((importedChildNode.Prefix.Length == 0) && (String.Equals(importedChildNode.NamespaceURI, XMakeAttributes.defaultXmlNamespace, StringComparison.OrdinalIgnoreCase)),
                                importedChildNode, "ProjectMustBeInMSBuildXmlNamespace", XMakeAttributes.defaultXmlNamespace);

                            // We have the <Project> element, so process it.
                            this.ProcessProjectAttributes((XmlElement)importedChildNode,
                                /* imported project */ true);
                            this.ProcessProjectChildren((XmlElement)importedChildNode,
                                Path.GetDirectoryName(import.EvaluatedProjectPath),
                                /* imported project */ true);

                            break;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Loads the XML for the specified project that is being imported into the main project.
        /// </summary>
        /// <owner>RGoel, SumedhK</owner>
        /// <param name="import">The project being imported</param>
        /// <returns>XML for imported project; null, if duplicate import.</returns>
        private XmlDocument LoadImportedProject(Import import)
        {
            XmlDocument importedDocument = null;
            bool importedFileExists = File.Exists(import.EvaluatedProjectPath);

            // NOTE: don't use ErrorUtilities.VerifyThrowFileExists() here because that exception doesn't carry XML node
            // information, and we need that data to show a better error message

            if (!importedFileExists)
            {
                ProjectErrorUtilities.VerifyThrowInvalidProject((this.loadSettings & ProjectLoadSettings.IgnoreMissingImports) != 0,
                    import.ProjectPathAttribute, "ImportedProjectNotFound", import.EvaluatedProjectPath);
            }

            // Make sure that the file we're about to import hasn't been imported previously.
            // This is how we prevent circular dependencies.  It so happens that this mechanism
            // also prevents the same file from being imported twice, even it it's not a
            // circular dependency, but that's fine -- no good reason to do that anyway.
            if ((this.imports[import.EvaluatedProjectPath] != null) ||
                (string.Equals(this.FullFileName, import.EvaluatedProjectPath, StringComparison.OrdinalIgnoreCase)))
            {
                ParentEngine.LoggingServices.LogWarning(projectBuildEventContext, Utilities.CreateBuildEventFileInfo(import.ProjectPathAttribute, FullFileName),
                    "DuplicateImport", import.EvaluatedProjectPath);
            }
            else
            {
                // See if the imported project is also a top-level project that has been loaded
                // by the engine.  If so, use the in-memory copy of the imported project instead
                // of reading the copy off of the disk.  This way, we can reflect any changes
                // that have been made to the in-memory copy.
                Project importedProject = this.ParentEngine.GetLoadedProject(import.EvaluatedProjectPath);
                if (importedProject != null)
                {
                    importedDocument = importedProject.XmlDocument;
                }
                // The imported project is not part of the engine, so read it off of disk.
                else
                {
                    // If the file doesn't exist on disk but we're told to ignore missing imports, simply skip it
                    if (importedFileExists)
                    {
                        // look up the engine's cache to see if we've already loaded this imported project on behalf of another
                        // top-level project
                        ImportedProject previouslyImportedProject = (ImportedProject)ParentEngine.ImportedProjectsCache[import.EvaluatedProjectPath];

                        // if this project hasn't been imported before, or if it has changed on disk, we need to load it
                        if ((previouslyImportedProject?.HasChangedOnDisk(import.EvaluatedProjectPath) != false))
                        {
                            try
                            {
                                // Do not validate the imported file against a schema.
                                // We only validate the parent project against a schema in V1, because without custom
                                // namespace support, we would have to pollute the msbuild namespace with everything that
                                // appears anywhere in our targets file.

                                // cache this imported project, so that if another top-level project also imports this project, we
                                // will not re-parse the XML (unless it changes)
                                previouslyImportedProject = new ImportedProject(import.EvaluatedProjectPath);
                                ParentEngine.ImportedProjectsCache[import.EvaluatedProjectPath] = previouslyImportedProject;
                            }
                            // catch XML exceptions early so that we still have the imported project file name
                            catch (XmlException e)
                            {
                                BuildEventFileInfo fileInfo = new BuildEventFileInfo(e);
                                ProjectFileErrorUtilities.VerifyThrowInvalidProjectFile(false,
                                    fileInfo,
                                    "InvalidImportedProjectFile", e.Message);
                            }
                            // catch IO exceptions, for example for when the file is in use. DDB #36839
                            catch (Exception e)
                            {
                                if (ExceptionHandling.NotExpectedException(e))
                                    throw;

                                BuildEventFileInfo fileInfo = new BuildEventFileInfo(import.EvaluatedProjectPath);
                                ProjectFileErrorUtilities.VerifyThrowInvalidProjectFile(false,
                                    fileInfo,
                                    "InvalidImportedProjectFile", e.Message);
                            }
                        }

                        importedDocument = previouslyImportedProject.Xml;
                    }
                }

                // Add the imported filename to our list, so we can be sure not to import
                // it again.  This helps prevent infinite recursion.
                this.imports[import.EvaluatedProjectPath] = import;
            }

            return importedDocument;
        }

        /// <summary>
        /// This method gets called by the engine when any loaded project gets renamed (e.g.,
        /// saved to a different location, etc.).  This method should be responsible for updating
        /// all internal data structures to reflect the new name of the imported file.
        /// </summary>
        /// <param name="oldFileName"></param>
        /// <param name="newFileName"></param>
        /// <owner>RGoel, LukaszG</owner>
        internal void OnRenameOfImportedFile(string oldFileName, string newFileName)
        {
            // Loop through every PropertyGroup in the current project.
            foreach (BuildPropertyGroup pg in this.PropertyGroups)
            {
                // If the PropertyGroup is imported ...
                if (pg.IsImported)
                {
                    // ... then check the filename of the PropertyGroup to see if it
                    // matches the *old* file name.
                    if (String.Equals(pg.ImportedFromFilename, oldFileName, StringComparison.OrdinalIgnoreCase))
                    {
                        // Okay, we found a PropertyGroup that appears to have originated from
                        // the imported file that just got renamed.  We should update the PropertyGroup
                        // with the new name.
                        pg.ImportedFromFilename = newFileName;
                    }
                }
            }
        }

        /// <summary>
        /// Here, we use the internal objects we created during processing of the
        /// XML to actually evaluate the properties, items, targets, etc.  We
        /// will be evaluating the conditions, and expanding property/item
        /// references, etc.  We don't actually build though.
        /// </summary>
        /// <remarks>
        /// If this has been called by <see cref="ProcessMainProjectElement"/> we don't evaluate properties
        /// again, since that's already been done. We don't evaluate import tags again, for the same reason.
        /// In such a case, this method represents only evaluation "Pass 2".
        /// </remarks>
        /// <owner>RGoel</owner>
        private void EvaluateProject(bool currentlyLoading)
        {
#if (!STANDALONEBUILD)
            using (new CodeMarkerStartEnd(CodeMarkerEvent.perfMSBuildProjectEvaluateBegin, CodeMarkerEvent.perfMSBuildProjectEvaluateEnd))
#endif
            {
#if MSBUILDENABLEVSPROFILING 
                try
                {
                    string beginProjectEvaluate = String.Format(CultureInfo.CurrentCulture, "Evaluate Project {0} Using Old OM - Start", this.FullFileName);
                    DataCollection.CommentMarkProfile(8812, beginProjectEvaluate);
#endif
                string currentPerThreadProjectDirectory = Project.PerThreadProjectDirectory;

                try
                {
                    // Make the correct project directory available. During load, we need "exists" (with relative paths)
                    // on conditions to work correctly, and for wildcards to evaluate relative to the project directory.
                    Project.PerThreadProjectDirectory = this.ProjectDirectory;

                    // In case we've just loaded the project file, we don't want to repeat all
                    // of the work done during ProcessProjectChildren(...) to evaluate the
                    // properties.
                    if (!currentlyLoading)
                    {
                        // "Pass 1"
                        evaluatedProperties.Clear();

                        evaluatedProperties.ImportInitialProperties(this.EnvironmentProperties, this.ReservedProperties, this.Toolset.BuildProperties, this.GlobalProperties);

                        conditionedPropertiesTable.Clear();

                        EvaluateAllPropertyGroups();
                    }

                    // "Pass 1.5"
                    itemDefinitionLibrary.Evaluate(evaluatedProperties);

                    // "Pass 2"
                    evaluatedItems.Clear();
                    evaluatedItemsByName.Clear();

                    taskRegistry.Clear();

                    ResetBuildStatus();

                    // Every time we're essentially processing the XML from scratch,
                    // evaluate the items ignoring the conditions (the first bool parameter).
                    // This gives us the complete list of items whether or not they happen to
                    // be active for a particular build flavor.  This is useful for purposes
                    // of displaying in Solution Explorer.
                    // But also, at the same time, evaluate the items taking into account
                    // the "Condition"s correctly (the second bool parameter).  This is the real
                    // list of items that will be used for the purposes of "build".
                    EvaluateAllItemGroups(this.dirtyNeedToReprocessXml, true);

                    EvaluateAllUsingTasks();

                    this.dirtyNeedToReevaluate = false;
                    this.dirtyNeedToReprocessXml = false;
                }
                finally
                {
                    // We reset the path back to the original value in case the 
                    // host is depending on the current directory to find projects
                    Project.PerThreadProjectDirectory = currentPerThreadProjectDirectory;
                }
#if MSBUILDENABLEVSPROFILING 
                }
                finally
                {
                    string beginProjectEvaluate = String.Format(CultureInfo.CurrentCulture, "Evaluate Project {0} Using Old OM - End", this.FullFileName);
                    DataCollection.CommentMarkProfile(8813, beginProjectEvaluate);
                }
#endif
            }
        }

        /// <summary>
        /// Walk through all of the PropertyGroups in the project (including
        /// imported PropertyGroups) in order, and evaluate the properties.
        /// We end up producing a final linear evaluated property collection
        /// called this.evaluatedProperties.
        /// </summary>
        /// <owner>RGoel</owner>
        private void EvaluateAllPropertyGroups
            (
            )
        {
            foreach (IItemPropertyGrouping propertyGroup in this.rawGroups.PropertyGroupsTopLevelAndChooses)
            {
                if (propertyGroup is BuildPropertyGroup)
                {
                    ((BuildPropertyGroup) propertyGroup).Evaluate(this.evaluatedProperties, this.conditionedPropertiesTable, ProcessingPass.Pass1);
                }
                else if (propertyGroup is Choose)
                {
                    ((Choose) propertyGroup).Evaluate(this.evaluatedProperties, false, true, this.conditionedPropertiesTable, ProcessingPass.Pass1);
                }
                else
                {
                    ErrorUtilities.VerifyThrow(false, "Unexpected return type from this.rawGroups.PropertyGroupsAndChooses");
                }
            }
        }

        /// <summary>
        /// Evaluate all the &lt;ItemGroup&gt;'s in the project (including imported
        /// &lt;ItemGroup&gt;'s) in order, producing a final list of evaluated items.
        /// </summary>
        /// <param name="ignoreCondition"></param>
        /// <param name="honorCondition"></param>
        /// <owner>RGoel</owner>
        private void EvaluateAllItemGroups
        (
            bool ignoreCondition,
            bool honorCondition
        )
        {
            error.VerifyThrow(ignoreCondition || honorCondition, "Both ignoreCondition and honorCondition can't be false.");

            foreach (IItemPropertyGrouping itemGroup in this.rawGroups.ItemGroupsTopLevelAndChooses)
            {
                if (itemGroup is BuildItemGroup)
                {
                    ((BuildItemGroup) itemGroup).Evaluate(this.evaluatedProperties, this.evaluatedItemsByName, ignoreCondition, honorCondition, ProcessingPass.Pass2);
                }
                else if (itemGroup is Choose)
                {
                    ((Choose) itemGroup).Evaluate(this.evaluatedProperties, ignoreCondition, honorCondition, this.conditionedPropertiesTable, ProcessingPass.Pass2);
                }
                else
                {
                    ErrorUtilities.VerifyThrow(false, "Unexpected return type from this.rawGroups.ItemGroupsAndChooses");
                }
            }
        }

        /// <summary>
        /// This processes the &lt;UsingTask&gt; elements in the project file as well
        /// as the imported project files, by adding the necessary data to the
        /// task registry.
        /// </summary>
        /// <owner>RGoel</owner>
        private void EvaluateAllUsingTasks()
        {
            Expander expander = new Expander(evaluatedProperties, evaluatedItemsByName);

            foreach (UsingTask usingTask in this.usingTasks)
            {
                taskRegistry.RegisterTask(usingTask, expander, ParentEngine.LoggingServices, projectBuildEventContext);
            }
        }

        /// <summary>
        /// Adds an item to the appropriate project's evaluated items collection.  This method is
        /// NOT to be used during the build process to add items that are emitted by tasks.
        /// This is only for the purposes of adding statically-declared items in the logical
        /// project file, or items added to the project file by an IDE modifying the project contents.
        /// </summary>
        /// <param name="itemToInclude">The specific item to add to the project</param>
        internal void AddToItemListByNameIgnoringCondition(BuildItem item)
        {
            // Get a reference to the project-level hash table which is supposed to
            // contain the list of items of this type regardless of condition.  Note that the item type, when
            // used as a key into the overall hash table, is case-insensitive.
            BuildItemGroup itemListByNameIgnoringCondition = (BuildItemGroup)this.evaluatedItemsByNameIgnoringCondition[item.Name];

            // If no such BuildItemGroup exists yet, create a new BuildItemGroup and add it to 
            // the hashtable of ItemGroups by type.
            if (itemListByNameIgnoringCondition == null)
            {
                itemListByNameIgnoringCondition = new BuildItemGroup();
                this.evaluatedItemsByNameIgnoringCondition[item.Name] = itemListByNameIgnoringCondition;
            }

            // Actually add the new item to the Project object's data structures.
            itemListByNameIgnoringCondition.AddItem(item);
            evaluatedItemsIgnoringCondition.AddItem(item);
        }

        /// <summary>
        /// Adds an item to the appropriate project's evaluated items collection.  This method is
        /// NOT to be used during the build process to add items that are emitted by tasks.
        /// This is only for the purposes of adding statically-declared items in the logical
        /// project file, or items added to the project file by an IDE modifying the project contents.
        /// </summary>
        /// <param name="itemToInclude">The specific item to add to the project</param>
        internal void AddToItemListByName(BuildItem item)
        {
            // Get a reference to the project-level hash table which is supposed to
            // contain the list of items of this type.  Note that the item type, when
            // used as a key into the overall hash table, is case-insensitive.
            BuildItemGroup itemListByName = (BuildItemGroup)this.evaluatedItemsByName[item.Name];

            // If no such BuildItemGroup exists yet, create a new BuildItemGroup and add it to 
            // the hashtable of ItemGroups by type.
            if (itemListByName == null)
            {
                itemListByName = new BuildItemGroup();
                this.evaluatedItemsByName[item.Name] = itemListByName;
            }

            // Actually add the new item to the Project object's data structures.
            itemListByName.AddItem(item);
            evaluatedItems.AddItem(item);
        }

        /// <summary>
        /// This method returns true if the specified filename is a solution file (.sln), otherwise
        /// it returns false.
        /// </summary>
        /// <param name="filename"></param>
        /// <returns></returns>
        /// <owner>jomof</owner>
        internal static bool IsSolutionFilename(string filename)
        {
            return string.Equals(Path.GetExtension(filename), ".sln", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Returns true if the specified filename is a VC++ project file, otherwise returns false
        /// </summary>
        /// <owner>LukaszG</owner>
        internal static bool IsVCProjFilename(string filename)
        {
            return string.Equals(Path.GetExtension(filename), ".vcproj", StringComparison.OrdinalIgnoreCase);
        }
    }
}
