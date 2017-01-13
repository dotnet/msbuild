// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//-----------------------------------------------------------------------
// </copyright>
// <summary>Class holding the parameters and settings which are global to the build.</summary>
//-----------------------------------------------------------------------

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Globalization;

using Microsoft.Build.BackEnd;
using Microsoft.Build.Collections;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Framework;
using Microsoft.Build.Internal;
using Microsoft.Build.Shared;

using ReservedPropertyNames = Microsoft.Build.Internal.ReservedPropertyNames;
using LoggerDescription = Microsoft.Build.Logging.LoggerDescription;
using ForwardingLoggerRecord = Microsoft.Build.Logging.ForwardingLoggerRecord;

namespace Microsoft.Build.Execution
{
    using System.Diagnostics;
    using Utilities = Microsoft.Build.Internal.Utilities;

    /// <summary>
    /// This class represents all of the settings which must be specified to start a build.
    /// </summary>
    public class BuildParameters : INodePacketTranslatable
    {
        /// <summary>
        /// The default thread stack size for threads owned by MSBuild.
        /// </summary>
        private const int DefaultThreadStackSize = 262144; // 256k

        /// <summary>
        /// The timeout for endpoints to shut down.
        /// </summary>
        private const int DefaultEndpointShutdownTimeout = 30 * 1000; // 30 seconds

        /// <summary>
        /// The timeout for the engine to shutdown.
        /// </summary>
        private const int DefaultEngineShutdownTimeout = Timeout.Infinite;

        /// <summary>
        /// The shutdown timeout for the logging thread.
        /// </summary>
        private const int DefaultLoggingThreadShutdownTimeout = 30 * 1000; // 30 seconds

        /// <summary>
        /// The shutdown timeout for the request builder.
        /// </summary>
        private const int DefaultRequestBuilderShutdownTimeout = Timeout.Infinite;

        /// <summary>
        /// The maximum number of idle request builders to retain before we start discarding them.
        /// </summary>
        private const int DefaultIdleRequestBuilderLimit = 2;

        /// <summary>
        /// The startup directory.
        /// </summary>
        private static string s_startupDirectory = NativeMethodsShared.GetCurrentDirectory();

        /// <summary>
        /// Indicates whether we should warn when a property is uninitialized when it is used.
        /// </summary>
        private static bool? s_warnOnUninitializedProperty = null;

        /// <summary>
        /// Indicates if we should dump string interning stats.
        /// </summary>
        private static bool? s_dumpOpportunisticInternStats = null;

        /// <summary>
        /// Indicates if we should debug the expander.
        /// </summary>
        private static bool? s_debugExpansion = null;

        /// <summary>
        /// Indicates if we should keep duplicate target outputs.
        /// </summary>
        private static bool? s_keepDuplicateOutputs = null;

        /// <summary>
        /// Indicates if we should enable the build plan
        /// </summary>
        private static bool? s_enableBuildPlan = null;

        /// <summary>
        /// The maximum number of idle request builders we will retain.
        /// </summary>
        private static int? s_idleRequestBuilderLimit = null;

        /// <summary>
        /// Location that msbuild.exe was last successfully found at.
        /// </summary>
        private static string s_msbuildExeKnownToExistAt;

        /// <summary>
        /// The build id
        /// </summary>
        private int _buildId = 0;

#if FEATURE_THREAD_PRIORITY
        /// <summary>
        /// The thread priority with which to run the in-proc node.
        /// </summary>
        private ThreadPriority _buildThreadPriority = ThreadPriority.Normal;
#endif

        /// <summary>
        /// The culture
        /// </summary>
        private CultureInfo _culture = CultureInfo.CurrentCulture;

        /// <summary>
        /// The default tools version.
        /// </summary>
        private string _defaultToolsVersion = "2.0";

        /// <summary>
        /// When true, causes the build to emit a summary of project build information
        /// </summary>
        private bool _detailedSummary = false;

        /// <summary>
        /// Flag indicating whether node reuse should be enabled.
        /// By default, it is enabled.
        /// </summary>
#if FEATURE_NODE_REUSE
        private bool _enableNodeReuse = true;
#else
        private bool _enableNodeReuse = false;
#endif

