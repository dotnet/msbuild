namespace Microsoft.Build.BuildEngine
{
    [System.Diagnostics.DebuggerDisplayAttribute("BuildItem (Name = { Name }, Include = { Include }, FinalItemSpec = { FinalItemSpec }, Condition = { Condition } )")]
    public partial class BuildItem
    {
        public BuildItem(string itemName, Microsoft.Build.Framework.ITaskItem taskItem) { }
        public BuildItem(string itemName, string itemInclude) { }
        public string Condition { get { throw null; } set { } }
        public int CustomMetadataCount { get { throw null; } }
        public System.Collections.ICollection CustomMetadataNames { get { throw null; } }
        public string Exclude { get { throw null; } set { } }
        public string FinalItemSpec { get { throw null; } }
        public string Include { get { throw null; } set { } }
        public bool IsImported { get { throw null; } }
        public int MetadataCount { get { throw null; } }
        public System.Collections.ICollection MetadataNames { get { throw null; } }
        public string Name { get { throw null; } set { } }
        public Microsoft.Build.BuildEngine.BuildItem Clone() { throw null; }
        public void CopyCustomMetadataTo(Microsoft.Build.BuildEngine.BuildItem destinationItem) { }
        public string GetEvaluatedMetadata(string metadataName) { throw null; }
        public string GetMetadata(string metadataName) { throw null; }
        public bool HasMetadata(string metadataName) { throw null; }
        public void RemoveMetadata(string metadataName) { }
        public void SetMetadata(string metadataName, string metadataValue) { }
        public void SetMetadata(string metadataName, string metadataValue, bool treatMetadataValueAsLiteral) { }
    }
    [System.Diagnostics.DebuggerDisplayAttribute("BuildItemGroup (Count = { Count }, Condition = { Condition })")]
    public partial class BuildItemGroup : System.Collections.IEnumerable
    {
        public BuildItemGroup() { }
        public string Condition { get { throw null; } set { } }
        public int Count { get { throw null; } }
        public bool IsImported { get { throw null; } }
        public Microsoft.Build.BuildEngine.BuildItem this[int index] { get { throw null; } }
        public Microsoft.Build.BuildEngine.BuildItem AddNewItem(string itemName, string itemInclude) { throw null; }
        public Microsoft.Build.BuildEngine.BuildItem AddNewItem(string itemName, string itemInclude, bool treatItemIncludeAsLiteral) { throw null; }
        public void Clear() { }
        public Microsoft.Build.BuildEngine.BuildItemGroup Clone(bool deepClone) { throw null; }
        public System.Collections.IEnumerator GetEnumerator() { throw null; }
        public void RemoveItem(Microsoft.Build.BuildEngine.BuildItem itemToRemove) { }
        public void RemoveItemAt(int index) { }
        public Microsoft.Build.BuildEngine.BuildItem[] ToArray() { throw null; }
    }
    public partial class BuildItemGroupCollection : System.Collections.ICollection, System.Collections.IEnumerable
    {
        internal BuildItemGroupCollection() { }
        public int Count { get { throw null; } }
        public bool IsSynchronized { get { throw null; } }
        public object SyncRoot { get { throw null; } }
        public void CopyTo(System.Array array, int index) { }
        public System.Collections.IEnumerator GetEnumerator() { throw null; }
    }
    [System.Diagnostics.DebuggerDisplayAttribute("BuildProperty (Name = { Name }, Value = { Value }, FinalValue = { FinalValue }, Condition = { Condition })")]
    public partial class BuildProperty
    {
        public BuildProperty(string propertyName, string propertyValue) { }
        public string Condition { get { throw null; } set { } }
        public string FinalValue { get { throw null; } }
        public bool IsImported { get { throw null; } }
        public string Name { get { throw null; } }
        public string Value { get { throw null; } set { } }
        public Microsoft.Build.BuildEngine.BuildProperty Clone(bool deepClone) { throw null; }
        public static explicit operator string (Microsoft.Build.BuildEngine.BuildProperty propertyToCast) { throw null; }
        public override string ToString() { throw null; }
    }
    [System.Diagnostics.DebuggerDisplayAttribute("BuildPropertyGroup (Count = { Count }, Condition = { Condition })")]
    public partial class BuildPropertyGroup : System.Collections.IEnumerable
    {
        public BuildPropertyGroup() { }
        public BuildPropertyGroup(Microsoft.Build.BuildEngine.Project parentProject) { }
        public string Condition { get { throw null; } set { } }
        public int Count { get { throw null; } }
        public bool IsImported { get { throw null; } }
        public Microsoft.Build.BuildEngine.BuildProperty this[string propertyName] { get { throw null; } set { } }
        public Microsoft.Build.BuildEngine.BuildProperty AddNewProperty(string propertyName, string propertyValue) { throw null; }
        public Microsoft.Build.BuildEngine.BuildProperty AddNewProperty(string propertyName, string propertyValue, bool treatPropertyValueAsLiteral) { throw null; }
        public void Clear() { }
        public Microsoft.Build.BuildEngine.BuildPropertyGroup Clone(bool deepClone) { throw null; }
        public System.Collections.IEnumerator GetEnumerator() { throw null; }
        public void RemoveProperty(Microsoft.Build.BuildEngine.BuildProperty property) { }
        public void RemoveProperty(string propertyName) { }
        public void SetImportedPropertyGroupCondition(string condition) { }
        public void SetProperty(string propertyName, string propertyValue) { }
        public void SetProperty(string propertyName, string propertyValue, bool treatPropertyValueAsLiteral) { }
    }
    public partial class BuildPropertyGroupCollection : System.Collections.ICollection, System.Collections.IEnumerable
    {
        internal BuildPropertyGroupCollection() { }
        public int Count { get { throw null; } }
        public bool IsSynchronized { get { throw null; } }
        public object SyncRoot { get { throw null; } }
        public void CopyTo(System.Array array, int index) { }
        public System.Collections.IEnumerator GetEnumerator() { throw null; }
    }
    [System.FlagsAttribute]
    public enum BuildSettings
    {
        DoNotResetPreviouslyBuiltTargets = 1,
        None = 0,
    }
    public partial class BuildTask
    {
        internal BuildTask() { }
        public string Condition { get { throw null; } set { } }
        public bool ContinueOnError { get { throw null; } set { } }
        public Microsoft.Build.Framework.ITaskHost HostObject { get { throw null; } set { } }
        public string Name { get { throw null; } }
        public System.Type Type { get { throw null; } }
        public void AddOutputItem(string taskParameter, string itemName) { }
        public void AddOutputProperty(string taskParameter, string propertyName) { }
        public bool Execute() { throw null; }
        public string[] GetParameterNames() { throw null; }
        public string GetParameterValue(string attributeName) { throw null; }
        public void SetParameterValue(string parameterName, string parameterValue) { }
        public void SetParameterValue(string parameterName, string parameterValue, bool treatParameterValueAsLiteral) { }
    }
    public delegate void ColorResetter();
    public delegate void ColorSetter(System.ConsoleColor color);
    public partial class ConfigurableForwardingLogger : Microsoft.Build.Framework.IForwardingLogger, Microsoft.Build.Framework.ILogger, Microsoft.Build.Framework.INodeLogger
    {
        public ConfigurableForwardingLogger() { }
        public Microsoft.Build.Framework.IEventRedirector BuildEventRedirector { get { throw null; } set { } }
        public int NodeId { get { throw null; } set { } }
        public string Parameters { get { throw null; } set { } }
        public Microsoft.Build.Framework.LoggerVerbosity Verbosity { get { throw null; } set { } }
        protected virtual void ForwardToCentralLogger(Microsoft.Build.Framework.BuildEventArgs e) { }
        public virtual void Initialize(Microsoft.Build.Framework.IEventSource eventSource) { }
        public void Initialize(Microsoft.Build.Framework.IEventSource eventSource, int nodeCount) { }
        public virtual void Shutdown() { }
    }
    public partial class ConsoleLogger : Microsoft.Build.Framework.ILogger, Microsoft.Build.Framework.INodeLogger
    {
        public ConsoleLogger() { }
        public ConsoleLogger(Microsoft.Build.Framework.LoggerVerbosity verbosity) { }
        public ConsoleLogger(Microsoft.Build.Framework.LoggerVerbosity verbosity, Microsoft.Build.BuildEngine.WriteHandler write, Microsoft.Build.BuildEngine.ColorSetter colorSet, Microsoft.Build.BuildEngine.ColorResetter colorReset) { }
        public string Parameters { get { throw null; } set { } }
        public bool ShowSummary { get { throw null; } set { } }
        public bool SkipProjectStartedText { get { throw null; } set { } }
        public Microsoft.Build.Framework.LoggerVerbosity Verbosity { get { throw null; } set { } }
        protected Microsoft.Build.BuildEngine.WriteHandler WriteHandler { get { throw null; } set { } }
        public void ApplyParameter(string parameterName, string parameterValue) { }
        public void BuildFinishedHandler(object sender, Microsoft.Build.Framework.BuildFinishedEventArgs e) { }
        public void BuildStartedHandler(object sender, Microsoft.Build.Framework.BuildStartedEventArgs e) { }
        public void CustomEventHandler(object sender, Microsoft.Build.Framework.CustomBuildEventArgs e) { }
        public void ErrorHandler(object sender, Microsoft.Build.Framework.BuildErrorEventArgs e) { }
        public virtual void Initialize(Microsoft.Build.Framework.IEventSource eventSource) { }
        public virtual void Initialize(Microsoft.Build.Framework.IEventSource eventSource, int nodeCount) { }
        public void MessageHandler(object sender, Microsoft.Build.Framework.BuildMessageEventArgs e) { }
        public void ProjectFinishedHandler(object sender, Microsoft.Build.Framework.ProjectFinishedEventArgs e) { }
        public void ProjectStartedHandler(object sender, Microsoft.Build.Framework.ProjectStartedEventArgs e) { }
        public virtual void Shutdown() { }
        public void TargetFinishedHandler(object sender, Microsoft.Build.Framework.TargetFinishedEventArgs e) { }
        public void TargetStartedHandler(object sender, Microsoft.Build.Framework.TargetStartedEventArgs e) { }
        public void TaskFinishedHandler(object sender, Microsoft.Build.Framework.TaskFinishedEventArgs e) { }
        public void TaskStartedHandler(object sender, Microsoft.Build.Framework.TaskStartedEventArgs e) { }
        public void WarningHandler(object sender, Microsoft.Build.Framework.BuildWarningEventArgs e) { }
    }
    public partial class DistributedFileLogger : Microsoft.Build.Framework.IForwardingLogger, Microsoft.Build.Framework.ILogger, Microsoft.Build.Framework.INodeLogger
    {
        public DistributedFileLogger() { }
        public Microsoft.Build.Framework.IEventRedirector BuildEventRedirector { get { throw null; } set { } }
        public int NodeId { get { throw null; } set { } }
        public string Parameters { get { throw null; } set { } }
        public Microsoft.Build.Framework.LoggerVerbosity Verbosity { get { throw null; } set { } }
        public void Initialize(Microsoft.Build.Framework.IEventSource eventSource) { }
        public void Initialize(Microsoft.Build.Framework.IEventSource eventSource, int nodeCount) { }
        public void Shutdown() { }
    }
    [System.ObsoleteAttribute("This class has been deprecated. Please use Microsoft.Build.Evaluation.ProjectCollection from the Microsoft.Build assembly instead.")]
    public partial class Engine
    {
        public Engine() { }
        public Engine(Microsoft.Build.BuildEngine.BuildPropertyGroup globalProperties) { }
        public Engine(Microsoft.Build.BuildEngine.BuildPropertyGroup globalProperties, Microsoft.Build.BuildEngine.ToolsetDefinitionLocations locations) { }
        public Engine(Microsoft.Build.BuildEngine.BuildPropertyGroup globalProperties, Microsoft.Build.BuildEngine.ToolsetDefinitionLocations locations, int numberOfCpus, string localNodeProviderParameters) { }
        public Engine(Microsoft.Build.BuildEngine.ToolsetDefinitionLocations locations) { }
        [System.ObsoleteAttribute("If you were simply passing in the .NET Framework location as the BinPath, just change to the parameterless Engine() constructor. Otherwise, you can define custom toolsets in the registry or config file, or by adding elements to the Engine's ToolsetCollection. Then use either the Engine() or Engine(ToolsetLocations) constructor instead.")]
        public Engine(string binPath) { }
        [System.ObsoleteAttribute("Avoid setting BinPath. If you were simply passing in the .NET Framework location as the BinPath, no other action is necessary. Otherwise, define Toolsets instead in the registry or config file, or by adding elements to the Engine's ToolsetCollection, in order to use a custom BinPath.")]
        public string BinPath { get { throw null; } set { } }
        public bool BuildEnabled { get { throw null; } set { } }
        public string DefaultToolsVersion { get { throw null; } set { } }
        public static Microsoft.Build.BuildEngine.Engine GlobalEngine { get { throw null; } }
        public Microsoft.Build.BuildEngine.BuildPropertyGroup GlobalProperties { get { throw null; } set { } }
        public bool IsBuilding { get { throw null; } }
        public bool OnlyLogCriticalEvents { get { throw null; } set { } }
        public Microsoft.Build.BuildEngine.ToolsetCollection Toolsets { get { throw null; } }
        public static System.Version Version { get { throw null; } }
        public bool BuildProject(Microsoft.Build.BuildEngine.Project project) { throw null; }
        public bool BuildProject(Microsoft.Build.BuildEngine.Project project, string targetName) { throw null; }
        public bool BuildProject(Microsoft.Build.BuildEngine.Project project, string[] targetNames) { throw null; }
        public bool BuildProject(Microsoft.Build.BuildEngine.Project project, string[] targetNames, System.Collections.IDictionary targetOutputs) { throw null; }
        public bool BuildProject(Microsoft.Build.BuildEngine.Project project, string[] targetNames, System.Collections.IDictionary targetOutputs, Microsoft.Build.BuildEngine.BuildSettings buildFlags) { throw null; }
        public bool BuildProjectFile(string projectFile) { throw null; }
        public bool BuildProjectFile(string projectFile, string targetName) { throw null; }
        public bool BuildProjectFile(string projectFile, string[] targetNames) { throw null; }
        public bool BuildProjectFile(string projectFile, string[] targetNames, Microsoft.Build.BuildEngine.BuildPropertyGroup globalProperties) { throw null; }
        public bool BuildProjectFile(string projectFile, string[] targetNames, Microsoft.Build.BuildEngine.BuildPropertyGroup globalProperties, System.Collections.IDictionary targetOutputs) { throw null; }
        public bool BuildProjectFile(string projectFile, string[] targetNames, Microsoft.Build.BuildEngine.BuildPropertyGroup globalProperties, System.Collections.IDictionary targetOutputs, Microsoft.Build.BuildEngine.BuildSettings buildFlags) { throw null; }
        public bool BuildProjectFile(string projectFile, string[] targetNames, Microsoft.Build.BuildEngine.BuildPropertyGroup globalProperties, System.Collections.IDictionary targetOutputs, Microsoft.Build.BuildEngine.BuildSettings buildFlags, string toolsVersion) { throw null; }
        public bool BuildProjectFiles(string[] projectFiles, string[][] targetNamesPerProject, Microsoft.Build.BuildEngine.BuildPropertyGroup[] globalPropertiesPerProject, System.Collections.IDictionary[] targetOutputsPerProject, Microsoft.Build.BuildEngine.BuildSettings buildFlags, string[] toolsVersions) { throw null; }
        public Microsoft.Build.BuildEngine.Project CreateNewProject() { throw null; }
        public Microsoft.Build.BuildEngine.Project GetLoadedProject(string projectFullFileName) { throw null; }
        public void RegisterDistributedLogger(Microsoft.Build.Framework.ILogger centralLogger, Microsoft.Build.BuildEngine.LoggerDescription forwardingLogger) { }
        public void RegisterLogger(Microsoft.Build.Framework.ILogger logger) { }
        public void Shutdown() { }
        public void UnloadAllProjects() { }
        public void UnloadProject(Microsoft.Build.BuildEngine.Project project) { }
        public void UnregisterAllLoggers() { }
    }
    public partial class FileLogger : Microsoft.Build.BuildEngine.ConsoleLogger
    {
        public FileLogger() { }
        public override void Initialize(Microsoft.Build.Framework.IEventSource eventSource) { }
        public override void Initialize(Microsoft.Build.Framework.IEventSource eventSource, int nodeCount) { }
        public override void Shutdown() { }
    }
    public partial class Import
    {
        internal Import() { }
        public string Condition { get { throw null; } set { } }
        public string EvaluatedProjectPath { get { throw null; } }
        public bool IsImported { get { throw null; } }
        public string ProjectPath { get { throw null; } set { } }
    }
    public partial class ImportCollection : System.Collections.ICollection, System.Collections.IEnumerable
    {
        internal ImportCollection() { }
        public int Count { get { throw null; } }
        public bool IsSynchronized { get { throw null; } }
        public object SyncRoot { get { throw null; } }
        public void AddNewImport(string projectFile, string condition) { }
        public void CopyTo(Microsoft.Build.BuildEngine.Import[] array, int index) { }
        public void CopyTo(System.Array array, int index) { }
        public System.Collections.IEnumerator GetEnumerator() { throw null; }
        public void RemoveImport(Microsoft.Build.BuildEngine.Import importToRemove) { }
    }
    public sealed partial class InternalLoggerException : System.Exception
    {
        public InternalLoggerException() { }
        public InternalLoggerException(string message) { }
        public InternalLoggerException(string message, System.Exception innerException) { }
        public Microsoft.Build.Framework.BuildEventArgs BuildEventArgs { get { throw null; } }
        public string ErrorCode { get { throw null; } }
        public string HelpKeyword { get { throw null; } }
        public bool InitializationException { get { throw null; } }
        [System.Security.Permissions.SecurityPermissionAttribute(System.Security.Permissions.SecurityAction.Demand, SerializationFormatter=true)]
        public override void GetObjectData(System.Runtime.Serialization.SerializationInfo info, System.Runtime.Serialization.StreamingContext context) { }
    }
    public sealed partial class InvalidProjectFileException : System.Exception
    {
        public InvalidProjectFileException() { }
        public InvalidProjectFileException(string message) { }
        public InvalidProjectFileException(string message, System.Exception innerException) { }
        public InvalidProjectFileException(string projectFile, int lineNumber, int columnNumber, int endLineNumber, int endColumnNumber, string message, string errorSubcategory, string errorCode, string helpKeyword) { }
        public InvalidProjectFileException(System.Xml.XmlNode xmlNode, string message, string errorSubcategory, string errorCode, string helpKeyword) { }
        public string BaseMessage { get { throw null; } }
        public int ColumnNumber { get { throw null; } }
        public int EndColumnNumber { get { throw null; } }
        public int EndLineNumber { get { throw null; } }
        public string ErrorCode { get { throw null; } }
        public string ErrorSubcategory { get { throw null; } }
        public string HelpKeyword { get { throw null; } }
        public int LineNumber { get { throw null; } }
        public override string Message { get { throw null; } }
        public string ProjectFile { get { throw null; } }
        [System.Security.Permissions.SecurityPermissionAttribute(System.Security.Permissions.SecurityAction.Demand, SerializationFormatter=true)]
        public override void GetObjectData(System.Runtime.Serialization.SerializationInfo info, System.Runtime.Serialization.StreamingContext context) { }
    }
    public partial class InvalidToolsetDefinitionException : System.Exception
    {
        public InvalidToolsetDefinitionException() { }
        protected InvalidToolsetDefinitionException(System.Runtime.Serialization.SerializationInfo info, System.Runtime.Serialization.StreamingContext context) { }
        public InvalidToolsetDefinitionException(string message) { }
        public InvalidToolsetDefinitionException(string message, System.Exception innerException) { }
        public InvalidToolsetDefinitionException(string message, string errorCode) { }
        public InvalidToolsetDefinitionException(string message, string errorCode, System.Exception innerException) { }
        public string ErrorCode { get { throw null; } }
        [System.Security.Permissions.SecurityPermissionAttribute(System.Security.Permissions.SecurityAction.Demand, SerializationFormatter=true)]
        public override void GetObjectData(System.Runtime.Serialization.SerializationInfo info, System.Runtime.Serialization.StreamingContext context) { }
    }
    public partial class LocalNode
    {
        internal LocalNode() { }
        public static void StartLocalNodeServer(int nodeNumber) { }
    }
    public partial class LoggerDescription
    {
        public LoggerDescription(string loggerClassName, string loggerAssemblyName, string loggerAssemblyFile, string loggerSwitchParameters, Microsoft.Build.Framework.LoggerVerbosity verbosity) { }
        public string LoggerSwitchParameters { get { throw null; } }
        public Microsoft.Build.Framework.LoggerVerbosity Verbosity { get { throw null; } }
    }
    [System.ObsoleteAttribute("This class has been deprecated. Please use Microsoft.Build.Evaluation.Project from the Microsoft.Build assembly instead.")]
    public partial class Project
    {
        public Project() { }
        public Project(Microsoft.Build.BuildEngine.Engine engine) { }
        public Project(Microsoft.Build.BuildEngine.Engine engine, string toolsVersion) { }
        public bool BuildEnabled { get { throw null; } set { } }
        public string DefaultTargets { get { throw null; } set { } }
        public string DefaultToolsVersion { get { throw null; } set { } }
        public System.Text.Encoding Encoding { get { throw null; } }
        public Microsoft.Build.BuildEngine.BuildItemGroup EvaluatedItems { get { throw null; } }
        public Microsoft.Build.BuildEngine.BuildItemGroup EvaluatedItemsIgnoringCondition { get { throw null; } }
        public Microsoft.Build.BuildEngine.BuildPropertyGroup EvaluatedProperties { get { throw null; } }
        public string FullFileName { get { throw null; } set { } }
        public Microsoft.Build.BuildEngine.BuildPropertyGroup GlobalProperties { get { throw null; } set { } }
        public bool HasToolsVersionAttribute { get { throw null; } }
        public Microsoft.Build.BuildEngine.ImportCollection Imports { get { throw null; } }
        public string InitialTargets { get { throw null; } set { } }
        public bool IsDirty { get { throw null; } }
        public bool IsValidated { get { throw null; } set { } }
        public Microsoft.Build.BuildEngine.BuildItemGroupCollection ItemGroups { get { throw null; } }
        public Microsoft.Build.BuildEngine.Engine ParentEngine { get { throw null; } }
        public Microsoft.Build.BuildEngine.BuildPropertyGroupCollection PropertyGroups { get { throw null; } }
        public string SchemaFile { get { throw null; } set { } }
        public Microsoft.Build.BuildEngine.TargetCollection Targets { get { throw null; } }
        public System.DateTime TimeOfLastDirty { get { throw null; } }
        public string ToolsVersion { get { throw null; } }
        public Microsoft.Build.BuildEngine.UsingTaskCollection UsingTasks { get { throw null; } }
        public string Xml { get { throw null; } }
        public void AddNewImport(string projectFile, string condition) { }
        public Microsoft.Build.BuildEngine.BuildItem AddNewItem(string itemName, string itemInclude) { throw null; }
        public Microsoft.Build.BuildEngine.BuildItem AddNewItem(string itemName, string itemInclude, bool treatItemIncludeAsLiteral) { throw null; }
        public Microsoft.Build.BuildEngine.BuildItemGroup AddNewItemGroup() { throw null; }
        public Microsoft.Build.BuildEngine.BuildPropertyGroup AddNewPropertyGroup(bool insertAtEndOfProject) { throw null; }
        public void AddNewUsingTaskFromAssemblyFile(string taskName, string assemblyFile) { }
        public void AddNewUsingTaskFromAssemblyName(string taskName, string assemblyName) { }
        public bool Build() { throw null; }
        public bool Build(string targetName) { throw null; }
        public bool Build(string[] targetNames) { throw null; }
        public bool Build(string[] targetNames, System.Collections.IDictionary targetOutputs) { throw null; }
        public bool Build(string[] targetNames, System.Collections.IDictionary targetOutputs, Microsoft.Build.BuildEngine.BuildSettings buildFlags) { throw null; }
        public string[] GetConditionedPropertyValues(string propertyName) { throw null; }
        public Microsoft.Build.BuildEngine.BuildItemGroup GetEvaluatedItemsByName(string itemName) { throw null; }
        public Microsoft.Build.BuildEngine.BuildItemGroup GetEvaluatedItemsByNameIgnoringCondition(string itemName) { throw null; }
        public string GetEvaluatedProperty(string propertyName) { throw null; }
        public string GetProjectExtensions(string id) { throw null; }
        public void Load(System.IO.TextReader textReader) { }
        public void Load(System.IO.TextReader textReader, Microsoft.Build.BuildEngine.ProjectLoadSettings projectLoadSettings) { }
        public void Load(string projectFileName) { }
        public void Load(string projectFileName, Microsoft.Build.BuildEngine.ProjectLoadSettings projectLoadSettings) { }
        public void LoadXml(string projectXml) { }
        public void LoadXml(string projectXml, Microsoft.Build.BuildEngine.ProjectLoadSettings projectLoadSettings) { }
        public void MarkProjectAsDirty() { }
        public void RemoveAllItemGroups() { }
        public void RemoveAllPropertyGroups() { }
        public void RemoveImportedPropertyGroup(Microsoft.Build.BuildEngine.BuildPropertyGroup propertyGroupToRemove) { }
        public void RemoveItem(Microsoft.Build.BuildEngine.BuildItem itemToRemove) { }
        public void RemoveItemGroup(Microsoft.Build.BuildEngine.BuildItemGroup itemGroupToRemove) { }
        public void RemoveItemGroupsWithMatchingCondition(string matchCondition) { }
        public void RemoveItemsByName(string itemName) { }
        public void RemovePropertyGroup(Microsoft.Build.BuildEngine.BuildPropertyGroup propertyGroupToRemove) { }
        public void RemovePropertyGroupsWithMatchingCondition(string matchCondition) { }
        public void RemovePropertyGroupsWithMatchingCondition(string matchCondition, bool includeImportedPropertyGroups) { }
        public void ResetBuildStatus() { }
        public void Save(System.IO.TextWriter textWriter) { }
        public void Save(string projectFileName) { }
        public void Save(string projectFileName, System.Text.Encoding encoding) { }
        public void SetImportedProperty(string propertyName, string propertyValue, string condition, Microsoft.Build.BuildEngine.Project importProject) { }
        public void SetImportedProperty(string propertyName, string propertyValue, string condition, Microsoft.Build.BuildEngine.Project importedProject, Microsoft.Build.BuildEngine.PropertyPosition position) { }
        public void SetImportedProperty(string propertyName, string propertyValue, string condition, Microsoft.Build.BuildEngine.Project importedProject, Microsoft.Build.BuildEngine.PropertyPosition position, bool treatPropertyValueAsLiteral) { }
        public void SetProjectExtensions(string id, string content) { }
        public void SetProperty(string propertyName, string propertyValue) { }
        public void SetProperty(string propertyName, string propertyValue, string condition) { }
        public void SetProperty(string propertyName, string propertyValue, string condition, Microsoft.Build.BuildEngine.PropertyPosition position) { }
        public void SetProperty(string propertyName, string propertyValue, string condition, Microsoft.Build.BuildEngine.PropertyPosition position, bool treatPropertyValueAsLiteral) { }
    }
    [System.FlagsAttribute]
    public enum ProjectLoadSettings
    {
        IgnoreMissingImports = 1,
        None = 0,
    }
    public enum PropertyPosition
    {
        UseExistingOrCreateAfterLastImport = 1,
        UseExistingOrCreateAfterLastPropertyGroup = 0,
    }
    public sealed partial class RemoteErrorException : System.Exception
    {
        internal RemoteErrorException() { }
        [System.Security.Permissions.SecurityPermissionAttribute(System.Security.Permissions.SecurityAction.Demand, SerializationFormatter=true)]
        public override void GetObjectData(System.Runtime.Serialization.SerializationInfo info, System.Runtime.Serialization.StreamingContext context) { }
    }
    public static partial class SolutionWrapperProject
    {
        public static string Generate(string solutionPath, string toolsVersionOverride, Microsoft.Build.Framework.BuildEventContext projectBuildEventContext) { throw null; }
    }
    [System.Diagnostics.DebuggerDisplayAttribute("Target (Name = { Name }, Condition = { Condition })")]
    public partial class Target : System.Collections.IEnumerable
    {
        internal Target() { }
        public string Condition { get { throw null; } set { } }
        public string DependsOnTargets { get { throw null; } set { } }
        public string Inputs { get { throw null; } set { } }
        public bool IsImported { get { throw null; } }
        public string Name { get { throw null; } }
        public string Outputs { get { throw null; } set { } }
        public Microsoft.Build.BuildEngine.BuildTask AddNewTask(string taskName) { throw null; }
        public System.Collections.IEnumerator GetEnumerator() { throw null; }
        public void RemoveTask(Microsoft.Build.BuildEngine.BuildTask taskElement) { }
    }
    public partial class TargetCollection : System.Collections.ICollection, System.Collections.IEnumerable
    {
        internal TargetCollection() { }
        public int Count { get { throw null; } }
        public bool IsSynchronized { get { throw null; } }
        public Microsoft.Build.BuildEngine.Target this[string index] { get { throw null; } }
        public object SyncRoot { get { throw null; } }
        public Microsoft.Build.BuildEngine.Target AddNewTarget(string targetName) { throw null; }
        public void CopyTo(System.Array array, int index) { }
        public bool Exists(string targetName) { throw null; }
        public System.Collections.IEnumerator GetEnumerator() { throw null; }
        public void RemoveTarget(Microsoft.Build.BuildEngine.Target targetToRemove) { }
    }
    public partial class Toolset
    {
        public Toolset(string toolsVersion, string toolsPath) { }
        public Toolset(string toolsVersion, string toolsPath, Microsoft.Build.BuildEngine.BuildPropertyGroup buildProperties) { }
        public Microsoft.Build.BuildEngine.BuildPropertyGroup BuildProperties { get { throw null; } }
        public string ToolsPath { get { throw null; } }
        public string ToolsVersion { get { throw null; } }
        public Microsoft.Build.BuildEngine.Toolset Clone() { throw null; }
    }
    public partial class ToolsetCollection : System.Collections.Generic.ICollection<Microsoft.Build.BuildEngine.Toolset>, System.Collections.Generic.IEnumerable<Microsoft.Build.BuildEngine.Toolset>, System.Collections.IEnumerable
    {
        internal ToolsetCollection() { }
        public int Count { get { throw null; } }
        public bool IsReadOnly { get { throw null; } }
        public Microsoft.Build.BuildEngine.Toolset this[string toolsVersion] { get { throw null; } }
        public System.Collections.Generic.IEnumerable<string> ToolsVersions { get { throw null; } }
        public void Add(Microsoft.Build.BuildEngine.Toolset item) { }
        public void Clear() { }
        public bool Contains(Microsoft.Build.BuildEngine.Toolset item) { throw null; }
        public bool Contains(string toolsVersion) { throw null; }
        public void CopyTo(Microsoft.Build.BuildEngine.Toolset[] array, int arrayIndex) { }
        public System.Collections.Generic.IEnumerator<Microsoft.Build.BuildEngine.Toolset> GetEnumerator() { throw null; }
        public bool Remove(Microsoft.Build.BuildEngine.Toolset item) { throw null; }
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() { throw null; }
    }
    [System.FlagsAttribute]
    public enum ToolsetDefinitionLocations
    {
        ConfigurationFile = 1,
        None = 0,
        Registry = 2,
    }
    public partial class UsingTask
    {
        internal UsingTask() { }
        public string AssemblyFile { get { throw null; } }
        public string AssemblyName { get { throw null; } }
        public string Condition { get { throw null; } }
        public bool IsImported { get { throw null; } }
        public string TaskName { get { throw null; } }
    }
    public partial class UsingTaskCollection : System.Collections.ICollection, System.Collections.IEnumerable
    {
        internal UsingTaskCollection() { }
        public int Count { get { throw null; } }
        public bool IsSynchronized { get { throw null; } }
        public object SyncRoot { get { throw null; } }
        public void CopyTo(Microsoft.Build.BuildEngine.UsingTask[] array, int index) { }
        public void CopyTo(System.Array array, int index) { }
        public System.Collections.IEnumerator GetEnumerator() { throw null; }
    }
    public static partial class Utilities
    {
        public static string Escape(string unescapedExpression) { throw null; }
    }
    public delegate void WriteHandler(string message);
}
