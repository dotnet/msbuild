// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using NuGet.Frameworks;

namespace NuGet.Legacy
{
    public class FrameworkAssemblyReference
    {
        public FrameworkAssemblyReference(string assemblyName, IEnumerable<NuGetFramework> supportedFrameworks)
        {
            if (string.IsNullOrEmpty(assemblyName))
            {
                throw new ArgumentException(nameof(assemblyName));
            }

            if (supportedFrameworks == null)
            {
                throw new ArgumentNullException(nameof(supportedFrameworks));
            }

            AssemblyName = assemblyName;
            SupportedFrameworks = supportedFrameworks;
        }

        public string AssemblyName { get; private set; }

        public IEnumerable<NuGetFramework> SupportedFrameworks { get; private set; }
    }
}