        /// <summary>
        /// The original process environment.
        /// </summary>
        private Dictionary<string, string> _buildProcessEnvironment;

        /// <summary>
        /// The environment properties for the build.
        /// </summary>
        private PropertyDictionary<ProjectPropertyInstance> _environmentProperties = new PropertyDictionary<ProjectPropertyInstance>();

        /// <summary>
        /// The forwarding logger records.
        /// </summary>
        private IEnumerable<ForwardingLoggerRecord> _forwardingLoggers = null;

        /// <summary>
        /// The build-global properties.
        /// </summary>
        private PropertyDictionary<ProjectPropertyInstance> _globalProperties = new PropertyDictionary<ProjectPropertyInstance>();

        /// <summary>
        /// The host services object.
        /// </summary>
        private HostServices _hostServices = null;

        /// <summary>
        /// The loggers.
        /// </summary>
        private IEnumerable<ILogger> _loggers = null;

        /// <summary>
        /// The maximum number of nodes to use.
        /// </summary>
        private int _maxNodeCount = 1;

        /// <summary>
        /// The maximum amount of memory to use.
        /// </summary>
        private int _memoryUseLimit = 0; // Unlimited

        /// <summary>
        /// The location of the node exe.  This is the full path including the exe file itself.
        /// </summary>
        private string _nodeExeLocation = null;

        /// <summary>
        /// The node id.
        /// </summary>
        private int _nodeId = 0;

        /// <summary>
        /// Flag indicating if we should only log critical events.
        /// </summary>
        private bool _onlyLogCriticalEvents = false;

        /// <summary>
        /// A list of warnings to treat as errors.
        /// </summary>
        private ISet<string> _warningsAsErrors = null;

        /// <summary>
        /// A list of warnings to treat as low importance messages.
        /// </summary>
        private ISet<string> _warningsAsMessages = null;

        /// <summary>
        /// The location of the toolset definitions.
        /// </summary>
        private ToolsetDefinitionLocations _toolsetDefinitionLocations = ToolsetDefinitionLocations.Default;

        /// <summary>
        /// The UI culture.
        /// </summary>
        private CultureInfo _uiCulture = CultureInfo.CurrentUICulture;

        /// <summary>
        /// The toolset provider
        /// </summary>
        private ToolsetProvider _toolsetProvider;

        /// <summary>
        /// Should the operating environment such as the current directory and environment be saved and restored between project builds and task invocations
        /// This should be defaulted to true as we should normally do this. This should be set to false for GlobalDTAR which could run at the same time in a different build manager.
        /// </summary>
        private bool _saveOperatingEnvironment = true;

        /// <summary>
        /// Should the logging service be done Synchronously when the number of cps's is 1
        /// </summary>
        private bool _useSynchronousLogging = false;

        /// <summary>
        /// Should the inprocess node be shutdown when the build finishes. By default this is false
        /// since visual studio needs to keep the inprocess node around after the build has finished.
        /// </summary>
        private bool _shutdownInProcNodeOnBuildFinish = false;

        /// <summary>
        /// When true, the in-proc node will not be available.
        /// </summary>
        private bool _disableInProcNode = false;

        /// <summary>
        /// When true, the build should log task inputs to the loggers.
        /// </summary>
        private bool _logTaskInputs = false;

        /// <summary>
        /// When true, the build should log the input parameters.  Note - logging these is very expensive!
        /// </summary>
        private bool _logInitialPropertiesAndItems = false;

        /// <summary>
        /// Constructor for those who intend to set all properties themselves.
        /// </summary>
        public BuildParameters()
        {
            Initialize(Utilities.GetEnvironmentProperties(), new ProjectRootElementCache(false), null);
        }

