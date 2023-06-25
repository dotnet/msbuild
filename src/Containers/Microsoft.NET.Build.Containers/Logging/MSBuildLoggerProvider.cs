// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.Build.Utilities;
using Microsoft.Extensions.Logging;

namespace Microsoft.NET.Build.Containers.Logging;

/// <summary>
/// An <see cref="ILoggerProvider"/> that creates <see cref="ILogger"/>s which passes
/// all the logs to MSBuild's <see cref="TaskLoggingHelper"/>.
/// </summary>
internal class MSBuildLoggerProvider : ILoggerProvider
{
    private readonly TaskLoggingHelper _loggingHelper;

    public MSBuildLoggerProvider(TaskLoggingHelper loggingHelperToWrap)
    {
        _loggingHelper = loggingHelperToWrap;
    }

    public ILogger CreateLogger(string categoryName)
    {
        return new MSBuildLogger(categoryName, _loggingHelper);
    }

    public void Dispose() { }
}
