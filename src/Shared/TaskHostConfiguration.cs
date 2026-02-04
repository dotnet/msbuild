// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using Microsoft.Build.Execution;
using Microsoft.Build.Shared;

#nullable disable

namespace Microsoft.Build.BackEnd
{
    /// <summary>
    /// TaskHostConfiguration contains information needed for the task host to
    /// configure itself for to execute a particular task.
    /// </summary>
    internal class TaskHostConfiguration : INodePacket
    {
        /// <summary>
        /// The node id (of the parent node, to make the logging work out)
        /// </summary>
        private int _nodeId;

        /// <summary>
        /// The startup directory
        /// </summary>
        private string _startupDirectory;

        /// <summary>
        /// The process environment.
        /// </summary>
        private Dictionary<string, string> _buildProcessEnvironment;

        /// <summary>
        /// The culture
        /// </summary>
        private CultureInfo _culture = CultureInfo.CurrentCulture;

        /// <summary>
        /// The UI culture.
        /// </summary>
        private CultureInfo _uiCulture = CultureInfo.CurrentUICulture;

#if FEATURE_APPDOMAIN
        /// <summary>
        /// The AppDomainSetup that we may want to use on AppDomainIsolated tasks.
        /// </summary>
        private AppDomainSetup _appDomainSetup;
#endif

        /// <summary>
        /// Line number where the instance of this task is defined.
        /// </summary>
        private int _lineNumberOfTask;

        /// <summary>
        /// Column number where the instance of this task is defined.
        /// </summary>
        private int _columnNumberOfTask;

        /// <summary>
        /// Project file where the instance of this task is defined.
        /// </summary>
        private string _projectFileOfTask;

        /// <summary>
        /// ContinueOnError flag for this particular task.
        /// </summary>
        private bool _continueOnError;

        /// <summary>
        /// Name of the task to be executed on the task host.
        /// </summary>
        private string _taskName;

        /// <summary>
        /// Location of the assembly containing the task to be executed.
        /// </summary>
        private string _taskLocation;

        /// <summary>
        /// Whether task inputs are logged.
        /// </summary>
        private bool _isTaskInputLoggingEnabled;

        /// <summary>
        /// Target name that is requesting the task execution.
        /// </summary>
        private string _targetName;

        /// <summary>
        /// Project file path that is requesting the task execution.
        /// </summary>
        private string _projectFile;

#if !NET35
        private HostServices _hostServices;
#endif

        /// <summary>
        /// The set of parameters to apply to the task prior to execution.
        /// </summary>
        private Dictionary<string, TaskParameter> _taskParameters;

        private Dictionary<string, string> _globalParameters;

        private ICollection<string> _warningsAsErrors;
        private ICollection<string> _warningsNotAsErrors;

