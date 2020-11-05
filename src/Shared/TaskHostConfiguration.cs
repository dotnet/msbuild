// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;

using Microsoft.Build.Shared;

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
        /// The set of parameters to apply to the task prior to execution.  
        /// </summary>
        private Dictionary<string, TaskParameter> _taskParameters;

        private Dictionary<string, string> _globalParameters;

#if FEATURE_APPDOMAIN
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="nodeId">The ID of the node being configured.</param>
        /// <param name="startupDirectory">The startup directory for the task being executed.</param>
        /// <param name="buildProcessEnvironment">The set of environment variables to apply to the task execution process.</param>
        /// <param name="culture">The culture of the thread that will execute the task.</param>
        /// <param name="uiCulture">The UI culture of the thread that will execute the task.</param>
        /// <param name="appDomainSetup">The AppDomainSetup that may be used to pass information to an AppDomainIsolated task.</param>
        /// <param name="lineNumberOfTask">The line number of the location from which this task was invoked.</param>
        /// <param name="columnNumberOfTask">The column number of the location from which this task was invoked.</param>
        /// <param name="projectFileOfTask">The project file from which this task was invoked.</param>
        /// <param name="continueOnError">Flag to continue with the build after a the task failed</param>
        /// <param name="taskName">Name of the task.</param>
        /// <param name="taskLocation">Location of the assembly the task is to be loaded from.</param>
        /// <param name="taskParameters">Parameters to apply to the task.</param>
        /// <param name="globalParameters">global properties for the current project.</param>
#else
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="nodeId">The ID of the node being configured.</param>
        /// <param name="startupDirectory">The startup directory for the task being executed.</param>
        /// <param name="buildProcessEnvironment">The set of environment variables to apply to the task execution process.</param>
        /// <param name="culture">The culture of the thread that will execute the task.</param>
        /// <param name="uiCulture">The UI culture of the thread that will execute the task.</param>
        /// <param name="lineNumberOfTask">The line number of the location from which this task was invoked.</param>
        /// <param name="columnNumberOfTask">The column number of the location from which this task was invoked.</param>
        /// <param name="projectFileOfTask">The project file from which this task was invoked.</param>
        /// <param name="continueOnError">Flag to continue with the build after a the task failed</param>
        /// <param name="taskName">Name of the task.</param>
        /// <param name="taskLocation">Location of the assembly the task is to be loaded from.</param>
        /// <param name="taskParameters">Parameters to apply to the task.</param>
        /// <param name="globalParameters">global properties for the current project.</param>
#endif
        public TaskHostConfiguration
            (
                int nodeId,
                string startupDirectory,
                IDictionary<string, string> buildProcessEnvironment,
                CultureInfo culture,
                CultureInfo uiCulture,
#if FEATURE_APPDOMAIN
                AppDomainSetup appDomainSetup,
#endif
                int lineNumberOfTask,
                int columnNumberOfTask,
                string projectFileOfTask,
                bool continueOnError,
                string taskName,
                string taskLocation,
                IDictionary<string, object> taskParameters,
                Dictionary<string, string> globalParameters
            )
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
#if FEATURE_APPDOMAIN
            _appDomainSetup = appDomainSetup;
#endif
            _lineNumberOfTask = lineNumberOfTask;
            _columnNumberOfTask = columnNumberOfTask;
            _projectFileOfTask = projectFileOfTask;
            _continueOnError = continueOnError;
            _taskName = taskName;
            _taskLocation = taskLocation;

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
            translator.TranslateDotNet(ref _appDomainSetup);
#endif
            translator.Translate(ref _lineNumberOfTask);
            translator.Translate(ref _columnNumberOfTask);
            translator.Translate(ref _projectFileOfTask);
            translator.Translate(ref _taskName);
            translator.Translate(ref _taskLocation);
            translator.TranslateDictionary(ref _taskParameters, StringComparer.OrdinalIgnoreCase, TaskParameter.FactoryForDeserialization);
            translator.Translate(ref _continueOnError);
            translator.TranslateDictionary(ref _globalParameters, StringComparer.OrdinalIgnoreCase);
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
