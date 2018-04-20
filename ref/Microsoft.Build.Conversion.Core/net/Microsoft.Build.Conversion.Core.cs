namespace Microsoft.Build.Conversion
{
    public sealed partial class ProjectFileConverter
    {
        public ProjectFileConverter() { }
        public bool ConversionSkippedBecauseProjectAlreadyConverted { get { throw null; } }
        public string[] ConversionWarnings { get { throw null; } }
        public bool IsMinorUpgrade { get { throw null; } set { } }
        public bool IsUserFile { get { throw null; } set { } }
        public string NewProjectFile { get { throw null; } set { } }
        public string OldProjectFile { get { throw null; } set { } }
        public string SolutionFile { get { throw null; } set { } }
        public void Convert() { }
        [System.ObsoleteAttribute("Use parameterless overload instead")]
        public void Convert(Microsoft.Build.BuildEngine.ProjectLoadSettings projectLoadSettings) { }
        [System.ObsoleteAttribute("Use parameterless overload instead.")]
        public void Convert(string msbuildBinPath) { }
        public Microsoft.Build.Construction.ProjectRootElement ConvertInMemory() { throw null; }
        [System.ObsoleteAttribute("Use parameterless ConvertInMemory() method instead")]
        public Microsoft.Build.BuildEngine.Project ConvertInMemory(Microsoft.Build.BuildEngine.Engine engine) { throw null; }
        [System.ObsoleteAttribute("Use parameterless ConvertInMemory() method instead")]
        public Microsoft.Build.BuildEngine.Project ConvertInMemory(Microsoft.Build.BuildEngine.Engine engine, Microsoft.Build.BuildEngine.ProjectLoadSettings projectLoadSettings) { throw null; }
        public bool FSharpSpecificConversions(bool actuallyMakeChanges) { throw null; }
    }
}
