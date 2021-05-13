// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.NET.Build.Tasks;

namespace Microsoft.DotNet.PackageValidation
{
    internal class PackageValidationLogger : IPackageLogger
    {
        private readonly Logger _log;

        public PackageValidationLogger(Logger log)
        {
            _log = log;
        }

        public void LogError(string code, string format, params string[] args)
        {
            _log.LogNonSdkError(code, format, args);
        }
    }
}
