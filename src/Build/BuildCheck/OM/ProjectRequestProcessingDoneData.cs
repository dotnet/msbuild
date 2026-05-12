// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Experimental.BuildCheck;

namespace Microsoft.Build.Experimental.BuildCheck;

/// <summary>
/// This data captures end of single build request on a project.
/// There can be multiple build request on a single project within single build
/// (e.g. multiple targetting, or there can be explicit request for results of specific targets)
/// </summary>
/// <param name="projectFilePath"></param>
/// <param name="projectConfigurationId"></param>
internal class ProjectRequestProcessingDoneData(string projectFilePath, int? projectConfigurationId) : CheckData(projectFilePath, projectConfigurationId);