        /// <summary>
        /// Creates BuildParameters from a ProjectCollection.
        /// </summary>
        /// <param name="projectCollection">The ProjectCollection from which the BuildParameters should populate itself.</param>
        public BuildParameters(ProjectCollection projectCollection)
        {
            ErrorUtilities.VerifyThrowArgumentNull(projectCollection, "projectCollection");

            Initialize(new PropertyDictionary<ProjectPropertyInstance>(projectCollection.EnvironmentProperties), projectCollection.ProjectRootElementCache, new ToolsetProvider(projectCollection.Toolsets));

            _maxNodeCount = projectCollection.MaxNodeCount;
            _onlyLogCriticalEvents = projectCollection.OnlyLogCriticalEvents;
            _toolsetDefinitionLocations = projectCollection.ToolsetLocations;
            _defaultToolsVersion = projectCollection.DefaultToolsVersion;

            _globalProperties = new PropertyDictionary<ProjectPropertyInstance>(projectCollection.GlobalPropertiesCollection);
        }

        /// <summary>
        /// Private constructor for translation
        /// </summary>
        private BuildParameters(INodePacketTranslator translator)
        {
            ((INodePacketTranslatable)this).Translate(translator);
        }

        /// <summary>
        /// Copy constructor
        /// </summary>
        private BuildParameters(BuildParameters other)
        {
            ErrorUtilities.VerifyThrowInternalNull(other, "other");

            _buildId = other._buildId;
            _culture = other._culture;
            _defaultToolsVersion = other._defaultToolsVersion;
            _enableNodeReuse = other._enableNodeReuse;
            _buildProcessEnvironment = other._buildProcessEnvironment != null ? new Dictionary<string, string>(other._buildProcessEnvironment) : null;
            _environmentProperties = other._environmentProperties != null ? new PropertyDictionary<ProjectPropertyInstance>(other._environmentProperties) : null;
            _forwardingLoggers = other._forwardingLoggers != null ? new List<ForwardingLoggerRecord>(other._forwardingLoggers) : null;
            _globalProperties = other._globalProperties != null ? new PropertyDictionary<ProjectPropertyInstance>(other._globalProperties) : null;
            _hostServices = other._hostServices;
            _loggers = other._loggers != null ? new List<ILogger>(other._loggers) : null;
            _maxNodeCount = other._maxNodeCount;
            _memoryUseLimit = other._memoryUseLimit;
            _nodeExeLocation = other._nodeExeLocation;
            _nodeId = other._nodeId;
            _onlyLogCriticalEvents = other._onlyLogCriticalEvents;
#if FEATURE_THREAD_PRIORITY
            _buildThreadPriority = other._buildThreadPriority;
#endif
            _toolsetProvider = other._toolsetProvider;
            _toolsetDefinitionLocations = other._toolsetDefinitionLocations;
            _toolsetProvider = other._toolsetProvider;
            _uiCulture = other._uiCulture;
            _detailedSummary = other._detailedSummary;
            _shutdownInProcNodeOnBuildFinish = other._shutdownInProcNodeOnBuildFinish;
            this.ProjectRootElementCache = other.ProjectRootElementCache;
            this.ResetCaches = other.ResetCaches;
            this.LegacyThreadingSemantics = other.LegacyThreadingSemantics;
            _saveOperatingEnvironment = other._saveOperatingEnvironment;
            _useSynchronousLogging = other._useSynchronousLogging;
            _disableInProcNode = other._disableInProcNode;
            _logTaskInputs = other._logTaskInputs;
            _logInitialPropertiesAndItems = other._logInitialPropertiesAndItems;
            _warningsAsErrors = other._warningsAsErrors == null ? null : new HashSet<string>(other._warningsAsErrors, StringComparer.OrdinalIgnoreCase);
            _warningsAsMessages = other._warningsAsMessages == null ? null : new HashSet<string>(other._warningsAsMessages, StringComparer.OrdinalIgnoreCase);
        }

#if FEATURE_THREAD_PRIORITY
        /// <summary>
        /// Sets the desired thread priority for building.
        /// </summary>
        public ThreadPriority BuildThreadPriority
        {
            get { return _buildThreadPriority; }
            set { _buildThreadPriority = value; }
        }
#endif

        /// <summary>
        /// By default if the number of processes is set to 1 we will use Asynchronous logging. However if we want to use synchronous logging when the number of cpu's is set to 1
        /// this property needs to be set to true.
        /// </summary>
        public bool UseSynchronousLogging
        {
            get { return _useSynchronousLogging; }
            set { _useSynchronousLogging = value; }
        }

