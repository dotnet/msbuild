// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Build.BackEnd.Logging;
using Microsoft.Build.Framework;

namespace Microsoft.Build.Experimental.BuildCheck;

internal class CheckLoggingContextFactory : ICheckContextFactory
{
    private readonly ILoggingService _loggingService;

    public CheckLoggingContextFactory(ILoggingService loggingService) => _loggingService = loggingService;

    public ICheckContext CreateCheckContext(BuildEventContext eventContext)
        => new CheckLoggingContext(_loggingService, eventContext);
}