        private ICollection<string> _warningsAsMessages;

#if FEATURE_APPDOMAIN
        /// <summary>
        /// Initializes a new instance of the <see cref="TaskHostConfiguration"/> class.
        /// </summary>
        /// <param name="nodeId">The ID of the node being configured.</param>
        /// <param name="startupDirectory">The startup directory for the task being executed.</param>
        /// <param name="buildProcessEnvironment">The set of environment variables to apply to the task execution process.</param>
        /// <param name="culture">The culture of the thread that will execute the task.</param>
        /// <param name="uiCulture">The UI culture of the thread that will execute the task.</param>
        /// <param name="hostServices">The host services to be used by the task host.</param>
        /// <param name="appDomainSetup">The AppDomainSetup that may be used to pass information to an AppDomainIsolated task.</param>
        /// <param name="lineNumberOfTask">The line number of the location from which this task was invoked.</param>
        /// <param name="columnNumberOfTask">The column number of the location from which this task was invoked.</param>
        /// <param name="projectFileOfTask">The project file from which this task was invoked.</param>
        /// <param name="continueOnError">A flag to indicate whether to continue with the build after the task fails.</param>
        /// <param name="taskName">The name of the task.</param>
        /// <param name="taskLocation">The location of the assembly from which the task is to be loaded.</param>
        /// <param name="targetName">The name of the target that is requesting the task execution.</param>
        /// <param name="projectFile">The project path that invokes the task.</param>
        /// <param name="isTaskInputLoggingEnabled">A flag to indicate whether task inputs are logged.</param>
        /// <param name="taskParameters">The parameters to apply to the task.</param>
        /// <param name="globalParameters">The global properties for the current project.</param>
        /// <param name="warningsAsErrors">A collection of warning codes to be treated as errors.</param>
        /// <param name="warningsNotAsErrors">A collection of warning codes not to be treated as errors.</param>
        /// <param name="warningsAsMessages">A collection of warning codes to be treated as messages.</param>
#else
        /// <summary>
        /// Initializes a new instance of the <see cref="TaskHostConfiguration"/> class.
        /// </summary>
        /// <param name="nodeId">The ID of the node being configured.</param>
        /// <param name="startupDirectory">The startup directory for the task being executed.</param>
        /// <param name="buildProcessEnvironment">The set of environment variables to apply to the task execution process.</param>
        /// <param name="culture">The culture of the thread that will execute the task.</param>
        /// <param name="uiCulture">The UI culture of the thread that will execute the task.</param>
        /// <param name="hostServices">The host services to be used by the task host.</param>
        /// <param name="lineNumberOfTask">The line number of the location from which this task was invoked.</param>
        /// <param name="columnNumberOfTask">The column number of the location from which this task was invoked.</param>
        /// <param name="projectFileOfTask">The project file from which this task was invoked.</param>
        /// <param name="continueOnError">A flag to indicate whether to continue with the build after the task fails.</param>
        /// <param name="taskName">The name of the task.</param>
        /// <param name="taskLocation">The location of the assembly from which the task is to be loaded.</param>
        /// <param name="targetName">The name of the target that is requesting the task execution.</param>
        /// <param name="projectFile">The project path that invokes the task.</param>
        /// <param name="isTaskInputLoggingEnabled">A flag to indicate whether task inputs are logged.</param>
        /// <param name="taskParameters">The parameters to apply to the task.</param>
        /// <param name="globalParameters">The global properties for the current project.</param>
        /// <param name="warningsAsErrors">A collection of warning codes to be treated as errors.</param>
        /// <param name="warningsNotAsErrors">A collection of warning codes not to be treated as errors.</param>
        /// <param name="warningsAsMessages">A collection of warning codes to be treated as messages.</param>
#endif
        public TaskHostConfiguration(
            int nodeId,
            string startupDirectory,
            IDictionary<string, string> buildProcessEnvironment,
            CultureInfo culture,
            CultureInfo uiCulture,
#if !NET35
            HostServices hostServices,
#endif
#if FEATURE_APPDOMAIN
            AppDomainSetup appDomainSetup,
#endif
            int lineNumberOfTask,
            int columnNumberOfTask,
            string projectFileOfTask,
            bool continueOnError,
            string taskName,
            string taskLocation,
            string targetName,
            string projectFile,
            bool isTaskInputLoggingEnabled,
            IDictionary<string, object> taskParameters,
            Dictionary<string, string> globalParameters,
            ICollection<string> warningsAsErrors,
            ICollection<string> warningsNotAsErrors,
            ICollection<string> warningsAsMessages)
        {
            ErrorUtilities.VerifyThrowInternalLength(taskName, nameof(taskName));
            ErrorUtilities.VerifyThrowInternalLength(taskLocation, nameof(taskLocation));

            _nodeId = nodeId;
            _startupDirectory = startupDirectory;

            if (buildProcessEnvironment != null)
            {
                _buildProcessEnvironment = buildProcessEnvironment as Dictionary<string, string>;

                if (_buildProcessEnvironment == null)
                {
                    _buildProcessEnvironment = new Dictionary<string, string>(buildProcessEnvironment);
                }
            }

            _culture = culture;
            _uiCulture = uiCulture;
#if !NET35
            _hostServices = hostServices;
#endif
#if FEATURE_APPDOMAIN
            _appDomainSetup = appDomainSetup;
#endif
            _lineNumberOfTask = lineNumberOfTask;
            _columnNumberOfTask = columnNumberOfTask;
            _projectFileOfTask = projectFileOfTask;
            _projectFile = projectFile;
            _continueOnError = continueOnError;
            _taskName = taskName;
            _taskLocation = taskLocation;
            _targetName = targetName;
            _isTaskInputLoggingEnabled = isTaskInputLoggingEnabled;
            _warningsAsErrors = warningsAsErrors;
            _warningsNotAsErrors = warningsNotAsErrors;
            _warningsAsMessages = warningsAsMessages;

            if (taskParameters != null)
            {
                _taskParameters = new Dictionary<string, TaskParameter>(StringComparer.OrdinalIgnoreCase);

                foreach (KeyValuePair<string, object> parameter in taskParameters)
                {
                    _taskParameters[parameter.Key] = new TaskParameter(parameter.Value);
                }
            }

            _globalParameters = globalParameters ?? new Dictionary<string, string>();
        }

