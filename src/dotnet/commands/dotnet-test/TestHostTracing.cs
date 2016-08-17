// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Diagnostics;

namespace Microsoft.DotNet.Tools.Test
{
    public static class TestHostTracing
    {
        public static readonly string TracingEnvironmentVariable = "DOTNET_TEST_TRACE";

        public static readonly TraceSource Source;

        static TestHostTracing()
        {
            Source = Environment.GetEnvironmentVariable(TracingEnvironmentVariable) == "1" 
                   ? new TraceSource("dotnet-test", SourceLevels.Verbose) 
                   : new TraceSource("dotnet-test", SourceLevels.Warning);

            Source.Listeners.Add(new TextWriterTraceListener(Console.Error));
        }

        public static void ClearListeners()
        {
            Source.Listeners.Clear();
        }
    }
}
