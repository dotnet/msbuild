// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Globalization;
using Microsoft.Build.TaskHost.Utilities;

namespace Microsoft.Build.TaskHost.BackEnd;

/// <summary>
/// TaskHostConfiguration contains information needed for the task host to
/// configure itself for to execute a particular task.
/// </summary>
internal sealed class TaskHostConfiguration : INodePacket
{
    /// <summary>
    /// The node id (of the parent node, to make the logging work out).
    /// </summary>
    private int _nodeId;

    /// <summary>
    /// The startup directory.
    /// </summary>
    private string? _startupDirectory;

    /// <summary>
    /// The process environment.
    /// </summary>
    private Dictionary<string, string?>? _buildProcessEnvironment;

    /// <summary>
    /// The culture.
    /// </summary>
    private CultureInfo? _culture = CultureInfo.CurrentCulture;

    /// <summary>
    /// The UI culture.
    /// </summary>
    private CultureInfo? _uiCulture = CultureInfo.CurrentUICulture;

    /// <summary>
    /// The AppDomainSetup that we may want to use on AppDomainIsolated tasks.
    /// </summary>
    private AppDomainSetup? _appDomainSetup;

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
    private string? _projectFileOfTask;

    /// <summary>
    /// ContinueOnError flag for this particular task.
    /// </summary>
    private bool _continueOnError;

    /// <summary>
    /// Name of the task to be executed on the task host.
    /// </summary>
    private string? _taskName;

    /// <summary>
    /// Location of the assembly containing the task to be executed.
    /// </summary>
    private string? _taskLocation;

    /// <summary>
    /// Whether task inputs are logged.
    /// </summary>
    private bool _isTaskInputLoggingEnabled;

    /// <summary>
    /// Target name that is requesting the task execution.
    /// </summary>
    private string? _targetName;

    /// <summary>
    /// Project file path that is requesting the task execution.
    /// </summary>
    private string? _projectFile;

    /// <summary>
    /// The set of parameters to apply to the task prior to execution.
    /// </summary>
    private Dictionary<string, TaskParameter>? _taskParameters;

    private Dictionary<string, string?>? _globalParameters;

    private ICollection<string>? _warningsAsErrors;
    private ICollection<string>? _warningsNotAsErrors;

    private ICollection<string>? _warningsAsMessages;

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
    public TaskHostConfiguration(
        int nodeId,
        string startupDirectory,
        Dictionary<string, string?> buildProcessEnvironment,
        CultureInfo culture,
        CultureInfo uiCulture,
        AppDomainSetup appDomainSetup,
        int lineNumberOfTask,
        int columnNumberOfTask,
        string projectFileOfTask,
        bool continueOnError,
        string taskName,
        string taskLocation,
        string targetName,
        string projectFile,
        bool isTaskInputLoggingEnabled,
        Dictionary<string, object?> taskParameters,
        Dictionary<string, string?> globalParameters,
        ICollection<string> warningsAsErrors,
        ICollection<string> warningsNotAsErrors,
        ICollection<string> warningsAsMessages)
    {
        ErrorUtilities.VerifyThrowInternalLength(taskName, nameof(taskName));
        ErrorUtilities.VerifyThrowInternalLength(taskLocation, nameof(taskLocation));

        _nodeId = nodeId;
        _startupDirectory = startupDirectory;

        _buildProcessEnvironment = buildProcessEnvironment;

        _culture = culture;
        _uiCulture = uiCulture;
        _appDomainSetup = appDomainSetup;
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
            _taskParameters = new(StringComparer.OrdinalIgnoreCase);

            foreach (KeyValuePair<string, object?> parameter in taskParameters)
            {
                _taskParameters[parameter.Key] = new TaskParameter(parameter.Value);
            }
        }

