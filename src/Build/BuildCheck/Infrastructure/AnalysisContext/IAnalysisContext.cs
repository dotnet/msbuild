﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Experimental.BuildCheck;

/// <summary>
/// Interface for dispatching <see cref="BuildEventContext"/>.
/// </summary>
internal interface IAnalysisContext
{
    BuildEventContext BuildEventContext { get; }

    void DispatchAsComment(MessageImportance importance, string messageResourceName, params object?[] messageArgs);

    void DispatchBuildEvent(BuildEventArgs buildEvent);

    void DispatchAsErrorFromText(string? subcategoryResourceName, string? errorCode, string? helpKeyword, BuildEventFileInfo file, string message);

    void DispatchAsCommentFromText(MessageImportance importance, string message);
}