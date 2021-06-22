// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.Framework;
using Microsoft.DotNet.ApiCompatibility.Abstractions;
using Microsoft.DotNet.Compatibility.ErrorSuppression;

namespace Microsoft.DotNet.PackageValidation
{
    public interface IPackageLogger
    {
        void LogError(Suppression suppression, string code, string format, params string[] args);
        void LogErrorHeader(string message);
        void LogMessage(MessageImportance importance, string format, params string[] args);
    }
}
