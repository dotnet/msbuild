// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Experimental.BuildCheck;

namespace Microsoft.Build.Experimental.BuildCheck;

internal class ProjectProcessingDoneData(string projectFilePath) : AnalysisData(projectFilePath);
