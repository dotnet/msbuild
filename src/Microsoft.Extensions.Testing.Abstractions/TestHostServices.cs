// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Extensions.Logging;

namespace Microsoft.Extensions.Testing.Abstractions
{
    public abstract class TestHostServices
    {
        public abstract ITestDiscoverySink TestDiscoverySink { get; }

        public abstract ITestExecutionSink TestExecutionSink { get; }

        public abstract ISourceInformationProvider SourceInformationProvider { get; }

        public abstract ILoggerFactory LoggerFactory { get; }
    }
}