// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NuGet.Common;

namespace Microsoft.DotNet.PackageValidation.Tests
{
    public class TestLogger : ILogger
    {
        public List<string> warnings = new();
        public List<string> errors = new();

        public void Log(LogLevel level, string data) => throw new NotImplementedException();
        public void Log(ILogMessage message) => throw new NotImplementedException();
        public Task LogAsync(LogLevel level, string data) => throw new NotImplementedException();
        public Task LogAsync(ILogMessage message) => throw new NotImplementedException();
        public void LogDebug(string data) => throw new NotImplementedException();
        public void LogInformation(string data) => throw new NotImplementedException();
        public void LogInformationSummary(string data) => throw new NotImplementedException();
        public void LogMinimal(string data) => throw new NotImplementedException();
        public void LogVerbose(string data) => throw new NotImplementedException();
       
        public void LogWarning(string data)
        {
            warnings.Add(data);
        }

        public void LogError(string data)
        {
            errors.Add(data);
        }
    }
}
