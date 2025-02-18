// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;

#if !TASKHOST
using System.Text.Json.Serialization;
using System.Text.Json;
using System.Text.Json.Nodes;
#endif

using Microsoft.Build.Framework;
using Microsoft.Build.Shared;

#nullable disable

namespace Microsoft.Build.BackEnd
{
    /// <summary>
    /// TaskHostConfiguration contains information needed for the task host to
    /// configure itself for to execute a particular task.
    /// </summary>
    internal class TaskHostConfiguration :
#if TASKHOST
        INodePacket
#else
        INodePacket2
#endif
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
        /// The set of parameters to apply to the task prior to execution.
        /// </summary>
        private Dictionary<string, TaskParameter> _taskParameters;

        private Dictionary<string, string> _globalParameters;

        private ICollection<string> _warningsAsErrors;
        private ICollection<string> _warningsNotAsErrors;

        private ICollection<string> _warningsAsMessages;

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
        /// <param name="isTaskInputLoggingEnabled">Whether task inputs are logged.</param>
        /// <param name="taskParameters">Parameters to apply to the task.</param>
        /// <param name="globalParameters">global properties for the current project.</param>
        /// <param name="warningsAsErrors">Warning codes to be treated as errors for the current project.</param>
        /// <param name="warningsNotAsErrors">Warning codes not to be treated as errors for the current project.</param>
        /// <param name="warningsAsMessages">Warning codes to be treated as messages for the current project.</param>
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
        /// <param name="isTaskInputLoggingEnabled">Whether task inputs are logged.</param>
        /// <param name="taskParameters">Parameters to apply to the task.</param>
        /// <param name="globalParameters">global properties for the current project.</param>
        /// <param name="warningsAsErrors">Warning codes to be logged as errors for the current project.</param>
        /// <param name="warningsNotAsErrors">Warning codes not to be treated as errors for the current project.</param>
        /// <param name="warningsAsMessages">Warning codes to be treated as messages for the current project.</param>
#endif
        public TaskHostConfiguration(
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
#if FEATURE_APPDOMAIN
            _appDomainSetup = appDomainSetup;
#endif
            _lineNumberOfTask = lineNumberOfTask;
            _columnNumberOfTask = columnNumberOfTask;
            _projectFileOfTask = projectFileOfTask;
            _continueOnError = continueOnError;
            _taskName = taskName;
            _taskLocation = taskLocation;
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
            translator.Translate(ref _lineNumberOfTask);
            translator.Translate(ref _columnNumberOfTask);
            translator.Translate(ref _projectFileOfTask);
            translator.Translate(ref _taskName);
            translator.Translate(ref _taskLocation);
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

#if !TASKHOST
        public void Translate(IJsonTranslator translator)
        {
            if (translator.Mode == TranslationDirection.WriteToStream)
            {
                var model = new
                {
                    nodeId = _nodeId,
                    startupDirectory = _startupDirectory,
                    buildProcessEnvironment = _buildProcessEnvironment,
                    culture = _culture?.Name,
                    uiCulture = _uiCulture?.Name,
                    lineNumberOfTask = _lineNumberOfTask,
                    columnNumberOfTask = _columnNumberOfTask,
                    projectFileOfTask = _projectFileOfTask,
                    continueOnError = _continueOnError,
                    taskName = _taskName,
                    taskLocation = _taskLocation,
                    isTaskInputLoggingEnabled = _isTaskInputLoggingEnabled,
                    taskParameters = _taskParameters,
                    globalParameters = _globalParameters,
                    warningsAsErrors = _warningsAsErrors,
                    warningsNotAsErrors = _warningsNotAsErrors,
                    warningsAsMessages = _warningsAsMessages
                };

                translator.TranslateToJson(model, _jsonSerializerOptions);
            }
            else // ReadFromStream
            {
                var model = translator.TranslateFromJson<TaskHostConfigurationModel>(_jsonSerializerOptions);

                _nodeId = model.nodeId;
                _startupDirectory = model.startupDirectory;
                _buildProcessEnvironment = model.buildProcessEnvironment;
                _culture = !string.IsNullOrEmpty(model.culture) ? CultureInfo.GetCultureInfo(model.culture) : null;
                _uiCulture = !string.IsNullOrEmpty(model.uiCulture) ? CultureInfo.GetCultureInfo(model.uiCulture) : null;
                _lineNumberOfTask = model.lineNumberOfTask;
                _columnNumberOfTask = model.columnNumberOfTask;
                _projectFileOfTask = model.projectFileOfTask;
                _continueOnError = model.continueOnError;
                _taskName = model.taskName;
                _taskLocation = model.taskLocation;
                _isTaskInputLoggingEnabled = model.isTaskInputLoggingEnabled;
                _taskParameters = model.taskParameters;
                _globalParameters = model.globalParameters;
                _warningsAsErrors = model.warningsAsErrors != null
                    ? new HashSet<string>(model.warningsAsErrors, StringComparer.OrdinalIgnoreCase)
                    : null;
                _warningsNotAsErrors = model.warningsNotAsErrors != null
                    ? new HashSet<string>(model.warningsNotAsErrors, StringComparer.OrdinalIgnoreCase)
                    : null;
                _warningsAsMessages = model.warningsAsMessages != null
                    ? new HashSet<string>(model.warningsAsMessages, StringComparer.OrdinalIgnoreCase)
                    : null;
            }
        }
#endif
        /// <summary>
        /// Factory for deserialization.
        /// </summary>
        internal static INodePacket FactoryForDeserialization(ITranslatorBase translator)
        {
            TaskHostConfiguration configuration = new TaskHostConfiguration();
            if (translator.Protocol == ProtocolType.Binary)
            {
                configuration.Translate((ITranslator)translator);
            }
#if !TASKHOST
            else
            {
                configuration.Translate((IJsonTranslator)translator);
            }
#endif
            return configuration;
        }

#if !TASKHOST

        public static JsonSerializerOptions _jsonSerializerOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            Converters =
                {
                    new JsonStringEnumConverter(),
                    new CustomTaskParameterConverter()
                },
        };