        /// <summary>
        /// Gets the environment variables which were set when this build was created.
        /// </summary>
        public IDictionary<string, string> BuildProcessEnvironment
        {
            get
            {
                if (_buildProcessEnvironment == null)
                {
                    return new ReadOnlyDictionary<string, string>(new Dictionary<string, string>(0));
                }

                return new ReadOnlyDictionary<string, string>(_buildProcessEnvironment);
            }
        }

        /// <summary>
        /// The name of the culture to use during the build.
        /// </summary>
        public CultureInfo Culture
        {
            get { return _culture; }
            set { _culture = value; }
        }

        /// <summary>
        /// The default tools version for the build.
        /// </summary>
        public string DefaultToolsVersion
        {
            get { return _defaultToolsVersion; }
            set { _defaultToolsVersion = value; }
        }

        /// <summary>
        /// When true, indicates that the build should emit a detailed summary at the end of the log.
        /// </summary>
        public bool DetailedSummary
        {
            get { return _detailedSummary; }
            set { _detailedSummary = value; }
        }

        /// <summary>
        /// When true, indicates the in-proc node should not be used.
        /// </summary>
        public bool DisableInProcNode
        {
            get { return _disableInProcNode; }
            set { _disableInProcNode = value; }
        }

        /// <summary>
        /// When true, indicates that the task parameters should be logged.
        /// </summary>
        public bool LogTaskInputs
        {
            get { return _logTaskInputs; }
            set { _logTaskInputs = value; }
        }

        /// <summary>
        /// When true, indicates that the initial properties and items should be logged.
        /// </summary>
        public bool LogInitialPropertiesAndItems
        {
            get { return _logInitialPropertiesAndItems; }
            set { _logInitialPropertiesAndItems = value; }
        }

        /// <summary>
        /// Indicates that the build should reset the configuration and results caches.
        /// </summary>
        public bool ResetCaches
        {
            get;
            set;
        }

        /// <summary>
        /// Flag indicating whether out-of-proc nodes should remain after the build and wait for further builds.
        /// </summary>
        public bool EnableNodeReuse
        {
            get { return _enableNodeReuse; }
            set { _enableNodeReuse = value; }
        }

        /// <summary>
        /// Gets an immutable collection of environment properties.
        /// </summary>
        /// <remarks>
        /// This differs from the BuildProcessEnvironment in that there are certain MSBuild-specific properties which are added, and those environment variables which
        /// would not be valid as MSBuild properties are removed.
        /// </remarks>
        public IDictionary<string, string> EnvironmentProperties
        {
            get
            {
                return new ReadOnlyConvertingDictionary<string, ProjectPropertyInstance, string>(_environmentProperties, delegate (ProjectPropertyInstance instance) { return ((IProperty)instance).EvaluatedValueEscaped; });
            }
        }

        /// <summary>
        /// The collection of forwarding logger descriptions.
        /// </summary>
        public IEnumerable<ForwardingLoggerRecord> ForwardingLoggers
        {
            get
            {
                return _forwardingLoggers;
            }

            set
            {
                if (value != null)
                {
                    foreach (ForwardingLoggerRecord logger in value)
                    {
                        ErrorUtilities.VerifyThrowArgumentNull(logger, "ForwardingLoggers", "NullLoggerNotAllowed");
                    }
                }

                _forwardingLoggers = value;
            }
        }

        /// <summary>
        /// Sets or retrieves an immutable collection of global properties.
        /// </summary>
        [SuppressMessage("Microsoft.Usage", "CA2227:CollectionPropertiesShouldBeReadOnly", Justification = "Accessor returns a readonly collection, and the BuildParameters class is immutable.")]
        public IDictionary<string, string> GlobalProperties
        {
            get
            {
                return new ReadOnlyConvertingDictionary<string, ProjectPropertyInstance, string>(_globalProperties, delegate (ProjectPropertyInstance instance) { return ((IProperty)instance).EvaluatedValueEscaped; });
            }

            set
            {
                _globalProperties = new PropertyDictionary<ProjectPropertyInstance>(value.Count);
                foreach (KeyValuePair<string, string> property in value)
                {
                    _globalProperties[property.Key] = ProjectPropertyInstance.Create(property.Key, property.Value);
                }
            }
        }

