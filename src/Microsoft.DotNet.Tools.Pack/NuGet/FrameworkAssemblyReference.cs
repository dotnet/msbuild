using System;
using System.Collections.Generic;
using NuGet.Frameworks;

namespace NuGet
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