        private class TaskHostConfigurationModel
        {
            public int nodeId { get; set; }
            public string startupDirectory { get; set; }
            public Dictionary<string, string> buildProcessEnvironment { get; set; }
            public string culture { get; set; }
            public string uiCulture { get; set; }
            public int lineNumberOfTask { get; set; }
            public int columnNumberOfTask { get; set; }
            public string projectFileOfTask { get; set; }
            public bool continueOnError { get; set; }
            public string taskName { get; set; }
            public string taskLocation { get; set; }
            public bool isTaskInputLoggingEnabled { get; set; }
            public Dictionary<string, TaskParameter> taskParameters { get; set; }
            public Dictionary<string, string> globalParameters { get; set; }
            public string[] warningsAsErrors { get; set; }
            public string[] warningsNotAsErrors { get; set; }
            public string[] warningsAsMessages { get; set; }
        }

        private class CustomTaskParameterConverter : JsonConverter<TaskParameter>
        {
            public override TaskParameter Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                if (reader.TokenType == JsonTokenType.Null)
                {
                    return null;
                }

                using JsonDocument doc = JsonDocument.ParseValue(ref reader);
                var element = doc.RootElement;

                if (element.TryGetProperty("value", out JsonElement valueElement))
                {
                    object value = null;

                    switch (valueElement.ValueKind)
                    {
                        case JsonValueKind.String:
                            value = valueElement.GetString();
                            break;
                        case JsonValueKind.Number:
                            if (valueElement.TryGetInt32(out int intValue))
                            {
                                value = intValue;
                            }
                            else if (valueElement.TryGetInt64(out long longValue))
                            {
                                value = longValue;
                            }
                            else
                            {
                                value = valueElement.GetDouble();
                            }
                            break;
                        case JsonValueKind.True:
                        case JsonValueKind.False:
                            value = valueElement.GetBoolean();
                            break;
                        case JsonValueKind.Array:
                            value = JsonSerializer.Deserialize<object[]>(valueElement.GetRawText(), options);
                            break;
                        case JsonValueKind.Object:
                            value = JsonSerializer.Deserialize<Dictionary<string, object>>(valueElement.GetRawText(), options);
                            break;
                    }

                    return new TaskParameter(value);
                }

                throw new JsonException("Invalid TaskParameter format");
            }

            public override void Write(Utf8JsonWriter writer, TaskParameter value, JsonSerializerOptions options)
            {
                if (value == null)
                {
                    writer.WriteNullValue();
                    return;
                }

                writer.WriteStartObject();
                writer.WritePropertyName("value");

                object wrappedValue = value.WrappedParameter;
                if (wrappedValue == null)
                {
                    writer.WriteNullValue();
                }
                else
                {
                    switch (wrappedValue)
                    {
                        case string strValue:
                            writer.WriteStringValue(strValue);
                            break;
                        case int intValue:
                            writer.WriteNumberValue(intValue);
                            break;
                        case long longValue:
                            writer.WriteNumberValue(longValue);
                            break;
                        case double doubleValue:
                            writer.WriteNumberValue(doubleValue);
                            break;
                        case float floatValue:
                            writer.WriteNumberValue(floatValue);
                            break;
                        case decimal decimalValue:
                            writer.WriteNumberValue(decimalValue);
                            break;
                        case bool boolValue:
                            writer.WriteBooleanValue(boolValue);
                            break;
                        case DateTime dateValue:
                            writer.WriteStringValue(dateValue);
                            break;
                        case ITaskItem taskItem:
                            WriteTaskItem(writer, taskItem);
                            break;
                        case ITaskItem[] taskItems:
                            WriteTaskItemArray(writer, taskItems);
                            break;
                        case IEnumerable enumerable:
                            WriteEnumerable(writer, enumerable, options);
                            break;
                        default:
                            JsonSerializer.Serialize(writer, wrappedValue, wrappedValue.GetType(), options);
                            break;
                    }
                }

                writer.WriteEndObject();
            }

            private static void WriteTaskItem(Utf8JsonWriter writer, ITaskItem taskItem)
            {
                writer.WriteStartObject();
                writer.WritePropertyName("itemSpec");
                writer.WriteStringValue(taskItem.ItemSpec);

                if (taskItem.MetadataCount > 0)
                {
                    writer.WritePropertyName("metadata");
                    writer.WriteStartObject();

                    foreach (string name in taskItem.MetadataNames)
                    {
                        writer.WritePropertyName(name);
                        writer.WriteStringValue(taskItem.GetMetadata(name));
                    }

                    writer.WriteEndObject();
                }

                writer.WriteEndObject();
            }

            private static void WriteTaskItemArray(Utf8JsonWriter writer, ITaskItem[] taskItems)
            {
                writer.WriteStartArray();

                foreach (var item in taskItems)
                {
                    WriteTaskItem(writer, item);
                }

                writer.WriteEndArray();
            }

            private static void WriteEnumerable(Utf8JsonWriter writer, IEnumerable enumerable, JsonSerializerOptions options)
            {
                writer.WriteStartArray();

                foreach (var item in enumerable)
                {
                    if (item == null)
                    {
                        writer.WriteNullValue();
                    }
                    else
                    {
                        JsonSerializer.Serialize(writer, item, item.GetType(), options);
                    }
                }

                writer.WriteEndArray();
            }
        }
#endif
    }
}