        /// <summary>
        /// Interface allowing the host to provide additional control over the build process.
        /// </summary>
        public HostServices HostServices
        {
            get { return _hostServices; }
            set { _hostServices = value; }
        }

        /// <summary>
        /// Enables or disables legacy threading semantics
        /// </summary>
        /// <remarks>
        /// Legacy threading semantics indicate that if a submission is to be built  
        /// only on the in-proc node and the submission is executed synchronously, then all of its
        /// requests will be built on the thread which invoked the build rather than a 
        /// thread owned by the BuildManager.
        /// </remarks>
        public bool LegacyThreadingSemantics
        {
            get;
            set;
        }

        /// <summary>
        /// The collection of loggers to use during the build.
        /// </summary>
        public IEnumerable<ILogger> Loggers
        {
            get
            {
                return _loggers;
            }

            set
            {
                if (value != null)
                {
                    foreach (ILogger logger in value)
                    {
                        ErrorUtilities.VerifyThrowArgumentNull(logger, "Loggers", "NullLoggerNotAllowed");
                    }
                }

                _loggers = value;
            }
        }

        /// <summary>
        /// The maximum number of nodes this build may use.
        /// </summary>
        public int MaxNodeCount
        {
            get
            {
                return _maxNodeCount;
            }

            set
            {
                ErrorUtilities.VerifyThrowArgument(value > 0, "InvalidMaxNodeCount");
                _maxNodeCount = value;
            }
        }

        /// <summary>
        /// The amount of memory the build should limit itself to using, in megabytes.
        /// </summary>
        public int MemoryUseLimit
        {
            get { return _memoryUseLimit; }
            set { _memoryUseLimit = value; }
        }

        /// <summary>
        /// The location of the build node executable.
        /// </summary>
        public string NodeExeLocation
        {
            get { return _nodeExeLocation; }
            set { _nodeExeLocation = value; }
        }

        /// <summary>
        /// Flag indicating if non-critical logging events should be discarded.
        /// </summary>
        public bool OnlyLogCriticalEvents
        {
            get { return _onlyLogCriticalEvents; }
            set { _onlyLogCriticalEvents = value; }
        }

        /// <summary>
        /// A list of warnings to treat as errors.  To treat all warnings as errors, set this to an empty <see cref="HashSet{String}"/>.  
        /// </summary>
        public ISet<string> WarningsAsErrors
        {
            get { return _warningsAsErrors; }
            set { _warningsAsErrors = value; }
        }

        /// <summary>
        /// A list of warnings to treat as low importance messages.
        /// </summary>
        public ISet<string> WarningsAsMessages
        {
            get { return _warningsAsMessages; }
            set { _warningsAsMessages = value; }
        }

        /// <summary>
        /// Locations to search for toolsets.
        /// </summary>
        public ToolsetDefinitionLocations ToolsetDefinitionLocations
        {
            get { return _toolsetDefinitionLocations; }
            set { _toolsetDefinitionLocations = value; }
        }

        /// <summary>
        /// Returns all of the toolsets.
        /// </summary>
        /// <comments>
        /// toolsetProvider.Toolsets is already a readonly collection. 
        /// </comments>
        public ICollection<Toolset> Toolsets
        {
            get { return _toolsetProvider.Toolsets; }
        }

        /// <summary>
        /// The name of the UI culture to use during the build.
        /// </summary>
        public CultureInfo UICulture
        {
            get { return _uiCulture; }
            set { _uiCulture = value; }
        }

        /// <summary>
        /// Flag indicating if the operating environment such as the current directory and environment be saved and restored between project builds and task invocations.
        /// This should be set to false for any other build managers running in the system so that we do not have two build managers trampling on each others environment.
        /// </summary>
        public bool SaveOperatingEnvironment
        {
            get { return _saveOperatingEnvironment; }
            set { _saveOperatingEnvironment = value; }
        }

        /// <summary>
        /// Shutdown the inprocess node when the build finishes. By default this is false 
        /// since visual studio needs to keep the inprocess node around after the build finishes.
        /// </summary>
        public bool ShutdownInProcNodeOnBuildFinish
        {
            get { return _shutdownInProcNodeOnBuildFinish; }
            set { _shutdownInProcNodeOnBuildFinish = value; }
        }

