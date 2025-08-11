﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Build.Experimental.BuildCheck.Infrastructure;

internal enum BuildCheckConfigurationErrorScope
{
    /// <summary>
    /// Error related to the single rule.
    /// </summary>
    SingleRule,

    /// <summary>
    /// Error related to the parsing of .editorconfig file. 
    /// </summary>
    EditorConfigParser
}
