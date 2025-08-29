﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Build.Logging.TerminalLogger;

/// <summary>
/// Represents a piece of diagnostic output (message/warning/error).
/// </summary>
internal record struct BuildMessage(MessageSeverity Severity, string Message)
{ }