        /// <summary>
        /// Gets the internal msbuild thread stack size.
        /// </summary>
        static internal int ThreadStackSize
        {
            get { return CommunicationsUtilities.GetIntegerVariableOrDefault("MSBUILDTHREADSTACKSIZE", DefaultThreadStackSize); }
        }

        /// <summary>
        /// Gets or sets the endpoint shutdown timeout.
        /// </summary>
        static internal int EndpointShutdownTimeout
        {
            get { return CommunicationsUtilities.GetIntegerVariableOrDefault("MSBUILDENDPOINTSHUTDOWNTIMEOUT", DefaultEndpointShutdownTimeout); }
        }

        /// <summary>
        /// Gets or sets the engine shutdown timeout.
        /// </summary>
        static internal int EngineShutdownTimeout
        {
            get { return CommunicationsUtilities.GetIntegerVariableOrDefault("MSBUILDENGINESHUTDOWNTIMEOUT", DefaultEngineShutdownTimeout); }
        }

        /// <summary>
        /// Gets the maximum number of idle request builders to retain.
        /// </summary>
        static internal int IdleRequestBuilderLimit
        {
            get { return GetStaticIntVariableOrDefault("MSBUILDIDLEREQUESTBUILDERLIMIT", ref s_idleRequestBuilderLimit, DefaultIdleRequestBuilderLimit); }
        }

        /// <summary>
        /// Gets the logging thread shutdown timeout.
        /// </summary>
        static internal int LoggingThreadShutdownTimeout
        {
            get { return CommunicationsUtilities.GetIntegerVariableOrDefault("MSBUILDLOGGINGTHREADSHUTDOWNTIMEOUT", DefaultLoggingThreadShutdownTimeout); }
        }

        /// <summary>
        /// Gets or sets the request builder shutdown timeout.
        /// </summary>
        static internal int RequestBuilderShutdownTimeout
        {
            get { return CommunicationsUtilities.GetIntegerVariableOrDefault("MSBUILDREQUESTBUILDERSHUTDOWNTIMEOUT", DefaultRequestBuilderShutdownTimeout); }
        }

        /// <summary>
        /// Gets or sets the startup directory.
        /// </summary>
        internal static string StartupDirectory
        {
            get { return BuildParameters.s_startupDirectory; }
        }

        /// <summary>
        /// Indicates whether the build plan is enabled or not.
        /// </summary>
        internal static bool EnableBuildPlan
        {
            get
            {
                return GetStaticBoolVariableOrDefault("MSBUILDENABLEBUILDPLAN", ref s_enableBuildPlan, false);
            }
        }

        /// <summary>
        /// Indicates whether we should warn when a property is uninitialized when it is used.
        /// </summary>
        internal static bool WarnOnUninitializedProperty
        {
            get
            {
                return GetStaticBoolVariableOrDefault("MSBUILDWARNONUNINITIALIZEDPROPERTY", ref s_warnOnUninitializedProperty, false);
            }

            set
            {
                s_warnOnUninitializedProperty = value;
            }
        }

        /// <summary>
        /// Indicates whether we should dump string interning stats
        /// </summary>
        internal static bool DumpOpportunisticInternStats
        {
            get
            {
                return GetStaticBoolVariableOrDefault("MSBUILDDUMPOPPORTUNISTICINTERNSTATS", ref s_dumpOpportunisticInternStats, false);
            }
        }

        /// <summary>
        /// Indicates whether we should dump debugging information about the expander
        /// </summary>
        internal static bool DebugExpansion
        {
            get
            {
                return GetStaticBoolVariableOrDefault("MSBUILDDEBUGEXPANSION", ref s_debugExpansion, false);
            }
        }

        /// <summary>
        /// Indicates whether we should keep duplicate target outputs
        /// </summary>
        internal static bool KeepDuplicateOutputs
        {
            get
            {
                return GetStaticBoolVariableOrDefault("MSBUILDKEEPDUPLICATEOUTPUTS", ref s_keepDuplicateOutputs, false);
            }
        }

        /// <summary>
        /// Gets or sets the build id.
        /// </summary>
        internal int BuildId
        {
            get { return _buildId; }
            set { _buildId = value; }
        }

