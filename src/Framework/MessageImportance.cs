﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Microsoft.Build.Framework;

/// <summary>
/// This enumeration provides three levels of importance for messages.
/// </summary>
[Serializable]
public enum MessageImportance
{
    /// <summary>
    /// High importance, appears in less verbose logs
    /// </summary>
    High,

    /// <summary>
    /// Normal importance
    /// </summary>
    Normal,

    /// <summary>
    /// Low importance, appears in more verbose logs
    /// </summary>
    Low
}