        /// <summary>
        /// Constructor for deserialization.
        /// </summary>
        private TaskHostConfiguration()
        {
        }

        /// <summary>
        /// The node id
        /// </summary>
        public int NodeId
        {
            [DebuggerStepThrough]
            get
            { return _nodeId; }
        }

        /// <summary>
        /// The startup directory
        /// </summary>
        public string StartupDirectory
        {
            [DebuggerStepThrough]
            get
            { return _startupDirectory; }
        }

        /// <summary>
        /// The process environment.
        /// </summary>
        public Dictionary<string, string> BuildProcessEnvironment
        {
            [DebuggerStepThrough]
            get
            { return _buildProcessEnvironment; }
        }

        /// <summary>
        /// The culture
        /// </summary>
        public CultureInfo Culture
        {
            [DebuggerStepThrough]
            get
            { return _culture; }
        }

        /// <summary>
        /// The UI culture.
        /// </summary>
        public CultureInfo UICulture
        {
            [DebuggerStepThrough]
            get
            { return _uiCulture; }
        }

#if FEATURE_APPDOMAIN
        /// <summary>
        /// The AppDomain configuration bytes that we may want to use to initialize
        /// AppDomainIsolated tasks.
        /// </summary>
        public AppDomainSetup AppDomainSetup
        {
            [DebuggerStepThrough]
            get
            { return _appDomainSetup; }
        }
#endif

#if !NET35
        /// <summary>
        /// The HostServices to be used by the task host.
        /// </summary>
        public HostServices HostServices
        {
            [DebuggerStepThrough]
            get
            { return _hostServices; }
        }
#endif

        /// <summary>
        /// Line number where the instance of this task is defined.
        /// </summary>
        public int LineNumberOfTask
        {
            [DebuggerStepThrough]
            get
            { return _lineNumberOfTask; }
        }

        /// <summary>
        /// Column number where the instance of this task is defined.
        /// </summary>
        public int ColumnNumberOfTask
        {
            [DebuggerStepThrough]
            get
            { return _columnNumberOfTask; }
        }

        /// <summary>
        /// Project file path that is requesting the task execution.
        /// </summary>
        public string ProjectFile
        {
            [DebuggerStepThrough]
            get
            { return _projectFile; }
        }

        /// <summary>
        /// Target name that is requesting the task execution.
        /// </summary>
        public string TargetName
        {
            [DebuggerStepThrough]
            get
            { return _targetName; }
        }

        /// <summary>
        /// ContinueOnError flag for this particular task
        /// </summary>
        public bool ContinueOnError
        {
            [DebuggerStepThrough]
            get
            { return _continueOnError; }
        }

        /// <summary>
        /// Project file where the instance of this task is defined.
        /// </summary>
        public string ProjectFileOfTask
        {
            [DebuggerStepThrough]
            get
            { return _projectFileOfTask; }
        }

        /// <summary>
        /// Name of the task to execute.
        /// </summary>
        public string TaskName
        {
            [DebuggerStepThrough]
            get
            { return _taskName; }
        }

        /// <summary>
        /// Path to the assembly to load the task from.
        /// </summary>
        public string TaskLocation
        {
            [DebuggerStepThrough]
            get
            { return _taskLocation; }
        }

        /// <summary>
        /// Returns <see langword="true"/> if the build is configured to log all task inputs.
        /// </summary>
        public bool IsTaskInputLoggingEnabled
        {
            [DebuggerStepThrough]
            get
            { return _isTaskInputLoggingEnabled; }
        }

        /// <summary>
        /// Parameters to set on the instantiated task prior to execution.
        /// </summary>
        public Dictionary<string, TaskParameter> TaskParameters
        {
            [DebuggerStepThrough]
            get
            { return _taskParameters; }
        }

        /// <summary>
        /// Gets the global properties for the current project.
        /// </summary>
        public Dictionary<string, string> GlobalProperties
        {
            [DebuggerStepThrough]
            get
            { return _globalParameters; }
        }

        /// <summary>
        /// The NodePacketType of this NodePacket
        /// </summary>
        public NodePacketType Type
        {
            [DebuggerStepThrough]
            get
            { return NodePacketType.TaskHostConfiguration; }
        }

        public ICollection<string> WarningsAsErrors
        {
            [DebuggerStepThrough]
            get
            {
                return _warningsAsErrors;
            }
        }