        /// <summary>
        /// Gets or sets the environment properties.
        /// </summary>
        /// <remarks>
        /// This is not the same as BuildProcessEnvironment.  See EnvironmentProperties.  These properties are those which
        /// are used during evaluation of a project, and exclude those properties which would not be valid MSBuild properties
        /// because they contain invalid characters (such as 'Program Files (x86)').
        /// </remarks>
        internal PropertyDictionary<ProjectPropertyInstance> EnvironmentPropertiesInternal
        {
            get
            {
                return _environmentProperties;
            }

            set
            {
                ErrorUtilities.VerifyThrowInternalNull(value, "EnvironmentPropertiesInternal");
                _environmentProperties = value;
            }
        }

        /// <summary>
        /// Gets the global properties.
        /// </summary>
        internal PropertyDictionary<ProjectPropertyInstance> GlobalPropertiesInternal
        {
            get { return _globalProperties; }
        }

        /// <summary>
        /// Gets or sets the node id.
        /// </summary>
        internal int NodeId
        {
            get { return _nodeId; }
            set { _nodeId = value; }
        }

        /// <summary>
        /// Gets the toolset provider.
        /// </summary>
        internal IToolsetProvider ToolsetProvider
        {
            get
            {
                EnsureToolsets();
                return _toolsetProvider;
            }
        }

        /// <summary>
        /// The one and only project root element cache to be used for the build.
        /// </summary>
        internal ProjectRootElementCache ProjectRootElementCache
        {
            get;
            set;
        }

#if FEATURE_APPDOMAIN
        /// <summary>
        /// Information for configuring child AppDomains.
        /// </summary>
        internal AppDomainSetup AppDomainSetup
        {
            get;
            set;
        }
#endif

        /// <summary>
        ///  (for diagnostic use) Whether or not this is out of proc
        /// </summary>
        internal bool IsOutOfProc
        {
            get;
            set;
        }

        /// <summary>
        /// Retrieves a toolset.
        /// </summary>
        public Toolset GetToolset(string toolsVersion)
        {
            EnsureToolsets();
            return _toolsetProvider.GetToolset(toolsVersion);
        }

        /// <summary>
        /// Creates a clone of this BuildParameters object.  This creates a clone of the logger collections, but does not deep clone
        /// the loggers within.
        /// </summary>
        public BuildParameters Clone()
        {
            return new BuildParameters(this);
        }

        /// <summary>
        /// Implementation of the serialization mechanism.
        /// </summary>
        void INodePacketTranslatable.Translate(INodePacketTranslator translator)
        {
            translator.Translate(ref _buildId);
            /* No build thread priority during translation.  We specifically use the default (which is ThreadPriority.Normal) */
            translator.TranslateDictionary(ref _buildProcessEnvironment, StringComparer.OrdinalIgnoreCase);
            translator.TranslateCulture(ref _culture);
            translator.Translate(ref _defaultToolsVersion);
            translator.Translate(ref _disableInProcNode);
            translator.Translate(ref _enableNodeReuse);
            translator.TranslateProjectPropertyInstanceDictionary(ref _environmentProperties);
            /* No forwarding logger information sent here - that goes with the node configuration */
            translator.TranslateProjectPropertyInstanceDictionary(ref _globalProperties);
            /* No host services during translation */
            /* No loggers during translation */
            translator.Translate(ref _maxNodeCount);
            translator.Translate(ref _memoryUseLimit);
            translator.Translate(ref _nodeExeLocation);
            /* No node id during translation */
            translator.Translate(ref _onlyLogCriticalEvents);
            translator.Translate(ref s_startupDirectory);
            translator.TranslateCulture(ref _uiCulture);
            translator.Translate(ref _toolsetProvider, Microsoft.Build.Evaluation.ToolsetProvider.FactoryForDeserialization);
            translator.Translate(ref _useSynchronousLogging);
            translator.Translate(ref _shutdownInProcNodeOnBuildFinish);
            translator.Translate(ref _logTaskInputs);
            translator.Translate(ref _logInitialPropertiesAndItems);

            // ProjectRootElementCache is not transmitted.
            // ResetCaches is not transmitted.
            // LegacyThreadingSemantics is not transmitted.
        }

