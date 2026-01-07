// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Globalization;
using System.Reflection;
using System.Threading;
using Xunit.Sdk;

namespace Xunit.NetCore.Extensions
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    public class UseInvariantCultureAttribute : BeforeAfterTestAttribute
    {
        private CultureInfo? originalCulture;
        private CultureInfo? originalUICulture;

        public override void Before(MethodInfo methodUnderTest)
        {
            originalCulture = CultureInfo.CurrentCulture;
            originalUICulture = CultureInfo.CurrentUICulture;

            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            CultureInfo.CurrentUICulture = CultureInfo.InvariantCulture;
        }

        public override void After(MethodInfo methodUnderTest)
        {
            if (originalCulture != null)
            {
                CultureInfo.CurrentCulture = originalCulture;
            }

            if (originalUICulture != null)
            {
                CultureInfo.CurrentUICulture = originalUICulture;
            }
        }
    }
}
