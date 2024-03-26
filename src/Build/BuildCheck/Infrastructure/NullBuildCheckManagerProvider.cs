// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Build.BackEnd;
using Microsoft.Build.Experimental.BuildCheck;

namespace Microsoft.Build.BuildCheck.Infrastructure;

internal class NullBuildCheckManagerProvider : IBuildCheckManagerProvider
{
    public IBuildCheckManager Instance { get; } = new NullBuildCheckManager();

    public void InitializeComponent(IBuildComponentHost host) { }
    public void ShutdownComponent() { }
}
