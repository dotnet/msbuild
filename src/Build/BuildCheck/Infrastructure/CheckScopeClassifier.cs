﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.Build.Experimental.BuildCheck;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;

namespace Microsoft.Build.BuildCheck.Infrastructure;

internal static class CheckScopeClassifier
{
    static CheckScopeClassifier()
    {
        FileClassifier.Shared.OnImmutablePathsInitialized += SubscribeImmutablePathsInitialized;
    }

    internal static event Action? NotifyOnScopingReadiness;

    internal static bool IsScopingInitialized => FileClassifier.Shared.IsImmutablePathsInitialized;

    /// <summary>
    /// Notifies the subscribers that the scoping is ready.
    /// </summary>
    internal static Func<EvaluationCheckScope, bool> IsScopingReady => (scope) => (scope is EvaluationCheckScope.ProjectFileOnly or EvaluationCheckScope.All) || IsScopingInitialized;

    /// <summary>
    /// Indicates whether given location is in the observed scope, based on currently built project path.
    /// </summary>
    /// <param name="scope"></param>
    /// <param name="location"></param>
    /// <param name="projectFileFullPath"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentOutOfRangeException"></exception>
    internal static bool IsActionInObservedScope(
        EvaluationCheckScope scope,
        IMSBuildElementLocation? location,
        string projectFileFullPath)
        => IsActionInObservedScope(scope, location?.File, projectFileFullPath);

    /// <summary>
    /// Indicates whether given location is in the observed scope, based on currently built project path.
    /// </summary>
    /// <param name="scope"></param>
    /// <param name="filePathOfEvent"></param>
    /// <param name="projectFileFullPath"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentOutOfRangeException"></exception>
    internal static bool IsActionInObservedScope(
        EvaluationCheckScope scope,
        string? filePathOfEvent,
        string projectFileFullPath)
    {
        switch (scope)
        {
            case EvaluationCheckScope.ProjectFileOnly:
                return filePathOfEvent == projectFileFullPath;
            case EvaluationCheckScope.WorkTreeImports:
                return
                    filePathOfEvent != null &&
                    !FileClassifier.Shared.IsNonModifiable(filePathOfEvent) &&
                    !IsGeneratedNugetImport(filePathOfEvent);
            case EvaluationCheckScope.All:
                return true;
            default:
                throw new ArgumentOutOfRangeException(nameof(scope), scope, null);
        }
    }

    private static bool IsGeneratedNugetImport(string file) =>
        file.EndsWith("nuget.g.props", StringComparison.OrdinalIgnoreCase)
        || file.EndsWith("nuget.g.targets", StringComparison.OrdinalIgnoreCase);

    private static void SubscribeImmutablePathsInitialized()
    {
        NotifyOnScopingReadiness?.Invoke();

        FileClassifier.Shared.OnImmutablePathsInitialized -= () => NotifyOnScopingReadiness?.Invoke();
    }
}