        public ICollection<string> WarningsNotAsErrors
        {
            [DebuggerStepThrough]
            get
            {
                return _warningsNotAsErrors;
            }
        }

        public ICollection<string> WarningsAsMessages
        {
            [DebuggerStepThrough]
            get
            {
                return _warningsAsMessages;
            }
        }

        /// <summary>
        /// Translates the packet to/from binary form.
        /// </summary>
        /// <param name="translator">The translator to use.</param>
        public void Translate(ITranslator translator)
        {
            translator.Translate(ref _nodeId);
            translator.Translate(ref _startupDirectory);
            translator.TranslateDictionary(ref _buildProcessEnvironment, StringComparer.OrdinalIgnoreCase);
            translator.TranslateCulture(ref _culture);
            translator.TranslateCulture(ref _uiCulture);
#if FEATURE_APPDOMAIN
            // The packet version is used to determine if the AppDomain configuration should be serialized.
            // null = CLR2 (NET35) task host - supports AppDomain
            // 0 = CLR4 (NET472) task host - supports AppDomain
            // We serialize AppDomain for Framework task hosts (null or 0), but not for .NET (>= 2).
            if (translator.NegotiatedPacketVersion is null or 0)
            {
                byte[] appDomainConfigBytes = null;

                // Set the configuration bytes just before serialization in case the SetConfigurationBytes was invoked during lifetime of this instance.
                if (translator.Mode == TranslationDirection.WriteToStream)
                {
                    appDomainConfigBytes = _appDomainSetup?.GetConfigurationBytes();
                }

                translator.Translate(ref appDomainConfigBytes);

                if (translator.Mode == TranslationDirection.ReadFromStream)
                {
                    _appDomainSetup = new AppDomainSetup();
                    _appDomainSetup.SetConfigurationBytes(appDomainConfigBytes);
                }
            }
#endif
            translator.Translate(ref _lineNumberOfTask);
            translator.Translate(ref _columnNumberOfTask);
            translator.Translate(ref _projectFileOfTask);
            translator.Translate(ref _taskName);
            translator.Translate(ref _taskLocation);

            // null = CLR2 (NET35) task hosts which don't have these fields compiled in.
            // 0 = CLR4, 2+ = .NET - both support these fields.
#if NET472 || NETCOREAPP
            if (translator.NegotiatedPacketVersion.HasValue && translator.NegotiatedPacketVersion is 0 or >= 2)
            {
                translator.Translate(ref _targetName);
                translator.Translate(ref _projectFile);
                translator.Translate(ref _hostServices);
            }
#endif

            translator.Translate(ref _isTaskInputLoggingEnabled);
            translator.TranslateDictionary(ref _taskParameters, StringComparer.OrdinalIgnoreCase, TaskParameter.FactoryForDeserialization);
            translator.Translate(ref _continueOnError);
            translator.TranslateDictionary(ref _globalParameters, StringComparer.OrdinalIgnoreCase);
            translator.Translate(collection: ref _warningsAsErrors,
                                 objectTranslator: (ITranslator t, ref string s) => t.Translate(ref s),
#if CLR2COMPATIBILITY
                                 collectionFactory: count => new HashSet<string>(StringComparer.OrdinalIgnoreCase));
#else
                                 collectionFactory: count => new HashSet<string>(count, StringComparer.OrdinalIgnoreCase));
#endif
            translator.Translate(collection: ref _warningsNotAsErrors,
                                 objectTranslator: (ITranslator t, ref string s) => t.Translate(ref s),
#if CLR2COMPATIBILITY
                                 collectionFactory: count => new HashSet<string>(StringComparer.OrdinalIgnoreCase));
#else
                                 collectionFactory: count => new HashSet<string>(count, StringComparer.OrdinalIgnoreCase));
#endif
            translator.Translate(collection: ref _warningsAsMessages,
                                 objectTranslator: (ITranslator t, ref string s) => t.Translate(ref s),
#if CLR2COMPATIBILITY
                                 collectionFactory: count => new HashSet<string>(StringComparer.OrdinalIgnoreCase));
#else
                                 collectionFactory: count => new HashSet<string>(count, StringComparer.OrdinalIgnoreCase));
#endif
        }

        /// <summary>
        /// Factory for deserialization.
        /// </summary>
        internal static INodePacket FactoryForDeserialization(ITranslator translator)
        {
            TaskHostConfiguration configuration = new TaskHostConfiguration();
            configuration.Translate(translator);

            return configuration;
        }
    }
}
