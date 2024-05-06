// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Build.BackEnd.Logging;

namespace Microsoft.Build.BuildCheck.Infrastructure;

/// <summary>
/// Forwarding logger for the build check infrastructure.
/// For now we jus want to forward all events, while disable verbose logging of tasks.
/// In the future we may need more specific behavior.
/// </summary>
internal class BuildCheckForwardingLogger : CentralForwardingLogger
{ }