        #region INodePacketTranslatable Members

        /// <summary>
        /// The class factory for deserialization.
        /// </summary>
        internal static BuildParameters FactoryForDeserialization(INodePacketTranslator translator)
        {
            return new BuildParameters(translator);
        }

        #endregion

        /// <summary>
        /// Gets the value of a boolean environment setting which is not expected to change.
        /// </summary>
        private static bool GetStaticBoolVariableOrDefault(string environmentVariable, ref bool? backing, bool @default)
        {
            if (!backing.HasValue)
            {
                if (String.IsNullOrEmpty(Environment.GetEnvironmentVariable(environmentVariable)))
                {
                    backing = @default;
                }
                else
                {
                    backing = true;
                }
            }

            return backing.Value;
        }

        /// <summary>
        /// Gets the value of an integer environment variable, or returns the default if none is set or it cannot be converted.
        /// </summary>
        private static int GetStaticIntVariableOrDefault(string environmentVariable, ref int? backingValue, int defaultValue)
        {
            if (!backingValue.HasValue)
            {
                string environmentValue = Environment.GetEnvironmentVariable(environmentVariable);
                if (String.IsNullOrEmpty(environmentValue))
                {
                    backingValue = defaultValue;
                }
                else
                {
                    int parsedValue = defaultValue;
                    if (!Int32.TryParse(environmentValue, out parsedValue))
                    {
                        backingValue = defaultValue;
                    }
                    else
                    {
                        backingValue = parsedValue;
                    }
                }
            }

            return backingValue.Value;
        }

        /// <summary>
        /// Centralization of the common parts of construction.
        /// </summary>
        private void Initialize(PropertyDictionary<ProjectPropertyInstance> environmentProperties, ProjectRootElementCache projectRootElementCache, ToolsetProvider toolsetProvider)
        {
            _buildProcessEnvironment = CommunicationsUtilities.GetEnvironmentVariables();
            _environmentProperties = environmentProperties;
            this.ProjectRootElementCache = projectRootElementCache;
            this.ResetCaches = true;
            _toolsetProvider = toolsetProvider;

            if (Environment.GetEnvironmentVariable("MSBUILDDISABLENODEREUSE") == "1") // For example to disable node reuse within Visual Studio
            {
                _enableNodeReuse = false;
            }

            if (Environment.GetEnvironmentVariable("MSBUILDDETAILEDSUMMARY") == "1") // For example to get detailed summary within Visual Studio
            {
                _detailedSummary = true;
            }

            _nodeExeLocation = FindMSBuildExe();
        }

        /// <summary>
        /// Loads the toolsets if we don't have them already.
        /// </summary>
        private void EnsureToolsets()
        {
            if (_toolsetProvider != null)
            {
                return;
            }

            _toolsetProvider = new ToolsetProvider(DefaultToolsVersion, _environmentProperties, _globalProperties, ToolsetDefinitionLocations);
        }

        /// <summary>
        /// This method determines where MSBuild.Exe is and sets the NodeExePath to that by default.
        /// </summary>
        private string FindMSBuildExe()
        {
            string location = _nodeExeLocation;

            // Use the location specified by the user in code.
            if (!string.IsNullOrEmpty(location) && CheckMSBuildExeExistsAt(location))
            {
                return location;
            }

            // Try what we think is the current executable path.
            return BuildEnvironmentHelper.Instance.CurrentMSBuildExePath;
        }

        /// <summary>
        /// Helper to avoid doing an expensive disk check for MSBuild.exe when
        /// we already checked in a previous build.
        /// This File.Exists otherwise can show up in profiles when there's a lot of 
        /// design time builds going on.
        /// </summary>
        private bool CheckMSBuildExeExistsAt(string path)
        {
            if (s_msbuildExeKnownToExistAt != null && string.Equals(path, s_msbuildExeKnownToExistAt, StringComparison.OrdinalIgnoreCase))
            {
                // We found it there last time: it must exist there.
                return true;
            }

            if (File.Exists(path))
            {
                s_msbuildExeKnownToExistAt = path;
                return true;
            }

            return false;
        }
    }
}
