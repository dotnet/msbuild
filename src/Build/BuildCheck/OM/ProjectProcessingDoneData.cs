using Microsoft.Build.Experimental.BuildCheck;

namespace Microsoft.Build.BuildCheck.Analyzers;

internal class ProjectProcessingDoneData(string projectFilePath) : AnalysisData(projectFilePath);