        _globalParameters = globalParameters ?? [];
    }

    private TaskHostConfiguration()
    {
    }

    /// <summary>
    /// Gets the node id.
    /// </summary>
    public int NodeId => _nodeId;

    /// <summary>
    /// Gets the startup directory.
    /// </summary>
    public string StartupDirectory => _startupDirectory!;

    /// <summary>
    /// Gets the process environment.
    /// </summary>
    public Dictionary<string, string?> BuildProcessEnvironment => _buildProcessEnvironment!;

    /// <summary>
    /// Gets the culture.
    /// </summary>
    public CultureInfo? Culture => _culture;

    /// <summary>
    /// Gets the UI culture.
    /// </summary>
    public CultureInfo? UICulture => _uiCulture;

    /// <summary>
    /// Gets the AppDomain configuration bytes that we may want to use to initialize
    /// AppDomainIsolated tasks.
    /// </summary>
    public AppDomainSetup AppDomainSetup => _appDomainSetup!;

    /// <summary>
    /// Gets the line number where the instance of this task is defined.
    /// </summary>
    public int LineNumberOfTask => _lineNumberOfTask;

    /// <summary>
    /// Gets the column number where the instance of this task is defined.
    /// </summary>
    public int ColumnNumberOfTask => _columnNumberOfTask;

    /// <summary>
    /// Gets the project file path that is requesting the task execution.
    /// </summary>
    public string? ProjectFile => _projectFile;

    /// <summary>
    /// Gets the target name that is requesting the task execution.
    /// </summary>
    public string? TargetName => _targetName;

    /// <summary>
    /// Gets the ContinueOnError flag for this particular task.
    /// </summary>
    public bool ContinueOnError => _continueOnError;

    /// <summary>
    /// Gets the project file where the instance of this task is defined.
    /// </summary>
    public string ProjectFileOfTask => _projectFileOfTask!;

    /// <summary>
    /// Gets the name of the task to execute.
    /// </summary>
    public string TaskName => _taskName!;

    /// <summary>
    /// Gets the path to the assembly to load the task from.
    /// </summary>
    public string TaskLocation => _taskLocation!;

    /// <summary>
    /// Returns <see langword="true"/> if the build is configured to log all task inputs.
    /// </summary>
    public bool IsTaskInputLoggingEnabled => _isTaskInputLoggingEnabled;

    /// <summary>
    /// Gets the parameters to set on the instantiated task prior to execution.
    /// </summary>
    public Dictionary<string, TaskParameter> TaskParameters => _taskParameters!;

    /// <summary>
    /// Gets the global properties for the current project.
    /// </summary>
    public Dictionary<string, string?>? GlobalProperties => _globalParameters;

    /// <summary>
    /// Gets the NodePacketType of this NodePacket.
    /// </summary>
    public NodePacketType Type => NodePacketType.TaskHostConfiguration;

    public ICollection<string>? WarningsAsErrors => _warningsAsErrors;

    public ICollection<string>? WarningsNotAsErrors => _warningsNotAsErrors;

    public ICollection<string>? WarningsAsMessages => _warningsAsMessages;

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

        // The packet version is used to determine if the AppDomain configuration should be serialized.
        // If the packet version is bigger then 0, it means the task host will running under .NET.
        // Although MSBuild.exe runs under .NET Framework and has AppDomain support,
        // we don't transmit AppDomain config when communicating with dotnet.exe (it is not supported in .NET 5+).
        if (translator.NegotiatedPacketVersion == 0)
        {
            byte[]? appDomainConfigBytes = null;

            // Set the configuration bytes just before serialization in case the SetConfigurationBytes was invoked during lifetime of this instance.
            if (translator.Mode == TranslationDirection.WriteToStream)
            {
                appDomainConfigBytes = _appDomainSetup!.GetConfigurationBytes();
            }

            translator.Translate(ref appDomainConfigBytes);

            if (translator.Mode == TranslationDirection.ReadFromStream)
            {
                _appDomainSetup = new AppDomainSetup();
                _appDomainSetup.SetConfigurationBytes(appDomainConfigBytes);
            }
        }

        translator.Translate(ref _lineNumberOfTask);
        translator.Translate(ref _columnNumberOfTask);
        translator.Translate(ref _projectFileOfTask);
        translator.Translate(ref _taskName);
        translator.Translate(ref _taskLocation);
        if (translator.NegotiatedPacketVersion >= 2)
        {
            translator.Translate(ref _targetName);
            translator.Translate(ref _projectFile);
        }

        translator.Translate(ref _isTaskInputLoggingEnabled);
        translator.TranslateDictionary(ref _taskParameters, StringComparer.OrdinalIgnoreCase, TaskParameter.FactoryForDeserialization);
        translator.Translate(ref _continueOnError);
        translator.TranslateDictionary(ref _globalParameters, StringComparer.OrdinalIgnoreCase);
        translator.Translate(collection: ref _warningsAsErrors,
                             objectTranslator: (t, ref s) => t.Translate(ref s!),
                             collectionFactory: count => new HashSet<string>(StringComparer.OrdinalIgnoreCase));
        translator.Translate(collection: ref _warningsNotAsErrors,
                             objectTranslator: (t, ref s) => t.Translate(ref s!),
                             collectionFactory: count => new HashSet<string>(StringComparer.OrdinalIgnoreCase));
        translator.Translate(collection: ref _warningsAsMessages,
                             objectTranslator: (t, ref s) => t.Translate(ref s!),
                             collectionFactory: count => new HashSet<string>(StringComparer.OrdinalIgnoreCase));
    }

    internal static INodePacket FactoryForDeserialization(ITranslator translator)
    {
        var configuration = new TaskHostConfiguration();
        configuration.Translate(translator);

        return configuration;
    }
}
