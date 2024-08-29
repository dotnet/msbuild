﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Build.BackEnd;
using Microsoft.Build.Experimental.BuildCheck;
using Microsoft.Build.Experimental.BuildCheck.Infrastructure;

namespace Microsoft.Build.Experimental.BuildCheck.Infrastructure;

internal class NullBuildCheckManagerProvider : IBuildCheckManagerProvider
{
    private readonly NullBuildCheckManager _instance = new NullBuildCheckManager();
    public IBuildCheckManager Instance => _instance;
    public IBuildEngineDataRouter BuildEngineDataRouter => _instance;

    public void InitializeComponent(IBuildComponentHost host) { }
    public void ShutdownComponent() { }
}
