using Microsoft.Extensions.DependencyModel;
using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.NET.TestFramework.Assertions
{
    public static class DependencyContextExtensions
    {
        public static DependencyContextAssertions Should(this DependencyContext dependencyContext)
        {
            return new DependencyContextAssertions(dependencyContext);
        }
    }
}
