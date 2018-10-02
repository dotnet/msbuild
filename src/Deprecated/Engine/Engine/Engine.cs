// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Xml;
using System.Security;
using System.Diagnostics;
using System.Resources;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Globalization;
using System.Threading;

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
    /// Flags for controlling the build.
    /// </summary>
    [Flags]
    public enum BuildSettings
    {
        /// <summary>
        /// The default build.
        /// </summary>
        None = 0,

        /// <summary>
        /// When this flag is passed in, MSBuild assumes that no important external
        /// state has changed (for example, no files reference by the project have
        /// been modified) and doesn't rebuild any previously built targets.
        /// </summary>
        DoNotResetPreviouslyBuiltTargets = 1
    };

    /// <summary>
    /// Flags for controlling the project load.
    /// </summary>
    [Flags]
    public enum ProjectLoadSettings
    {
        /// <summary>
        /// Normal load
        /// </summary>
        None = 0,

        /// <summary>
        /// Ignore nonexistent targets files when loading the project
        /// </summary>
        IgnoreMissingImports = 1
    }

    /// <summary>
    /// Flags for controlling the toolset initialization.
    /// </summary>
    [Flags]
    public enum ToolsetDefinitionLocations
    {
        /// <summary>
        /// Do not read toolset information from any external location.
        /// </summary>
        None = 0,

        /// <summary>
        /// Read toolset information from the exe configuration file.
        /// </summary>
        ConfigurationFile = 1,

        /// <summary>
        /// Read toolset information from the registry (HKLM\Software\Microsoft\MSBuild\ToolsVersions).
        /// </summary>
        Registry = 2
    }

    /// <summary>
    /// This class represents the MSBuild engine. In a system of project-to-project dependencies, this class keeps track of the
    /// various projects being built, so that we can avoid building the same target in the same project more than once in a given
    /// build.
    /// </summary>
    /// <owner>RGoel</owner>
    [Obsolete("This class has been deprecated. Please use Microsoft.Build.Evaluation.ProjectCollection from the Microsoft.Build assembly instead.")]
    public class Engine
    {

        #region Member Data

        // For those folks who want to share a single Engine object across many projects
        // in a single AppDomain, we have a global one here that they can use.
        private static Engine globalEngine;

        // This is just a dummy XmlDocument that we use when we need to create a new
        // XmlElement or XmlAttribute that does not belong to any specific XML document.
        // All XmlElements and XmlAttributes must have an associated XmlDocument ...
        // hence this object.
        private static XmlDocument globalDummyXmlDoc;

        // Dictionary of toolset states
        // K: tools version
        // V: matching toolset state
        private Dictionary<string, ToolsetState> toolsetStateMap;

        // The collection of toolsets registered with this engine.
        private ToolsetCollection toolsets;

        // The name of the current default toolsVersion. Starts with Constants.defaultVersion
        private string defaultToolsVersion;
        
        // The node Id which the engine is running on
        private int nodeId;

        // the engine's version stamp
        private static Version engineVersion;

        // The initial set of properties to be used in building.
        // This should include any properties that were set on the XMake
        // command-line.  This should not include environment variables,
        // as those are added by the engine itself.
        private BuildPropertyGroup engineGlobalProperties;

        // The properties gathered from the environment variables.  This is populated
        // by the engine itself, so callers need not do anything with this.
        private readonly BuildPropertyGroup environmentProperties;

        // A list of projects currently loaded, indexed by project file fullpath.
        private Hashtable projectsLoadedByHost;

        // A list of projects that are part of the current build.
        private ProjectManager cacheOfBuildingProjects;

        // The number of projects that are currently in the process of building.
        private int numberOfProjectsInProgress = 0;

        // This is the default value used by newly created projects for whether or not the building
        // of targets is enabled.  This is for security purposes in case a host wants to closely
        // control which projects it allows to run targets/tasks.
        private bool buildEnabled = true;

        // used to cache imported projects so that we do not repeatedly parse the project files
        // PERF NOTE: even in a medium-sized build, since almost every project imports the same .targets files, we load the same
        // XML over and over again
        private Hashtable importedProjectsCache;

        // The logging services used to process all events raised by either engine or tasks. This
        // logging service while running on the child maybe directing events to another logging service
        // or out of the process
        private EngineLoggingServices primaryLoggingServices;

        // External logging service responsible for sending the events from child to the parent
        private EngineLoggingServices externalLoggingServices;

        // An event used by the logging service to indicate that it should be flushed
        private ManualResetEvent flushRequestEvent;

        private DualQueue<BuildRequest> buildRequests;

        private ManualResetEvent engineAbortEvent = new ManualResetEvent(false);
        
        // a cached version of the engineAbortEvent so we don't have to wait on it to determine the value.
        // If we do have to wait for something to happen we still need the event though.
        private volatile bool engineAbortCachedValue = false;

        private object engineAbortLock = new object();

        private DualQueue<EngineCommand> engineCommands;

        private DualQueue<TaskExecutionContext> taskOutputUpdates;

        private Scheduler scheduler;

        private Router router;

        private EngineCallback engineCallback;

        // the engine's event source
        private readonly EventSource eventSource;

        private EventSource eventSourceForForwarding;

        // the central or old style loggers listening to build events
        private ArrayList loggers;

        // the forwarding loggers listening to build events
        private ArrayList forwardingLoggers;

        // Node manager for the engine, directs requests to and from remote nodes
        private NodeManager nodeManager;

        private CacheManager cacheManager;

        // this seed is used to generate unique logger ids for each distributed logger
        private int lastUsedLoggerId;
        
        // this boolean is true if central logging is enabled 
        private bool enabledCentralLogging;

        // The class used to observe engine operation to collect data and detect errors
        private Introspector introspector;

        // Last time stamp of activity in the engine build loop
        private long lastLoopActivity;

        // Number of CPUs this engine is instantiated with
        private int numberOfCpus;

        // The current directory at the time the Engine was constructed -- 
        // if msbuild.exe is hosting, this is the current directory when
        // msbuild.exe was started
        private string startupDirectory;

        // Next TaskId for tasks which will be run on this engine instance
        private int nextTaskId = 0;

        // Counter for project objects
        private int nextTargetId = 0;

        // Counter for project objects
        private int nextProjectId = 0;

        // Counter of node Ids
        private int nextNodeId = 1;

        // Context of the event in which the fatal error terminating the execution occured
        private BuildEventContext fatalErrorContext;

        // File name of the project in which fatal error occured (cached in order to avoid complexity in the
        // finally clause)
        private string fatalErrorProjectName;

        // parameters used to configure the local node provider
        private string localNodeProviderParameters;

        // Set to true once the local node provider has been initialize
        private bool initializedLocaLNodeProvider;

        internal static bool debugMode = (Environment.GetEnvironmentVariable("MSBUILDDEBUG") == "1");

        // True if timing data for the build should be collected
        private bool profileBuild = (Environment.GetEnvironmentVariable("MSBUILDPROFILE") == "1");

        /// <summary>
        /// A array of string which list the properties that should be serialized accross from the child node
        /// </summary>
        private string[] propertyListToSerialize;

        /// <summary>
        ///  A ; delimited string which says which properties should be serialized accross from the child node
        /// </summary>
        private string forwardPropertiesFromChild = null;
        #endregion

        #region Constructors

        /// <summary>
        /// Default constructor that reads toolset information from both the registry
        /// and configuration file.
        /// The need for parameterless constructor is dictated by COM interop. 
        /// </summary>
        public Engine()
            : this(1 /* cpu */, false /* not child node */, 0 /* default NodeId */, null/*No msbuild.exe path*/, null, ToolsetDefinitionLocations.ConfigurationFile | ToolsetDefinitionLocations.Registry)
        {
        }

        /// <summary>
        /// Constructor to initialize binPath.
        /// </summary>
        [Obsolete("If you were simply passing in the .NET Framework location as the BinPath, just change to the parameterless Engine() constructor. Otherwise, you can define custom toolsets in the registry or config file, or by adding elements to the Engine's ToolsetCollection. Then use either the Engine() or Engine(ToolsetLocations) constructor instead.")]
        public Engine(string binPath)
            : this(1 /* number of cpus */, false /* not child node */, 0 /* default NodeId */, null/*No msbuild.exe path*/, null, ToolsetDefinitionLocations.ConfigurationFile | ToolsetDefinitionLocations.Registry)
        {
            // If a binpath was passed, overwrite the tools path of the default
            // tools version with it: this is how we emulate the old behavior of binpath
            if (binPath != null)
            {
                BinPath = binPath;
            }
        }

        /// <summary>
        /// Constructor providing the global properties the engine should inherit.
        /// </summary>
        public Engine(BuildPropertyGroup globalProperties)
            : this(globalProperties, ToolsetDefinitionLocations.ConfigurationFile | ToolsetDefinitionLocations.Registry)
        {
        }

        /// <summary>
        /// Constructor to specify whether toolsets should be initialized from the msbuild configuration file and from the registry 
        /// </summary>
        public Engine(ToolsetDefinitionLocations locations)
            : this(null, locations)
        {
        }

        /// <summary>
        /// Constructor to specify the global properties the engine should inherit and 
        /// the locations the engine should inspect for toolset definitions.
        /// </summary>
        public Engine(BuildPropertyGroup globalProperties, ToolsetDefinitionLocations locations)
            : this(1 /* cpu */, false /* not child node */, 0 /* default NodeId */, null/*No msbuild.exe path*/, globalProperties, locations)
        {
        }

        /// <summary>
        /// Constructor used by msbuild.exe and any other multiproc aware MSBuild hosts.
        /// </summary>
        public Engine(BuildPropertyGroup globalProperties, ToolsetDefinitionLocations locations, int numberOfCpus, string localNodeProviderParameters)
            : this(numberOfCpus, false, 0  /* default NodeId */, localNodeProviderParameters, globalProperties, locations)
        {
        }

        /// <summary>
        /// Constructor used to initialize an Engine on a child node. Called only by LocalNode to create a child node's engine.
        /// </summary>
        internal Engine(BuildPropertyGroup globalProperties, ToolsetDefinitionLocations locations, int numberOfCpus, bool isChildNode, int parentNodeId, string startupDirectory, string localNodeProviderParameters)
            : this(numberOfCpus, isChildNode, parentNodeId, localNodeProviderParameters, globalProperties, locations)
        {
            // Override the startup directory with the one we were passed
            ErrorUtilities.VerifyThrow(startupDirectory != null, "Need startup directory");
            this.startupDirectory = startupDirectory;
            
            forwardPropertiesFromChild = Environment.GetEnvironmentVariable("MSBuildForwardPropertiesFromChild");
            // Get a list of properties which should be serialized
            if (!String.IsNullOrEmpty(forwardPropertiesFromChild))
            {
                propertyListToSerialize = forwardPropertiesFromChild.Split(new char[]{';'}, StringSplitOptions.RemoveEmptyEntries);
            }
        }

        /// <summary>
        /// Constructor to init all data except for BinPath which is initialized separately because 
        /// a parameterless constructor is needed for COM interop
        /// </summary>
        internal Engine
        (
            int numberOfCpus, 
            bool isChildNode, 
            int parentNodeId, 
            string localNodeProviderParameters,
            BuildPropertyGroup globalProperties, 
            ToolsetDefinitionLocations locations
        )
        {
            // No need to check whether locations parameter 
            // is null, because it is a value type

            this.startupDirectory = Environment.CurrentDirectory;
            this.engineGlobalProperties = globalProperties == null ? new BuildPropertyGroup() : globalProperties;
            this.environmentProperties = new BuildPropertyGroup();
            this.toolsetStateMap = new Dictionary<string, ToolsetState>(StringComparer.OrdinalIgnoreCase);
            this.toolsets = new ToolsetCollection(this);

            // Every environment variable can be referenced just like a property
            // from the project file.  Here, we go ahead and add all the environment
            // variables to the property bag, so they can be treated just like any
            // other property later on.
            this.environmentProperties.GatherEnvironmentVariables();

            this.projectsLoadedByHost = new Hashtable(StringComparer.OrdinalIgnoreCase);

            this.cacheOfBuildingProjects = new ProjectManager();

            this.eventSource = new EventSource();

            this.buildEnabled = true;

            this.flushRequestEvent = new ManualResetEvent(false);

            this.primaryLoggingServices = new EngineLoggingServicesInProc(eventSource, false, flushRequestEvent);

            // Read any toolsets from the registry and config file
            PopulateToolsetStateMap(locations);

            this.nodeId = parentNodeId;
            this.localNodeProviderParameters = localNodeProviderParameters;
            this.numberOfCpus = numberOfCpus;

            if (this.numberOfCpus == 1 && !isChildNode)
            {
                this.primaryLoggingServices.FlushBuildEventsImmediatly = true;
            }

            this.buildRequests = new DualQueue<BuildRequest>();

            this.taskOutputUpdates = new DualQueue<TaskExecutionContext>();

            this.engineCommands = new DualQueue<EngineCommand>();

            this.engineCallback = new EngineCallback(this);
            this.nodeManager = new NodeManager(this.numberOfCpus, isChildNode, this);
            this.scheduler = new Scheduler(this.nodeId, this);
            this.router = new Router(this, scheduler);
            this.cacheManager = new CacheManager(this.DefaultToolsVersion);

            this.lastUsedLoggerId = EngineLoggingServicesInProc.FIRST_AVAILABLE_LOGGERID;

            this.enabledCentralLogging = false;

            this.introspector = new Introspector(this, cacheOfBuildingProjects, nodeManager);

            // Initialize the node provider
            InitializeLocalNodeProvider(locations);
        }

        /// <summary>
        /// Accessor wrapper for the engine abort event and cached value. This method is thread safe.
        /// </summary>
        /// <param name="value"></param>
        internal void SetEngineAbortTo(bool value)
        {
            lock (engineAbortLock)
            {
                this.engineAbortCachedValue = value;

                if (value)
                {
                    engineAbortEvent.Set();
                }
                else
                {
                    engineAbortEvent.Reset();
                }
            }
        }

        /// <summary>
        /// Initialize the local node provider
        /// Only happens on the parent node.
        /// </summary>
        private void InitializeLocalNodeProvider(ToolsetDefinitionLocations locations)
        {
            // Check if the local node provider has already been initialized
            if (initializedLocaLNodeProvider)
            {
                return;
            }

            // Don't register a local node provider if this is a child node engine
            if (!Router.ChildMode && numberOfCpus > 1)
            {
                LocalNodeProvider localNodeProvider = new LocalNodeProvider();


                string configuration = string.Empty;
                if (localNodeProviderParameters.EndsWith(";", StringComparison.OrdinalIgnoreCase))
                {
                    configuration = localNodeProviderParameters + "maxcpucount=" + Convert.ToString(numberOfCpus, CultureInfo.InvariantCulture);
                }
                else
                {
                    configuration = localNodeProviderParameters + ";maxcpucount=" + Convert.ToString(numberOfCpus, CultureInfo.InvariantCulture);
                }

                localNodeProvider.Initialize(configuration, engineCallback, engineGlobalProperties, locations, startupDirectory);
                this.nodeManager.RegisterNodeProvider(localNodeProvider);

                initializedLocaLNodeProvider = true;
            }
        }

        #endregion

        #region Properties

        /// <summary>
        /// Obsolete way to get or set the tools path for the current default tools version.
        /// </summary>
        /// <remarks>
        /// BinPath is an obsolete concept. We retain it for now for all the hosts that use the BinPath
        /// property, or the Engine(binPath) constructor, but internally it is just the tools path 
        /// of the default tools version.
        /// </remarks>
        /// <value>The MSBuild path.</value>
        [Obsolete("Avoid setting BinPath. If you were simply passing in the .NET Framework location as the BinPath, no other action is necessary. Otherwise, define Toolsets instead in the registry or config file, or by adding elements to the Engine's ToolsetCollection, in order to use a custom BinPath.")]
        public string BinPath
        {
            get
            {
                return this.ToolsetStateMap[this.defaultToolsVersion].ToolsPath;
            }
            set
            {
                error.VerifyThrowArgumentNull(value, "BinPath");

                // Replace the toolspath for the default tools version with this binpath
                UpdateToolsPath(defaultToolsVersion, value);
            }
        }


        /// <summary>
        /// Is this engine in the process of building?
        /// </summary>
        public bool IsBuilding
        {
            get
            {
                return numberOfProjectsInProgress > 0;
            }
        }
        
        /// <summary>
        /// The node Id the current engine instance is running on
        /// </summary>
        internal int NodeId
        {
            get { return nodeId; }
        }

        /// <summary>
        /// Gets the dummy owner document for "virtual" items.
        /// </summary>
        /// <owner>SumedhK</owner>
        /// <remarks>PERF NOTE: this property helps to delay creation of the XmlDocument object</remarks>
        /// <value>The dummy XmlDocument.</value>
        internal static XmlDocument GlobalDummyXmlDoc
        {
            get
            {
                if (globalDummyXmlDoc == null)
                {
                    globalDummyXmlDoc = new XmlDocument();
                }

                return globalDummyXmlDoc;
            }
        }

        /// <summary>
        /// An array of strings which list the properties that should be serialized from the child node to the parent node
        /// </summary>
        internal string[] PropertyListToSerialize
        {
            get
            {
                return propertyListToSerialize;
            }
        }
        /// <summary>
        /// Returns an instance of the Engine that is global (shared) for this AppDomain.
        /// Delays creation until necessary.
        /// </summary>
        /// <value>The global Engine instance.</value>
        /// <owner>RGoel</owner>
        public static Engine GlobalEngine
        {
            get
            {
                if (Engine.globalEngine == null)
                {
                    Engine.globalEngine = new Engine();
                }

                return Engine.globalEngine;
            }
        }

        /// <summary>
        /// Gets the file version of the file in which the Engine assembly lies.
        /// </summary>
        /// <remarks>
        /// This is the Windows file version (specifically the value of the ProductVersion
        /// resource), not necessarily the assembly version.
        /// </remarks>
        /// <owner>RGoel</owner>
        /// <value>The engine version string.</value>
        public static Version Version
        {
            // If you want the assembly version, use Engine.Constants.AssemblyVersion.
            get
            {
                if (engineVersion == null)
                {
                    string msbuildPath = null;

                    try
                    {
                        // Get the file version from the currently executing assembly.
                        // Use .CodeBase instead of .Location, because .Location doesn't
                        // work when Microsoft.Build.Engine.dll has been shadow-copied, for example
                        // in scenarios where NUnit is loading Microsoft.Build.Engine.
                        msbuildPath = new Uri(Assembly.GetExecutingAssembly().EscapedCodeBase).LocalPath;
                    }
                    catch (InvalidOperationException)
                    {
                        // Workaround for Watson Bug: #161292 people getting relative uri crash here.
                        // Last resort. We may have a problem when the assembly is shadow-copied.
                        msbuildPath = Path.GetFullPath(typeof(Engine).Assembly.Location);
                    }

                    var versionInfo = FileVersionInfo.GetVersionInfo(msbuildPath);
                    engineVersion = new Version(versionInfo.FileMajorPart, versionInfo.FileMinorPart, versionInfo.FileBuildPart, versionInfo.FilePrivatePart);
                }

                return engineVersion;
            }
        }

        /// <summary>
        /// Accessor for the engine's global properties. Global properties are those that would be set via the /p: switch at the
        /// command-line, or things that the IDE wants to set before building a project (such as the "Configuration" property).
        /// These global properties shall be applied to all projects that are built with this engine.
        /// </summary>
        /// <owner>RGoel</owner>
        /// <value>The global property bag.</value>
        public BuildPropertyGroup GlobalProperties
        {
            get
            {
                return this.engineGlobalProperties;
            }

            set
            {
                this.engineGlobalProperties = value;
            }
        }

        /// <summary>
        /// Read-only accessor for the environment variables.
        /// </summary>
        /// <value>The property bag of environment variables.</value>
        /// <owner>RGoel</owner>
        internal BuildPropertyGroup EnvironmentProperties
        {
            get
            {
                return this.environmentProperties;
            }
        }
        
        /// <summary>
        ///  Get a new TaskId
        /// (NOT Thread safe)
        /// </summary>
        internal int GetNextTaskId()
        {
            return this.nextTaskId++;
        }

        /// <summary>
        /// Returns an ID that is unique among all targets for this project.
        /// (NOT Thread safe)
        /// </summary>
        internal int GetNextTargetId()
        {
            return nextTargetId++;
        }

        /// <summary>
        /// This ID can be used to keep distinct task registries for each project object, even when the project objects
        /// are loaded from the same path on disk, but have different global properties
        /// (NOT Thread safe)
        /// </summary>
        internal int GetNextProjectId()
        {
            return nextProjectId++;
        }

        /// <summary>
        /// Gets a node ID for the provided engine
        /// (NOT Thread safe)
        /// </summary>
        internal int GetNextNodeId()
        {
            return nextNodeId++;
        }

        /// <summary>
        /// This is the default value used by newly created projects for whether or not the building
        /// of targets is enabled.  This is for security purposes in case a host wants to closely
        /// control which projects it allows to run targets/tasks.
        /// </summary>
        /// <owner>RGoel</owner>
        public bool BuildEnabled
        {
            get
            {
                return this.buildEnabled;
            }

            set
            {
                this.buildEnabled = value;
            }
        }

        /// <summary>
        /// Gets the cache of projects imported during the build.
        /// </summary>
        /// <remarks>PERF NOTE: this property helps to delay creation of the cache</remarks>
        /// <owner>SumedhK</owner>
        /// <value>Hashtable of imported projects.</value>
        internal Hashtable ImportedProjectsCache
        {
            get
            {
                if (importedProjectsCache == null)
                {
                    importedProjectsCache = new Hashtable(StringComparer.OrdinalIgnoreCase);
                }

                return importedProjectsCache;
            }
        }

        /// <summary>
        /// Returns the table of projects loaded by the host.
        /// </summary>
        /// <owner>RGoel</owner>
        /// <remarks>Marked "internal" for unit tests only.  To maintain encapsulation, please try not to 
        /// use this accessor in real msbuild code, except from within this class.</remarks>
        internal Hashtable ProjectsLoadedByHost
        {
            get
            {
                return this.projectsLoadedByHost;
            }
        }

        /// <summary>
        /// Dictionary of toolset states
        /// K: tools version
        /// V: matching toolset state
        /// </summary>
        internal Dictionary<string, ToolsetState> ToolsetStateMap
        {
            get
            {
                return this.toolsetStateMap;
            }
        }

        /// <summary>
        /// Returns the collection of Toolsets recognized by this Engine instance.
        /// </summary>
        public ToolsetCollection Toolsets
        {
            get
            {
                return this.toolsets;
            }
        }

        /// <summary>
        /// Returns the service that can be used to log events
        /// </summary>
        internal EngineLoggingServices LoggingServices
        {
            get
            {
                return this.primaryLoggingServices;
            }
            set
            {
                this.primaryLoggingServices = value;
            }
        }

        /// <summary>
        /// Provide the number of Cpus the engine was started with. This is used to communicate this number with the project for
        /// the reserved property MSBuildBuildNodeCount
        /// </summary>
        internal int EngineCpuCount
        {
            get
            {
                return numberOfCpus;
            }
        }

        /// <summary>
        /// The external logging service is used on the child to forward events from child to parent
        /// </summary>
        internal EngineLoggingServices ExternalLoggingServices
        {
            get
            {
                return externalLoggingServices;
            }
            set
            {
                this.externalLoggingServices = value;
            }
        }
        internal Scheduler Scheduler
        {
            get
            {
                return this.scheduler;
            }
        }

        internal Router Router
        {
            get
            {
                return this.router;
            }
        }

        internal NodeManager NodeManager
        {
            get
            {
                return this.nodeManager;
            }
        }

        internal CacheManager CacheManager
        {
            get
            {
                return this.cacheManager;
            }
        }

        internal Introspector Introspector
        {
            get
            {
                return this.introspector;
            }
        }

        internal EngineCallback EngineCallback
        {
            get
            {
                return engineCallback;
            }
        }


        internal bool EnabledCentralLogging
        {
            get
            {
                return enabledCentralLogging;
            }
        }

        /// <summary>
        /// Return true if the timing data for the build should be collected
        /// </summary>
        internal bool ProfileBuild
        {
            get
            {
                return profileBuild;
            }
        }

        /// <summary>
        /// Returns the event that can be used to trigger a flush of logging services
        /// </summary>
        internal ManualResetEvent FlushRequestEvent
        {
            get
            {
                return flushRequestEvent;
            }
        }

        /// <summary>
        /// The current directory at the time the Engine was constructed -- 
        /// if msbuild.exe is hosting, this is the current directory when
        /// msbuild.exe was started
        /// </summary>
        internal string StartupDirectory
        {
            get { return startupDirectory; }
        }
        
        #endregion

        #region Methods

        /// <summary>
        /// Return the global task registry for a particular toolset.
        /// </summary>
        internal ITaskRegistry GetTaskRegistry(BuildEventContext buildEventContext, string toolsetVersion)
        {
            error.VerifyThrow(toolsetVersion != null, "Expect non-null toolset version");
            error.VerifyThrow(toolsetStateMap.ContainsKey(toolsetVersion), "Expected to see the toolset in the table");

            ToolsetState toolsetState = toolsetStateMap[toolsetVersion];

            return toolsetState.GetTaskRegistry(buildEventContext);
        }


        /// <summary>
        /// Adds a new toolset to the engine. Any pre-existing toolset with the same
        /// tools version is replaced with the provided toolset.
        /// </summary>
        /// <param name="toolset">the Toolset</param>
        internal void AddToolset(Toolset toolset)
        {
            error.VerifyThrowArgumentNull(toolset, "toolset");

            if (toolsetStateMap.ContainsKey(toolset.ToolsVersion))
            {
                // It already exists: replace it with the new toolset
                toolsetStateMap[toolset.ToolsVersion] = new ToolsetState(this, toolset);

                // We must be sure to notify all of the loaded projects with this 
                // tools version that they are dirty so they will later pick up any changes 
                // to the ToolsetState.
                DirtyProjectsUsingToolsVersion(toolset.ToolsVersion);
            }
            else
            {
                toolsetStateMap.Add(toolset.ToolsVersion, new ToolsetState(this, toolset));
            }
        }

        /// <summary>
        /// Updates the tools path for the specified tools version. If no toolset with
        /// that tools version exists, it creates a new one.
        /// </summary>
        internal void UpdateToolsPath(string toolsVersion, string toolsPath)
        {
            BuildPropertyGroup buildProperties = null;

            if (toolsetStateMap.ContainsKey(toolsVersion))
            {
                buildProperties = toolsetStateMap[toolsVersion].BuildProperties.Clone(true /* deep clone */);
            }
            
            toolsets.Add(new Toolset(toolsVersion, toolsPath, buildProperties));
        }

        /// <summary>
        /// Marks as dirty any projects currently using the specified tools version,
        /// so they'll update with any new values in it
        /// </summary>
        private void DirtyProjectsUsingToolsVersion(string toolsVersion)
        {
            foreach (Project project in projectsLoadedByHost.Values)
            {
                if (String.Equals(project.ToolsVersion, toolsVersion, StringComparison.OrdinalIgnoreCase))
                {
                    project.MarkProjectAsDirtyForReprocessXml();
                }
            }
        }

        /// <summary>
        /// Populate ToolsetStateMap with a dictionary of (toolset version, ToolsetState) 
        /// using information from the registry and config file, if any.
        /// </summary>
        /// <remarks>Internal for unit testing purposes only</remarks>
        /// <param name="locations"></param>
        internal void PopulateToolsetStateMap(ToolsetDefinitionLocations locations)
        {
            BuildPropertyGroup initialProperties = new BuildPropertyGroup();
            initialProperties.ImportProperties(EnvironmentProperties);
            initialProperties.ImportProperties(GlobalProperties);

            string defaultVersionFromReaders = ToolsetReader.ReadAllToolsets(toolsets, GlobalProperties, initialProperties, locations);

            // If we got a default version from the registry or config file, we should
            // use that from now on. The readers guarantee that any default version they return
            // has a corresponding Toolset too.
            if (defaultVersionFromReaders != null)
            {
                this.DefaultToolsVersion = defaultVersionFromReaders;
            }
            else
            {
                // We're going to choose a hard coded default tools version of 2.0.
                // But don't overwrite any existing tools path for this default we're choosing.
                if (!toolsetStateMap.ContainsKey(Constants.defaultToolsVersion))
                {
                    string pathTo20Framework = FrameworkLocationHelper.PathToDotNetFrameworkV20;

                    if (pathTo20Framework == null)
                    {
                        // We have been given no default, so we want to choose 2.0, but .NET 2.0 is not installed.
                        // In general we do not verify that MSBuildToolsPath's point to a valid location, 
                        // so failing here would be inconsistent. The build might not even use this toolset.
                        // Instead, synthesize what would be the path to the .NET 2.0 install location.
                        // If the build tries to use the default toolset, the problem will be discovered then.
                        pathTo20Framework = Path.Combine(Environment.SystemDirectory, @"Microsoft.NET\Framework\v2.0.50727");
                    }

                    // There's no tools path already for 2.0, so use the path to the v2.0 .NET Framework.
                    // If an old-fashioned caller sets BinPath property, or passed a BinPath to the constructor,
                    // that will overwrite what we're setting here.
                    toolsets.Add(new Toolset(Constants.defaultToolsVersion, pathTo20Framework));
                }

                // Now update the default tools version to 2.0
                DefaultToolsVersion = Constants.defaultToolsVersion;
            }

        }

        /// <summary>
        /// The default tools version of this Engine. Projects use this tools version if they
        /// aren't otherwise told what tools version to use.
        /// This value is gotten from the .exe.config file, or else in the registry, 
        /// or if neither specify a default tools version then it is hard-coded to the tools version "2.0".
        /// </summary>
        public string DefaultToolsVersion
        {
            get
            {
                ErrorUtilities.VerifyThrow(this.defaultToolsVersion != null,
                    "The entry for the default tools version should have been created by now");

                return this.defaultToolsVersion;
            }
            set
            {
                // We don't allow DefaultToolsVersion to be set after any projects have been loaded by the
                // engine, because the semantics would be odd.
                ErrorUtilities.VerifyThrowInvalidOperation(this.ProjectsLoadedByHost.Count == 0,
                   "CannotSetDefaultToolsVersionAfterLoadingProjects");

                ErrorUtilities.VerifyThrowArgumentNull(value, "value");

                // We don't check there is actually a matching toolset, because the default for 4.0 is 2.0
                // even if 2.0 isn't installed

                this.defaultToolsVersion = value;
            }
        }

        /// <summary>
        /// Called to register loggers with the engine. Once loggers are registered, all build events will be sent to them.
        /// </summary>
        /// <exception cref="LoggerException">Logger indicating it failed in a controlled way</exception>
        /// <exception cref="InternalLoggerException">Logger threw arbitrary exception</exception>
        public void RegisterLogger(ILogger logger)
        {
            error.VerifyThrowArgumentNull(logger, "logger");

            // Since we are registering a central logger - need to make sure central logging is enabled for all nodes
            if (!enabledCentralLogging)
            {
                enabledCentralLogging = true;
                nodeManager.UpdateSettings(enabledCentralLogging, this.primaryLoggingServices.OnlyLogCriticalEvents, true);
            }

            RegisterLoggerInternal(logger, eventSource, false);
        }

        /// <summary>
        /// Initializes the logger and adds it to the list of loggers maintained by the engine
        /// </summary>
        /// <exception cref="LoggerException">Logger indicating it failed in a controlled way</exception>
        /// <exception cref="InternalLoggerException">Logger threw arbitrary exception</exception>
        private void RegisterLoggerInternal(ILogger logger, EventSource sourceForLogger, bool forwardingLogger)
        {
            try
            {
                if (logger is INodeLogger)
                {
                    ((INodeLogger)logger).Initialize(sourceForLogger, this.numberOfCpus);
                }
                else
                {
                    logger.Initialize(sourceForLogger);
                }
            }
            // Polite logger failure
            catch (LoggerException)
            {
                throw;
            }
            catch (Exception e)
            {
                InternalLoggerException.Throw(e, null, "FatalErrorWhileInitializingLogger", false, logger.GetType().Name);
            }

            if (forwardingLogger)
            {
                if (forwardingLoggers == null)
                {
                    forwardingLoggers = new ArrayList();
                }

                forwardingLoggers.Add(logger);
            }
            else
            {
                if (loggers == null)
                {
                    loggers = new ArrayList();
                }

                loggers.Add(logger);
            }
        }

        /// <summary>
        /// Called to register distributed loggers with the engine. 
        /// This method is not thread safe. All loggers should registered prior to
        /// starting the build in order to guarantee uniform behavior
        /// </summary>
        /// <exception cref="LoggerException">Logger indicating it failed in a controlled way</exception>
        /// <exception cref="InternalLoggerException">Logger threw arbitrary exception</exception>
        public void RegisterDistributedLogger(ILogger centralLogger, LoggerDescription forwardingLogger)
        {
            error.VerifyThrowArgumentNull(forwardingLogger, "forwardingLogger");
            if (centralLogger == null)
            {
                centralLogger = new NullCentralLogger();
            }

            // If this is the first distributed logger we need to create an event source for local
            // forwarding loggers
            if (eventSourceForForwarding == null)
            {
                eventSourceForForwarding = new EventSource();
                ((EngineLoggingServicesInProc)primaryLoggingServices).RegisterEventSource
                    (EngineLoggingServicesInProc.LOCAL_FORWARDING_EVENTSOURCE, eventSourceForForwarding);
            }
            // Assign a unique logger Id to this distributed logger
            int loggerId = lastUsedLoggerId;
            lastUsedLoggerId++;
            forwardingLogger.LoggerId = loggerId;

            //Create and configure the local node logger 
            IForwardingLogger localForwardingLogger = null;
            try
            {
                localForwardingLogger = forwardingLogger.CreateForwardingLogger();
                // Check if the class was not found in the assembly
                if (localForwardingLogger == null)
                {
                    InternalLoggerException.Throw(null, null, "LoggerNotFoundError", true, forwardingLogger.Name);
                }
                // Configure the object 
                EventRedirector newRedirector = new EventRedirector(forwardingLogger.LoggerId, primaryLoggingServices);
                localForwardingLogger.BuildEventRedirector = newRedirector;
                localForwardingLogger.Parameters = forwardingLogger.LoggerSwitchParameters;
                localForwardingLogger.Verbosity = forwardingLogger.Verbosity;
                localForwardingLogger.NodeId= nodeId;
                // Convert the path to the logger DLL to full path before passing it to the node provider
                forwardingLogger.ConvertPathsToFullPaths();
            }
            // Polite logger failure
            catch (LoggerException)
            {
                throw;
            }
            // Logger class was not found
            catch (InternalLoggerException)
            {
                throw;
            }
            catch (Exception e)
            {
                InternalLoggerException.Throw(e, null, "LoggerCreationError", true, forwardingLogger.Name);
            }

            // Register the local forwarding logger to listen for all local events
            RegisterLoggerInternal(localForwardingLogger, eventSourceForForwarding, true);

            //Register this logger's node logger with the node manager so that all 
            //the nodes instantiate this node logger and forward the events
            nodeManager.RegisterNodeLogger(forwardingLogger);

            // Create a private event source that will be used by this distributed logger and register
            // the central logger with the engine
            EventSource privateEventSource = new EventSource();
            RegisterLoggerInternal(centralLogger, privateEventSource, false);

            // Register the private event source with the logging services so that the events from the local
            // node logger are forwarded to the central logger
            ((EngineLoggingServicesInProc)primaryLoggingServices).RegisterEventSource(forwardingLogger.LoggerId, privateEventSource);
        }

        /// <summary>
        /// Stop forwarding events to any loggers
        /// </summary>
        internal void BeginEatingLoggingEvents()
        {
            primaryLoggingServices.BeginEatingEvents();
        }

        /// <summary>
        /// Resume forwarding events to loggers
        /// </summary>
        internal void EndEatingLoggingEvents()
        {
            primaryLoggingServices.EndEatingEvents();
        }

        /// <summary>
        /// Clear out all registered loggers so that none are registered.
        /// </summary>
        /// <exception cref="LoggerException">Logger indicating it failed in a controlled way</exception>
        /// <exception cref="InternalLoggerException">Logger threw arbitrary exception</exception>
        public void UnregisterAllLoggers()
        {
            if (forwardingLoggers != null && forwardingLoggers.Count > 0)
            {
                // Disconnect forwarding loggers from the event source
                ((EngineLoggingServicesInProc)primaryLoggingServices).UnregisterEventSource
                                        (EngineLoggingServicesInProc.LOCAL_FORWARDING_EVENTSOURCE);
                // Shutdown forwarding loggers
                UnregisterLoggersInternal(forwardingLoggers);
                forwardingLoggers = null;
            }

            // Make that events generated during the shutdown of forwarding loggers reach central loggers
            primaryLoggingServices.ProcessPostedLoggingEvents();
            // Disconnect central and old style loggers
            primaryLoggingServices.Shutdown();
            // Shutdown central and old style loggers
            UnregisterLoggersInternal(loggers);
            loggers = null;
        }

        /// <summary>
        /// Call shutdown method on each of the loggers in the given list
        /// </summary>
        internal void UnregisterLoggersInternal(ArrayList loggersToUnregister)
        {
            if (loggersToUnregister != null)
            {
                foreach (ILogger logger in loggersToUnregister)
                {
                    try
                    {
                        logger.Shutdown();
                    }
                    // Polite logger failure
                    catch (LoggerException)
                    {
                        throw;
                    }
                    catch (Exception e)
                    {
                        InternalLoggerException.Throw(e, null, "FatalErrorDuringLoggerShutdown", false, logger.GetType().Name);
                    }
                }
            }
        }

        /// <summary>
        /// Log BuildFinished event (if there is no unhandled exception) and clear
        /// the projects loaded by host from the 
        /// </summary>
        internal void EndingEngineExecution(bool buildResult, bool exitedDueToError)
        {
            // Don't log BuildFinished if there is an unhandled exception
            if (!exitedDueToError)
            {
                // Log BuildFinished event first to the forwarding loggers and then to the
                // central logger. On the child node all events maybe being forwarding to the
                // parent node, so post the event directly to the forwarding loggers.
                if (Router.ChildMode)
                {
                    if (loggers != null && loggers.Count > 0)
                    {
                        // Flush all the events currently in the queue
                        LoggingServices.ProcessPostedLoggingEvents();

                        LoggingServices.LogBuildFinished(buildResult, EngineLoggingServicesInProc.CENTRAL_ENGINE_EVENTSOURCE);

                        // Flush the queue causing the forwarding loggers to process all event and the BuildFinished event
                        LoggingServices.ProcessPostedLoggingEvents();
                    }
                }
                else
                {
                    // Cause the events to be posted to the child nodes and there event queues to be flushed
                    NodeManager.ShutdownNodes(buildResult ? Node.NodeShutdownLevel.BuildCompleteSuccess :
                                                            Node.NodeShutdownLevel.BuildCompleteFailure);

                    // Post the event to old style loggers and forwarding loggers on parent node
                    LoggingServices.LogBuildFinished(buildResult);

                    // Cause the forwarding loggers to process BuildFinished event and whatever other events 
                    // were in the queue (on the child the event are flushed to the level of the outofproc logging service)
                    LoggingServices.ProcessPostedLoggingEvents();

                    // Post the event to all the central loggers
                    LoggingServices.LogBuildFinished(buildResult, EngineLoggingServicesInProc.ALL_PRIVATE_EVENTSOURCES);

                    // Flush the queue causing the central loggers to process any event sent to them
                    LoggingServices.ProcessPostedLoggingEvents();
                }
            }

            foreach (string loadedProjectFullPath in projectsLoadedByHost.Keys)
            {
                // For each of the projects that the host has actually loaded (and holding on to),
                // remove all projects with that same fullpath from the ProjectManager.  There are
                // a couple of reasons for this:
                // 1.   Because the host is hanging on to this projects, during design-time the host 
                //      might decide to change the GlobalProperties on one of these projects.  He might
                //      change the GlobalProperties such that they now are equivalent to the GlobalProperties
                //      for one of the projects in the ProjectManager.  That would get weird because 
                //      we'd end up with two projects with the same fullpath and same GlobalProperties,
                //      and we wouldn't know which one to choose (on the next build).
                // 2.   Because the host is hanging on to the projects, it may decide to make in-memory 
                //      changes to the project.  On next build, we need to take those changes into
                //      account, and any instances of Project in the ProjectManager won't have those 
                //      changes.
                this.cacheOfBuildingProjects.RemoveProjects(loadedProjectFullPath);
            }
        }

        /// <summary>
        /// Called when the host is done with this engine; unregisters loggers and
        /// shuts down nodes and TEM's.
        /// </summary>
        public void Shutdown()
        {
            // First shutdown the nodes, this may generate some logging events
            NodeManager.ShutdownNodes(Node.NodeShutdownLevel.PoliteShutdown);
            // Shutdown the loggers. First shutdown the forwarding loggers allowing them to generate events
            UnregisterAllLoggers();
        }


        /// <summary>
        /// Creates a new empty Project object that is associated with this engine. All projects must be associated with an
        /// engine, because they need loggers, global properties, reserved properties, etc.
        /// </summary>
        /// <owner>RGoel</owner>
        /// <returns>The created project.</returns>
        public Project CreateNewProject
            (
            )
        {
            return new Project(this);
        }

        /// <summary>
        /// Retrieves the project object for the specified project file full path, if it has
        /// been loaded by this Engine.  Returns null if this project is unknown to us.
        /// </summary>
        /// <param name="projectFullFileName"></param>
        /// <returns>The project object associated with this engine that matches the full path.</returns>
        /// <owner>RGoel</owner>
        public Project GetLoadedProject
            (
            string projectFullFileName
            )
        {
            ErrorUtilities.VerifyThrowArgumentNull(projectFullFileName, "projectFullFileName");
            return (Project)this.projectsLoadedByHost[projectFullFileName];
        }

        /// <summary>
        /// Removes a project object from our hash table of loaded projects.  After this is
        /// called, it is illegal to attempt to do anything else with the Project, so don't
        /// call it unless you are completely done with the project.
        ///
        /// IDEs should call this when they're done with a particular project.  This
        /// causes us to unhook the project from the Engine object, so that there
        /// will be no more references to the project, and the garbage collector
        /// can clean up.
        /// </summary>
        /// <param name="project"></param>
        /// <owner>RGoel</owner>
        public void UnloadProject
            (
            Project project
            )
        {
            error.VerifyThrowArgumentNull(project, "project");

            ErrorUtilities.VerifyThrow(project.IsLoadedByHost, "How did the caller get a reference to this Project object if it's not marked as loaded?");
            // Make sure this project object is associated with this engine object.
            ErrorUtilities.VerifyThrowInvalidOperation(project.ParentEngine == this, "IncorrectObjectAssociation", "Project", "Engine");

            // Host is mucking with this project.  Remove the cached versions of
            // all projects with this same full path.  Over aggressively getting rid
            // of stuff from the cache is better than accidentally leaving crud in 
            // there.
            UnloadProject(project, true /* Unload all versions */);
        }

        internal void UnloadProject(Project project, bool unloadAllVersions )
        {
            if (project.FullFileName.Length > 0)
            {
                if (this.projectsLoadedByHost.Contains(project.FullFileName))
                {
                    this.projectsLoadedByHost.Remove(project.FullFileName);
                }
                if (unloadAllVersions)
                {
                    this.cacheOfBuildingProjects.RemoveProjects(project.FullFileName);
                }
                else
                {
                    this.cacheOfBuildingProjects.RemoveProject(project);
                }
            }
            project.ClearParentEngine();
        }

        /// <summary>
        /// Notifies the engine on a project rename, so that we can update our hash tables.
        /// </summary>
        /// <param name="project"></param>
        /// <param name="oldFullFileName"></param>
        /// <param name="newFullFileName"></param>
        /// <owner>RGoel</owner>
        internal void OnRenameProject
        (
            Project project,
            string oldFullFileName,
            string newFullFileName
        )
        {
            ErrorUtilities.VerifyThrow(project.IsLoadedByHost, "This method can only be called for projects loaded by the host.");

            oldFullFileName = (oldFullFileName == null) ? String.Empty : oldFullFileName;
            newFullFileName = (newFullFileName == null) ? String.Empty : newFullFileName;
            if (oldFullFileName == newFullFileName)
            {
                // Nothing to do, since this really isn't a rename.
                return;
            }
            // We don't store nameless projects.  So only if the old name was non-empty, remove
            // it from our hash table.
            if (oldFullFileName.Length > 0)
            {
                error.VerifyThrow((Project)projectsLoadedByHost[oldFullFileName] == project, "The engine's loaded project table was not up-to-date.");
                this.projectsLoadedByHost.Remove(oldFullFileName);

                // Host is mucking with this project.  Remove the cached versions of
                // all projects with this same full path.  Over aggressively getting rid
                // of stuff from the cache is better than accidentally leaving crud in 
                // there.
                this.cacheOfBuildingProjects.RemoveProjects(oldFullFileName);
            }
            // We don't store nameless projects.  So only if the new name is non-empty, add
            // it to our hash table.
            if (newFullFileName.Length > 0)
            {
                // If there's already a project with this new name in our table ...
                Project oldProject = (Project)projectsLoadedByHost[newFullFileName];
                if (oldProject != null)
                {
                    // Our IDE should never do this.  BUT in case someone does, just
                    // kick out the previous project from the table.
                    this.UnloadProject(oldProject);
                }

                this.projectsLoadedByHost[newFullFileName] = project;

                // Host is mucking with this project.  Remove the cached versions of
                // all projects with this same full path.  Over aggressively getting rid
                // of stuff from the cache is better than accidentally leaving crud in 
                // there.
                this.cacheOfBuildingProjects.RemoveProjects(newFullFileName);
            }

            if ((oldFullFileName.Length > 0) && (newFullFileName.Length > 0))
            {
                // MSBuild projects keep track of PropertyGroups that are imported from other
                // files.  It does this tracking by using the project file name of the imported
                // file.  So when a project gets renamed, as is being done here, we need 
                // to go update all those imported PropertyGroup records with the new filename.

                // Loop through every loaded project, and inform it about the newly named
                // file, so it can react accordingly.
                foreach (Project loadedProject in projectsLoadedByHost.Values)
                {
                    // Note:  We notify every project, even though every project is probably
                    // not actually importing this file that just got renamed.
                    loadedProject.OnRenameOfImportedFile(oldFullFileName, newFullFileName);
                }
            }
        }

        /// <summary>
        /// Remove all references to Project objects from our cache.  This is called by the
        /// IDE on Solution Close.
        /// </summary>
        /// <owner>RGoel</owner>
        public void UnloadAllProjects
            (
            )
        {
            Debug.Assert(this.projectsLoadedByHost.Count == 0, "Shouldn't the host have unloaded the projects already?");

            // Copy the contents of the hashtable into a temporary array, because we can't
            // be modifying the hashtable while we're iterating through it.
            Project[] arrayOfprojectsLoadedByHost = new Project[this.projectsLoadedByHost.Count];
            this.projectsLoadedByHost.Values.CopyTo(arrayOfprojectsLoadedByHost, 0);

            foreach (Project project in arrayOfprojectsLoadedByHost)
            {
                // This removes the project from the projectsLoadedByHost hashtable.
                this.UnloadProject(project);
            }

            ErrorUtilities.VerifyThrow(this.projectsLoadedByHost.Count == 0, "All projects did not get unloaded?");

            this.cacheOfBuildingProjects.Clear();
        }

        /// <summary>
        /// When true, only log critical events such as warnings and errors. Has to be in here for API compat
        /// </summary>
        public bool OnlyLogCriticalEvents
        {
            get
            {
                return this.primaryLoggingServices.OnlyLogCriticalEvents;
            }
            set
            {
                this.primaryLoggingServices.OnlyLogCriticalEvents = value;
                nodeManager.UpdateSettings(enabledCentralLogging, value, true);
            }
        }

        /// <summary>
        /// Builds the default targets in an already-loaded project.
        /// </summary>
        /// <param name="project"></param>
        /// <owner>RGoel</owner>
        public bool BuildProject
        (
            Project project
        )
        {
            return BuildProject(project, null, null, BuildSettings.None);
        }

        /// <summary>
        /// Builds a single target in an already-loaded project.
        /// </summary>
        /// <param name="project"></param>
        /// <param name="targetName"></param>
        /// <owner>RGoel</owner>
        public bool BuildProject
        (
            Project project,
            string targetName
        )
        {
            return BuildProject(project, (targetName == null) ? null : new string[] { targetName }, null, BuildSettings.None);
        }
        
        /// <summary>
        /// Builds a list of targets in an already-loaded project.
        /// </summary>
        /// <param name="project"></param>
        /// <param name="targetNames"></param>
        /// <owner>RGoel</owner>
        public bool BuildProject
        (
            Project project,
            string[] targetNames
        )
        {
            return BuildProject(project, targetNames, null, BuildSettings.None);
        }

        /// <summary>
        /// Builds a list of targets in an already-loaded project, and returns the target outputs.
        /// </summary>
        /// <param name="project"></param>
        /// <param name="targetNames"></param>
        /// <param name="targetOutputs"></param>
        /// <owner>RGoel</owner>
        public bool BuildProject
        (
            Project project,
            string[] targetNames,
            IDictionary targetOutputs   // can be null if outputs are not needed
        )
        {
          return BuildProject(project, targetNames, targetOutputs, BuildSettings.None);
        }

        /// <summary>
        /// Builds a list of targets in an already-loaded project using the specified
        /// flags, and returns the target outputs.
        /// </summary>
        /// <owner>RGoel</owner>
        /// <param name="project"></param>
        /// <param name="targetNames"></param>
        /// <param name="targetOutputs"></param>
        /// <param name="buildFlags">whether previously built targets should be reset or not</param>
        /// <returns>true, if build succeeds</returns>
        public bool BuildProject
        (
            Project project,
            string[] targetNames,
            IDictionary targetOutputs,   // can be null if outputs are not needed
            BuildSettings buildFlags
        )
        {
            return PostProjectEvaluationRequests(project, new string[] { null }, new string[1][] { targetNames },
                                                 new BuildPropertyGroup[] { null }, new IDictionary[] { targetOutputs }, buildFlags, new string[] { null });
        }

        /// <summary>
        /// Main engine loop.
        /// </summary>
        internal BuildResult EngineBuildLoop(BuildRequest terminatingBuildRequest)
        {
            ErrorUtilities.VerifyThrow(this.numberOfProjectsInProgress == 0 || terminatingBuildRequest != null,
                                       "We can only call this method once");

            // Create an array of events to which this thread responds
            WaitHandle[] waitHandles = new WaitHandle[5];
            waitHandles[0] = engineAbortEvent;                  // Exit event
            waitHandles[1] = engineCommands.QueueReadyEvent;    // New engine command
            waitHandles[2] = buildRequests.QueueReadyEvent;     // New build request
            waitHandles[3] = taskOutputUpdates.QueueReadyEvent; // New task outputs
            waitHandles[4] = flushRequestEvent;                 // A logging service needs a flush

            BuildResult buildResult = null;
            bool continueExecution = true;
            lastLoopActivity = DateTime.Now.Ticks;
            int loopTimeout= Introspector.initialLoopTimeout;   // Inactivity timeout which triggers deadlock check
            int loopTimeoutRemaining = Introspector.initialLoopTimeout;
            int flushTimeout = EngineLoggingServices.flushTimeoutInMS; // Timeout with which the log is flushed
            bool forceFlush = false;
            while (
                    continueExecution && 
                    (terminatingBuildRequest == null || terminatingBuildRequest.BuildCompleted == false)
                  )
            {
                int eventType = 0;

                // See if we have anything to do without waiting on the handles which is expensive 
                // for kernel mode objects.
                if (this.engineAbortCachedValue == true)
                {
                    eventType = 0;
                }
                else if (engineCommands.Count > 0)
                {
                    eventType = 1;
                }
                else if (buildRequests.Count > 0)
                {
                    eventType = 2;
                }
                else if (taskOutputUpdates.Count > 0)
                {
                    eventType = 3;
                }
                else if (primaryLoggingServices.NeedsFlush(lastLoopActivity))
                {
                    eventType = 4;
                }
                else
                {
                    // Nothing going on at the moment, go to sleep and wait for something to happen
                    eventType = WaitHandle.WaitAny(waitHandles, flushTimeout, false);
                }

                if (eventType == WaitHandle.WaitTimeout)
                {
                    // Decrement time remaining until deadlock check
                    if (loopTimeoutRemaining != Timeout.Infinite)
                    {
                        loopTimeoutRemaining = flushTimeout > loopTimeoutRemaining ? 
                                                  0 : loopTimeoutRemaining - flushTimeout;
                    }
                    // Always force a flush on a time
                    forceFlush = true;

                    // If time has run out perform deadlock check
                    if (loopTimeoutRemaining == 0)
                    {
                        loopTimeout =
                            introspector.DetectDeadlock(buildRequests.Count + this.taskOutputUpdates.Count,
                                                        lastLoopActivity, loopTimeout);
                        loopTimeoutRemaining = loopTimeout;
                    }
                }
                else if (eventType == 0)
                {
                    continueExecution = false;
                    SetEngineAbortTo(false);
                }
                // Received an engine command
                else if (eventType == 1)
                {
                    EngineCommand engineCommand = this.engineCommands.Dequeue();
                    ErrorUtilities.VerifyThrow(engineCommand != null, "Should have gotten a command");
                    // Execute the command
                    engineCommand.Execute(this);

                    // Don't consider node status request to be activity 
                    if (!(engineCommand is RequestStatusEngineCommand))
                    {
                        lastLoopActivity = DateTime.Now.Ticks;
                        loopTimeoutRemaining = loopTimeout;
                    }
                }
                // New build requests have been posted
                else if (eventType == 2)
                {
                    BuildRequest currentRequest = this.buildRequests.Dequeue();
                    ErrorUtilities.VerifyThrow(currentRequest != null, "Should have gotten an evalution request");

                    //Console.WriteLine( "Child mode: " + Scheduler.ChildMode +" Got request to build " + currentRequest.GetTargetNamesList() + " in " + currentRequest.ProjectFileName + " Time: " + DateTime.Now.ToLongTimeString() + ":" + DateTime.Now.Millisecond);

                    if (!currentRequest.BuildCompleted)
                    {
                        if (currentRequest.ProjectToBuild != null)
                        {
                            Scheduler.NotifyOfSchedulingDecision(currentRequest, EngineCallback.inProcNode);
                            BuildProjectInternal(currentRequest, null, null, true);
                        }
                        else
                        {
                            BuildProjectFileInternal(currentRequest);
                        }
                    }
                    else
                    {
                        InvalidProjectFileException projectException = currentRequest.BuildException;
                        primaryLoggingServices.LogInvalidProjectFileError(currentRequest.ParentBuildEventContext, projectException);
                        Scheduler.NotifyOfSchedulingDecision(currentRequest, this.nodeId);
                        HandleProjectFileInternalException(currentRequest);
                    }
                    lastLoopActivity = DateTime.Now.Ticks;
                    loopTimeoutRemaining = loopTimeout;
                }
                // New task outputs have been posted
                else if (eventType == 3)
                {
                    TaskExecutionContext taskExecutionContext = this.taskOutputUpdates.Dequeue();
                    ErrorUtilities.VerifyThrow(taskExecutionContext != null, "Should have gotten a task update");

                    // Clear the node proxy state, all the write to the proxy state should come from the engine thread
                    EngineCallback.ClearContextState(taskExecutionContext.HandleId);

                    if (Engine.debugMode)
                    {
                        if (taskExecutionContext.BuildContext.BuildRequest != null)
                            Console.WriteLine("NodeId: " + NodeId + " Got output update " + taskExecutionContext.ParentProject.FullFileName + " HandleId: " + taskExecutionContext.BuildContext.BuildRequest.HandleId + " Time: " + DateTime.Now.ToLongTimeString() + ":" + DateTime.Now.Millisecond);
                        else
                            Console.WriteLine("NodeId: " + NodeId + " Got output update " + taskExecutionContext.ParentProject.FullFileName + " HandleId: None Time: " + DateTime.Now.ToLongTimeString() + ":" + DateTime.Now.Millisecond);
                    }

                    // In inproc scenario we may receive a task done notification for a build context
                    // which has already completed with an exception. In this case we can ignore the
                    // notification because the context is already completed.
                    if (!taskExecutionContext.BuildContext.BuildComplete)
                    {
                        BuildProjectInternal(taskExecutionContext.BuildContext.BuildRequest, taskExecutionContext.BuildContext, taskExecutionContext, false);
                    }
                    else
                    {
                        if (Engine.debugMode)
                        {
                            if (taskExecutionContext.BuildContext.BuildRequest != null)
                                Console.WriteLine("Ignoring task output notification. NodeId: " + NodeId + " Got output update " + taskExecutionContext.ParentProject.FullFileName + " HandleId: " + taskExecutionContext.BuildContext.BuildRequest.HandleId);
                            else
                                Console.WriteLine("Ignoring task output notification. NodeId: " + NodeId + " Got output update " + taskExecutionContext.ParentProject.FullFileName);
                        }
                    }

                    lastLoopActivity = DateTime.Now.Ticks;
                    loopTimeoutRemaining = loopTimeout;
                }
                else if (eventType == 4)
                {
                    // Clear the flush requested event, the logging providers are flushed at the end of the loop
                    flushRequestEvent.Reset();
                    forceFlush = true;
                }
                else
                {
                    ErrorUtilities.VerifyThrow(false, "The event type should be 0, 1, 2 or 3");
                }

                if (NodeManager.TaskExecutionModule == null)
                {
                    // Shutting down, eg due to deadlock. Attempt to flush.
                    forceFlush = true;
                }

                // If necessary flush the queue of logging events (it may have already been flushed recently)
                if (LoggingServices.NeedsFlush(lastLoopActivity) || forceFlush)
                {
                    if (LoggingServices.ProcessPostedLoggingEvents())
                    {
                        lastLoopActivity = DateTime.Now.Ticks;
                        loopTimeoutRemaining = loopTimeout;
                    }
                }

                if (ExternalLoggingServices != null && ( ExternalLoggingServices.NeedsFlush(lastLoopActivity) || forceFlush ))
                {
                    if (ExternalLoggingServices.ProcessPostedLoggingEvents())
                    {
                        lastLoopActivity = DateTime.Now.Ticks;
                        loopTimeoutRemaining = loopTimeout;
                    }
                }

                // Reset the flag forcing the flushing of logging providers
                forceFlush = false;

                // TEM will be null if we're shutting down
                if (NodeManager.TaskExecutionModule != null)
                {
                    if (NodeManager.TaskExecutionModule.UseBreadthFirstTraversal == false /* using depth first traversal */ &&
                        buildRequests.Count == 0 && taskOutputUpdates.Count == 0 &&
                        NodeManager.TaskExecutionModule.IsIdle
                        )
                    {
                        NodeManager.TaskExecutionModule.UseBreadthFirstTraversal = true; /* use breadth first traversal */
                        if (Router.ChildMode)
                        {
                            // Send the status back to the parent as the parent needs to know this node has run out of work, so it can switch the entire system
                            // to breadth first traversal
                            Router.ParentNode.PostStatus(new NodeStatus(true /* use breadth first traversal */), false /* don't block waiting on the send */);
                        }
                        else
                        {
                            // Send the traversal switch directly to all child nodes as the parent has run out of work
                            NodeManager.ChangeNodeTraversalType(true /* use breadth first traversal */);
                        }
                    }
                }
            }

            if (terminatingBuildRequest != null)
            {
                buildResult = terminatingBuildRequest.GetBuildResult();
            }

            return buildResult;
        }

        /// <summary>
        /// Builds the specific targets in an MSBuild project. Since projects can build other projects, this method may get called
        /// back recursively. It keeps track of the projects being built, so that it knows when we've popped back out to the root
        /// of the callstack again, so we can reset the state of all the projects.  Otherwise, you wouldn't be able to do more
        /// than one build using the same Engine object, because the 2nd, 3rd, etc. builds would just say "hmm, looks like this
        /// project has already been built, so I'm not going to build it again".
        /// </summary>
        private void BuildProjectInternal
        (
            BuildRequest buildRequest, 
            ProjectBuildState buildContext,
            TaskExecutionContext taskExecutionContext,
            bool initialCall
        )
        {
            Project project = buildRequest.ProjectToBuild;

            bool exitedDueToError = true;

            try
            {
                SetBuildItemCurrentDirectory(project);
                if (initialCall)
                {
#if (!STANDALONEBUILD)
                    CodeMarkers.Instance.CodeMarker(CodeMarkerEvent.perfMSBuildEngineBuildProjectBegin);
#endif
#if MSBUILDENABLEVSPROFILING 
                    string beginProjectBuild = String.Format(CultureInfo.CurrentCulture, "Build Project {0} Using Old OM - Start", project.FullFileName);
                    DataCollection.CommentMarkProfile(8802, beginProjectBuild);
#endif 

                    // Make sure we were passed in a project object.
                    error.VerifyThrowArgument(project != null, "MissingProject", "Project");

                    // Make sure this project object is associated with this engine object.
                    error.VerifyThrowInvalidOperation(project.ParentEngine == this, "IncorrectObjectAssociation", "Project", "Engine");
                }

                try
                { 
                    if (initialCall)
                    {
                        BuildProjectInternalInitial(buildRequest, project);
                    }
                    else
                    {
                        BuildProjectInternalContinue(buildRequest, buildContext, taskExecutionContext, project);
                    }

                    exitedDueToError = false;
                }
                /**********************************************************************************************************************
                * WARNING: Do NOT add any more catch blocks below! Exceptions should be caught as close to their point of origin as
                * possible, and converted into one of the known exceptions. The code that causes an exception best understands the
                * reason for the exception, and only that code can provide the proper error message. We do NOT want to display
                * messages from unknown exceptions, because those messages are most likely neither localized, nor composed in the
                * canonical form with the correct prefix.
                *********************************************************************************************************************/
                // Handle errors in the project file.
                catch (InvalidProjectFileException e)
                {
                    primaryLoggingServices.LogInvalidProjectFileError(buildRequest.ParentBuildEventContext, e);
                }
                // Handle logger failures -- abort immediately
                catch (LoggerException)
                {
                    // Polite logger failure
                    throw;
                }
                catch (InternalLoggerException)
                {
                    // Logger threw arbitrary exception
                    throw;
                }
                // Handle all other errors.  These errors are completely unexpected, so
                // make sure to give the callstack as well.
                catch (Exception)
                {
                    fatalErrorContext = buildRequest.ParentBuildEventContext;
                    fatalErrorProjectName = project.FullFileName;

                    // Rethrow so that the host can catch it and possibly rethrow it again
                    // so that Watson can give the user the option to send us an error report.
                    throw;
                }

                /**********************************************************************************************************************
                * WARNING: Do NOT add any more catch blocks above!
                *********************************************************************************************************************/
                finally
                {
                    FinishBuildProjectInProgress(buildRequest, buildContext, exitedDueToError);
                }
            }
            finally
            {
                // Flush out all the logging messages, which may have been posted outside target execution
                primaryLoggingServices.ProcessPostedLoggingEvents();

                if (buildRequest != null && buildRequest.BuildCompleted || exitedDueToError)
                {
#if (!STANDALONEBUILD)
                    CodeMarkers.Instance.CodeMarker(CodeMarkerEvent.perfMSBuildEngineBuildProjectEnd);
#endif
#if MSBUILDENABLEVSPROFILING 
                    string endProjectBuild = String.Format(CultureInfo.CurrentCulture, "Build Project {0} Using Old OM - End", project.FullFileName);
                    DataCollection.CommentMarkProfile(8803, endProjectBuild);
#endif 
                }
            }
        }

        /// <summary>
        /// On the initial call to BuildProjectInternal the number of projects in progress in incremented
        /// to indicate a new project build request is in progress.
        /// </summary>
        private void BuildProjectInternalInitial(BuildRequest buildRequest, Project project)
        {
            bool startRootProjectBuild = this.numberOfProjectsInProgress == 0 && !Router.ChildMode;
            /*
            The number of projects in progress is incremented prior to starting the root project build
            so if there is an error the number of projects inprogress is decrement from 1 rather than 0.
            Decrementing from 0 projects in progress causes an exception.
            */
            IncrementProjectsInProgress();

            if (ProfileBuild)
            {
                buildRequest.StartTime = DateTime.Now.Ticks;
                buildRequest.ProcessingStartTime = buildRequest.StartTime;
            }
            
            if (startRootProjectBuild)
            {
                StartRootProjectBuild(buildRequest, project);
            }
            
            project.BuildInternal(buildRequest);
        }

        /// <summary>
        /// Sets the current Directory and PerThreadProjectDirectory to the project.ProjectDirectory
        /// This is done so any BuildItems on this thread have the correct root directory,
        /// so that they can evaluate their built-in metadata correctly while building;
        /// also so that "exists" conditions can evaluate relative paths.
        /// </summary>
        private void SetBuildItemCurrentDirectory(Project project)
        {
            if (!Router.SingleThreadedMode)
            {
                Project.PerThreadProjectDirectory = project.ProjectDirectory;
            }
            else // We are in single thread mode and need to make sure the project directory is the current directory
            {
                if (Directory.GetCurrentDirectory() != project.ProjectDirectory && !string.IsNullOrEmpty(project.ProjectDirectory))
                {
                    Directory.SetCurrentDirectory(project.ProjectDirectory);
                }
            }
        }

        /// <summary>
        /// This method will continue a project build which is in progress
        /// </summary>
        private void BuildProjectInternalContinue(BuildRequest buildRequest, ProjectBuildState buildContext, TaskExecutionContext taskExecutionContext, Project project)
        {
            if (buildRequest != null && ProfileBuild )
            {
                buildRequest.ProcessingStartTime = DateTime.Now.Ticks;
            }

            project.ContinueBuild(buildContext, taskExecutionContext);
        }


        private void IncrementProjectsInProgress()
        {
            Interlocked.Increment(ref this.numberOfProjectsInProgress);
        }

        private void FinishBuildProjectInProgress(BuildRequest buildRequest, ProjectBuildState buildContext, bool exitedDueToError)
        {
            if (buildRequest != null && ProfileBuild)
            {
                buildRequest.ProcessingTotalTime += DateTime.Now.Ticks - buildRequest.ProcessingStartTime;
            }

            if (buildRequest != null && buildRequest.BuildCompleted ||
                buildContext != null && buildContext.BuildComplete )
            {
                DecrementProjectsInProgress();
            }

            if (exitedDueToError)
            {
                SetEngineAbortTo(true);
            }
        }

        internal void DecrementProjectsInProgress()
        {
            ErrorUtilities.VerifyThrow(this.numberOfProjectsInProgress != 0, "Number of Projects in progress should not be 0 before the count is decremented");
            Interlocked.Decrement(ref this.numberOfProjectsInProgress);
            CheckForBuildCompletion();
        }

        private void CheckForBuildCompletion()
        {
            // If the number of projects in progress reaches zero again, we've popped back
            // out of the root of the recursion.
            if (this.numberOfProjectsInProgress == 0 && this.buildRequests.Count == 0)
            {
                // Fire the event that says the overall build is complete.
                if (!Router.ChildMode)
                {
                    SetEngineAbortTo(true);
                }

                if (Engine.debugMode)
                {
                    Console.WriteLine(" Done All projects and my queue is empty, EngineNodeID: " + this.nodeId);
                }
            }
        }

        /// <summary>
        /// Engine.BuildProject gets called recursively when projects use the
        /// MSBuild *task* to build other child projects.  If "numberOfProjectsInProgress"
        /// is 0, then we know we are currently NOT in a recursive call.  We
        /// are really being called at the top level.
        /// </summary>
        private void StartRootProjectBuild(BuildRequest buildRequest, Project project)
        {
            foreach (Project loadedProject in projectsLoadedByHost.Values)
            {
                // There should be no projects in the ProjectManager with the same full path, global properties and tools version
                // as any of the loaded projects.  If there are, something went badly awry, because
                // we were supposed to have deleted them after the last build.
                ErrorUtilities.VerifyThrow(null == this.cacheOfBuildingProjects.GetProject(loadedProject.FullFileName, loadedProject.GlobalProperties, loadedProject.ToolsVersion),
                    "Project shouldn't be in ProjectManager already.");

                // Add the loaded project to the list of projects being built, just
                // so that during the build, we have only one place we need to look
                // instead of having to search multiple lists.
                this.cacheOfBuildingProjects.AddProject(loadedProject);
            }

            if (0 == (buildRequest.BuildSettings & BuildSettings.DoNotResetPreviouslyBuiltTargets))
            {
                // Reset the build state for all projects that are still cached from the 
                // last build and the currently loaded projects that we just added to
                // the ProjectManager.
                this.cacheOfBuildingProjects.ResetBuildStatusForAllProjects();

                // Clear the project build results cache
                this.cacheManager.ClearCache();

                // Reset the build state for the project that we're going to build.  This may not
                // be in the ProjectManager if it doesn't have a FullFileName property.
                project.ResetBuildStatus();
            }
        }

        /// <summary>
        /// Loads a project file from disk, and builds the default targets.
        /// </summary>
        /// <owner>RGoel</owner>
        /// <param name="projectFile"></param>
        /// <returns>true, if build succeeds</returns>
        public bool BuildProjectFile
            (
            string projectFile
            )
        {
            return this.BuildProjectFile(projectFile, null, this.GlobalProperties, null, BuildSettings.None);
        }

        /// <summary>
        /// Loads a project file from disk, and builds the specified target.
        /// </summary>
        /// <owner>RGoel</owner>
        /// <param name="projectFile"></param>
        /// <param name="targetName">Can be null, if caller wants to build the default targets for the project.</param>
        /// <returns>true, if build succeeds</returns>
        public bool BuildProjectFile
            (
            string projectFile,
            string targetName
            )
        {
            return this.BuildProjectFile(projectFile, new string[] {targetName}, this.GlobalProperties,
                null, BuildSettings.None);
        }

        /// <summary>
        /// Loads a project file from disk, and builds the specified list of targets.
        /// </summary>
        /// <owner>RGoel</owner>
        /// <param name="projectFile"></param>
        /// <param name="targetNames">Can be null, if caller wants to build the default targets for the project.</param>
        /// <returns>true, if build succeeds</returns>
        public bool BuildProjectFile
            (
            string projectFile,
            string[] targetNames
            )
        {
            return this.BuildProjectFile(projectFile, targetNames, this.GlobalProperties,
                null, BuildSettings.None);
        }

        /// <summary>
        /// Loads a project file from disk, and builds the specified list of targets.  This overload
        /// takes a set of global properties to use for the build.
        /// </summary>
        /// <owner>RGoel</owner>
        /// <param name="projectFile"></param>
        /// <param name="targetNames">Can be null, if caller wants to build the default targets for the project.</param>
        /// <param name="globalProperties">Can be null if no global properties are needed.</param>
        /// <returns>true, if build succeeds</returns>
        public bool BuildProjectFile
            (
            string projectFile,
            string[] targetNames,
            BuildPropertyGroup globalProperties
            )
        {
            return this.BuildProjectFile(projectFile, targetNames, globalProperties,
                null, BuildSettings.None);
        }

        /// <summary>
        /// Loads a project file from disk, and builds the specified list of targets.  This overload
        /// takes a set of global properties to use for the build and returns the target outputs.
        /// </summary>
        /// <owner>RGoel</owner>
        /// <param name="projectFile"></param>
        /// <param name="targetNames">Can be null, if caller wants to build the default targets for the project.</param>
        /// <param name="globalProperties">Can be null if no global properties are needed.</param>
        /// <param name="targetOutputs">Can be null if outputs are not needed.</param>
        /// <returns>true, if build succeeds</returns>
        public bool BuildProjectFile
            (
            string projectFile,
            string[] targetNames,
            BuildPropertyGroup globalProperties,
            IDictionary targetOutputs
            )
        {
            return this.BuildProjectFile(projectFile, targetNames, globalProperties,
                targetOutputs, BuildSettings.None);
        }

        /// <summary>
        /// Loads a project file from disk, and builds the specified list of targets.  This overload
        /// takes a set of global properties to use for the build, returns the target outputs, and also
        /// allows the caller to specify additional build flags.
        /// </summary>
        /// <remarks>
        /// If this project file is already in our list of in-progress projects, we use the
        /// existing Project object instead of instantiating a new one. Always use this method to 
        /// build projects within projects, otherwise the build won't be optimized.
        /// </remarks>
        /// <param name="projectFile"></param>
        /// <param name="targetNames">Can be null, if caller wants to build the default targets for the project.</param>
        /// <param name="globalProperties">Can be null if no global properties are needed.</param>
        /// <param name="targetOutputs">Can be null if outputs are not needed.</param>
        /// <param name="buildFlags">Specifies additional options to alter the behavior of the build.</param>
        /// <returns>true, if build succeeds</returns>
        public bool BuildProjectFile
        (
            string projectFile,
            string[] targetNames,
            BuildPropertyGroup globalProperties,
            IDictionary targetOutputs,
            BuildSettings buildFlags
        )
        {
            error.VerifyThrowArgumentNull(projectFile, "projectFileName");
            error.VerifyThrowArgument(projectFile.Length > 0, "EmptyProjectFileName");

            return BuildProjectFile(projectFile, targetNames, globalProperties, targetOutputs, buildFlags, null);
        }

        /// <summary>
        /// Loads a project file from disk, and builds the specified list of targets.  This overload
        /// takes a set of global properties to use for the build, returns the target outputs, and also
        /// allows the caller to specify additional build flags.
        /// </summary>
        /// <remarks>
        /// If this project file is already in our list of in-progress projects, we use the
        /// existing Project object instead of instantiating a new one. Always use this method to 
        /// build projects within projects, otherwise the build won't be optimized.
        /// </remarks>
        /// <param name="projectFile"></param>
        /// <param name="targetNames">Can be null, if caller wants to build the default targets for the project.</param>
        /// <param name="globalProperties">Can be null if no global properties are needed.</param>
        /// <param name="targetOutputs">Can be null if outputs are not needed.</param>
        /// <param name="buildFlags">Specifies additional options to alter the behavior of the build.</param>
        /// <param name="toolsVersion">Tools version to impose on the project in this build</param>
        /// <returns>true, if build succeeds</returns>
        public bool BuildProjectFile
            (
            string projectFile,
            string[] targetNames,
            BuildPropertyGroup globalProperties,
            IDictionary targetOutputs,
            BuildSettings buildFlags,
            string toolsVersion
            )
        {
            return PostProjectEvaluationRequests
                (null, new string[] { projectFile }, new string[][] { targetNames },
                 new BuildPropertyGroup[] { globalProperties }, new IDictionary[] { targetOutputs }, buildFlags,
                 new string[] {toolsVersion});
        }

        /// <summary>
        /// Loads a set of project files from disk, and builds the given list of targets for each one. This overload
        /// takes a set of global properties for each project to use for the build, returns the target outputs, 
        /// and also allows the caller to specify additional build flags.
        /// </summary>
        /// <param name="projectFiles">Array of project files to build (can't be null)</param>
        /// <param name="targetNamesPerProject">Array of targets for each project(can't be null)</param>
        /// <param name="globalPropertiesPerProject">Array of properties for each project (can't be null)</param>
        /// <param name="targetOutputsPerProject">Array of tables for target outputs (can't be null)</param>
        /// <param name="buildFlags"></param>
        /// <param name="toolsVersions">Tools version to impose on the project in this build</param>
        /// <returns>True if all given project build successfully</returns>
        public bool BuildProjectFiles
        (
            string[] projectFiles,
            string[][] targetNamesPerProject,
            BuildPropertyGroup[] globalPropertiesPerProject,
            IDictionary[] targetOutputsPerProject,
            BuildSettings buildFlags,
            string [] toolsVersions
        )
        {
            // Verify the arguments to the API
            error.VerifyThrowArgumentArraysSameLength(projectFiles, targetNamesPerProject, "projectFiles", "targetNamesPerProject");
            error.VerifyThrowArgument(projectFiles.Length > 0, "projectFilesEmpty");
            error.VerifyThrowArgumentArraysSameLength(projectFiles, globalPropertiesPerProject, "projectFiles", "globalPropertiesPerProject");
            error.VerifyThrowArgumentArraysSameLength(projectFiles, targetOutputsPerProject, "projectFiles", "targetOutputsPerProject");
            error.VerifyThrowArgumentArraysSameLength(projectFiles, toolsVersions, "projectFiles", "toolsVersions");

            // Verify the entries in the project file array
            for (int i = 0; i < projectFiles.Length; i++)
            {
                error.VerifyThrowArgumentNull(projectFiles[i], "projectFiles[" + i +"]");
                error.VerifyThrowArgument(projectFiles[i].Length > 0, "projectFilesEmptyElement", i);
            }

            return PostProjectEvaluationRequests
                (null, projectFiles, targetNamesPerProject, globalPropertiesPerProject, targetOutputsPerProject, 
                 buildFlags, toolsVersions);
        }

        internal bool PostProjectEvaluationRequests
        (
            Project project,
            string[] projectFiles,
            string[][] targetNames,
            BuildPropertyGroup[] globalPropertiesPerProject,
            IDictionary[] targetOutputsPerProject,
            BuildSettings buildFlags,
            string [] toolVersions
        )
        {
            string currentDirectory = Environment.CurrentDirectory;
            string currentPerThreadProjectDirectory = Project.PerThreadProjectDirectory;
            fatalErrorContext = null;

            BuildEventContext buildEventContext;
               
            // Already have an instantiated project in the OM and it has not fired a project started event for itself yet
            if (project != null && !project.HaveUsedInitialProjectContextId)
            {
                buildEventContext = project.ProjectBuildEventContext;
            }

            else // Dont have an already instantiated project, need to make a new context
            {
                buildEventContext = new BuildEventContext(
                                                this.nodeId,
                                                BuildEventContext.InvalidTargetId,
                                                BuildEventContext.InvalidProjectContextId,
                                                BuildEventContext.InvalidTaskId
                                                );
            }
            
            // Currently, MSBuild requires that the calling thread be marked "STA" -- single
            // threaded apartment.  This is because today we are calling the tasks' Execute()
            // method on this main thread, and there are tasks out there that create unmarshallable
            // COM objects that require the "Apartment" threading model.  Once the engine supports
            // multi-threaded builds, and is spinning up its own threads to call the tasks, then
            // we don't care so much about the STA vs. MTA designation on the main thread.  But for
            // now, we do.
            if (Environment.GetEnvironmentVariable("MSBUILDOLDOM") != "1" && Thread.CurrentThread.GetApartmentState() != ApartmentState.STA)
            {
                // Bug VSWhidbey 126031.  Really, we would like to,
                //
                //   error.VerifyThrowInvalidOperation((Thread.CurrentThread.ApartmentState == ApartmentState.STA),"STARequired");
                //
                // But NUnit-console.exe is not in the STA and so we would get some assert dialogs
                // and build failures.  If we ever upgrade to NUnit 2.1 or later, there is an option
                // to tell NUnit to run in STA mode, and then maybe we can be more strict here.
                primaryLoggingServices.LogWarning(buildEventContext, new BuildEventFileInfo(/* there is truly no file associated with this warning */ String.Empty),
                   "STARequired", Thread.CurrentThread.GetApartmentState());
            }

            BuildRequest[] buildRequests = new BuildRequest[projectFiles.Length];
            Hashtable[] targetOutputsWorkingCopy = new Hashtable[buildRequests.Length];
            for (int i = 0; i < buildRequests.Length; i++)
            {
                // if the caller wants to retrieve target outputs, create a working copy to avoid clobbering 
                // other data in the hashtable
                if (targetOutputsPerProject[i] != null)
                {
                    targetOutputsWorkingCopy[i] = new Hashtable(StringComparer.OrdinalIgnoreCase);
                }

                buildRequests[i] = 
                    CreateLocalBuildRequest(buildEventContext, project, projectFiles[i], targetNames[i],
                                            globalPropertiesPerProject[i], targetOutputsWorkingCopy[i], buildFlags,
                                            toolVersions[i]);
            }

            bool overallResult = true;
            bool exitedDueToError = true;
            try
            {
                // Reset the current directory stored in the per thread variable for the current thread
                Project.PerThreadProjectDirectory = null;
                // Initialize the scheduler with the current information
                Scheduler.Initialize(NodeManager.GetNodeDescriptions());
                // Fire the event that says the overall build is beginning.
                LoggingServices.LogBuildStarted();
                LoggingServices.ProcessPostedLoggingEvents();
                // Post all the build requests into the engine queue
                PostBuildRequests(buildRequests);
                // Trigger the actual build (this call will not return until build is complete)
                EngineBuildLoop(null);

                for (int i = 0; i < buildRequests.Length; i++)
                {
                    // Collect the outputs
                    BuildResult buildResult = buildRequests[i].GetBuildResult();
                    if (buildResult.OutputsByTarget != null && targetOutputsPerProject[i] != null)
                    {
                        buildResult.ConvertToTaskItems();
                        foreach (DictionaryEntry de in buildResult.OutputsByTarget)
                        {
                            targetOutputsPerProject[i][de.Key] = de.Value;
                        }
                    }
                    overallResult = overallResult && buildRequests[i].BuildSucceeded;
                }

                exitedDueToError = false;
            }
            catch (Exception e)
            {
                // Only log the error once at the root of the recursion for unhandled exceptions, instead of
                // logging the error multiple times for each level of project-to-project
                // recursion until we get to the top of the stack.
                if (fatalErrorContext != null)
                {
                    primaryLoggingServices.LogFatalBuildError(fatalErrorContext, e, new BuildEventFileInfo(fatalErrorProjectName));
                }
                else
                {
                    primaryLoggingServices.LogFatalBuildError(buildEventContext, e, new BuildEventFileInfo(String.Empty));
                }

                // Rethrow so that the host can catch it and possibly rethrow it again
                // so that Watson can give the user the option to send us an error report.
                throw;
            }
            finally
            {
                // Post build finished event if the finally is not being executed due to an exception
                EndingEngineExecution(overallResult, exitedDueToError);
                // Reset the current directory to the value before this 
                // project built
                Environment.CurrentDirectory = currentDirectory;
                // We reset the path back to the original value in case the 
                // host is depending on the current directory to find projects
                Project.PerThreadProjectDirectory = currentPerThreadProjectDirectory;
            }

            return overallResult;
        }

        /// <summary>
        /// Create a build request which will be posted to the local engine queue, having a HandleId of -1 meaning it came from the local 
        /// engine rather than an engine call back 
        /// </summary>
        /// <returns></returns>
        private BuildRequest CreateLocalBuildRequest(BuildEventContext buildEventContext, Project project, string projectFile, string[] targetNames, BuildPropertyGroup globalProperties, IDictionary targetOutputs, BuildSettings buildFlags, string toolsVersion)
        {
            // Global Properties should not be null as this will cause a crash when we try and cache the build result of the project.
            // Whidby also set global properties to empty if they were null.
            if (globalProperties == null)
            {
                globalProperties = new BuildPropertyGroup();
            }

            BuildRequest buildRequest =  new BuildRequest(EngineCallback.invalidEngineHandle, projectFile, targetNames, globalProperties, toolsVersion, -1, true, false);
            buildRequest.ParentBuildEventContext = buildEventContext;
            // Set the project object to the passed in project
            buildRequest.ProjectToBuild = project;
            // Set the request build flags
            buildRequest.BuildSettings = buildFlags;
            // Set the boolean requesting the project start/finish events 
            buildRequest.FireProjectStartedFinishedEvents = true;
            // Set the dictionary to return target outputs in, if any
            buildRequest.OutputsByTarget = targetOutputs;
            // If the tools version is null and we have a project object then use the project's tools version
            // If we do not have a project and we have a null tools version then the build request's tools version will be null, which will later be set in BuildProjectFileInternal
            if (String.IsNullOrEmpty(toolsVersion) && project != null)
            {
                buildRequest.ToolsetVersion = project.ToolsVersion;
            }
            // Set project filename correctly if only the project object is passed in
            if (buildRequest.ProjectFileName == null)
            {
                buildRequest.ProjectFileName = project.FullFileName;
            }
            return buildRequest;
        }

        /// <summary>
        /// Loads a project file from disk, and builds the specified list of targets.  This overload
        /// takes a set of global properties to use for the build, returns the target outputs, and also
        /// allows the caller to specify additional build flags.
        /// </summary>
        /// <remarks>
        /// If this project file is already in our list of in-progress projects, we use the
        /// existing Project object instead of instantiating a new one. Always use this method to 
        /// build projects within projects, otherwise the build won't be optimized.
        /// </remarks>
        internal void BuildProjectFileInternal
            (
                BuildRequest buildRequest
            )
        {
            string projectFile = buildRequest.ProjectFileName;

            error.VerifyThrowArgumentNull(projectFile, "projectFileName");
            error.VerifyThrowArgument(projectFile.Length > 0, "EmptyProjectFileName");
            // When we get to this point the global properties should not be null, as they are used to determine if a project has already been loaded / built before.
            error.VerifyThrow(buildRequest.GlobalProperties != null, "Global Properties should not be null");

            // Convert the project filename to a fully qualified path.
            FileInfo projectFileInfo = new FileInfo(projectFile);

            // If the project file doesn't actually exist on disk, it's a failure.
            ErrorUtilities.VerifyThrowArgument(projectFileInfo.Exists, "ProjectFileNotFound", projectFile);

            try
            {
                ArrayList actuallyBuiltTargets;

                // If the tools version is empty take a quick peek at the project file to determine if it has a tools version defined
                if(String.IsNullOrEmpty(buildRequest.ToolsetVersion))
                {
                    buildRequest.ToolsetVersion = XmlUtilities.GetAttributeValueForElementFromFile(buildRequest.ProjectFileName, XMakeAttributes.project, XMakeAttributes.toolsVersion);
                    buildRequest.ToolsVersionPeekedFromProjectFile = true;
                }

                // Check if there is a cached result available for this build
                BuildResult cachedResult = cacheManager.GetCachedBuildResult(buildRequest, out actuallyBuiltTargets);
                if (cachedResult != null)
                {
                    // Notify the scheduler of the dependecy and indicate that it will be evaluated (aka retrieved from the cache locally)
                    Scheduler.NotifyOfSchedulingDecision(buildRequest, this.nodeId);
                    ProcessCachedResult(buildRequest, projectFileInfo, actuallyBuiltTargets, cachedResult);
                }
                else
                {
                    // There's no cached result: we have to build it. Figure out which node to build it on.
                    Project matchingProjectCurrentlyLoaded = null;
                    Project projectCurrentlyLoaded = null;

                    // See if we have a project loaded by the host already that matches the full path, in the
                    // list of projects which were loaded at the beginning of the build.
                    projectCurrentlyLoaded = (Project)this.projectsLoadedByHost[projectFileInfo.FullName];

                    if (projectCurrentlyLoaded != null)
                    {
                        // See if the global properties and tools version match.
                        if (projectCurrentlyLoaded.IsEquivalentToProject
                                    (
                                    projectCurrentlyLoaded.FullFileName,
                                    buildRequest.GlobalProperties,
                                    buildRequest.ToolsetVersion
                                    )
                            )
                        {
                            // If so, use it.
                            matchingProjectCurrentlyLoaded = projectCurrentlyLoaded;
                        }
                    }

                    // Decide to build the project on either the current node or remote node
                    string toolsVersionToUse = buildRequest.ToolsetVersion == null ? DefaultToolsVersion : buildRequest.ToolsetVersion;

                    // If a matching project is currently loaded, we will build locally.
                    bool isLocal = (matchingProjectCurrentlyLoaded != null);

                    // If not, we need to search our cache of building projects to see if we have built this project
                    // locally already.
                    if (!isLocal)
                    {
                        // Determine if the project was previously loaded, but is now unloaded.
                        bool projectWasPreviouslyLoaded = this.cacheOfBuildingProjects.HasProjectBeenLoaded(projectFileInfo.FullName, buildRequest.GlobalProperties, toolsVersionToUse);

                        // We do this check because we need to know if the project is already building on this node.
                        // Unlike the check of projectsLoadedByHost, this will also find projects which were added
                        // after the start of the build, such as MSBuild task-generated build requests.
                        bool projectIsLoaded = this.cacheOfBuildingProjects.GetProject(projectFileInfo.FullName, buildRequest.GlobalProperties, toolsVersionToUse) != null;

                        isLocal = projectWasPreviouslyLoaded || projectIsLoaded;
                    }

                    int nodeIndex = EngineCallback.inProcNode;

                    // If the project, properties and tools version is not known locally, it is either being services by a remote node
                    // or we need to let the scheduler pick a node for it to be serviced by using its algorithm.
                    if (!isLocal)
                    {
                        nodeIndex = cacheOfBuildingProjects.GetRemoteProject(projectFileInfo.FullName, buildRequest.GlobalProperties, toolsVersionToUse);
                    }

                    int evaluationNode = Scheduler.CalculateNodeForBuildRequest(buildRequest, nodeIndex);
                    if (matchingProjectCurrentlyLoaded == null && evaluationNode == EngineCallback.inProcNode)
                    {
                        // We haven't already got this project loaded in this Engine, or it was previously unloaded from this Engine,
                        // and we've been scheduled to build it on this node. So create a new project if necessary.
                        // If we peeked at the project file then we need to make sure that if the tools version in the project is not marked as an override then
                        // the project's tools version is the same. If they are the same then override should be false.
                        try
                        {
                            matchingProjectCurrentlyLoaded = GetMatchingProject(projectCurrentlyLoaded,
                                projectFileInfo.FullName, buildRequest.GlobalProperties,
                                buildRequest.ToolsetVersion, buildRequest.TargetNames, buildRequest.ParentBuildEventContext, buildRequest.ToolsVersionPeekedFromProjectFile);
                        }
                        catch (InvalidProjectFileException e)
                        {
                            primaryLoggingServices.LogInvalidProjectFileError(buildRequest.ParentBuildEventContext, e);
                            throw;
                        }
                    }

                    if (evaluationNode != EngineCallback.inProcNode)
                    {
                        // The project will be evaluated remotely so add a record 
                        // indicating where this project is being evaluated
                        if (evaluationNode != EngineCallback.parentNode)
                        {
                            cacheOfBuildingProjects.AddRemoteProject(projectFileInfo.FullName, buildRequest.GlobalProperties, toolsVersionToUse, evaluationNode);
                        }
                    }

                    if (Engine.debugMode)
                    {
                        Console.WriteLine("###Missing cached result for " + buildRequest.GetTargetNamesList() + " in " +
                                           buildRequest.ProjectFileName + " - building");
                    }

                    if (evaluationNode == EngineCallback.inProcNode)
                    {
                        ErrorUtilities.VerifyThrow(cacheOfBuildingProjects.GetRemoteProject(projectFileInfo.FullName, buildRequest.GlobalProperties, toolsVersionToUse) == EngineCallback.invalidNode,
                                                   "Should not build remote projects");
                        buildRequest.ProjectToBuild = matchingProjectCurrentlyLoaded;
                        this.BuildProjectInternal(buildRequest, null, null, true);
                    }
                    else
                    {
                        // Increment number of projects in progress 
                        if (!buildRequest.IsGeneratedRequest)
                        {
                            IncrementProjectsInProgress();
                        }
                       Router.PostBuildRequest(buildRequest, evaluationNode);
                    }
                }
            }
            catch (InvalidProjectFileException)
            {
                // eat the exception because it has already been logged
                HandleProjectFileInternalException(buildRequest);
            }
        }

        private void HandleProjectFileInternalException(BuildRequest buildRequest)
        {
            // Flush out all the logging messages, which may have been posted outside target execution
            primaryLoggingServices.ProcessPostedLoggingEvents();
            
            // Mark evaluation as complete
            buildRequest.BuildCompleted = true;

            if (buildRequest.HandleId != EngineCallback.invalidEngineHandle)
            {
                Router.PostDoneNotice(buildRequest);
            }

            CheckForBuildCompletion();
        }

        /// <summary>
        /// Pretend we're actually building a project when really we're just retrieving the results from the cache.
        /// </summary>
        /// <param name="buildRequest"></param>
        /// <param name="projectFileInfo"></param>
        /// <param name="actuallyBuiltTargets"></param>
        /// <param name="cachedResult"></param>
        private void ProcessCachedResult
        (
            BuildRequest buildRequest, 
            FileInfo projectFileInfo, 
            ArrayList actuallyBuiltTargets, 
            BuildResult cachedResult
        )
        {
            buildRequest.InitializeFromCachedResult(cachedResult);

            if (Engine.debugMode)
            {
                Console.WriteLine("===Reusing cached result for " + buildRequest.GetTargetNamesList() + " in " +
                                   buildRequest.ProjectFileName + " result is " + buildRequest.BuildSucceeded + " EngineNodeID: " + this.nodeId);
            }

            BuildEventContext requestContext = buildRequest.ParentBuildEventContext;
            BuildEventContext currentContext = new BuildEventContext(this.nodeId, BuildEventContext.InvalidTargetId, GetNextProjectId(), BuildEventContext.InvalidTaskId);

            primaryLoggingServices.LogProjectStarted(cachedResult.ProjectId, requestContext, currentContext, projectFileInfo.FullName,
                buildRequest.GetTargetNamesList(),
                new BuildPropertyGroupProxy(new BuildPropertyGroup()),
                new BuildItemGroupProxy(new BuildItemGroup()));
            primaryLoggingServices.LogComment(currentContext, MessageImportance.Low, "ToolsVersionInEffectForBuild", buildRequest.ToolsetVersion);

            for (int i = 0; i < actuallyBuiltTargets.Count; i++)
            {
                string builtTargetName = EscapingUtilities.UnescapeAll((string)actuallyBuiltTargets[i]);
                Target.BuildState buildState = (Target.BuildState)cachedResult.ResultByTarget[builtTargetName];
                buildRequest.ResultByTarget[builtTargetName] = buildState;

                primaryLoggingServices.LogComment(currentContext,
                    ((buildState == Target.BuildState.CompletedSuccessfully) ? "TargetAlreadyCompleteSuccess" : "TargetAlreadyCompleteFailure"),
                    builtTargetName);

                if (buildState == Target.BuildState.CompletedUnsuccessfully)
                {
                    break;
                }
            }

            primaryLoggingServices.LogProjectFinished(currentContext, projectFileInfo.FullName, cachedResult.EvaluationResult);

            if (!buildRequest.IsGeneratedRequest)
            {
                CheckForBuildCompletion();
            }
            else
            {
                Router.PostDoneNotice(buildRequest);
            }
        }

        /// <summary>
        /// Returns a project object that matches the full path and global properties passed in.
        /// First, it checks our cache of building projects to see if such a project already exists.
        /// If so, we reuse that.  Otherwise, we create a new Project object with the specified
        /// full path and global properties.  The "existingProject" parameter passed in is just
        /// so we can reuse the Xml if there's already a project available with the same full path.
        /// </summary>
        /// <param name="existingProject"></param>
        /// <param name="projectFullPath"></param>
        /// <param name="globalPropertiesToUse"></param>
        /// <param name="buildEventContext"></param>
        internal Project GetMatchingProject
            (
            Project existingProject,
            string projectFullPath,
            BuildPropertyGroup globalPropertiesToUse,
            string toolsVersion,
            string [] targetNames,
            BuildEventContext buildEventContext,
            bool toolsVersionPeekedFromProjectFile
            )
        {
            // See if we already have a project with the exact same full path and global properties
            // that the caller is requesting us to build.  If so, use that.
            Project returnProject = this.cacheOfBuildingProjects.GetProject(projectFullPath, globalPropertiesToUse, toolsVersion);

            // If this project was not found in our list, create a new project,
            // and load the contents from the project file.
            if (returnProject == null)
            {
                #if DEBUG
                if (!String.IsNullOrEmpty(Environment.GetEnvironmentVariable("MSBUILDASSERTONLOADMULTIPLECOPIESOFPROJECT")))
                {
                    if (this.projectsLoadedByHost.Contains(projectFullPath))
                    {
                        // We're about to load a second copy of a project that is already loaded in the IDE.
                        // This assert can fire when different projects in the IDE have different sets of
                        // global properties.  For example, suppose there are two projects loaded in the IDE,
                        // ProjectA and ProjectB.  Suppose the project system has given ProjectA two global
                        // properties (e.g., Configuration=Debug and Platform=AnyCPU), and has given ProjectB
                        // three global properties (e.g., Configuration=Foobar, Platform=x86, and DevEnvDir=c:\vs).
                        // Now, if ProjectB has a P2P reference to ProjectA, we've got a problem because when
                        // ProjectB calls the <MSBuild> task to grab the output of ProjectA, the engine is going
                        // to try and merge together the global properties, and the merged set will consist
                        // of all three properties.  Since we won't have a copy of ProjectA in our projectsLoadedByHost
                        // list that has all three of the same global properties, we'll decide we have to create
                        // a new Project object, and this is a big unnecessary perf hit (as well as being incorrect).
                        // If a user customized his build process and is explicitly passing in Properties to the
                        // <MSBuild> task, then we would be entering this codepath for a totally legitimate
                        // scenario, so we don't want to disallow it.  We just want to know about it if it happens
                        // to anyone before we ship, just so we can investigate to see if there may be a bug 
                        // somewhere.
                        if (this.projectsLoadedByHost.Count > 1)
                        {
                            // The assert condition (projectsLoadedByHost.Count == 1) is there because
                            // for command-line builds using msbuild.exe, the # of projects loaded by the host will
                            // always be exactly 1.  We don't want to assert for command-line builds, because then
                            // we'd be firing this all the time for perfectly legitimate scenarios.
                            // We also don't want to assert in razzle, because the razzle build is expected to do this
                            // to accomplish traversal.
                            Debug.Assert(!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("_NTROOT")),
                                "This assertion is here to catch potential bugs wrt MSBuild and VS integration.  " +
                                "It is okay to ignore this assert only if you are in the IDE and have customized " +
                                "your build process such that you are passing the Properties parameter into the " +
                                "<MSBuild> task in way that causes the same project to be built with different " +
                                "sets of global properties.  However, if you are just using a standard VS-generated project, " +
                                "this assert should not fire, so please open a bug under the \\VSCORE\\MSBuild\\VSIntegration " +
                                "path.");
                        }
                    }
                }
                #endif

                // Check if the project has been previously unloaded due to a user request during the current build
                // In this case reloaded a project is an error because we can't ensure a consistent state of the reloaded project
                // and the cached resulted of the original
                string toolsVersionToUse = toolsVersion == null ? DefaultToolsVersion : toolsVersion;
                if (this.cacheOfBuildingProjects.HasProjectBeenLoaded(projectFullPath, globalPropertiesToUse, toolsVersionToUse))
                {
                    string joinedNames = ResourceUtilities.FormatResourceString("DefaultTargets");
                    if (targetNames != null && targetNames.Length > 0)
                    {
                        joinedNames = EscapingUtilities.UnescapeAll(String.Join(";", targetNames));
                    }
                    BuildEventFileInfo fileInfo = new BuildEventFileInfo(projectFullPath);
                    string errorCode;
                    string helpKeyword;
                    string message = ResourceUtilities.FormatResourceString(out errorCode, out helpKeyword, "ReloadingPreviouslyUnloadedProject", projectFullPath, joinedNames);
                    throw new InvalidProjectFileException(projectFullPath, fileInfo.Line, fileInfo.Column, fileInfo.EndLine, fileInfo.EndColumn, message, null, errorCode, helpKeyword);
                }
                // This creates a new project.
                try
                {
                    // If the tools version was peeked from the project file then we reset the tools version to null as the tools version is read by the project object again
                    // when setting or getting the ToolsVersion property. If the tools version was not peeked from the project file than it is an override.
                    if (toolsVersionPeekedFromProjectFile)
                    {
                        toolsVersion = null;
                    }
                    returnProject = new Project(this, toolsVersion);
                }
                catch (InvalidOperationException)
                {
                    BuildEventFileInfo fileInfo = new BuildEventFileInfo(projectFullPath);
                    string errorCode;
                    string helpKeyword;
                    string message = ResourceUtilities.FormatResourceString(out errorCode, out helpKeyword, "UnrecognizedToolsVersion", toolsVersion);
                    throw new InvalidProjectFileException(projectFullPath,fileInfo.Line,fileInfo.Column,fileInfo.EndLine, fileInfo.EndColumn,message, null, errorCode,helpKeyword);
                }

                // We're only building this project ... it is not loaded by a host, and we
                // should feel free to discard this whenever we like.
                returnProject.IsLoadedByHost = false;

                // Give it the global properties that were requested.
                returnProject.GlobalProperties = globalPropertiesToUse;

                // Load the project file.  If we don't already have an XmlDocument for this
                // project file, load it off disk.  Otherwise, use one of the XmlDocuments
                // that we already have.  Two advantages:  1.) perf  2.) using the in-memory
                // contents that the host may have altered.
                if (existingProject != null)
                {
                    //Console.WriteLine("Reusing an existing project: " + projectFullPath);
                    returnProject.FullFileName = projectFullPath;
                    returnProject.LoadFromXmlDocument(existingProject.XmlDocument, buildEventContext, existingProject.LoadSettings);
                }
                else
                {
                    //Console.WriteLine("Found new project: " + projectFullPath);
                    returnProject.Load(projectFullPath, buildEventContext, ProjectLoadSettings.None);
                }

                // Add the newly created Project object to the ProjectManager.
                this.cacheOfBuildingProjects.AddProject(returnProject);
            }

            return returnProject;
        }
        
        /// <summary>
        /// When using the MSBuild task to build a child project, we need to figure out the set of 
        /// global properties that the child should be built with.  It is a merge of whatever
        /// properties the parent project was being built with, plus whatever properties were
        /// actually passed into the MSBuild task (in the "Properties" parameter).  However,
        /// the slightly wrinkle is the child project may have actually been one that is 
        /// currently loaded in the IDE, and the IDE controls what Configuration/Platform each
        /// project should be built with, so we have to honor that too.  So, the order in which
        /// we look at global properties are:
        /// 
        ///     1.  Whatever global properties the parent project was building with.  (The parent
        ///         project is the one that called the &lt;MSBuild&lt; task.
        ///     2.  If the child project was already previously loaded by the host, whatever global 
        ///         properties were sent into the child project by the host (via Project.GlobalProperties).
        ///     3.  Whatever properties were passed into the "Properties" parameter of the &lt;MSBuild&lt;
        ///         task.
        /// 
        /// </summary>
        /// <param name="parentProjectGlobalProperties"></param>
        /// <param name="childProjectFile"></param>
        /// <param name="globalPropertiesPassedIntoTask"></param>
        /// <owner>RGoel</owner>
        /// <returns>merged PropertyGroup</returns>
        internal BuildPropertyGroup MergeGlobalProperties
            (
            BuildPropertyGroup parentProjectGlobalProperties,
            BuildPropertyGroup postMergeProperties,
            string childProjectFile,
            IDictionary globalPropertiesPassedIntoTask
            )
        {
            if (childProjectFile != null)
            {
                // The childProject can be null (if user wants us to just use the calling project as the
                // callee project).  But if it's not null, it really shouldn't be blank, and it should
                // exist on disk.  If it doesn't we can't get its full path.
                error.VerifyThrowArgument(childProjectFile.Length > 0, "EmptyProjectFileName");

                // If the project file doesn't actually exist on disk, it's a failure.
                ErrorUtilities.VerifyThrowArgument(File.Exists(childProjectFile), "ProjectFileNotFound", childProjectFile);
            }
            
            // Create a new BuildPropertyGroup to represent the final set of global properties that we're going to
            // use for the child project.
            BuildPropertyGroup finalGlobalProperties = new BuildPropertyGroup();
            
            // Start with the global properties from the parent project.
            if (postMergeProperties == null)
            {
                finalGlobalProperties.ImportProperties(parentProjectGlobalProperties);
            }
            else
            {
                finalGlobalProperties.ImportProperties(postMergeProperties);
            }
            
            // childProjectFile could be null when no Projects were passed into the MSBuild task, which
            // means parentProject == childProject, which means no need to import the same properties again.
            if (childProjectFile != null)
            {
                // Get the full path of the child project file.
                string childProjectFullPath = Path.GetFullPath(childProjectFile);

                // Find out if there's a project already loaded with the same full path.
                Project loadedProjectWithSameFullPath = (Project) this.projectsLoadedByHost[childProjectFullPath];

                // Then ... if there is a loaded project with the same full path, merge in its global properties.
                // This way, we honor whatever settings the IDE has requested for this project (e.g. Configuration=Release, or whatever).
                if (loadedProjectWithSameFullPath != null)
                {
                    finalGlobalProperties.ImportProperties(loadedProjectWithSameFullPath.GlobalProperties);
                }
            }
            
            // Finally, whatever global properties were passed into the task ... those are the final winners.
            if (globalPropertiesPassedIntoTask != null)
            {
                foreach (DictionaryEntry newGlobalProperty in globalPropertiesPassedIntoTask)
                {
                    finalGlobalProperties.SetProperty((string) newGlobalProperty.Key, 
                        (string) newGlobalProperty.Value);
                }
            }

            return finalGlobalProperties;
        }

        internal void PostBuildRequests(BuildRequest[] buildRequestArray)
        {
            buildRequests.EnqueueArray(buildRequestArray);
        }

        internal void PostBuildRequest(BuildRequest buildRequest)
        {
            buildRequests.Enqueue(buildRequest);
        }

        internal void PostTaskOutputUpdates(TaskExecutionContext executionContext)
        {
            taskOutputUpdates.Enqueue(executionContext);
        }

        internal void PostEngineCommand(EngineCommand engineCommand)
        {
            engineCommands.Enqueue(engineCommand);
        }

        internal TaskExecutionContext GetTaskOutputUpdates()
        {
            TaskExecutionContext taskExecutionContext = taskOutputUpdates.Dequeue();

            if (taskExecutionContext != null)
            {
                // Clear the node proxy state, all the write to the proxy state should come from the engine thread
                EngineCallback.ClearContextState(taskExecutionContext.HandleId);
            }

            return taskExecutionContext;
        }

        /// <summary>
        /// This function collects status about the inprogress targets and engine operations. 
        /// This function should always run from the engine domain because it touch engine data
        /// structures.
        /// </summary>
        internal NodeStatus RequestStatus(int requestId)
        {
            // Find out the list of the inprogress waiting targets
            List<BuildRequest []> outstandingRequests = new List<BuildRequest []>();
            int [] handleIds = NodeManager.TaskExecutionModule.GetWaitingTaskData(outstandingRequests);
            Target [] waitingTargets = EngineCallback.GetListOfTargets(handleIds);

            // Find out the list of targets waiting due to dependency or onerror call but not actively in progress
            List<Project> inProgressProject = cacheOfBuildingProjects.GetInProgressProjects();
            List<Target> inProgressTargets = new List<Target>();
            foreach (Project project in inProgressProject)
            {
                foreach (Target target in project.Targets)
                {
                    if (target.ExecutionState != null && target.ExecutionState.BuildingRequiredTargets)
                    {
                        inProgressTargets.Add(target);
                    }
                }
            }
            TargetInProgessState[] stateOfInProgressTargets = 
                    new TargetInProgessState[waitingTargets.Length + inProgressTargets.Count];
            for (int i = 0; i < waitingTargets.Length; i++)
            {
                stateOfInProgressTargets[i] = null;
                // Skip if the in progress task has already completed (the task is running in the TEM domain)
                if (waitingTargets[i] != null)
                {
                    TargetExecutionWrapper executionState = waitingTargets[i].ExecutionState;
                    // Skip the target if it has already completed
                    if (executionState != null)
                    {
                        stateOfInProgressTargets[i] =
                            new TargetInProgessState(EngineCallback, waitingTargets[i], executionState.GetWaitingBuildContexts(),
                                                     executionState.InitiatingBuildContext,
                                                     outstandingRequests[i], waitingTargets[i].ParentProject.FullFileName);
                    }
                }
            }
            for (int i = 0; i < inProgressTargets.Count; i++)
            {
                TargetExecutionWrapper executionState = inProgressTargets[i].ExecutionState;
                ErrorUtilities.VerifyThrow(executionState != null,
                                           "Engine thread is blocked so target state should not change");

                stateOfInProgressTargets[waitingTargets.Length + i] =
                    new TargetInProgessState(EngineCallback, inProgressTargets[i], executionState.GetWaitingBuildContexts(),
                                             executionState.InitiatingBuildContext,
                                             null, inProgressTargets[i].ParentProject.FullFileName);
            }

            NodeStatus nodeStatus = new NodeStatus(requestId, true, buildRequests.Count + taskOutputUpdates.Count,
                                                   NodeManager.TaskExecutionModule.LastTaskActivity(),
                                                   lastLoopActivity, false);

            nodeStatus.StateOfInProgressTargets = stateOfInProgressTargets;

            return nodeStatus;
        }

        internal void PostNodeStatus(int postingNodeId, NodeStatus nodeStatus)
        {
            if (nodeStatus.RequestId != NodeStatus.UnrequestedStatus)
            {
                nodeManager.PostNodeStatus(postingNodeId, nodeStatus);
            }
            else if (nodeStatus.UnhandledException != null)
            {
                PostEngineCommand(new ReportExceptionEngineCommand(nodeStatus.UnhandledException));
            }
            else
            {
                if (!Router.ChildMode)
                {
                    // If we are changing to breadth first traversal it means we are out of work. Traversal type is false when depth first is requested
                    if (nodeStatus.TraversalType)
                    {
                        Scheduler.NotifyOfBlockedNode(postingNodeId);
                    }
                    else if (Engine.debugMode && !nodeStatus.TraversalType)
                    {
                        Console.WriteLine("Switch to Depth first traversal is requested by " + postingNodeId);
                    }
                }

                PostEngineCommand(new ChangeTraversalTypeCommand(nodeStatus.TraversalType, false));
            }
        }

        #endregion

        /// <summary>
        /// Reset the cache of loaded projects and all other per build data
        /// </summary>
        internal void ResetPerBuildDataStructures()
        {
            // Reset the build state for all projects that are still cached from the 
            // last build and the currently loaded projects that we just added to
            // the ProjectManager.
            this.cacheOfBuildingProjects.ResetBuildStatusForAllProjects();
            // Clear all the cached results
            this.CacheManager.ClearCache();
        }
    }
}
