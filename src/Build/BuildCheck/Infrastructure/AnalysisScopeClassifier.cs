﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.Build.Experimental.BuildCheck;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;

namespace Microsoft.Build.BuildCheck.Infrastructure;
internal static class AnalysisScopeClassifier
{
    /// <summary>
    /// Indicates whether given location is in the observed scope, based on currently built project path.
    /// </summary>
    /// <param name="scope"></param>
    /// <param name="location"></param>
    /// <param name="projectFileFullPath"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentOutOfRangeException"></exception>
    internal static bool IsActionInObservedScope(
        EvaluationAnalysisScope scope,
        IMsBuildElementLocation? location,
        string projectFileFullPath)
    {
        switch (scope)
        {
            case EvaluationAnalysisScope.ProjectFileOnly:
                return location != null && location.File == projectFileFullPath;
            case EvaluationAnalysisScope.WorkTreeImports:
                return
                    location != null &&
                    !FileClassifier.Shared.IsNonModifiable(location.File) &&
                    !IsGeneratedNugetImport(location.File);
            case EvaluationAnalysisScope.All:
                return true;
            default:
                throw new ArgumentOutOfRangeException(nameof(scope), scope, null);
        }
    }

    private static bool IsGeneratedNugetImport(string file)
    {
        return file.EndsWith("nuget.g.props", StringComparison.OrdinalIgnoreCase) ||
               file.EndsWith("nuget.g.targets", StringComparison.OrdinalIgnoreCase);
    }
}
