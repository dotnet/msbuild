namespace Microsoft.Build.Construction
{
    public abstract partial class ElementLocation
    {
        protected ElementLocation() { }
        [System.Diagnostics.DebuggerBrowsableAttribute((System.Diagnostics.DebuggerBrowsableState)(0))]
        public abstract int Column { get; }
        [System.Diagnostics.DebuggerBrowsableAttribute((System.Diagnostics.DebuggerBrowsableState)(0))]
        public abstract string File { get; }
        [System.Diagnostics.DebuggerBrowsableAttribute((System.Diagnostics.DebuggerBrowsableState)(0))]
        public abstract int Line { get; }
        public string LocationString { get { throw null; } }
        public override bool Equals(object obj) { throw null; }
        public override int GetHashCode() { throw null; }
        public override string ToString() { throw null; }
    }
    public enum ImplicitImportLocation
    {
        Bottom = 2,
        None = 0,
        Top = 1,
    }
    [System.Diagnostics.DebuggerDisplayAttribute("ProjectChooseElement (#Children={Count} HasOtherwise={OtherwiseElement != null})")]
    public partial class ProjectChooseElement : Microsoft.Build.Construction.ProjectElementContainer
    {
        internal ProjectChooseElement() { }
        public override string Condition { get { throw null; } set { } }
        public override Microsoft.Build.Construction.ElementLocation ConditionLocation { get { throw null; } }
        public Microsoft.Build.Construction.ProjectOtherwiseElement OtherwiseElement { get { throw null; } }
        public System.Collections.Generic.ICollection<Microsoft.Build.Construction.ProjectWhenElement> WhenElements { get { throw null; } }
        protected override Microsoft.Build.Construction.ProjectElement CreateNewInstance(Microsoft.Build.Construction.ProjectRootElement owner) { throw null; }
    }
    public sealed partial class ProjectConfigurationInSolution
    {
        internal ProjectConfigurationInSolution() { }
        public string ConfigurationName { get { throw null; } }
        public string FullName { get { throw null; } }
        public bool IncludeInBuild { get { throw null; } }
        public string PlatformName { get { throw null; } }
    }
    public abstract partial class ProjectElement : Microsoft.Build.Framework.IProjectElement
    {
        internal ProjectElement() { }
        public System.Collections.Generic.IEnumerable<Microsoft.Build.Construction.ProjectElementContainer> AllParents { get { throw null; } }
        public virtual string Condition { [System.Diagnostics.DebuggerStepThroughAttribute]get { throw null; } [System.Diagnostics.DebuggerStepThroughAttribute]set { } }
        public virtual Microsoft.Build.Construction.ElementLocation ConditionLocation { get { throw null; } }
        public Microsoft.Build.Construction.ProjectRootElement ContainingProject { get { throw null; } }
        public string ElementName { get { throw null; } }
        public string Label { [System.Diagnostics.DebuggerStepThroughAttribute]get { throw null; } [System.Diagnostics.DebuggerStepThroughAttribute]set { } }
        public Microsoft.Build.Construction.ElementLocation LabelLocation { get { throw null; } }
        public Microsoft.Build.Construction.ElementLocation Location { get { throw null; } }
        public Microsoft.Build.Construction.ProjectElement NextSibling { [System.Diagnostics.DebuggerStepThroughAttribute, System.Runtime.CompilerServices.CompilerGeneratedAttribute]get { throw null; } }
        public string OuterElement { get { throw null; } }
        public Microsoft.Build.Construction.ProjectElementContainer Parent { [System.Diagnostics.DebuggerStepThroughAttribute]get { throw null; } }
        public Microsoft.Build.Construction.ProjectElement PreviousSibling { [System.Diagnostics.DebuggerStepThroughAttribute, System.Runtime.CompilerServices.CompilerGeneratedAttribute]get { throw null; } }
        public Microsoft.Build.Construction.ProjectElement Clone() { throw null; }
        protected internal virtual Microsoft.Build.Construction.ProjectElement Clone(Microsoft.Build.Construction.ProjectRootElement factory) { throw null; }
        public virtual void CopyFrom(Microsoft.Build.Construction.ProjectElement element) { }
        protected abstract Microsoft.Build.Construction.ProjectElement CreateNewInstance(Microsoft.Build.Construction.ProjectRootElement owner);
        protected virtual bool ShouldCloneXmlAttribute(System.Xml.XmlAttribute attribute) { throw null; }
    }
    public abstract partial class ProjectElementContainer : Microsoft.Build.Construction.ProjectElement
    {
        internal ProjectElementContainer() { }
        public System.Collections.Generic.IEnumerable<Microsoft.Build.Construction.ProjectElement> AllChildren { get { throw null; } }
        public System.Collections.Generic.ICollection<Microsoft.Build.Construction.ProjectElement> Children { [System.Diagnostics.DebuggerStepThroughAttribute]get { throw null; } }
        public System.Collections.Generic.ICollection<Microsoft.Build.Construction.ProjectElement> ChildrenReversed { [System.Diagnostics.DebuggerStepThroughAttribute]get { throw null; } }
        public int Count { [System.Diagnostics.DebuggerStepThroughAttribute]get { throw null; } }
        public Microsoft.Build.Construction.ProjectElement FirstChild { [System.Diagnostics.DebuggerStepThroughAttribute, System.Runtime.CompilerServices.CompilerGeneratedAttribute]get { throw null; } }
        public Microsoft.Build.Construction.ProjectElement LastChild { [System.Diagnostics.DebuggerStepThroughAttribute, System.Runtime.CompilerServices.CompilerGeneratedAttribute]get { throw null; } }
        public void AppendChild(Microsoft.Build.Construction.ProjectElement child) { }
        protected internal virtual Microsoft.Build.Construction.ProjectElementContainer DeepClone(Microsoft.Build.Construction.ProjectRootElement factory, Microsoft.Build.Construction.ProjectElementContainer parent) { throw null; }
        public virtual void DeepCopyFrom(Microsoft.Build.Construction.ProjectElementContainer element) { }
        public void InsertAfterChild(Microsoft.Build.Construction.ProjectElement child, Microsoft.Build.Construction.ProjectElement reference) { }
        public void InsertBeforeChild(Microsoft.Build.Construction.ProjectElement child, Microsoft.Build.Construction.ProjectElement reference) { }
        public void PrependChild(Microsoft.Build.Construction.ProjectElement child) { }
        public void RemoveAllChildren() { }
        public void RemoveChild(Microsoft.Build.Construction.ProjectElement child) { }
    }
    public partial class ProjectExtensionsElement : Microsoft.Build.Construction.ProjectElement
    {
        internal ProjectExtensionsElement() { }
        public override string Condition { get { throw null; } set { } }
        public override Microsoft.Build.Construction.ElementLocation ConditionLocation { get { throw null; } }
        public string Content { [System.Diagnostics.DebuggerStepThroughAttribute]get { throw null; } set { } }
        public string this[string name] { get { throw null; } set { } }
        public override void CopyFrom(Microsoft.Build.Construction.ProjectElement element) { }
        protected override Microsoft.Build.Construction.ProjectElement CreateNewInstance(Microsoft.Build.Construction.ProjectRootElement owner) { throw null; }
    }
    [System.Diagnostics.DebuggerDisplayAttribute("Project={Project} Condition={Condition}")]
    public partial class ProjectImportElement : Microsoft.Build.Construction.ProjectElement
    {
        internal ProjectImportElement() { }
        public Microsoft.Build.Construction.ImplicitImportLocation ImplicitImportLocation { [System.Runtime.CompilerServices.CompilerGeneratedAttribute]get { throw null; } }
        public string MinimumVersion { get { throw null; } set { } }
        public Microsoft.Build.Construction.ProjectElement OriginalElement { [System.Runtime.CompilerServices.CompilerGeneratedAttribute]get { throw null; } }
        public string Project { get { throw null; } set { } }
        public Microsoft.Build.Construction.ElementLocation ProjectLocation { get { throw null; } }
        public string Sdk { get { throw null; } set { } }
        public Microsoft.Build.Construction.ElementLocation SdkLocation { get { throw null; } }
        public string Version { get { throw null; } set { } }
        protected override Microsoft.Build.Construction.ProjectElement CreateNewInstance(Microsoft.Build.Construction.ProjectRootElement owner) { throw null; }
    }
    [System.Diagnostics.DebuggerDisplayAttribute("#Imports={Count} Condition={Condition} Label={Label}")]
    public partial class ProjectImportGroupElement : Microsoft.Build.Construction.ProjectElementContainer
    {
        internal ProjectImportGroupElement() { }
        public System.Collections.Generic.ICollection<Microsoft.Build.Construction.ProjectImportElement> Imports { get { throw null; } }
        public Microsoft.Build.Construction.ProjectImportElement AddImport(string project) { throw null; }
        protected override Microsoft.Build.Construction.ProjectElement CreateNewInstance(Microsoft.Build.Construction.ProjectRootElement owner) { throw null; }
    }
    public sealed partial class ProjectInSolution
    {
        internal ProjectInSolution() { }
        public string AbsolutePath { get { throw null; } }
        public System.Collections.Generic.IReadOnlyList<string> Dependencies { get { throw null; } }
        public string ParentProjectGuid { get { throw null; } }
        public System.Collections.Generic.IReadOnlyDictionary<string, Microsoft.Build.Construction.ProjectConfigurationInSolution> ProjectConfigurations { get { throw null; } }
        public string ProjectGuid { get { throw null; } }
        public string ProjectName { get { throw null; } }
        public Microsoft.Build.Construction.SolutionProjectType ProjectType { get { throw null; } set { } }
        public string RelativePath { get { throw null; } }
    }
    [System.Diagnostics.DebuggerDisplayAttribute("{ItemType} #Metadata={Count} Condition={Condition}")]
    public partial class ProjectItemDefinitionElement : Microsoft.Build.Construction.ProjectElementContainer
    {
        internal ProjectItemDefinitionElement() { }
        public string ItemType { get { throw null; } }
        public System.Collections.Generic.ICollection<Microsoft.Build.Construction.ProjectMetadataElement> Metadata { get { throw null; } }
        public Microsoft.Build.Construction.ProjectMetadataElement AddMetadata(string name, string unevaluatedValue) { throw null; }
        protected override Microsoft.Build.Construction.ProjectElement CreateNewInstance(Microsoft.Build.Construction.ProjectRootElement owner) { throw null; }
    }
    [System.Diagnostics.DebuggerDisplayAttribute("#ItemDefinitions={Count} Condition={Condition} Label={Label}")]
    public partial class ProjectItemDefinitionGroupElement : Microsoft.Build.Construction.ProjectElementContainer
    {
        internal ProjectItemDefinitionGroupElement() { }
        public System.Collections.Generic.ICollection<Microsoft.Build.Construction.ProjectItemDefinitionElement> ItemDefinitions { get { throw null; } }
        public Microsoft.Build.Construction.ProjectItemDefinitionElement AddItemDefinition(string itemType) { throw null; }
        protected override Microsoft.Build.Construction.ProjectElement CreateNewInstance(Microsoft.Build.Construction.ProjectRootElement owner) { throw null; }
    }
    [System.Diagnostics.DebuggerDisplayAttribute("{ItemType} Include={Include} Exclude={Exclude} #Metadata={Count} Condition={Condition}")]
    public partial class ProjectItemElement : Microsoft.Build.Construction.ProjectElementContainer
    {
        internal ProjectItemElement() { }
        public string Exclude { [System.Diagnostics.DebuggerStepThroughAttribute]get { throw null; } set { } }
        public Microsoft.Build.Construction.ElementLocation ExcludeLocation { get { throw null; } }
        public bool HasMetadata { get { throw null; } }
        public string Include { [System.Diagnostics.DebuggerStepThroughAttribute]get { throw null; } set { } }
        public Microsoft.Build.Construction.ElementLocation IncludeLocation { get { throw null; } }
        public string ItemType { [System.Diagnostics.DebuggerStepThroughAttribute]get { throw null; } set { } }
        public string KeepDuplicates { [System.Diagnostics.DebuggerStepThroughAttribute]get { throw null; } set { } }
        public Microsoft.Build.Construction.ElementLocation KeepDuplicatesLocation { get { throw null; } }
        public string KeepMetadata { [System.Diagnostics.DebuggerStepThroughAttribute]get { throw null; } set { } }
        public Microsoft.Build.Construction.ElementLocation KeepMetadataLocation { get { throw null; } }
        public System.Collections.Generic.ICollection<Microsoft.Build.Construction.ProjectMetadataElement> Metadata { get { throw null; } }
        public string Remove { [System.Diagnostics.DebuggerStepThroughAttribute]get { throw null; } set { } }
        public Microsoft.Build.Construction.ElementLocation RemoveLocation { get { throw null; } }
        public string RemoveMetadata { [System.Diagnostics.DebuggerStepThroughAttribute]get { throw null; } set { } }
        public Microsoft.Build.Construction.ElementLocation RemoveMetadataLocation { get { throw null; } }
        public string Update { [System.Diagnostics.DebuggerStepThroughAttribute]get { throw null; } set { } }
        public Microsoft.Build.Construction.ElementLocation UpdateLocation { get { throw null; } }
        public Microsoft.Build.Construction.ProjectMetadataElement AddMetadata(string name, string unevaluatedValue) { throw null; }
        public Microsoft.Build.Construction.ProjectMetadataElement AddMetadata(string name, string unevaluatedValue, bool expressAsAttribute) { throw null; }
        public override void CopyFrom(Microsoft.Build.Construction.ProjectElement element) { }
        protected override Microsoft.Build.Construction.ProjectElement CreateNewInstance(Microsoft.Build.Construction.ProjectRootElement owner) { throw null; }
        protected override bool ShouldCloneXmlAttribute(System.Xml.XmlAttribute attribute) { throw null; }
    }
    [System.Diagnostics.DebuggerDisplayAttribute("#Items={Count} Condition={Condition} Label={Label}")]
    public partial class ProjectItemGroupElement : Microsoft.Build.Construction.ProjectElementContainer
    {
        internal ProjectItemGroupElement() { }
        public System.Collections.Generic.ICollection<Microsoft.Build.Construction.ProjectItemElement> Items { get { throw null; } }
        public Microsoft.Build.Construction.ProjectItemElement AddItem(string itemType, string include) { throw null; }
        public Microsoft.Build.Construction.ProjectItemElement AddItem(string itemType, string include, System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<string, string>> metadata) { throw null; }
        public override void CopyFrom(Microsoft.Build.Construction.ProjectElement element) { }
        protected override Microsoft.Build.Construction.ProjectElement CreateNewInstance(Microsoft.Build.Construction.ProjectRootElement owner) { throw null; }
    }
    [System.Diagnostics.DebuggerDisplayAttribute("{Name} Value={Value} Condition={Condition}")]
    public partial class ProjectMetadataElement : Microsoft.Build.Construction.ProjectElement
    {
        internal ProjectMetadataElement() { }
        public bool ExpressedAsAttribute { get { throw null; } set { } }
        public string Name { get { throw null; } set { } }
        public string Value { get { throw null; } set { } }
        protected override Microsoft.Build.Construction.ProjectElement CreateNewInstance(Microsoft.Build.Construction.ProjectRootElement owner) { throw null; }
    }
    [System.Diagnostics.DebuggerDisplayAttribute("ExecuteTargetsAttribute={ExecuteTargetsAttribute}")]
    public partial class ProjectOnErrorElement : Microsoft.Build.Construction.ProjectElement
    {
        internal ProjectOnErrorElement() { }
        public string ExecuteTargetsAttribute { [System.Diagnostics.DebuggerStepThroughAttribute]get { throw null; } set { } }
        public Microsoft.Build.Construction.ElementLocation ExecuteTargetsLocation { get { throw null; } }
        protected override Microsoft.Build.Construction.ProjectElement CreateNewInstance(Microsoft.Build.Construction.ProjectRootElement owner) { throw null; }
    }
    [System.Diagnostics.DebuggerDisplayAttribute("#Children={Count}")]
    public partial class ProjectOtherwiseElement : Microsoft.Build.Construction.ProjectElementContainer
    {
        internal ProjectOtherwiseElement() { }
        public System.Collections.Generic.ICollection<Microsoft.Build.Construction.ProjectChooseElement> ChooseElements { get { throw null; } }
        public override string Condition { get { throw null; } set { } }
        public override Microsoft.Build.Construction.ElementLocation ConditionLocation { get { throw null; } }
        public System.Collections.Generic.ICollection<Microsoft.Build.Construction.ProjectItemGroupElement> ItemGroups { get { throw null; } }
        public System.Collections.Generic.ICollection<Microsoft.Build.Construction.ProjectPropertyGroupElement> PropertyGroups { get { throw null; } }
        protected override Microsoft.Build.Construction.ProjectElement CreateNewInstance(Microsoft.Build.Construction.ProjectRootElement owner) { throw null; }
    }
    [System.Diagnostics.DebuggerDisplayAttribute("TaskParameter={TaskParameter} ItemType={ItemType} PropertyName={PropertyName} Condition={Condition}")]
    public partial class ProjectOutputElement : Microsoft.Build.Construction.ProjectElement
    {
        internal ProjectOutputElement() { }
        public bool IsOutputItem { get { throw null; } }
        public bool IsOutputProperty { get { throw null; } }
        public string ItemType { [System.Diagnostics.DebuggerStepThroughAttribute]get { throw null; } set { } }
        public Microsoft.Build.Construction.ElementLocation ItemTypeLocation { get { throw null; } }
        public string PropertyName { [System.Diagnostics.DebuggerStepThroughAttribute]get { throw null; } set { } }
        public Microsoft.Build.Construction.ElementLocation PropertyNameLocation { get { throw null; } }
        public string TaskParameter { [System.Diagnostics.DebuggerStepThroughAttribute]get { throw null; } [System.Diagnostics.DebuggerStepThroughAttribute]set { } }
        public Microsoft.Build.Construction.ElementLocation TaskParameterLocation { get { throw null; } }
        protected override Microsoft.Build.Construction.ProjectElement CreateNewInstance(Microsoft.Build.Construction.ProjectRootElement owner) { throw null; }
    }
    [System.Diagnostics.DebuggerDisplayAttribute("{Name} Value={Value} Condition={Condition}")]
    public partial class ProjectPropertyElement : Microsoft.Build.Construction.ProjectElement
    {
        internal ProjectPropertyElement() { }
        public string Name { get { throw null; } set { } }
        public string Value { get { throw null; } set { } }
        protected override Microsoft.Build.Construction.ProjectElement CreateNewInstance(Microsoft.Build.Construction.ProjectRootElement owner) { throw null; }
    }
    [System.Diagnostics.DebuggerDisplayAttribute("#Properties={Count} Condition={Condition} Label={Label}")]
    public partial class ProjectPropertyGroupElement : Microsoft.Build.Construction.ProjectElementContainer
    {
        internal ProjectPropertyGroupElement() { }
        public System.Collections.Generic.ICollection<Microsoft.Build.Construction.ProjectPropertyElement> Properties { get { throw null; } }
        public System.Collections.Generic.ICollection<Microsoft.Build.Construction.ProjectPropertyElement> PropertiesReversed { get { throw null; } }
        public Microsoft.Build.Construction.ProjectPropertyElement AddProperty(string name, string unevaluatedValue) { throw null; }
        protected override Microsoft.Build.Construction.ProjectElement CreateNewInstance(Microsoft.Build.Construction.ProjectRootElement owner) { throw null; }
        public Microsoft.Build.Construction.ProjectPropertyElement SetProperty(string name, string unevaluatedValue) { throw null; }
    }
    [System.Diagnostics.DebuggerDisplayAttribute("{FullPath} #Children={Count} DefaultTargets={DefaultTargets} ToolsVersion={ToolsVersion} InitialTargets={InitialTargets} ExplicitlyLoaded={IsExplicitlyLoaded}")]
    public partial class ProjectRootElement : Microsoft.Build.Construction.ProjectElementContainer
    {
        internal ProjectRootElement() { }
        public System.Collections.Generic.ICollection<Microsoft.Build.Construction.ProjectChooseElement> ChooseElements { get { throw null; } }
        public override string Condition { get { throw null; } set { } }
        public override Microsoft.Build.Construction.ElementLocation ConditionLocation { get { throw null; } }
        public string DefaultTargets { [System.Diagnostics.DebuggerStepThroughAttribute]get { throw null; } [System.Diagnostics.DebuggerStepThroughAttribute]set { } }
        public Microsoft.Build.Construction.ElementLocation DefaultTargetsLocation { get { throw null; } }
        public string DirectoryPath { [System.Diagnostics.DebuggerStepThroughAttribute]get { throw null; } }
        public System.Text.Encoding Encoding { get { throw null; } }
        public string FullPath { get { throw null; } set { } }
        public bool HasUnsavedChanges { get { throw null; } }
        public System.Collections.Generic.ICollection<Microsoft.Build.Construction.ProjectImportGroupElement> ImportGroups { get { throw null; } }
        public System.Collections.Generic.ICollection<Microsoft.Build.Construction.ProjectImportGroupElement> ImportGroupsReversed { get { throw null; } }
        public System.Collections.Generic.ICollection<Microsoft.Build.Construction.ProjectImportElement> Imports { get { throw null; } }
        public string InitialTargets { [System.Diagnostics.DebuggerStepThroughAttribute]get { throw null; } [System.Diagnostics.DebuggerStepThroughAttribute]set { } }
        public Microsoft.Build.Construction.ElementLocation InitialTargetsLocation { get { throw null; } }
        public System.Collections.Generic.ICollection<Microsoft.Build.Construction.ProjectItemDefinitionGroupElement> ItemDefinitionGroups { get { throw null; } }
        public System.Collections.Generic.ICollection<Microsoft.Build.Construction.ProjectItemDefinitionGroupElement> ItemDefinitionGroupsReversed { get { throw null; } }
        public System.Collections.Generic.ICollection<Microsoft.Build.Construction.ProjectItemDefinitionElement> ItemDefinitions { get { throw null; } }
        public System.Collections.Generic.ICollection<Microsoft.Build.Construction.ProjectItemGroupElement> ItemGroups { get { throw null; } }
        public System.Collections.Generic.ICollection<Microsoft.Build.Construction.ProjectItemGroupElement> ItemGroupsReversed { get { throw null; } }
        public System.Collections.Generic.ICollection<Microsoft.Build.Construction.ProjectItemElement> Items { get { throw null; } }
        public System.DateTime LastWriteTimeWhenRead { get { throw null; } }
        public bool PreserveFormatting { get { throw null; } }
        public Microsoft.Build.Construction.ElementLocation ProjectFileLocation { get { throw null; } }
        public System.Collections.Generic.ICollection<Microsoft.Build.Construction.ProjectPropertyElement> Properties { get { throw null; } }
        public System.Collections.Generic.ICollection<Microsoft.Build.Construction.ProjectPropertyGroupElement> PropertyGroups { get { throw null; } }
        public System.Collections.Generic.ICollection<Microsoft.Build.Construction.ProjectPropertyGroupElement> PropertyGroupsReversed { get { throw null; } }
        public string RawXml { get { throw null; } }
        public string Sdk { [System.Diagnostics.DebuggerStepThroughAttribute]get { throw null; } [System.Diagnostics.DebuggerStepThroughAttribute]set { } }
        public Microsoft.Build.Construction.ElementLocation SdkLocation { get { throw null; } }
        public System.Collections.Generic.ICollection<Microsoft.Build.Construction.ProjectTargetElement> Targets { get { throw null; } }
        public System.DateTime TimeLastChanged { get { throw null; } }
        public string ToolsVersion { [System.Diagnostics.DebuggerStepThroughAttribute]get { throw null; } [System.Diagnostics.DebuggerStepThroughAttribute]set { } }
        public Microsoft.Build.Construction.ElementLocation ToolsVersionLocation { get { throw null; } }
        public string TreatAsLocalProperty { [System.Diagnostics.DebuggerStepThroughAttribute]get { throw null; } [System.Diagnostics.DebuggerStepThroughAttribute]set { } }
        public Microsoft.Build.Construction.ElementLocation TreatAsLocalPropertyLocation { get { throw null; } }
        public System.Collections.Generic.ICollection<Microsoft.Build.Construction.ProjectUsingTaskElement> UsingTasks { get { throw null; } }
        public int Version { get { throw null; } }
        public Microsoft.Build.Construction.ProjectImportElement AddImport(string project) { throw null; }
        public Microsoft.Build.Construction.ProjectImportGroupElement AddImportGroup() { throw null; }
        public Microsoft.Build.Construction.ProjectItemElement AddItem(string itemType, string include) { throw null; }
        public Microsoft.Build.Construction.ProjectItemElement AddItem(string itemType, string include, System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<string, string>> metadata) { throw null; }
        public Microsoft.Build.Construction.ProjectItemDefinitionElement AddItemDefinition(string itemType) { throw null; }
        public Microsoft.Build.Construction.ProjectItemDefinitionGroupElement AddItemDefinitionGroup() { throw null; }
        public Microsoft.Build.Construction.ProjectItemGroupElement AddItemGroup() { throw null; }
        public Microsoft.Build.Construction.ProjectPropertyElement AddProperty(string name, string value) { throw null; }
        public Microsoft.Build.Construction.ProjectPropertyGroupElement AddPropertyGroup() { throw null; }
        public Microsoft.Build.Construction.ProjectTargetElement AddTarget(string name) { throw null; }
        public Microsoft.Build.Construction.ProjectUsingTaskElement AddUsingTask(string name, string assemblyFile, string assemblyName) { throw null; }
        public static Microsoft.Build.Construction.ProjectRootElement Create() { throw null; }
        public static Microsoft.Build.Construction.ProjectRootElement Create(Microsoft.Build.Evaluation.NewProjectFileOptions projectFileOptions) { throw null; }
        public static Microsoft.Build.Construction.ProjectRootElement Create(Microsoft.Build.Evaluation.ProjectCollection projectCollection) { throw null; }
        public static Microsoft.Build.Construction.ProjectRootElement Create(Microsoft.Build.Evaluation.ProjectCollection projectCollection, Microsoft.Build.Evaluation.NewProjectFileOptions projectFileOptions) { throw null; }
        public static Microsoft.Build.Construction.ProjectRootElement Create(string path) { throw null; }
        public static Microsoft.Build.Construction.ProjectRootElement Create(string path, Microsoft.Build.Evaluation.NewProjectFileOptions newProjectFileOptions) { throw null; }
        public static Microsoft.Build.Construction.ProjectRootElement Create(string path, Microsoft.Build.Evaluation.ProjectCollection projectCollection) { throw null; }
        public static Microsoft.Build.Construction.ProjectRootElement Create(string path, Microsoft.Build.Evaluation.ProjectCollection projectCollection, Microsoft.Build.Evaluation.NewProjectFileOptions newProjectFileOptions) { throw null; }
        public static Microsoft.Build.Construction.ProjectRootElement Create(System.Xml.XmlReader xmlReader) { throw null; }
        public static Microsoft.Build.Construction.ProjectRootElement Create(System.Xml.XmlReader xmlReader, Microsoft.Build.Evaluation.ProjectCollection projectCollection) { throw null; }
        public static Microsoft.Build.Construction.ProjectRootElement Create(System.Xml.XmlReader xmlReader, Microsoft.Build.Evaluation.ProjectCollection projectCollection, bool preserveFormatting) { throw null; }
        public Microsoft.Build.Construction.ProjectChooseElement CreateChooseElement() { throw null; }
        public Microsoft.Build.Construction.ProjectImportElement CreateImportElement(string project) { throw null; }
        public Microsoft.Build.Construction.ProjectImportGroupElement CreateImportGroupElement() { throw null; }
        public Microsoft.Build.Construction.ProjectItemDefinitionElement CreateItemDefinitionElement(string itemType) { throw null; }
        public Microsoft.Build.Construction.ProjectItemDefinitionGroupElement CreateItemDefinitionGroupElement() { throw null; }
        public Microsoft.Build.Construction.ProjectItemElement CreateItemElement(string itemType) { throw null; }
        public Microsoft.Build.Construction.ProjectItemElement CreateItemElement(string itemType, string include) { throw null; }
        public Microsoft.Build.Construction.ProjectItemGroupElement CreateItemGroupElement() { throw null; }
        public Microsoft.Build.Construction.ProjectMetadataElement CreateMetadataElement(string name) { throw null; }
        public Microsoft.Build.Construction.ProjectMetadataElement CreateMetadataElement(string name, string unevaluatedValue) { throw null; }
        protected override Microsoft.Build.Construction.ProjectElement CreateNewInstance(Microsoft.Build.Construction.ProjectRootElement owner) { throw null; }
        public Microsoft.Build.Construction.ProjectOnErrorElement CreateOnErrorElement(string executeTargets) { throw null; }
        public Microsoft.Build.Construction.ProjectOtherwiseElement CreateOtherwiseElement() { throw null; }
        public Microsoft.Build.Construction.ProjectOutputElement CreateOutputElement(string taskParameter, string itemType, string propertyName) { throw null; }
        public Microsoft.Build.Construction.ProjectExtensionsElement CreateProjectExtensionsElement() { throw null; }
        public Microsoft.Build.Construction.ProjectSdkElement CreateProjectSdkElement(string sdkName, string sdkVersion) { throw null; }
        public Microsoft.Build.Construction.ProjectPropertyElement CreatePropertyElement(string name) { throw null; }
        public Microsoft.Build.Construction.ProjectPropertyGroupElement CreatePropertyGroupElement() { throw null; }
        public Microsoft.Build.Construction.ProjectTargetElement CreateTargetElement(string name) { throw null; }
        public Microsoft.Build.Construction.ProjectTaskElement CreateTaskElement(string name) { throw null; }
        public Microsoft.Build.Construction.ProjectUsingTaskBodyElement CreateUsingTaskBodyElement(string evaluate, string body) { throw null; }
        public Microsoft.Build.Construction.ProjectUsingTaskElement CreateUsingTaskElement(string taskName, string assemblyFile, string assemblyName) { throw null; }
        public Microsoft.Build.Construction.ProjectUsingTaskElement CreateUsingTaskElement(string taskName, string assemblyFile, string assemblyName, string runtime, string architecture) { throw null; }
        public Microsoft.Build.Construction.ProjectUsingTaskParameterElement CreateUsingTaskParameterElement(string name, string output, string required, string parameterType) { throw null; }
        public Microsoft.Build.Construction.UsingTaskParameterGroupElement CreateUsingTaskParameterGroupElement() { throw null; }
        public Microsoft.Build.Construction.ProjectWhenElement CreateWhenElement(string condition) { throw null; }
        public Microsoft.Build.Construction.ProjectRootElement DeepClone() { throw null; }
        public static Microsoft.Build.Construction.ProjectRootElement Open(string path) { throw null; }
        public static Microsoft.Build.Construction.ProjectRootElement Open(string path, Microsoft.Build.Evaluation.ProjectCollection projectCollection) { throw null; }
        public static Microsoft.Build.Construction.ProjectRootElement Open(string path, Microsoft.Build.Evaluation.ProjectCollection projectCollection, System.Nullable<bool> preserveFormatting) { throw null; }
        public void Reload(bool throwIfUnsavedChanges=true, System.Nullable<bool> preserveFormatting=default(System.Nullable<bool>)) { }
        public void ReloadFrom(string path, bool throwIfUnsavedChanges=true, System.Nullable<bool> preserveFormatting=default(System.Nullable<bool>)) { }
        public void ReloadFrom(System.Xml.XmlReader reader, bool throwIfUnsavedChanges=true, System.Nullable<bool> preserveFormatting=default(System.Nullable<bool>)) { }
        public void Save() { }
        public void Save(System.IO.TextWriter writer) { }
        public void Save(string path) { }
        public void Save(string path, System.Text.Encoding encoding) { }
        public void Save(System.Text.Encoding saveEncoding) { }
        public static Microsoft.Build.Construction.ProjectRootElement TryOpen(string path) { throw null; }
        public static Microsoft.Build.Construction.ProjectRootElement TryOpen(string path, Microsoft.Build.Evaluation.ProjectCollection projectCollection) { throw null; }
        public static Microsoft.Build.Construction.ProjectRootElement TryOpen(string path, Microsoft.Build.Evaluation.ProjectCollection projectCollection, System.Nullable<bool> preserveFormatting) { throw null; }
    }
    public partial class ProjectSdkElement : Microsoft.Build.Construction.ProjectElementContainer
    {
        internal ProjectSdkElement() { }
        public string MinimumVersion { get { throw null; } set { } }
        public string Name { get { throw null; } set { } }
        public string Version { get { throw null; } set { } }
        protected override Microsoft.Build.Construction.ProjectElement CreateNewInstance(Microsoft.Build.Construction.ProjectRootElement owner) { throw null; }
    }
    [System.Diagnostics.DebuggerDisplayAttribute("Name={Name} #Children={Count} Condition={Condition}")]
    public partial class ProjectTargetElement : Microsoft.Build.Construction.ProjectElementContainer
    {
        internal ProjectTargetElement() { }
        public string AfterTargets { [System.Diagnostics.DebuggerStepThroughAttribute]get { throw null; } set { } }
        public Microsoft.Build.Construction.ElementLocation AfterTargetsLocation { get { throw null; } }
        public string BeforeTargets { [System.Diagnostics.DebuggerStepThroughAttribute]get { throw null; } set { } }
        public Microsoft.Build.Construction.ElementLocation BeforeTargetsLocation { get { throw null; } }
        public string DependsOnTargets { [System.Diagnostics.DebuggerStepThroughAttribute]get { throw null; } set { } }
        public Microsoft.Build.Construction.ElementLocation DependsOnTargetsLocation { get { throw null; } }
        public string Inputs { [System.Diagnostics.DebuggerStepThroughAttribute]get { throw null; } set { } }
        public Microsoft.Build.Construction.ElementLocation InputsLocation { get { throw null; } }
        public System.Collections.Generic.ICollection<Microsoft.Build.Construction.ProjectItemGroupElement> ItemGroups { get { throw null; } }
        public string KeepDuplicateOutputs { [System.Diagnostics.DebuggerStepThroughAttribute]get { throw null; } set { } }
        public Microsoft.Build.Construction.ElementLocation KeepDuplicateOutputsLocation { get { throw null; } }
        public string Name { [System.Diagnostics.DebuggerStepThroughAttribute]get { throw null; } set { } }
        public Microsoft.Build.Construction.ElementLocation NameLocation { get { throw null; } }
        public System.Collections.Generic.ICollection<Microsoft.Build.Construction.ProjectOnErrorElement> OnErrors { get { throw null; } }
        public string Outputs { [System.Diagnostics.DebuggerStepThroughAttribute]get { throw null; } set { } }
        public Microsoft.Build.Construction.ElementLocation OutputsLocation { get { throw null; } }
        public System.Collections.Generic.ICollection<Microsoft.Build.Construction.ProjectPropertyGroupElement> PropertyGroups { get { throw null; } }
        public string Returns { [System.Diagnostics.DebuggerStepThroughAttribute]get { throw null; } set { } }
        public Microsoft.Build.Construction.ElementLocation ReturnsLocation { get { throw null; } }
        public System.Collections.Generic.ICollection<Microsoft.Build.Construction.ProjectTaskElement> Tasks { get { throw null; } }
        public Microsoft.Build.Construction.ProjectItemGroupElement AddItemGroup() { throw null; }
        public Microsoft.Build.Construction.ProjectPropertyGroupElement AddPropertyGroup() { throw null; }
        public Microsoft.Build.Construction.ProjectTaskElement AddTask(string taskName) { throw null; }
        public override void CopyFrom(Microsoft.Build.Construction.ProjectElement element) { }
        protected override Microsoft.Build.Construction.ProjectElement CreateNewInstance(Microsoft.Build.Construction.ProjectRootElement owner) { throw null; }
    }
    [System.Diagnostics.DebuggerDisplayAttribute("{Name} Condition={Condition} ContinueOnError={ContinueOnError} MSBuildRuntime={MSBuildRuntime} MSBuildArchitecture={MSBuildArchitecture} #Outputs={Count}")]
    public partial class ProjectTaskElement : Microsoft.Build.Construction.ProjectElementContainer
    {
        internal ProjectTaskElement() { }
        public string ContinueOnError { [System.Diagnostics.DebuggerStepThroughAttribute]get { throw null; } [System.Diagnostics.DebuggerStepThroughAttribute]set { } }
        public Microsoft.Build.Construction.ElementLocation ContinueOnErrorLocation { get { throw null; } }
        public string MSBuildArchitecture { [System.Diagnostics.DebuggerStepThroughAttribute]get { throw null; } [System.Diagnostics.DebuggerStepThroughAttribute]set { } }
        public Microsoft.Build.Construction.ElementLocation MSBuildArchitectureLocation { get { throw null; } }
        public string MSBuildRuntime { [System.Diagnostics.DebuggerStepThroughAttribute]get { throw null; } [System.Diagnostics.DebuggerStepThroughAttribute]set { } }
        public Microsoft.Build.Construction.ElementLocation MSBuildRuntimeLocation { get { throw null; } }
        public string Name { get { throw null; } }
        public System.Collections.Generic.ICollection<Microsoft.Build.Construction.ProjectOutputElement> Outputs { get { throw null; } }
        public System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<string, Microsoft.Build.Construction.ElementLocation>> ParameterLocations { get { throw null; } }
        public System.Collections.Generic.IDictionary<string, string> Parameters { get { throw null; } }
        public Microsoft.Build.Construction.ProjectOutputElement AddOutputItem(string taskParameter, string itemType) { throw null; }
        public Microsoft.Build.Construction.ProjectOutputElement AddOutputItem(string taskParameter, string itemType, string condition) { throw null; }
        public Microsoft.Build.Construction.ProjectOutputElement AddOutputProperty(string taskParameter, string propertyName) { throw null; }
        public Microsoft.Build.Construction.ProjectOutputElement AddOutputProperty(string taskParameter, string propertyName, string condition) { throw null; }
        public override void CopyFrom(Microsoft.Build.Construction.ProjectElement element) { }
        protected override Microsoft.Build.Construction.ProjectElement CreateNewInstance(Microsoft.Build.Construction.ProjectRootElement owner) { throw null; }
        public string GetParameter(string name) { throw null; }
        public void RemoveAllParameters() { }
        public void RemoveParameter(string name) { }
        public void SetParameter(string name, string unevaluatedValue) { }
    }
    [System.Diagnostics.DebuggerDisplayAttribute("Evaluate={Evaluate} TaskBody={TaskBody}")]
    public partial class ProjectUsingTaskBodyElement : Microsoft.Build.Construction.ProjectElement
    {
        internal ProjectUsingTaskBodyElement() { }
        public override string Condition { get { throw null; } set { } }
        public override Microsoft.Build.Construction.ElementLocation ConditionLocation { get { throw null; } }
        public string Evaluate { get { throw null; } set { } }
        public Microsoft.Build.Construction.ElementLocation EvaluateLocation { get { throw null; } }
        public string TaskBody { get { throw null; } set { } }
        protected override Microsoft.Build.Construction.ProjectElement CreateNewInstance(Microsoft.Build.Construction.ProjectRootElement owner) { throw null; }
    }
    [System.Diagnostics.DebuggerDisplayAttribute("TaskName={TaskName} AssemblyName={AssemblyName} AssemblyFile={AssemblyFile} Condition={Condition} Runtime={Runtime} Architecture={Architecture}")]
    public partial class ProjectUsingTaskElement : Microsoft.Build.Construction.ProjectElementContainer
    {
        internal ProjectUsingTaskElement() { }
        public string Architecture { get { throw null; } set { } }
        public Microsoft.Build.Construction.ElementLocation ArchitectureLocation { get { throw null; } }
        public string AssemblyFile { get { throw null; } set { } }
        public Microsoft.Build.Construction.ElementLocation AssemblyFileLocation { get { throw null; } }
        public string AssemblyName { get { throw null; } set { } }
        public Microsoft.Build.Construction.ElementLocation AssemblyNameLocation { get { throw null; } }
        public Microsoft.Build.Construction.UsingTaskParameterGroupElement ParameterGroup { get { throw null; } }
        public string Runtime { get { throw null; } set { } }
        public Microsoft.Build.Construction.ElementLocation RuntimeLocation { get { throw null; } }
        public Microsoft.Build.Construction.ProjectUsingTaskBodyElement TaskBody { get { throw null; } }
        public string TaskFactory { get { throw null; } set { } }
        public Microsoft.Build.Construction.ElementLocation TaskFactoryLocation { get { throw null; } }
        public string TaskName { get { throw null; } set { } }
        public Microsoft.Build.Construction.ElementLocation TaskNameLocation { get { throw null; } }
        public Microsoft.Build.Construction.UsingTaskParameterGroupElement AddParameterGroup() { throw null; }
        public Microsoft.Build.Construction.ProjectUsingTaskBodyElement AddUsingTaskBody(string evaluate, string taskBody) { throw null; }
        protected override Microsoft.Build.Construction.ProjectElement CreateNewInstance(Microsoft.Build.Construction.ProjectRootElement owner) { throw null; }
    }
    [System.Diagnostics.DebuggerDisplayAttribute("Name={Name} ParameterType={ParameterType} Output={Output} Required={Required}")]
    public partial class ProjectUsingTaskParameterElement : Microsoft.Build.Construction.ProjectElement
    {
        internal ProjectUsingTaskParameterElement() { }
        public override string Condition { get { throw null; } set { } }
        public override Microsoft.Build.Construction.ElementLocation ConditionLocation { get { throw null; } }
        public string Name { [System.Diagnostics.DebuggerStepThroughAttribute]get { throw null; } set { } }
        public string Output { get { throw null; } set { } }
        public Microsoft.Build.Construction.ElementLocation OutputLocation { get { throw null; } }
        public string ParameterType { get { throw null; } set { } }
        public Microsoft.Build.Construction.ElementLocation ParameterTypeLocation { get { throw null; } }
        public string Required { get { throw null; } set { } }
        public Microsoft.Build.Construction.ElementLocation RequiredLocation { get { throw null; } }
        protected override Microsoft.Build.Construction.ProjectElement CreateNewInstance(Microsoft.Build.Construction.ProjectRootElement owner) { throw null; }
    }
    [System.Diagnostics.DebuggerDisplayAttribute("#Children={Count} Condition={Condition}")]
    public partial class ProjectWhenElement : Microsoft.Build.Construction.ProjectElementContainer
    {
        internal ProjectWhenElement() { }
        public System.Collections.Generic.ICollection<Microsoft.Build.Construction.ProjectChooseElement> ChooseElements { get { throw null; } }
        public System.Collections.Generic.ICollection<Microsoft.Build.Construction.ProjectItemGroupElement> ItemGroups { get { throw null; } }
        public System.Collections.Generic.ICollection<Microsoft.Build.Construction.ProjectPropertyGroupElement> PropertyGroups { get { throw null; } }
        protected override Microsoft.Build.Construction.ProjectElement CreateNewInstance(Microsoft.Build.Construction.ProjectRootElement owner) { throw null; }
    }
    public sealed partial class SolutionConfigurationInSolution
    {
        internal SolutionConfigurationInSolution() { }
        public string ConfigurationName { get { throw null; } }
        public string FullName { get { throw null; } }
        public string PlatformName { get { throw null; } }
    }
    public sealed partial class SolutionFile
    {
        internal SolutionFile() { }
        public System.Collections.Generic.IReadOnlyDictionary<string, Microsoft.Build.Construction.ProjectInSolution> ProjectsByGuid { get { throw null; } }
        public System.Collections.Generic.IReadOnlyList<Microsoft.Build.Construction.ProjectInSolution> ProjectsInOrder { get { throw null; } }
        public System.Collections.Generic.IReadOnlyList<Microsoft.Build.Construction.SolutionConfigurationInSolution> SolutionConfigurations { get { throw null; } }
        public string GetDefaultConfigurationName() { throw null; }
        public string GetDefaultPlatformName() { throw null; }
        public static Microsoft.Build.Construction.SolutionFile Parse(string solutionFile) { throw null; }
    }
    public enum SolutionProjectType
    {
        EtpSubProject = 5,
        KnownToBeMSBuildFormat = 1,
        SharedProject = 6,
        SolutionFolder = 2,
        Unknown = 0,
        WebDeploymentProject = 4,
        WebProject = 3,
    }
    [System.Diagnostics.DebuggerDisplayAttribute("#Parameters={Count}")]
    public partial class UsingTaskParameterGroupElement : Microsoft.Build.Construction.ProjectElementContainer
    {
        internal UsingTaskParameterGroupElement() { }
        public override string Condition { get { throw null; } set { } }
        public override Microsoft.Build.Construction.ElementLocation ConditionLocation { get { throw null; } }
        public System.Collections.Generic.ICollection<Microsoft.Build.Construction.ProjectUsingTaskParameterElement> Parameters { get { throw null; } }
        public Microsoft.Build.Construction.ProjectUsingTaskParameterElement AddParameter(string name) { throw null; }
        public Microsoft.Build.Construction.ProjectUsingTaskParameterElement AddParameter(string name, string output, string required, string parameterType) { throw null; }
        protected override Microsoft.Build.Construction.ProjectElement CreateNewInstance(Microsoft.Build.Construction.ProjectRootElement owner) { throw null; }
    }
}
namespace Microsoft.Build.Definition
{
    public partial class ProjectOptions
    {
        public ProjectOptions() { }
        public Microsoft.Build.Evaluation.Context.EvaluationContext EvaluationContext { [System.Runtime.CompilerServices.CompilerGeneratedAttribute]get { throw null; } [System.Runtime.CompilerServices.CompilerGeneratedAttribute]set { } }
        public System.Collections.Generic.IDictionary<string, string> GlobalProperties { [System.Runtime.CompilerServices.CompilerGeneratedAttribute]get { throw null; } [System.Runtime.CompilerServices.CompilerGeneratedAttribute]set { } }
        public Microsoft.Build.Evaluation.ProjectLoadSettings LoadSettings { [System.Runtime.CompilerServices.CompilerGeneratedAttribute]get { throw null; } [System.Runtime.CompilerServices.CompilerGeneratedAttribute]set { } }
        public Microsoft.Build.Evaluation.ProjectCollection ProjectCollection { [System.Runtime.CompilerServices.CompilerGeneratedAttribute]get { throw null; } [System.Runtime.CompilerServices.CompilerGeneratedAttribute]set { } }
        public string SubToolsetVersion { [System.Runtime.CompilerServices.CompilerGeneratedAttribute]get { throw null; } [System.Runtime.CompilerServices.CompilerGeneratedAttribute]set { } }
        public string ToolsVersion { [System.Runtime.CompilerServices.CompilerGeneratedAttribute]get { throw null; } [System.Runtime.CompilerServices.CompilerGeneratedAttribute]set { } }
    }
}
namespace Microsoft.Build.Evaluation
{
    public partial class GlobResult
    {
        public GlobResult(Microsoft.Build.Construction.ProjectItemElement itemElement, System.Collections.Generic.IEnumerable<string> includeGlobStrings, Microsoft.Build.Globbing.IMSBuildGlob globWithGaps, System.Collections.Generic.IEnumerable<string> excludeFragmentStrings, System.Collections.Generic.IEnumerable<string> removeFragmentStrings) { }
        public System.Collections.Generic.IEnumerable<string> Excludes { [System.Runtime.CompilerServices.CompilerGeneratedAttribute]get { throw null; } }
        public System.Collections.Generic.IEnumerable<string> IncludeGlobs { [System.Runtime.CompilerServices.CompilerGeneratedAttribute]get { throw null; } }
        public Microsoft.Build.Construction.ProjectItemElement ItemElement { [System.Runtime.CompilerServices.CompilerGeneratedAttribute]get { throw null; } }
        public Microsoft.Build.Globbing.IMSBuildGlob MsBuildGlob { [System.Runtime.CompilerServices.CompilerGeneratedAttribute]get { throw null; } [System.Runtime.CompilerServices.CompilerGeneratedAttribute]set { } }
        public System.Collections.Generic.IEnumerable<string> Removes { [System.Runtime.CompilerServices.CompilerGeneratedAttribute]get { throw null; } [System.Runtime.CompilerServices.CompilerGeneratedAttribute]set { } }
    }
    [System.FlagsAttribute]
    public enum NewProjectFileOptions
    {
        IncludeAllOptions = -1,
        IncludeToolsVersion = 2,
        IncludeXmlDeclaration = 1,
        IncludeXmlNamespace = 4,
        None = 0,
    }
    public enum Operation
    {
        Exclude = 1,
        Include = 0,
        Remove = 3,
        Update = 2,
    }
    [System.Diagnostics.DebuggerDisplayAttribute("{FullPath} EffectiveToolsVersion={ToolsVersion} #GlobalProperties={_data._globalProperties.Count} #Properties={_data.Properties.Count} #ItemTypes={_data.ItemTypes.Count} #ItemDefinitions={_data.ItemDefinitions.Count} #Items={_data.Items.Count} #Targets={_data.Targets.Count}")]
    public partial class Project
    {
        public Project() { }
        public Project(Microsoft.Build.Construction.ProjectRootElement xml) { }
        public Project(Microsoft.Build.Construction.ProjectRootElement xml, System.Collections.Generic.IDictionary<string, string> globalProperties, string toolsVersion) { }
        public Project(Microsoft.Build.Construction.ProjectRootElement xml, System.Collections.Generic.IDictionary<string, string> globalProperties, string toolsVersion, Microsoft.Build.Evaluation.ProjectCollection projectCollection) { }
        public Project(Microsoft.Build.Construction.ProjectRootElement xml, System.Collections.Generic.IDictionary<string, string> globalProperties, string toolsVersion, Microsoft.Build.Evaluation.ProjectCollection projectCollection, Microsoft.Build.Evaluation.ProjectLoadSettings loadSettings) { }
        public Project(Microsoft.Build.Construction.ProjectRootElement xml, System.Collections.Generic.IDictionary<string, string> globalProperties, string toolsVersion, string subToolsetVersion, Microsoft.Build.Evaluation.ProjectCollection projectCollection, Microsoft.Build.Evaluation.ProjectLoadSettings loadSettings) { }
        public Project(Microsoft.Build.Evaluation.NewProjectFileOptions newProjectFileOptions) { }
        public Project(Microsoft.Build.Evaluation.ProjectCollection projectCollection) { }
        public Project(Microsoft.Build.Evaluation.ProjectCollection projectCollection, Microsoft.Build.Evaluation.NewProjectFileOptions newProjectFileOptions) { }
        public Project(System.Collections.Generic.IDictionary<string, string> globalProperties, string toolsVersion, Microsoft.Build.Evaluation.ProjectCollection projectCollection) { }
        public Project(System.Collections.Generic.IDictionary<string, string> globalProperties, string toolsVersion, Microsoft.Build.Evaluation.ProjectCollection projectCollection, Microsoft.Build.Evaluation.NewProjectFileOptions newProjectFileOptions) { }
        public Project(string projectFile) { }
        public Project(string projectFile, System.Collections.Generic.IDictionary<string, string> globalProperties, string toolsVersion) { }
        public Project(string projectFile, System.Collections.Generic.IDictionary<string, string> globalProperties, string toolsVersion, Microsoft.Build.Evaluation.ProjectCollection projectCollection) { }
        public Project(string projectFile, System.Collections.Generic.IDictionary<string, string> globalProperties, string toolsVersion, Microsoft.Build.Evaluation.ProjectCollection projectCollection, Microsoft.Build.Evaluation.ProjectLoadSettings loadSettings) { }
        public Project(string projectFile, System.Collections.Generic.IDictionary<string, string> globalProperties, string toolsVersion, string subToolsetVersion, Microsoft.Build.Evaluation.ProjectCollection projectCollection, Microsoft.Build.Evaluation.ProjectLoadSettings loadSettings) { }
        public Project(System.Xml.XmlReader xmlReader) { }
        public Project(System.Xml.XmlReader xmlReader, System.Collections.Generic.IDictionary<string, string> globalProperties, string toolsVersion) { }
        public Project(System.Xml.XmlReader xmlReader, System.Collections.Generic.IDictionary<string, string> globalProperties, string toolsVersion, Microsoft.Build.Evaluation.ProjectCollection projectCollection) { }
        public Project(System.Xml.XmlReader xmlReader, System.Collections.Generic.IDictionary<string, string> globalProperties, string toolsVersion, Microsoft.Build.Evaluation.ProjectCollection projectCollection, Microsoft.Build.Evaluation.ProjectLoadSettings loadSettings) { }
        public Project(System.Xml.XmlReader xmlReader, System.Collections.Generic.IDictionary<string, string> globalProperties, string toolsVersion, string subToolsetVersion, Microsoft.Build.Evaluation.ProjectCollection projectCollection, Microsoft.Build.Evaluation.ProjectLoadSettings loadSettings) { }
        public System.Collections.Generic.ICollection<Microsoft.Build.Evaluation.ProjectMetadata> AllEvaluatedItemDefinitionMetadata { get { throw null; } }
        public System.Collections.Generic.ICollection<Microsoft.Build.Evaluation.ProjectItem> AllEvaluatedItems { get { throw null; } }
        public System.Collections.Generic.ICollection<Microsoft.Build.Evaluation.ProjectProperty> AllEvaluatedProperties { get { throw null; } }
        public System.Collections.Generic.IDictionary<string, System.Collections.Generic.List<string>> ConditionedProperties { [System.Diagnostics.DebuggerStepThroughAttribute]get { throw null; } }
        public string DirectoryPath { [System.Diagnostics.DebuggerStepThroughAttribute]get { throw null; } }
        public bool DisableMarkDirty { [System.Runtime.CompilerServices.CompilerGeneratedAttribute]get { throw null; } [System.Runtime.CompilerServices.CompilerGeneratedAttribute]set { } }
        public int EvaluationCounter { get { throw null; } }
        public string FullPath { [System.Diagnostics.DebuggerStepThroughAttribute]get { throw null; } [System.Diagnostics.DebuggerStepThroughAttribute]set { } }
        public System.Collections.Generic.IDictionary<string, string> GlobalProperties { [System.Diagnostics.DebuggerStepThroughAttribute]get { throw null; } }
        public System.Collections.Generic.IList<Microsoft.Build.Evaluation.ResolvedImport> Imports { get { throw null; } }
        public System.Collections.Generic.IList<Microsoft.Build.Evaluation.ResolvedImport> ImportsIncludingDuplicates { get { throw null; } }
        public bool IsBuildEnabled { get { throw null; } set { } }
        public bool IsDirty { get { throw null; } }
        public System.Collections.Generic.IDictionary<string, Microsoft.Build.Evaluation.ProjectItemDefinition> ItemDefinitions { [System.Diagnostics.DebuggerStepThroughAttribute]get { throw null; } }
        public System.Collections.Generic.ICollection<Microsoft.Build.Evaluation.ProjectItem> Items { [System.Diagnostics.DebuggerStepThroughAttribute]get { throw null; } }
        public System.Collections.Generic.ICollection<Microsoft.Build.Evaluation.ProjectItem> ItemsIgnoringCondition { [System.Diagnostics.DebuggerStepThroughAttribute]get { throw null; } }
        public System.Collections.Generic.ICollection<string> ItemTypes { [System.Diagnostics.DebuggerStepThroughAttribute]get { throw null; } }
        public int LastEvaluationId { get { throw null; } }
        public Microsoft.Build.Evaluation.ProjectCollection ProjectCollection { [System.Diagnostics.DebuggerStepThroughAttribute]get { throw null; } }
        public Microsoft.Build.Construction.ElementLocation ProjectFileLocation { get { throw null; } }
        public System.Collections.Generic.ICollection<Microsoft.Build.Evaluation.ProjectProperty> Properties { [System.Diagnostics.DebuggerStepThroughAttribute]get { throw null; } }
        public bool SkipEvaluation { [System.Runtime.CompilerServices.CompilerGeneratedAttribute]get { throw null; } [System.Runtime.CompilerServices.CompilerGeneratedAttribute]set { } }
        public string SubToolsetVersion { get { throw null; } }
        public System.Collections.Generic.IDictionary<string, Microsoft.Build.Execution.ProjectTargetInstance> Targets { [System.Diagnostics.DebuggerStepThroughAttribute]get { throw null; } }
        public bool ThrowInsteadOfSplittingItemElement { [System.Runtime.CompilerServices.CompilerGeneratedAttribute]get { throw null; } [System.Runtime.CompilerServices.CompilerGeneratedAttribute]set { } }
        public string ToolsVersion { get { throw null; } }
        public Microsoft.Build.Construction.ProjectRootElement Xml { [System.Diagnostics.DebuggerStepThroughAttribute]get { throw null; } }
        public System.Collections.Generic.IList<Microsoft.Build.Evaluation.ProjectItem> AddItem(string itemType, string unevaluatedInclude) { throw null; }
        public System.Collections.Generic.IList<Microsoft.Build.Evaluation.ProjectItem> AddItem(string itemType, string unevaluatedInclude, System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<string, string>> metadata) { throw null; }
        public System.Collections.Generic.IList<Microsoft.Build.Evaluation.ProjectItem> AddItemFast(string itemType, string unevaluatedInclude) { throw null; }
        public System.Collections.Generic.IList<Microsoft.Build.Evaluation.ProjectItem> AddItemFast(string itemType, string unevaluatedInclude, System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<string, string>> metadata) { throw null; }
        public bool Build() { throw null; }
        public bool Build(Microsoft.Build.Framework.ILogger logger) { throw null; }
        public bool Build(System.Collections.Generic.IEnumerable<Microsoft.Build.Framework.ILogger> loggers) { throw null; }
        public bool Build(System.Collections.Generic.IEnumerable<Microsoft.Build.Framework.ILogger> loggers, System.Collections.Generic.IEnumerable<Microsoft.Build.Logging.ForwardingLoggerRecord> remoteLoggers) { throw null; }
        public bool Build(string target) { throw null; }
        public bool Build(string target, System.Collections.Generic.IEnumerable<Microsoft.Build.Framework.ILogger> loggers) { throw null; }
        public bool Build(string target, System.Collections.Generic.IEnumerable<Microsoft.Build.Framework.ILogger> loggers, System.Collections.Generic.IEnumerable<Microsoft.Build.Logging.ForwardingLoggerRecord> remoteLoggers) { throw null; }
        public bool Build(string[] targets) { throw null; }
        public bool Build(string[] targets, System.Collections.Generic.IEnumerable<Microsoft.Build.Framework.ILogger> loggers) { throw null; }
        public bool Build(string[] targets, System.Collections.Generic.IEnumerable<Microsoft.Build.Framework.ILogger> loggers, System.Collections.Generic.IEnumerable<Microsoft.Build.Logging.ForwardingLoggerRecord> remoteLoggers) { throw null; }
        public Microsoft.Build.Execution.ProjectInstance CreateProjectInstance() { throw null; }
        public Microsoft.Build.Execution.ProjectInstance CreateProjectInstance(Microsoft.Build.Execution.ProjectInstanceSettings settings) { throw null; }
        public string ExpandString(string unexpandedValue) { throw null; }
        public static Microsoft.Build.Evaluation.Project FromFile(string file, Microsoft.Build.Definition.ProjectOptions options) { throw null; }
        public static Microsoft.Build.Evaluation.Project FromProjectRootElement(Microsoft.Build.Construction.ProjectRootElement rootElement, Microsoft.Build.Definition.ProjectOptions options) { throw null; }
        public static Microsoft.Build.Evaluation.Project FromXmlReader(System.Xml.XmlReader reader, Microsoft.Build.Definition.ProjectOptions options) { throw null; }
        public System.Collections.Generic.List<Microsoft.Build.Evaluation.GlobResult> GetAllGlobs() { throw null; }
        public System.Collections.Generic.List<Microsoft.Build.Evaluation.GlobResult> GetAllGlobs(string itemType) { throw null; }
        public static string GetEvaluatedItemIncludeEscaped(Microsoft.Build.Evaluation.ProjectItem item) { throw null; }
        public static string GetEvaluatedItemIncludeEscaped(Microsoft.Build.Evaluation.ProjectItemDefinition item) { throw null; }
        public System.Collections.Generic.List<Microsoft.Build.Evaluation.ProvenanceResult> GetItemProvenance(Microsoft.Build.Evaluation.ProjectItem item) { throw null; }
        public System.Collections.Generic.List<Microsoft.Build.Evaluation.ProvenanceResult> GetItemProvenance(string itemToMatch) { throw null; }
        public System.Collections.Generic.List<Microsoft.Build.Evaluation.ProvenanceResult> GetItemProvenance(string itemToMatch, string itemType) { throw null; }
        public System.Collections.Generic.ICollection<Microsoft.Build.Evaluation.ProjectItem> GetItems(string itemType) { throw null; }
        public System.Collections.Generic.ICollection<Microsoft.Build.Evaluation.ProjectItem> GetItemsByEvaluatedInclude(string evaluatedInclude) { throw null; }
        public System.Collections.Generic.ICollection<Microsoft.Build.Evaluation.ProjectItem> GetItemsIgnoringCondition(string itemType) { throw null; }
        public System.Collections.Generic.IEnumerable<Microsoft.Build.Construction.ProjectElement> GetLogicalProject() { throw null; }
        public static string GetMetadataValueEscaped(Microsoft.Build.Evaluation.ProjectItem item, string name) { throw null; }
        public static string GetMetadataValueEscaped(Microsoft.Build.Evaluation.ProjectItemDefinition item, string name) { throw null; }
        public static string GetMetadataValueEscaped(Microsoft.Build.Evaluation.ProjectMetadata metadatum) { throw null; }
        [System.Diagnostics.DebuggerStepThroughAttribute]
        public Microsoft.Build.Evaluation.ProjectProperty GetProperty(string name) { throw null; }
        public string GetPropertyValue(string name) { throw null; }
        public static string GetPropertyValueEscaped(Microsoft.Build.Evaluation.ProjectProperty property) { throw null; }
        public void MarkDirty() { }
        public void ReevaluateIfNecessary() { }
        public void ReevaluateIfNecessary(Microsoft.Build.Evaluation.Context.EvaluationContext evaluationContext) { }
        public bool RemoveGlobalProperty(string name) { throw null; }
        public bool RemoveItem(Microsoft.Build.Evaluation.ProjectItem item) { throw null; }
        public void RemoveItems(System.Collections.Generic.IEnumerable<Microsoft.Build.Evaluation.ProjectItem> items) { }
        public bool RemoveProperty(Microsoft.Build.Evaluation.ProjectProperty property) { throw null; }
        public void Save() { }
        public void Save(System.IO.TextWriter writer) { }
        public void Save(string path) { }
        public void Save(string path, System.Text.Encoding encoding) { }
        public void Save(System.Text.Encoding encoding) { }
        public void SaveLogicalProject(System.IO.TextWriter writer) { }
        public bool SetGlobalProperty(string name, string escapedValue) { throw null; }
        public Microsoft.Build.Evaluation.ProjectProperty SetProperty(string name, string unevaluatedValue) { throw null; }
    }
    public partial class ProjectChangedEventArgs : System.EventArgs
    {
        internal ProjectChangedEventArgs() { }
        public Microsoft.Build.Evaluation.Project Project { [System.Runtime.CompilerServices.CompilerGeneratedAttribute]get { throw null; } }
    }
    public partial class ProjectCollection : System.IDisposable
    {
        public ProjectCollection() { }
        public ProjectCollection(Microsoft.Build.Evaluation.ToolsetDefinitionLocations toolsetLocations) { }
        public ProjectCollection(System.Collections.Generic.IDictionary<string, string> globalProperties) { }
        public ProjectCollection(System.Collections.Generic.IDictionary<string, string> globalProperties, System.Collections.Generic.IEnumerable<Microsoft.Build.Framework.ILogger> loggers, Microsoft.Build.Evaluation.ToolsetDefinitionLocations toolsetDefinitionLocations) { }
        public ProjectCollection(System.Collections.Generic.IDictionary<string, string> globalProperties, System.Collections.Generic.IEnumerable<Microsoft.Build.Framework.ILogger> loggers, System.Collections.Generic.IEnumerable<Microsoft.Build.Logging.ForwardingLoggerRecord> remoteLoggers, Microsoft.Build.Evaluation.ToolsetDefinitionLocations toolsetDefinitionLocations, int maxNodeCount, bool onlyLogCriticalEvents) { }
        public int Count { get { throw null; } }
        public string DefaultToolsVersion { get { throw null; } set { } }
        public bool DisableMarkDirty { get { throw null; } set { } }
        public static Microsoft.Build.Evaluation.ProjectCollection GlobalProjectCollection { get { throw null; } }
        public System.Collections.Generic.IDictionary<string, string> GlobalProperties { get { throw null; } }
        public Microsoft.Build.Execution.HostServices HostServices { get { throw null; } set { } }
        public bool IsBuildEnabled { [System.Diagnostics.DebuggerStepThroughAttribute]get { throw null; } [System.Diagnostics.DebuggerStepThroughAttribute]set { } }
        public System.Collections.Generic.ICollection<Microsoft.Build.Evaluation.Project> LoadedProjects { get { throw null; } }
        public System.Collections.Generic.ICollection<Microsoft.Build.Framework.ILogger> Loggers { [System.Diagnostics.DebuggerStepThroughAttribute]get { throw null; } }
        public bool OnlyLogCriticalEvents { get { throw null; } set { } }
        public bool SkipEvaluation { get { throw null; } set { } }
        public Microsoft.Build.Evaluation.ToolsetDefinitionLocations ToolsetLocations { [System.Diagnostics.DebuggerStepThroughAttribute]get { throw null; } }
        public System.Collections.Generic.ICollection<Microsoft.Build.Evaluation.Toolset> Toolsets { get { throw null; } }
        public static System.Version Version { get { throw null; } }
        public event Microsoft.Build.Evaluation.ProjectCollection.ProjectAddedEventHandler ProjectAdded { add { } remove { } }
        public event System.EventHandler<Microsoft.Build.Evaluation.ProjectChangedEventArgs> ProjectChanged { add { } remove { } }
        public event System.EventHandler<Microsoft.Build.Evaluation.ProjectCollectionChangedEventArgs> ProjectCollectionChanged { add { } remove { } }
        public event System.EventHandler<Microsoft.Build.Evaluation.ProjectXmlChangedEventArgs> ProjectXmlChanged { add { } remove { } }
        public void AddToolset(Microsoft.Build.Evaluation.Toolset toolset) { }
        public bool ContainsToolset(string toolsVersion) { throw null; }
        public void Dispose() { }
        protected virtual void Dispose(bool disposing) { }
        public static string Escape(string unescapedString) { throw null; }
        public string GetEffectiveToolsVersion(string explicitToolsVersion, string toolsVersionFromProject) { throw null; }
        public Microsoft.Build.Execution.ProjectPropertyInstance GetGlobalProperty(string name) { throw null; }
        public System.Collections.Generic.ICollection<Microsoft.Build.Evaluation.Project> GetLoadedProjects(string fullPath) { throw null; }
        public Microsoft.Build.Evaluation.Toolset GetToolset(string toolsVersion) { throw null; }
        public Microsoft.Build.Evaluation.Project LoadProject(string fileName) { throw null; }
        public Microsoft.Build.Evaluation.Project LoadProject(string fileName, System.Collections.Generic.IDictionary<string, string> globalProperties, string toolsVersion) { throw null; }
        public Microsoft.Build.Evaluation.Project LoadProject(string fileName, string toolsVersion) { throw null; }
        public Microsoft.Build.Evaluation.Project LoadProject(System.Xml.XmlReader xmlReader) { throw null; }
        public Microsoft.Build.Evaluation.Project LoadProject(System.Xml.XmlReader xmlReader, System.Collections.Generic.IDictionary<string, string> globalProperties, string toolsVersion) { throw null; }
        public Microsoft.Build.Evaluation.Project LoadProject(System.Xml.XmlReader xmlReader, string toolsVersion) { throw null; }
        public void RegisterForwardingLoggers(System.Collections.Generic.IEnumerable<Microsoft.Build.Logging.ForwardingLoggerRecord> remoteLoggers) { }
        public void RegisterLogger(Microsoft.Build.Framework.ILogger logger) { }
        public void RegisterLoggers(System.Collections.Generic.IEnumerable<Microsoft.Build.Framework.ILogger> loggers) { }
        public void RemoveAllToolsets() { }
        public bool RemoveGlobalProperty(string name) { throw null; }
        public bool RemoveToolset(string toolsVersion) { throw null; }
        public void SetGlobalProperty(string name, string value) { }
        public bool TryUnloadProject(Microsoft.Build.Construction.ProjectRootElement projectRootElement) { throw null; }
        public static string Unescape(string escapedString) { throw null; }
        public void UnloadAllProjects() { }
        public void UnloadProject(Microsoft.Build.Construction.ProjectRootElement projectRootElement) { }
        public void UnloadProject(Microsoft.Build.Evaluation.Project project) { }
        public void UnregisterAllLoggers() { }
        public delegate void ProjectAddedEventHandler(object sender, Microsoft.Build.Evaluation.ProjectCollection.ProjectAddedToProjectCollectionEventArgs e);
        public partial class ProjectAddedToProjectCollectionEventArgs : System.EventArgs
        {
            public ProjectAddedToProjectCollectionEventArgs(Microsoft.Build.Construction.ProjectRootElement element) { }
            public Microsoft.Build.Construction.ProjectRootElement ProjectRootElement { get { throw null; } }
        }
    }
    public partial class ProjectCollectionChangedEventArgs : System.EventArgs
    {
        internal ProjectCollectionChangedEventArgs() { }
        public Microsoft.Build.Evaluation.ProjectCollectionChangedState Changed { [System.Runtime.CompilerServices.CompilerGeneratedAttribute]get { throw null; } }
    }
    public enum ProjectCollectionChangedState
    {
        DefaultToolsVersion = 0,
        DisableMarkDirty = 7,
        GlobalProperties = 3,
        HostServices = 6,
        IsBuildEnabled = 4,
        Loggers = 2,
        OnlyLogCriticalEvents = 5,
        SkipEvaluation = 8,
        Toolsets = 1,
    }
    [System.Diagnostics.DebuggerDisplayAttribute("{ItemType}={EvaluatedInclude} [{UnevaluatedInclude}] #DirectMetadata={DirectMetadataCount}")]
    public partial class ProjectItem
    {
        internal ProjectItem() { }
        public System.Collections.Generic.IEnumerable<Microsoft.Build.Evaluation.ProjectMetadata> DirectMetadata { get { throw null; } }
        public int DirectMetadataCount { [System.Diagnostics.DebuggerStepThroughAttribute]get { throw null; } }
        public string EvaluatedInclude { [System.Diagnostics.DebuggerStepThroughAttribute]get { throw null; } }
        public bool IsImported { get { throw null; } }
        public string ItemType { [System.Diagnostics.DebuggerStepThroughAttribute]get { throw null; } set { } }
        public System.Collections.Generic.ICollection<Microsoft.Build.Evaluation.ProjectMetadata> Metadata { [System.Diagnostics.DebuggerStepThroughAttribute]get { throw null; } }
        public int MetadataCount { [System.Diagnostics.DebuggerStepThroughAttribute]get { throw null; } }
        [System.Diagnostics.DebuggerBrowsableAttribute((System.Diagnostics.DebuggerBrowsableState)(0))]
        public Microsoft.Build.Evaluation.Project Project { [System.Diagnostics.DebuggerStepThroughAttribute]get { throw null; } }
        public string UnevaluatedInclude { [System.Diagnostics.DebuggerStepThroughAttribute]get { throw null; } set { } }
        [System.Diagnostics.DebuggerBrowsableAttribute((System.Diagnostics.DebuggerBrowsableState)(0))]
        public Microsoft.Build.Construction.ProjectItemElement Xml { [System.Diagnostics.DebuggerStepThroughAttribute]get { throw null; } }
        public Microsoft.Build.Evaluation.ProjectMetadata GetMetadata(string name) { throw null; }
        public string GetMetadataValue(string name) { throw null; }
        public bool HasMetadata(string name) { throw null; }
        public bool RemoveMetadata(string name) { throw null; }
        public void Rename(string name) { }
        public Microsoft.Build.Evaluation.ProjectMetadata SetMetadataValue(string name, string unevaluatedValue) { throw null; }
        public Microsoft.Build.Evaluation.ProjectMetadata SetMetadataValue(string name, string unevaluatedValue, bool propagateMetadataToSiblingItems) { throw null; }
    }
    [System.Diagnostics.DebuggerDisplayAttribute("{_itemType} #Metadata={MetadataCount}")]
    public partial class ProjectItemDefinition
    {
        internal ProjectItemDefinition() { }
        [System.Diagnostics.DebuggerBrowsableAttribute((System.Diagnostics.DebuggerBrowsableState)(0))]
        public string ItemType { [System.Diagnostics.DebuggerStepThroughAttribute]get { throw null; } }
        public System.Collections.Generic.IEnumerable<Microsoft.Build.Evaluation.ProjectMetadata> Metadata { get { throw null; } }
        public int MetadataCount { get { throw null; } }
        [System.Diagnostics.DebuggerBrowsableAttribute((System.Diagnostics.DebuggerBrowsableState)(0))]
        public Microsoft.Build.Evaluation.Project Project { [System.Diagnostics.DebuggerStepThroughAttribute]get { throw null; } }
        [System.Diagnostics.DebuggerStepThroughAttribute]
        public Microsoft.Build.Evaluation.ProjectMetadata GetMetadata(string name) { throw null; }
        public string GetMetadataValue(string name) { throw null; }
        public Microsoft.Build.Evaluation.ProjectMetadata SetMetadataValue(string name, string unevaluatedValue) { throw null; }
    }
    [System.FlagsAttribute]
    public enum ProjectLoadSettings
    {
        Default = 0,
        DoNotEvaluateElementsWithFalseCondition = 32,
        IgnoreEmptyImports = 16,
        IgnoreInvalidImports = 64,
        IgnoreMissingImports = 1,
        ProfileEvaluation = 128,
        RecordDuplicateButNotCircularImports = 2,
        RecordEvaluatedItemElements = 8,
        RejectCircularImports = 4,
    }
    [System.Diagnostics.DebuggerDisplayAttribute("{Name}={EvaluatedValue} [{_xml.Value}]")]
    public partial class ProjectMetadata : System.IEquatable<Microsoft.Build.Evaluation.ProjectMetadata>
    {
        internal ProjectMetadata() { }
        public Microsoft.Build.Construction.ElementLocation ConditionLocation { get { throw null; } }
        public string EvaluatedValue { [System.Diagnostics.DebuggerStepThroughAttribute]get { throw null; } }
        public bool IsImported { get { throw null; } }
        public string ItemType { get { throw null; } }
        public Microsoft.Build.Construction.ElementLocation Location { get { throw null; } }
        public string Name { [System.Diagnostics.DebuggerStepThroughAttribute]get { throw null; } }
        public Microsoft.Build.Evaluation.ProjectMetadata Predecessor { [System.Diagnostics.DebuggerStepThroughAttribute]get { throw null; } }
        public Microsoft.Build.Evaluation.Project Project { [System.Diagnostics.DebuggerStepThroughAttribute]get { throw null; } }
        public string UnevaluatedValue { [System.Diagnostics.DebuggerStepThroughAttribute]get { throw null; } [System.Diagnostics.DebuggerStepThroughAttribute]set { } }
        [System.Diagnostics.DebuggerBrowsableAttribute((System.Diagnostics.DebuggerBrowsableState)(0))]
        public Microsoft.Build.Construction.ProjectMetadataElement Xml { [System.Diagnostics.DebuggerStepThroughAttribute]get { throw null; } }
        bool System.IEquatable<Microsoft.Build.Evaluation.ProjectMetadata>.Equals(Microsoft.Build.Evaluation.ProjectMetadata other) { throw null; }
    }
    [System.Diagnostics.DebuggerDisplayAttribute("{Name}={EvaluatedValue} [{UnevaluatedValue}]")]
    public abstract partial class ProjectProperty : System.IEquatable<Microsoft.Build.Evaluation.ProjectProperty>
    {
        internal ProjectProperty() { }
        public string EvaluatedValue { [System.Diagnostics.DebuggerStepThroughAttribute]get { throw null; } }
        public abstract bool IsEnvironmentProperty { [System.Diagnostics.DebuggerStepThroughAttribute]get; }
        public abstract bool IsGlobalProperty { [System.Diagnostics.DebuggerStepThroughAttribute]get; }
        public abstract bool IsImported { get; }
        public abstract bool IsReservedProperty { [System.Diagnostics.DebuggerStepThroughAttribute]get; }
        [System.Diagnostics.DebuggerBrowsableAttribute((System.Diagnostics.DebuggerBrowsableState)(0))]
        public abstract string Name { [System.Diagnostics.DebuggerStepThroughAttribute]get; }
        public abstract Microsoft.Build.Evaluation.ProjectProperty Predecessor { [System.Diagnostics.DebuggerStepThroughAttribute]get; }
        [System.Diagnostics.DebuggerBrowsableAttribute((System.Diagnostics.DebuggerBrowsableState)(0))]
        public Microsoft.Build.Evaluation.Project Project { [System.Diagnostics.DebuggerStepThroughAttribute]get { throw null; } }
        public abstract string UnevaluatedValue { [System.Diagnostics.DebuggerStepThroughAttribute]get; set; }
        [System.Diagnostics.DebuggerBrowsableAttribute((System.Diagnostics.DebuggerBrowsableState)(0))]
        public abstract Microsoft.Build.Construction.ProjectPropertyElement Xml { [System.Diagnostics.DebuggerStepThroughAttribute]get; }
        bool System.IEquatable<Microsoft.Build.Evaluation.ProjectProperty>.Equals(Microsoft.Build.Evaluation.ProjectProperty other) { throw null; }
    }
    public partial class ProjectXmlChangedEventArgs : System.EventArgs
    {
        internal ProjectXmlChangedEventArgs() { }
        public Microsoft.Build.Construction.ProjectRootElement ProjectXml { [System.Runtime.CompilerServices.CompilerGeneratedAttribute]get { throw null; } }
        public string Reason { get { throw null; } }
    }
    [System.FlagsAttribute]
    public enum Provenance
    {
        Glob = 2,
        Inconclusive = 4,
        StringLiteral = 1,
        Undefined = 0,
    }
    public partial class ProvenanceResult
    {
        public ProvenanceResult(Microsoft.Build.Construction.ProjectItemElement itemElement, Microsoft.Build.Evaluation.Operation operation, Microsoft.Build.Evaluation.Provenance provenance, int occurrences) { }
        public Microsoft.Build.Construction.ProjectItemElement ItemElement { [System.Runtime.CompilerServices.CompilerGeneratedAttribute]get { throw null; } }
        public int Occurrences { [System.Runtime.CompilerServices.CompilerGeneratedAttribute]get { throw null; } }
        public Microsoft.Build.Evaluation.Operation Operation { [System.Runtime.CompilerServices.CompilerGeneratedAttribute]get { throw null; } }
        public Microsoft.Build.Evaluation.Provenance Provenance { [System.Runtime.CompilerServices.CompilerGeneratedAttribute]get { throw null; } }
    }
    [System.Runtime.InteropServices.StructLayoutAttribute(System.Runtime.InteropServices.LayoutKind.Sequential)]
    public partial struct ResolvedImport
    {
        public Microsoft.Build.Construction.ProjectRootElement ImportedProject { [System.Runtime.CompilerServices.CompilerGeneratedAttribute]get { throw null; } }
        public Microsoft.Build.Construction.ProjectImportElement ImportingElement { [System.Runtime.CompilerServices.CompilerGeneratedAttribute]get { throw null; } }
        public bool IsImported { [System.Runtime.CompilerServices.CompilerGeneratedAttribute]get { throw null; } }
        public Microsoft.Build.Framework.SdkResult SdkResult { [System.Runtime.CompilerServices.CompilerGeneratedAttribute]get { throw null; } }
    }
    [System.Diagnostics.DebuggerDisplayAttribute("SubToolsetVersion={SubToolsetVersion} #Properties={_properties.Count}")]
    public partial class SubToolset
    {
        internal SubToolset() { }
        public System.Collections.Generic.IDictionary<string, Microsoft.Build.Execution.ProjectPropertyInstance> Properties { get { throw null; } }
        public string SubToolsetVersion { get { throw null; } }
    }
    [System.Diagnostics.DebuggerDisplayAttribute("ToolsVersion={ToolsVersion} ToolsPath={ToolsPath} #Properties={_properties.Count}")]
    public partial class Toolset
    {
        public Toolset(string toolsVersion, string toolsPath, Microsoft.Build.Evaluation.ProjectCollection projectCollection, string msbuildOverrideTasksPath) { }
        public Toolset(string toolsVersion, string toolsPath, System.Collections.Generic.IDictionary<string, string> buildProperties, Microsoft.Build.Evaluation.ProjectCollection projectCollection, System.Collections.Generic.IDictionary<string, Microsoft.Build.Evaluation.SubToolset> subToolsets, string msbuildOverrideTasksPath) { }
        public Toolset(string toolsVersion, string toolsPath, System.Collections.Generic.IDictionary<string, string> buildProperties, Microsoft.Build.Evaluation.ProjectCollection projectCollection, string msbuildOverrideTasksPath) { }
        public string DefaultSubToolsetVersion { get { throw null; } }
        public System.Collections.Generic.IDictionary<string, Microsoft.Build.Execution.ProjectPropertyInstance> Properties { get { throw null; } }
        public System.Collections.Generic.IDictionary<string, Microsoft.Build.Evaluation.SubToolset> SubToolsets { get { throw null; } }
        public string ToolsPath { get { throw null; } }
        public string ToolsVersion { get { throw null; } }
        public string GenerateSubToolsetVersion() { throw null; }
        public string GenerateSubToolsetVersion(System.Collections.Generic.IDictionary<string, string> overrideGlobalProperties, int solutionVersion) { throw null; }
        public Microsoft.Build.Execution.ProjectPropertyInstance GetProperty(string propertyName, string subToolsetVersion) { throw null; }
    }
    [System.FlagsAttribute]
    public enum ToolsetDefinitionLocations
    {
        ConfigurationFile = 1,
        Default = 3,
        Local = 4,
        None = 0,
        Registry = 2,
    }
}
namespace Microsoft.Build.Evaluation.Context
{
    public partial class EvaluationContext
    {
        internal EvaluationContext() { }
        public static Microsoft.Build.Evaluation.Context.EvaluationContext Create(Microsoft.Build.Evaluation.Context.EvaluationContext.SharingPolicy policy) { throw null; }
        public void ResetCaches() { }
        public enum SharingPolicy
        {
            Isolated = 1,
            Shared = 0,
        }
    }
}
namespace Microsoft.Build.Exceptions
{
    public partial class BuildAbortedException : System.Exception
    {
        public BuildAbortedException() { }
        protected BuildAbortedException(System.Runtime.Serialization.SerializationInfo info, System.Runtime.Serialization.StreamingContext context) { }
        public BuildAbortedException(string message) { }
        public BuildAbortedException(string message, System.Exception innerException) { }
        public string ErrorCode { [System.Runtime.CompilerServices.CompilerGeneratedAttribute]get { throw null; } }
        [System.Security.Permissions.SecurityPermissionAttribute(System.Security.Permissions.SecurityAction.Demand, SerializationFormatter=true)]
        public override void GetObjectData(System.Runtime.Serialization.SerializationInfo info, System.Runtime.Serialization.StreamingContext context) { }
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
        public string BaseMessage { get { throw null; } }
        public int ColumnNumber { get { throw null; } }
        public int EndColumnNumber { get { throw null; } }
        public int EndLineNumber { get { throw null; } }
        public string ErrorCode { get { throw null; } }
        public string ErrorSubcategory { get { throw null; } }
        public bool HasBeenLogged { get { throw null; } }
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
}
namespace Microsoft.Build.Execution
{
    public partial class BuildManager : System.IDisposable
    {
        public BuildManager() { }
        public BuildManager(string hostName) { }
        public static Microsoft.Build.Execution.BuildManager DefaultBuildManager { get { throw null; } }
        public void BeginBuild(Microsoft.Build.Execution.BuildParameters parameters) { }
        public Microsoft.Build.Execution.BuildResult Build(Microsoft.Build.Execution.BuildParameters parameters, Microsoft.Build.Execution.BuildRequestData requestData) { throw null; }
        public Microsoft.Build.Execution.BuildResult BuildRequest(Microsoft.Build.Execution.BuildRequestData requestData) { throw null; }
        public void CancelAllSubmissions() { }
        public void Dispose() { }
        public void EndBuild() { }
        ~BuildManager() { }
        public Microsoft.Build.Execution.ProjectInstance GetProjectInstanceForBuild(Microsoft.Build.Evaluation.Project project) { throw null; }
        public Microsoft.Build.Execution.BuildSubmission PendBuildRequest(Microsoft.Build.Execution.BuildRequestData requestData) { throw null; }
        public void ResetCaches() { }
        public void ShutdownAllNodes() { }
    }
    public partial class BuildParameters
    {
        public BuildParameters() { }
        public BuildParameters(Microsoft.Build.Evaluation.ProjectCollection projectCollection) { }
        public System.Collections.Generic.IDictionary<string, string> BuildProcessEnvironment { get { throw null; } }
        public System.Threading.ThreadPriority BuildThreadPriority { [System.Runtime.CompilerServices.CompilerGeneratedAttribute]get { throw null; } [System.Runtime.CompilerServices.CompilerGeneratedAttribute]set { } }
        public System.Globalization.CultureInfo Culture { get { throw null; } set { } }
        public string DefaultToolsVersion { get { throw null; } set { } }
        public bool DetailedSummary { get { throw null; } set { } }
        public bool DisableInProcNode { get { throw null; } set { } }
        public bool EnableNodeReuse { get { throw null; } set { } }
        public System.Collections.Generic.IDictionary<string, string> EnvironmentProperties { get { throw null; } }
        public System.Collections.Generic.IEnumerable<Microsoft.Build.Logging.ForwardingLoggerRecord> ForwardingLoggers { get { throw null; } set { } }
        public System.Collections.Generic.IDictionary<string, string> GlobalProperties { get { throw null; } set { } }
        public Microsoft.Build.Execution.HostServices HostServices { get { throw null; } set { } }
        public bool LegacyThreadingSemantics { [System.Runtime.CompilerServices.CompilerGeneratedAttribute]get { throw null; } [System.Runtime.CompilerServices.CompilerGeneratedAttribute]set { } }
        public System.Collections.Generic.IEnumerable<Microsoft.Build.Framework.ILogger> Loggers { get { throw null; } set { } }
        public bool LogInitialPropertiesAndItems { get { throw null; } set { } }
        public bool LogTaskInputs { get { throw null; } set { } }
        public int MaxNodeCount { get { throw null; } set { } }
        public int MemoryUseLimit { get { throw null; } set { } }
        public string NodeExeLocation { get { throw null; } set { } }
        public bool OnlyLogCriticalEvents { get { throw null; } set { } }
        public Microsoft.Build.Evaluation.ProjectLoadSettings ProjectLoadSettings { get { throw null; } set { } }
        public bool ResetCaches { [System.Runtime.CompilerServices.CompilerGeneratedAttribute]get { throw null; } [System.Runtime.CompilerServices.CompilerGeneratedAttribute]set { } }
        public bool SaveOperatingEnvironment { get { throw null; } set { } }
        public bool ShutdownInProcNodeOnBuildFinish { get { throw null; } set { } }
        public Microsoft.Build.Evaluation.ToolsetDefinitionLocations ToolsetDefinitionLocations { get { throw null; } set { } }
        public System.Collections.Generic.ICollection<Microsoft.Build.Evaluation.Toolset> Toolsets { get { throw null; } }
        public System.Globalization.CultureInfo UICulture { get { throw null; } set { } }
        public bool UseSynchronousLogging { get { throw null; } set { } }
        public System.Collections.Generic.ISet<string> WarningsAsErrors { get { throw null; } set { } }
        public System.Collections.Generic.ISet<string> WarningsAsMessages { get { throw null; } set { } }
        public Microsoft.Build.Execution.BuildParameters Clone() { throw null; }
        public Microsoft.Build.Evaluation.Toolset GetToolset(string toolsVersion) { throw null; }
    }
    public partial class BuildRequestData
    {
        public BuildRequestData(Microsoft.Build.Execution.ProjectInstance projectInstance, string[] targetsToBuild) { }
        public BuildRequestData(Microsoft.Build.Execution.ProjectInstance projectInstance, string[] targetsToBuild, Microsoft.Build.Execution.HostServices hostServices) { }
        public BuildRequestData(Microsoft.Build.Execution.ProjectInstance projectInstance, string[] targetsToBuild, Microsoft.Build.Execution.HostServices hostServices, Microsoft.Build.Execution.BuildRequestDataFlags flags) { }
        public BuildRequestData(Microsoft.Build.Execution.ProjectInstance projectInstance, string[] targetsToBuild, Microsoft.Build.Execution.HostServices hostServices, Microsoft.Build.Execution.BuildRequestDataFlags flags, System.Collections.Generic.IEnumerable<string> propertiesToTransfer) { }
        public BuildRequestData(Microsoft.Build.Execution.ProjectInstance projectInstance, string[] targetsToBuild, Microsoft.Build.Execution.HostServices hostServices, Microsoft.Build.Execution.BuildRequestDataFlags flags, System.Collections.Generic.IEnumerable<string> propertiesToTransfer, Microsoft.Build.Execution.RequestedProjectState requestedProjectState) { }
        public BuildRequestData(string projectFullPath, System.Collections.Generic.IDictionary<string, string> globalProperties, string toolsVersion, string[] targetsToBuild, Microsoft.Build.Execution.HostServices hostServices) { }
        public BuildRequestData(string projectFullPath, System.Collections.Generic.IDictionary<string, string> globalProperties, string toolsVersion, string[] targetsToBuild, Microsoft.Build.Execution.HostServices hostServices, Microsoft.Build.Execution.BuildRequestDataFlags flags) { }
        public BuildRequestData(string projectFullPath, System.Collections.Generic.IDictionary<string, string> globalProperties, string toolsVersion, string[] targetsToBuild, Microsoft.Build.Execution.HostServices hostServices, Microsoft.Build.Execution.BuildRequestDataFlags flags, Microsoft.Build.Execution.RequestedProjectState requestedProjectState) { }
        public string ExplicitlySpecifiedToolsVersion { [System.Runtime.CompilerServices.CompilerGeneratedAttribute]get { throw null; } }
        public Microsoft.Build.Execution.BuildRequestDataFlags Flags { [System.Runtime.CompilerServices.CompilerGeneratedAttribute]get { throw null; } }
        public System.Collections.Generic.ICollection<Microsoft.Build.Execution.ProjectPropertyInstance> GlobalProperties { get { throw null; } }
        public Microsoft.Build.Execution.HostServices HostServices { [System.Runtime.CompilerServices.CompilerGeneratedAttribute]get { throw null; } }
        public string ProjectFullPath { [System.Runtime.CompilerServices.CompilerGeneratedAttribute]get { throw null; } }
        public Microsoft.Build.Execution.ProjectInstance ProjectInstance { [System.Runtime.CompilerServices.CompilerGeneratedAttribute]get { throw null; } }
        public System.Collections.Generic.IEnumerable<string> PropertiesToTransfer { [System.Runtime.CompilerServices.CompilerGeneratedAttribute]get { throw null; } }
        public Microsoft.Build.Execution.RequestedProjectState RequestedProjectState { [System.Runtime.CompilerServices.CompilerGeneratedAttribute]get { throw null; } }
        public System.Collections.Generic.ICollection<string> TargetNames { [System.Runtime.CompilerServices.CompilerGeneratedAttribute]get { throw null; } }
    }
    [System.FlagsAttribute]
    public enum BuildRequestDataFlags
    {
        ClearCachesAfterBuild = 8,
        IgnoreExistingProjectState = 4,
        IgnoreMissingEmptyAndInvalidImports = 64,
        None = 0,
        ProvideProjectStateAfterBuild = 2,
        ProvideSubsetOfStateAfterBuild = 32,
        ReplaceExistingProjectInstance = 1,
        SkipNonexistentTargets = 16,
    }
    public partial class BuildResult
    {
        public BuildResult() { }
        public bool CircularDependency { [System.Diagnostics.DebuggerStepThroughAttribute]get { throw null; } }
        public int ConfigurationId { [System.Diagnostics.DebuggerStepThroughAttribute]get { throw null; } }
        public System.Exception Exception { [System.Diagnostics.DebuggerStepThroughAttribute]get { throw null; } }
        public int GlobalRequestId { [System.Diagnostics.DebuggerStepThroughAttribute]get { throw null; } }
        public Microsoft.Build.Execution.ITargetResult this[string target] { [System.Diagnostics.DebuggerStepThroughAttribute]get { throw null; } }
        public int NodeRequestId { [System.Diagnostics.DebuggerStepThroughAttribute]get { throw null; } }
        public Microsoft.Build.Execution.BuildResultCode OverallResult { get { throw null; } }
        public int ParentGlobalRequestId { [System.Diagnostics.DebuggerStepThroughAttribute]get { throw null; } }
        public Microsoft.Build.Execution.ProjectInstance ProjectStateAfterBuild { get { throw null; } set { } }
        public System.Collections.Generic.IDictionary<string, Microsoft.Build.Execution.TargetResult> ResultsByTarget { [System.Diagnostics.DebuggerStepThroughAttribute]get { throw null; } }
        public int SubmissionId { [System.Diagnostics.DebuggerStepThroughAttribute]get { throw null; } }
        public void AddResultsForTarget(string target, Microsoft.Build.Execution.TargetResult result) { }
        public bool HasResultsForTarget(string target) { throw null; }
        public void MergeResults(Microsoft.Build.Execution.BuildResult results) { }
    }
    public enum BuildResultCode
    {
        Failure = 1,
        Success = 0,
    }
    public partial class BuildSubmission
    {
        internal BuildSubmission() { }
        public object AsyncContext { [System.Runtime.CompilerServices.CompilerGeneratedAttribute]get { throw null; } }
        public Microsoft.Build.Execution.BuildManager BuildManager { [System.Runtime.CompilerServices.CompilerGeneratedAttribute]get { throw null; } }
        public Microsoft.Build.Execution.BuildResult BuildResult { get { throw null; } set { } }
        public bool IsCompleted { get { throw null; } }
        public int SubmissionId { [System.Runtime.CompilerServices.CompilerGeneratedAttribute]get { throw null; } }
        public System.Threading.WaitHandle WaitHandle { get { throw null; } }
        public Microsoft.Build.Execution.BuildResult Execute() { throw null; }
        public void ExecuteAsync(Microsoft.Build.Execution.BuildSubmissionCompleteCallback callback, object context) { }
    }
    public delegate void BuildSubmissionCompleteCallback(Microsoft.Build.Execution.BuildSubmission submission);
    [System.Diagnostics.DebuggerDisplayAttribute("#Entries={_hostObjectMap.Count}")]
    public partial class HostServices
    {
        public HostServices() { }
        public Microsoft.Build.Framework.ITaskHost GetHostObject(string projectFile, string targetName, string taskName) { throw null; }
        public Microsoft.Build.Execution.NodeAffinity GetNodeAffinity(string projectFile) { throw null; }
        public void OnRenameProject(string oldFullPath, string newFullPath) { }
        public void RegisterHostObject(string projectFile, string targetName, string taskName, Microsoft.Build.Framework.ITaskHost hostObject) { }
        public void SetNodeAffinity(string projectFile, Microsoft.Build.Execution.NodeAffinity nodeAffinity) { }
        public void UnregisterProject(string projectFullPath) { }
    }
    public partial interface ITargetResult
    {
        System.Exception Exception { get; }
        Microsoft.Build.Framework.ITaskItem[] Items { get; }
        Microsoft.Build.Execution.TargetResultCode ResultCode { get; }
    }
    public enum NodeAffinity
    {
        Any = 2,
        InProc = 0,
        OutOfProc = 1,
    }
    public enum NodeEngineShutdownReason
    {
        BuildComplete = 0,
        BuildCompleteReuse = 1,
        ConnectionFailed = 2,
        Error = 3,
    }
    public partial class OutOfProcNode
    {
        public OutOfProcNode() { }
        public Microsoft.Build.Execution.NodeEngineShutdownReason Run(bool enableReuse, out System.Exception shutdownException) { shutdownException = default(System.Exception); throw null; }
        public Microsoft.Build.Execution.NodeEngineShutdownReason Run(out System.Exception shutdownException) { shutdownException = default(System.Exception); throw null; }
    }
    [System.Diagnostics.DebuggerDisplayAttribute("{FullPath} #Targets={TargetsCount} DefaultTargets={(DefaultTargets == null) ? System.String.Empty : System.String.Join(\";\", DefaultTargets.ToArray())} ToolsVersion={Toolset.ToolsVersion} InitialTargets={(InitialTargets == null) ? System.String.Empty : System.String.Join(\";\", InitialTargets.ToArray())} #GlobalProperties={GlobalProperties.Count} #Properties={Properties.Count} #ItemTypes={ItemTypes.Count} #Items={Items.Count}")]
    public partial class ProjectInstance
    {
        public ProjectInstance(Microsoft.Build.Construction.ProjectRootElement xml) { }
        public ProjectInstance(Microsoft.Build.Construction.ProjectRootElement xml, System.Collections.Generic.IDictionary<string, string> globalProperties, string toolsVersion, Microsoft.Build.Evaluation.ProjectCollection projectCollection) { }
        public ProjectInstance(Microsoft.Build.Construction.ProjectRootElement xml, System.Collections.Generic.IDictionary<string, string> globalProperties, string toolsVersion, string subToolsetVersion, Microsoft.Build.Evaluation.ProjectCollection projectCollection) { }
        public ProjectInstance(string projectFile) { }
        public ProjectInstance(string projectFile, System.Collections.Generic.IDictionary<string, string> globalProperties, string toolsVersion) { }
        public ProjectInstance(string projectFile, System.Collections.Generic.IDictionary<string, string> globalProperties, string toolsVersion, Microsoft.Build.Evaluation.ProjectCollection projectCollection) { }
        public ProjectInstance(string projectFile, System.Collections.Generic.IDictionary<string, string> globalProperties, string toolsVersion, string subToolsetVersion, Microsoft.Build.Evaluation.ProjectCollection projectCollection) { }
        public System.Collections.Generic.List<string> DefaultTargets { get { throw null; } }
        public string Directory { [System.Diagnostics.DebuggerStepThroughAttribute]get { throw null; } }
        public System.Collections.Generic.List<Microsoft.Build.Construction.ProjectItemElement> EvaluatedItemElements { [System.Runtime.CompilerServices.CompilerGeneratedAttribute]get { throw null; } }
        public int EvaluationId { get { throw null; } set { } }
        public string FullPath { [System.Diagnostics.DebuggerStepThroughAttribute]get { throw null; } }
        public System.Collections.Generic.IDictionary<string, string> GlobalProperties { [System.Diagnostics.DebuggerStepThroughAttribute]get { throw null; } }
        public System.Collections.Generic.List<string> InitialTargets { get { throw null; } }
        public bool IsImmutable { get { throw null; } }
        public System.Collections.Generic.IDictionary<string, Microsoft.Build.Execution.ProjectItemDefinitionInstance> ItemDefinitions { [System.Diagnostics.DebuggerStepThroughAttribute]get { throw null; } }
        public System.Collections.Generic.ICollection<Microsoft.Build.Execution.ProjectItemInstance> Items { [System.Diagnostics.DebuggerStepThroughAttribute]get { throw null; } }
        public System.Collections.Generic.ICollection<string> ItemTypes { [System.Diagnostics.DebuggerStepThroughAttribute]get { throw null; } }
        public Microsoft.Build.Construction.ElementLocation ProjectFileLocation { get { throw null; } }
        public System.Collections.Generic.ICollection<Microsoft.Build.Execution.ProjectPropertyInstance> Properties { [System.Diagnostics.DebuggerStepThroughAttribute]get { throw null; } }
        public System.Collections.Generic.IDictionary<string, Microsoft.Build.Execution.ProjectTargetInstance> Targets { [System.Diagnostics.DebuggerStepThroughAttribute]get { throw null; } }
        public string ToolsVersion { get { throw null; } }
        public bool TranslateEntireState { get { throw null; } set { } }
        public Microsoft.Build.Execution.ProjectItemInstance AddItem(string itemType, string evaluatedInclude) { throw null; }
        public Microsoft.Build.Execution.ProjectItemInstance AddItem(string itemType, string evaluatedInclude, System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<string, string>> metadata) { throw null; }
        public bool Build() { throw null; }
        public bool Build(System.Collections.Generic.IEnumerable<Microsoft.Build.Framework.ILogger> loggers) { throw null; }
        public bool Build(System.Collections.Generic.IEnumerable<Microsoft.Build.Framework.ILogger> loggers, System.Collections.Generic.IEnumerable<Microsoft.Build.Logging.ForwardingLoggerRecord> remoteLoggers) { throw null; }
        public bool Build(string target, System.Collections.Generic.IEnumerable<Microsoft.Build.Framework.ILogger> loggers) { throw null; }
        public bool Build(string target, System.Collections.Generic.IEnumerable<Microsoft.Build.Framework.ILogger> loggers, System.Collections.Generic.IEnumerable<Microsoft.Build.Logging.ForwardingLoggerRecord> remoteLoggers) { throw null; }
        public bool Build(string[] targets, System.Collections.Generic.IEnumerable<Microsoft.Build.Framework.ILogger> loggers) { throw null; }
        public bool Build(string[] targets, System.Collections.Generic.IEnumerable<Microsoft.Build.Framework.ILogger> loggers, out System.Collections.Generic.IDictionary<string, Microsoft.Build.Execution.TargetResult> targetOutputs) { targetOutputs = default(System.Collections.Generic.IDictionary<string, Microsoft.Build.Execution.TargetResult>); throw null; }
        public bool Build(string[] targets, System.Collections.Generic.IEnumerable<Microsoft.Build.Framework.ILogger> loggers, System.Collections.Generic.IEnumerable<Microsoft.Build.Logging.ForwardingLoggerRecord> remoteLoggers) { throw null; }
        public bool Build(string[] targets, System.Collections.Generic.IEnumerable<Microsoft.Build.Framework.ILogger> loggers, System.Collections.Generic.IEnumerable<Microsoft.Build.Logging.ForwardingLoggerRecord> remoteLoggers, out System.Collections.Generic.IDictionary<string, Microsoft.Build.Execution.TargetResult> targetOutputs) { targetOutputs = default(System.Collections.Generic.IDictionary<string, Microsoft.Build.Execution.TargetResult>); throw null; }
        public Microsoft.Build.Execution.ProjectInstance DeepCopy() { throw null; }
        public Microsoft.Build.Execution.ProjectInstance DeepCopy(bool isImmutable) { throw null; }
        public bool EvaluateCondition(string condition) { throw null; }
        public string ExpandString(string unexpandedValue) { throw null; }
        public Microsoft.Build.Execution.ProjectInstance FilteredCopy(Microsoft.Build.Execution.RequestedProjectState filter) { throw null; }
        public static string GetEvaluatedItemIncludeEscaped(Microsoft.Build.Execution.ProjectItemDefinitionInstance item) { throw null; }
        public static string GetEvaluatedItemIncludeEscaped(Microsoft.Build.Execution.ProjectItemInstance item) { throw null; }
        public System.Collections.Generic.ICollection<Microsoft.Build.Execution.ProjectItemInstance> GetItems(string itemType) { throw null; }
        public System.Collections.Generic.IEnumerable<Microsoft.Build.Execution.ProjectItemInstance> GetItemsByItemTypeAndEvaluatedInclude(string itemType, string evaluatedInclude) { throw null; }
        public static string GetMetadataValueEscaped(Microsoft.Build.Execution.ProjectItemDefinitionInstance item, string name) { throw null; }
        public static string GetMetadataValueEscaped(Microsoft.Build.Execution.ProjectItemInstance item, string name) { throw null; }
        public static string GetMetadataValueEscaped(Microsoft.Build.Execution.ProjectMetadataInstance metadatum) { throw null; }
        [System.Diagnostics.DebuggerStepThroughAttribute]
        public Microsoft.Build.Execution.ProjectPropertyInstance GetProperty(string name) { throw null; }
        public string GetPropertyValue(string name) { throw null; }
        public static string GetPropertyValueEscaped(Microsoft.Build.Execution.ProjectPropertyInstance property) { throw null; }
        public bool RemoveItem(Microsoft.Build.Execution.ProjectItemInstance item) { throw null; }
        public bool RemoveProperty(string name) { throw null; }
        public Microsoft.Build.Execution.ProjectPropertyInstance SetProperty(string name, string evaluatedValue) { throw null; }
        public Microsoft.Build.Construction.ProjectRootElement ToProjectRootElement() { throw null; }
        public void UpdateStateFrom(Microsoft.Build.Execution.ProjectInstance projectState) { }
    }
    [System.FlagsAttribute]
    public enum ProjectInstanceSettings
    {
        Immutable = 1,
        ImmutableWithFastItemLookup = 3,
        None = 0,
    }
    [System.Diagnostics.DebuggerDisplayAttribute("{_itemType} #Metadata={MetadataCount}")]
    public partial class ProjectItemDefinitionInstance
    {
        internal ProjectItemDefinitionInstance() { }
        [System.Diagnostics.DebuggerBrowsableAttribute((System.Diagnostics.DebuggerBrowsableState)(0))]
        public string ItemType { [System.Diagnostics.DebuggerStepThroughAttribute]get { throw null; } }
        public System.Collections.Generic.ICollection<Microsoft.Build.Execution.ProjectMetadataInstance> Metadata { get { throw null; } }
        public int MetadataCount { get { throw null; } }
        public System.Collections.Generic.IEnumerable<string> MetadataNames { get { throw null; } }
        [System.Diagnostics.DebuggerStepThroughAttribute]
        public Microsoft.Build.Execution.ProjectMetadataInstance GetMetadata(string name) { throw null; }
    }
    [System.Diagnostics.DebuggerDisplayAttribute("Condition={_condition}")]
    public partial class ProjectItemGroupTaskInstance : Microsoft.Build.Execution.ProjectTargetInstanceChild
    {
        internal ProjectItemGroupTaskInstance() { }
        public override string Condition { [System.Diagnostics.DebuggerStepThroughAttribute]get { throw null; } }
        public override Microsoft.Build.Construction.ElementLocation ConditionLocation { get { throw null; } }
        public System.Collections.Generic.ICollection<Microsoft.Build.Execution.ProjectItemGroupTaskItemInstance> Items { [System.Diagnostics.DebuggerStepThroughAttribute]get { throw null; } }
        public override Microsoft.Build.Construction.ElementLocation Location { get { throw null; } }
    }
    [System.Diagnostics.DebuggerDisplayAttribute("{_itemType} Include={_include} Exclude={_exclude} Remove={_remove} Condition={_condition}")]
    public partial class ProjectItemGroupTaskItemInstance
    {
        internal ProjectItemGroupTaskItemInstance() { }
        public string Condition { [System.Diagnostics.DebuggerStepThroughAttribute]get { throw null; } }
        public Microsoft.Build.Construction.ElementLocation ConditionLocation { [System.Diagnostics.DebuggerStepThroughAttribute]get { throw null; } }
        public string Exclude { [System.Diagnostics.DebuggerStepThroughAttribute]get { throw null; } }
        public Microsoft.Build.Construction.ElementLocation ExcludeLocation { [System.Diagnostics.DebuggerStepThroughAttribute]get { throw null; } }
        public string Include { [System.Diagnostics.DebuggerStepThroughAttribute]get { throw null; } }
        public Microsoft.Build.Construction.ElementLocation IncludeLocation { [System.Diagnostics.DebuggerStepThroughAttribute]get { throw null; } }
        public string ItemType { [System.Diagnostics.DebuggerStepThroughAttribute]get { throw null; } }
        public string KeepDuplicates { [System.Diagnostics.DebuggerStepThroughAttribute]get { throw null; } }
        public Microsoft.Build.Construction.ElementLocation KeepDuplicatesLocation { [System.Diagnostics.DebuggerStepThroughAttribute]get { throw null; } }
        public string KeepMetadata { [System.Diagnostics.DebuggerStepThroughAttribute]get { throw null; } }
        public Microsoft.Build.Construction.ElementLocation KeepMetadataLocation { [System.Diagnostics.DebuggerStepThroughAttribute]get { throw null; } }
        public Microsoft.Build.Construction.ElementLocation Location { [System.Diagnostics.DebuggerStepThroughAttribute]get { throw null; } }
        public System.Collections.Generic.ICollection<Microsoft.Build.Execution.ProjectItemGroupTaskMetadataInstance> Metadata { [System.Diagnostics.DebuggerStepThroughAttribute]get { throw null; } }
        public string Remove { [System.Diagnostics.DebuggerStepThroughAttribute]get { throw null; } }
        public Microsoft.Build.Construction.ElementLocation RemoveLocation { [System.Diagnostics.DebuggerStepThroughAttribute]get { throw null; } }
        public string RemoveMetadata { [System.Diagnostics.DebuggerStepThroughAttribute]get { throw null; } }
        public Microsoft.Build.Construction.ElementLocation RemoveMetadataLocation { [System.Diagnostics.DebuggerStepThroughAttribute]get { throw null; } }
    }
    [System.Diagnostics.DebuggerDisplayAttribute("{_name} Value={_value} Condition={_condition}")]
    public partial class ProjectItemGroupTaskMetadataInstance
    {
        internal ProjectItemGroupTaskMetadataInstance() { }
        public string Condition { [System.Diagnostics.DebuggerStepThroughAttribute]get { throw null; } }
        public Microsoft.Build.Construction.ElementLocation ConditionLocation { [System.Diagnostics.DebuggerStepThroughAttribute]get { throw null; } }
        public Microsoft.Build.Construction.ElementLocation Location { [System.Diagnostics.DebuggerStepThroughAttribute]get { throw null; } }
        public string Name { [System.Diagnostics.DebuggerStepThroughAttribute]get { throw null; } }
        public string Value { [System.Diagnostics.DebuggerStepThroughAttribute]get { throw null; } }
    }
    [System.Diagnostics.DebuggerDisplayAttribute("{ItemType}={EvaluatedInclude} #DirectMetadata={DirectMetadataCount})")]
    public partial class ProjectItemInstance : Microsoft.Build.Framework.ITaskItem, Microsoft.Build.Framework.ITaskItem2
    {
        internal ProjectItemInstance() { }
        public int DirectMetadataCount { get { throw null; } }
        public string EvaluatedInclude { [System.Diagnostics.DebuggerStepThroughAttribute]get { throw null; } [System.Diagnostics.DebuggerStepThroughAttribute]set { } }
        [System.Diagnostics.DebuggerBrowsableAttribute((System.Diagnostics.DebuggerBrowsableState)(0))]
        public string ItemType { [System.Diagnostics.DebuggerStepThroughAttribute]get { throw null; } }
        public System.Collections.Generic.IEnumerable<Microsoft.Build.Execution.ProjectMetadataInstance> Metadata { get { throw null; } }
        public int MetadataCount { get { throw null; } }
        public System.Collections.Generic.ICollection<string> MetadataNames { get { throw null; } }
        string Microsoft.Build.Framework.ITaskItem.ItemSpec { get { throw null; } set { } }
        System.Collections.ICollection Microsoft.Build.Framework.ITaskItem.MetadataNames { get { throw null; } }
        string Microsoft.Build.Framework.ITaskItem2.EvaluatedIncludeEscaped { [System.Diagnostics.DebuggerStepThroughAttribute]get { throw null; } set { } }
        [System.Diagnostics.DebuggerBrowsableAttribute((System.Diagnostics.DebuggerBrowsableState)(0))]
        public Microsoft.Build.Execution.ProjectInstance Project { get { throw null; } }
        public Microsoft.Build.Execution.ProjectMetadataInstance GetMetadata(string name) { throw null; }
        public string GetMetadataValue(string name) { throw null; }
        public bool HasMetadata(string name) { throw null; }
        System.Collections.IDictionary Microsoft.Build.Framework.ITaskItem.CloneCustomMetadata() { throw null; }
        void Microsoft.Build.Framework.ITaskItem.CopyMetadataTo(Microsoft.Build.Framework.ITaskItem destinationItem) { }
        string Microsoft.Build.Framework.ITaskItem.GetMetadata(string metadataName) { throw null; }
        void Microsoft.Build.Framework.ITaskItem.SetMetadata(string metadataName, string metadataValue) { }
        System.Collections.IDictionary Microsoft.Build.Framework.ITaskItem2.CloneCustomMetadataEscaped() { throw null; }
        string Microsoft.Build.Framework.ITaskItem2.GetMetadataValueEscaped(string name) { throw null; }
        void Microsoft.Build.Framework.ITaskItem2.SetMetadataValueLiteral(string metadataName, string metadataValue) { }
        public void RemoveMetadata(string metadataName) { }
        public void SetMetadata(System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<string, string>> metadataDictionary) { }
        public Microsoft.Build.Execution.ProjectMetadataInstance SetMetadata(string name, string evaluatedValue) { throw null; }
        public override string ToString() { throw null; }
    }
    [System.Diagnostics.DebuggerDisplayAttribute("{_name}={EvaluatedValue}")]
    public partial class ProjectMetadataInstance : System.IEquatable<Microsoft.Build.Execution.ProjectMetadataInstance>
    {
        internal ProjectMetadataInstance() { }
        public string EvaluatedValue { [System.Diagnostics.DebuggerStepThroughAttribute]get { throw null; } }
        [System.Diagnostics.DebuggerBrowsableAttribute((System.Diagnostics.DebuggerBrowsableState)(0))]
        public string Name { [System.Diagnostics.DebuggerStepThroughAttribute]get { throw null; } }
        public Microsoft.Build.Execution.ProjectMetadataInstance DeepClone() { throw null; }
        bool System.IEquatable<Microsoft.Build.Execution.ProjectMetadataInstance>.Equals(Microsoft.Build.Execution.ProjectMetadataInstance other) { throw null; }
        public override string ToString() { throw null; }
    }
    [System.Diagnostics.DebuggerDisplayAttribute("ExecuteTargets={_executeTargets} Condition={_condition}")]
    public sealed partial class ProjectOnErrorInstance : Microsoft.Build.Execution.ProjectTargetInstanceChild
    {
        internal ProjectOnErrorInstance() { }
        public override string Condition { get { throw null; } }
        public override Microsoft.Build.Construction.ElementLocation ConditionLocation { get { throw null; } }
        public string ExecuteTargets { get { throw null; } }
        public Microsoft.Build.Construction.ElementLocation ExecuteTargetsLocation { get { throw null; } }
        public override Microsoft.Build.Construction.ElementLocation Location { get { throw null; } }
    }
    [System.Diagnostics.DebuggerDisplayAttribute("Condition={_condition}")]
    public partial class ProjectPropertyGroupTaskInstance : Microsoft.Build.Execution.ProjectTargetInstanceChild
    {
        internal ProjectPropertyGroupTaskInstance() { }
        public override string Condition { [System.Diagnostics.DebuggerStepThroughAttribute]get { throw null; } }
        public override Microsoft.Build.Construction.ElementLocation ConditionLocation { get { throw null; } }
        public override Microsoft.Build.Construction.ElementLocation Location { get { throw null; } }
        public System.Collections.Generic.ICollection<Microsoft.Build.Execution.ProjectPropertyGroupTaskPropertyInstance> Properties { [System.Diagnostics.DebuggerStepThroughAttribute]get { throw null; } }
    }
    [System.Diagnostics.DebuggerDisplayAttribute("{_name}={Value} Condition={_condition}")]
    public partial class ProjectPropertyGroupTaskPropertyInstance
    {
        internal ProjectPropertyGroupTaskPropertyInstance() { }
        public string Condition { [System.Diagnostics.DebuggerStepThroughAttribute]get { throw null; } }
        public Microsoft.Build.Construction.ElementLocation ConditionLocation { get { throw null; } }
        public Microsoft.Build.Construction.ElementLocation Location { get { throw null; } }
        public string Name { [System.Diagnostics.DebuggerStepThroughAttribute]get { throw null; } }
        public string Value { [System.Diagnostics.DebuggerStepThroughAttribute]get { throw null; } }
    }
    [System.Diagnostics.DebuggerDisplayAttribute("{_name}={_escapedValue}")]
    public partial class ProjectPropertyInstance : System.IEquatable<Microsoft.Build.Execution.ProjectPropertyInstance>
    {
        internal ProjectPropertyInstance() { }
        public string EvaluatedValue { [System.Diagnostics.DebuggerStepThroughAttribute]get { throw null; } [System.Diagnostics.DebuggerStepThroughAttribute]set { } }
        public virtual bool IsImmutable { get { throw null; } }
        [System.Diagnostics.DebuggerBrowsableAttribute((System.Diagnostics.DebuggerBrowsableState)(0))]
        public string Name { [System.Diagnostics.DebuggerStepThroughAttribute]get { throw null; } }
        bool System.IEquatable<Microsoft.Build.Execution.ProjectPropertyInstance>.Equals(Microsoft.Build.Execution.ProjectPropertyInstance other) { throw null; }
        public override string ToString() { throw null; }
    }
    [System.Diagnostics.DebuggerDisplayAttribute("Name={_name} Count={_children.Count} Condition={_condition} Inputs={_inputs} Outputs={_outputs} DependsOnTargets={_dependsOnTargets}")]
    public sealed partial class ProjectTargetInstance
    {
        internal ProjectTargetInstance() { }
        public Microsoft.Build.Construction.ElementLocation AfterTargetsLocation { [System.Diagnostics.DebuggerStepThroughAttribute]get { throw null; } }
        public Microsoft.Build.Construction.ElementLocation BeforeTargetsLocation { [System.Diagnostics.DebuggerStepThroughAttribute]get { throw null; } }
        public System.Collections.Generic.IList<Microsoft.Build.Execution.ProjectTargetInstanceChild> Children { [System.Diagnostics.DebuggerStepThroughAttribute]get { throw null; } }
        public string Condition { [System.Diagnostics.DebuggerStepThroughAttribute]get { throw null; } }
        public Microsoft.Build.Construction.ElementLocation ConditionLocation { [System.Diagnostics.DebuggerStepThroughAttribute]get { throw null; } }
        public string DependsOnTargets { [System.Diagnostics.DebuggerStepThroughAttribute]get { throw null; } }
        public Microsoft.Build.Construction.ElementLocation DependsOnTargetsLocation { [System.Diagnostics.DebuggerStepThroughAttribute]get { throw null; } }
        public string FullPath { get { throw null; } }
        public string Inputs { [System.Diagnostics.DebuggerStepThroughAttribute]get { throw null; } }
        public Microsoft.Build.Construction.ElementLocation InputsLocation { [System.Diagnostics.DebuggerStepThroughAttribute]get { throw null; } }
        public string KeepDuplicateOutputs { [System.Diagnostics.DebuggerStepThroughAttribute]get { throw null; } }
        public Microsoft.Build.Construction.ElementLocation KeepDuplicateOutputsLocation { [System.Diagnostics.DebuggerStepThroughAttribute]get { throw null; } }
        public Microsoft.Build.Construction.ElementLocation Location { [System.Diagnostics.DebuggerStepThroughAttribute]get { throw null; } }
        public string Name { [System.Diagnostics.DebuggerStepThroughAttribute]get { throw null; } }
        public System.Collections.Generic.IList<Microsoft.Build.Execution.ProjectOnErrorInstance> OnErrorChildren { [System.Diagnostics.DebuggerStepThroughAttribute]get { throw null; } }
        public string Outputs { [System.Diagnostics.DebuggerStepThroughAttribute]get { throw null; } }
        public Microsoft.Build.Construction.ElementLocation OutputsLocation { [System.Diagnostics.DebuggerStepThroughAttribute]get { throw null; } }
        public string Returns { [System.Diagnostics.DebuggerStepThroughAttribute]get { throw null; } }
        public Microsoft.Build.Construction.ElementLocation ReturnsLocation { [System.Diagnostics.DebuggerStepThroughAttribute]get { throw null; } }
        public System.Collections.Generic.ICollection<Microsoft.Build.Execution.ProjectTaskInstance> Tasks { get { throw null; } }
    }
    public abstract partial class ProjectTargetInstanceChild
    {
        protected ProjectTargetInstanceChild() { }
        public abstract string Condition { get; }
        public abstract Microsoft.Build.Construction.ElementLocation ConditionLocation { get; }
        public string FullPath { get { throw null; } }
        public abstract Microsoft.Build.Construction.ElementLocation Location { get; }
    }
    [System.Diagnostics.DebuggerDisplayAttribute("Name={_name} Condition={_condition} ContinueOnError={_continueOnError} MSBuildRuntime={MSBuildRuntime} MSBuildArchitecture={MSBuildArchitecture} #Parameters={_parameters.Count} #Outputs={_outputs.Count}")]
    public sealed partial class ProjectTaskInstance : Microsoft.Build.Execution.ProjectTargetInstanceChild
    {
        internal ProjectTaskInstance() { }
        public override string Condition { get { throw null; } }
        public override Microsoft.Build.Construction.ElementLocation ConditionLocation { get { throw null; } }
        public string ContinueOnError { get { throw null; } }
        public Microsoft.Build.Construction.ElementLocation ContinueOnErrorLocation { get { throw null; } }
        public override Microsoft.Build.Construction.ElementLocation Location { get { throw null; } }
        public string MSBuildArchitecture { get { throw null; } }
        public Microsoft.Build.Construction.ElementLocation MSBuildArchitectureLocation { get { throw null; } }
        public string MSBuildRuntime { get { throw null; } }
        public Microsoft.Build.Construction.ElementLocation MSBuildRuntimeLocation { get { throw null; } }
        public string Name { get { throw null; } }
        public System.Collections.Generic.IList<Microsoft.Build.Execution.ProjectTaskInstanceChild> Outputs { get { throw null; } }
        public System.Collections.Generic.IDictionary<string, string> Parameters { get { throw null; } }
    }
    public abstract partial class ProjectTaskInstanceChild
    {
        protected ProjectTaskInstanceChild() { }
        public abstract string Condition { get; }
        public abstract Microsoft.Build.Construction.ElementLocation ConditionLocation { get; }
        public abstract Microsoft.Build.Construction.ElementLocation Location { get; }
        public abstract Microsoft.Build.Construction.ElementLocation TaskParameterLocation { get; }
    }
    public sealed partial class ProjectTaskOutputItemInstance : Microsoft.Build.Execution.ProjectTaskInstanceChild
    {
        internal ProjectTaskOutputItemInstance() { }
        public override string Condition { get { throw null; } }
        public override Microsoft.Build.Construction.ElementLocation ConditionLocation { get { throw null; } }
        public string ItemType { get { throw null; } }
        public Microsoft.Build.Construction.ElementLocation ItemTypeLocation { get { throw null; } }
        public override Microsoft.Build.Construction.ElementLocation Location { get { throw null; } }
        public string TaskParameter { get { throw null; } }
        public override Microsoft.Build.Construction.ElementLocation TaskParameterLocation { get { throw null; } }
    }
    public sealed partial class ProjectTaskOutputPropertyInstance : Microsoft.Build.Execution.ProjectTaskInstanceChild
    {
        internal ProjectTaskOutputPropertyInstance() { }
        public override string Condition { get { throw null; } }
        public override Microsoft.Build.Construction.ElementLocation ConditionLocation { get { throw null; } }
        public override Microsoft.Build.Construction.ElementLocation Location { get { throw null; } }
        public string PropertyName { get { throw null; } }
        public Microsoft.Build.Construction.ElementLocation PropertyNameLocation { get { throw null; } }
        public string TaskParameter { get { throw null; } }
        public override Microsoft.Build.Construction.ElementLocation TaskParameterLocation { get { throw null; } }
    }
    public partial class RequestedProjectState
    {
        public RequestedProjectState() { }
        public System.Collections.Generic.IDictionary<string, System.Collections.Generic.List<string>> ItemFilters { get { throw null; } set { } }
        public System.Collections.Generic.List<string> PropertyFilters { get { throw null; } set { } }
    }
    public partial class TargetResult : Microsoft.Build.Execution.ITargetResult
    {
        internal TargetResult() { }
        public System.Exception Exception { [System.Diagnostics.DebuggerStepThroughAttribute]get { throw null; } }
        public Microsoft.Build.Framework.ITaskItem[] Items { [System.Diagnostics.DebuggerStepThroughAttribute]get { throw null; } }
        public Microsoft.Build.Execution.TargetResultCode ResultCode { [System.Diagnostics.DebuggerStepThroughAttribute]get { throw null; } }
    }
    public enum TargetResultCode : byte
    {
        Failure = (byte)2,
        Skipped = (byte)0,
        Success = (byte)1,
    }
}
namespace Microsoft.Build.Globbing
{
    public partial class CompositeGlob : Microsoft.Build.Globbing.IMSBuildGlob
    {
        public CompositeGlob(params Microsoft.Build.Globbing.IMSBuildGlob[] globs) { }
        public CompositeGlob(System.Collections.Generic.IEnumerable<Microsoft.Build.Globbing.IMSBuildGlob> globs) { }
        public System.Collections.Generic.IEnumerable<Microsoft.Build.Globbing.IMSBuildGlob> Globs { [System.Runtime.CompilerServices.CompilerGeneratedAttribute]get { throw null; } }
        public bool IsMatch(string stringToMatch) { throw null; }
    }
    public partial interface IMSBuildGlob
    {
        bool IsMatch(string stringToMatch);
    }
    public partial class MSBuildGlob : Microsoft.Build.Globbing.IMSBuildGlob
    {
        internal MSBuildGlob() { }
        public string FilenamePart { get { throw null; } }
        public string FixedDirectoryPart { get { throw null; } }
        public bool IsLegal { get { throw null; } }
        public string WildcardDirectoryPart { get { throw null; } }
        public bool IsMatch(string stringToMatch) { throw null; }
        public Microsoft.Build.Globbing.MSBuildGlob.MatchInfoResult MatchInfo(string stringToMatch) { throw null; }
        public static Microsoft.Build.Globbing.MSBuildGlob Parse(string fileSpec) { throw null; }
        public static Microsoft.Build.Globbing.MSBuildGlob Parse(string globRoot, string fileSpec) { throw null; }
        [System.Runtime.InteropServices.StructLayoutAttribute(System.Runtime.InteropServices.LayoutKind.Sequential)]
        public partial struct MatchInfoResult
        {
            public string FilenamePartMatchGroup { [System.Runtime.CompilerServices.CompilerGeneratedAttribute]get { throw null; } }
            public string FixedDirectoryPartMatchGroup { [System.Runtime.CompilerServices.CompilerGeneratedAttribute]get { throw null; } }
            public bool IsMatch { [System.Runtime.CompilerServices.CompilerGeneratedAttribute]get { throw null; } }
            public string WildcardDirectoryPartMatchGroup { [System.Runtime.CompilerServices.CompilerGeneratedAttribute]get { throw null; } }
        }
    }
    public partial class MSBuildGlobWithGaps : Microsoft.Build.Globbing.IMSBuildGlob
    {
        public MSBuildGlobWithGaps(Microsoft.Build.Globbing.IMSBuildGlob mainGlob, params Microsoft.Build.Globbing.IMSBuildGlob[] gaps) { }
        public MSBuildGlobWithGaps(Microsoft.Build.Globbing.IMSBuildGlob mainGlob, System.Collections.Generic.IEnumerable<Microsoft.Build.Globbing.IMSBuildGlob> gaps) { }
        public Microsoft.Build.Globbing.IMSBuildGlob Gaps { [System.Runtime.CompilerServices.CompilerGeneratedAttribute]get { throw null; } }
        public Microsoft.Build.Globbing.IMSBuildGlob MainGlob { [System.Runtime.CompilerServices.CompilerGeneratedAttribute]get { throw null; } }
        public bool IsMatch(string stringToMatch) { throw null; }
    }
}
namespace Microsoft.Build.Globbing.Extensions
{
    public static partial class MSBuildGlobExtensions
    {
        public static System.Collections.Generic.IEnumerable<Microsoft.Build.Globbing.MSBuildGlob> GetParsedGlobs(this Microsoft.Build.Globbing.IMSBuildGlob glob) { throw null; }
    }
}
namespace Microsoft.Build.Logging
{
    public sealed partial class BinaryLogger : Microsoft.Build.Framework.ILogger
    {
        public BinaryLogger() { }
        public Microsoft.Build.Logging.BinaryLogger.ProjectImportsCollectionMode CollectProjectImports { [System.Runtime.CompilerServices.CompilerGeneratedAttribute]get { throw null; } [System.Runtime.CompilerServices.CompilerGeneratedAttribute]set { } }
        public string Parameters { [System.Runtime.CompilerServices.CompilerGeneratedAttribute]get { throw null; } [System.Runtime.CompilerServices.CompilerGeneratedAttribute]set { } }
        public Microsoft.Build.Framework.LoggerVerbosity Verbosity { [System.Runtime.CompilerServices.CompilerGeneratedAttribute]get { throw null; } [System.Runtime.CompilerServices.CompilerGeneratedAttribute]set { } }
        public void Initialize(Microsoft.Build.Framework.IEventSource eventSource) { }
        public void Shutdown() { }
        public enum ProjectImportsCollectionMode
        {
            Embed = 1,
            None = 0,
            ZipFile = 2,
        }
    }
    public sealed partial class BinaryLogReplayEventSource : Microsoft.Build.Logging.EventArgsDispatcher
    {
        public BinaryLogReplayEventSource() { }
        public void Replay(string sourceFilePath) { }
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
        public ConsoleLogger(Microsoft.Build.Framework.LoggerVerbosity verbosity, Microsoft.Build.Logging.WriteHandler write, Microsoft.Build.Logging.ColorSetter colorSet, Microsoft.Build.Logging.ColorResetter colorReset) { }
        public string Parameters { get { throw null; } set { } }
        public bool ShowSummary { get { throw null; } set { } }
        public bool SkipProjectStartedText { get { throw null; } set { } }
        public Microsoft.Build.Framework.LoggerVerbosity Verbosity { get { throw null; } set { } }
        protected Microsoft.Build.Logging.WriteHandler WriteHandler { get { throw null; } set { } }
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
    public partial class EventArgsDispatcher : Microsoft.Build.Framework.IEventSource
    {
        public EventArgsDispatcher() { }
        public event Microsoft.Build.Framework.AnyEventHandler AnyEventRaised { add { } remove { } }
        public event Microsoft.Build.Framework.BuildFinishedEventHandler BuildFinished { add { } remove { } }
        public event Microsoft.Build.Framework.BuildStartedEventHandler BuildStarted { add { } remove { } }
        public event Microsoft.Build.Framework.CustomBuildEventHandler CustomEventRaised { add { } remove { } }
        public event Microsoft.Build.Framework.BuildErrorEventHandler ErrorRaised { add { } remove { } }
        public event Microsoft.Build.Framework.BuildMessageEventHandler MessageRaised { add { } remove { } }
        public event Microsoft.Build.Framework.ProjectFinishedEventHandler ProjectFinished { add { } remove { } }
        public event Microsoft.Build.Framework.ProjectStartedEventHandler ProjectStarted { add { } remove { } }
        public event Microsoft.Build.Framework.BuildStatusEventHandler StatusEventRaised { add { } remove { } }
        public event Microsoft.Build.Framework.TargetFinishedEventHandler TargetFinished { add { } remove { } }
        public event Microsoft.Build.Framework.TargetStartedEventHandler TargetStarted { add { } remove { } }
        public event Microsoft.Build.Framework.TaskFinishedEventHandler TaskFinished { add { } remove { } }
        public event Microsoft.Build.Framework.TaskStartedEventHandler TaskStarted { add { } remove { } }
        public event Microsoft.Build.Framework.BuildWarningEventHandler WarningRaised { add { } remove { } }
        public void Dispatch(Microsoft.Build.Framework.BuildEventArgs buildEvent) { }
    }
    public partial class FileLogger : Microsoft.Build.Logging.ConsoleLogger
    {
        public FileLogger() { }
        public override void Initialize(Microsoft.Build.Framework.IEventSource eventSource) { }
        public override void Initialize(Microsoft.Build.Framework.IEventSource eventSource, int nodeCount) { }
        public override void Shutdown() { }
    }
    public partial class ForwardingLoggerRecord
    {
        public ForwardingLoggerRecord(Microsoft.Build.Framework.ILogger centralLogger, Microsoft.Build.Logging.LoggerDescription forwardingLoggerDescription) { }
        public Microsoft.Build.Framework.ILogger CentralLogger { [System.Runtime.CompilerServices.CompilerGeneratedAttribute]get { throw null; } }
        public Microsoft.Build.Logging.LoggerDescription ForwardingLoggerDescription { [System.Runtime.CompilerServices.CompilerGeneratedAttribute]get { throw null; } }
    }
    public partial class LoggerDescription
    {
        public LoggerDescription(string loggerClassName, string loggerAssemblyName, string loggerAssemblyFile, string loggerSwitchParameters, Microsoft.Build.Framework.LoggerVerbosity verbosity) { }
        public string LoggerSwitchParameters { get { throw null; } }
        public Microsoft.Build.Framework.LoggerVerbosity Verbosity { get { throw null; } }
        public Microsoft.Build.Framework.ILogger CreateLogger() { throw null; }
    }
    public sealed partial class ProfilerLogger : Microsoft.Build.Framework.ILogger
    {
        public ProfilerLogger(string fileToLog) { }
        public string FileToLog { [System.Runtime.CompilerServices.CompilerGeneratedAttribute]get { throw null; } }
        public string Parameters { [System.Runtime.CompilerServices.CompilerGeneratedAttribute]get { throw null; } [System.Runtime.CompilerServices.CompilerGeneratedAttribute]set { } }
        public Microsoft.Build.Framework.LoggerVerbosity Verbosity { [System.Runtime.CompilerServices.CompilerGeneratedAttribute]get { throw null; } [System.Runtime.CompilerServices.CompilerGeneratedAttribute]set { } }
        public void Initialize(Microsoft.Build.Framework.IEventSource eventSource) { }
        public void Shutdown() { }
    }
    public delegate void WriteHandler(string message);
}
