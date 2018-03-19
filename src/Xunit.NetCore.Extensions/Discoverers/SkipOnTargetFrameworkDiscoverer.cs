// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Linq;
using System.Collections.Generic;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace Xunit.NetCore.Extensions
{
    /// <summary>
    /// This class discovers all of the tests and test classes that have
    /// applied the TestOnTargetFrameworkDiscoverer attribute
    /// </summary>
    public class SkipOnTargetFrameworkDiscoverer : ITraitDiscoverer
    {
        /// <summary>
        /// Gets the trait values from the Category attribute.
        /// </summary>
        /// <param name="traitAttribute">The trait attribute containing the trait values.</param>
        /// <returns>The trait values.</returns>
        public IEnumerable<KeyValuePair<string, string>> GetTraits(IAttributeInfo traitAttribute)
        {
            TargetFrameworkMonikers platform = (TargetFrameworkMonikers)traitAttribute.GetConstructorArguments().First();
            if (platform.HasFlag(TargetFrameworkMonikers.Net45))
                yield return new KeyValuePair<string, string>(XunitConstants.Category, XunitConstants.NonNet45Test);
            if (platform.HasFlag(TargetFrameworkMonikers.Net451))
                yield return new KeyValuePair<string, string>(XunitConstants.Category, XunitConstants.NonNet451Test);
            if (platform.HasFlag(TargetFrameworkMonikers.Net452))
                yield return new KeyValuePair<string, string>(XunitConstants.Category, XunitConstants.NonNet452Test);
            if (platform.HasFlag(TargetFrameworkMonikers.Net46))
                yield return new KeyValuePair<string, string>(XunitConstants.Category, XunitConstants.NonNet46Test);
            if (platform.HasFlag(TargetFrameworkMonikers.Net461))
                yield return new KeyValuePair<string, string>(XunitConstants.Category, XunitConstants.NonNet461Test);
            if (platform.HasFlag(TargetFrameworkMonikers.Net462))
                yield return new KeyValuePair<string, string>(XunitConstants.Category, XunitConstants.NonNet462Test);
            if (platform.HasFlag(TargetFrameworkMonikers.Net463))
                yield return new KeyValuePair<string, string>(XunitConstants.Category, XunitConstants.NonNet463Test);
            if (platform.HasFlag(TargetFrameworkMonikers.Netcore50))
                yield return new KeyValuePair<string, string>(XunitConstants.Category, XunitConstants.NonNetcore50Test);
            if (platform.HasFlag(TargetFrameworkMonikers.Netcore50aot))
                yield return new KeyValuePair<string, string>(XunitConstants.Category, XunitConstants.NonNetcore50aotTest);
            if (platform.HasFlag(TargetFrameworkMonikers.Netcoreapp1_0))
                yield return new KeyValuePair<string, string>(XunitConstants.Category, XunitConstants.NonNetcoreapp1_0Test);
            if (platform.HasFlag(TargetFrameworkMonikers.Netcoreapp1_1))
                yield return new KeyValuePair<string, string>(XunitConstants.Category, XunitConstants.NonNetcoreapp1_1Test);
            if (platform.HasFlag(TargetFrameworkMonikers.NetFramework))
                yield return new KeyValuePair<string, string>(XunitConstants.Category, XunitConstants.NonNetfxTest);
            if (platform.HasFlag(TargetFrameworkMonikers.Mono))
                yield return new KeyValuePair<string, string>(XunitConstants.Category, XunitConstants.NonMonoTest);
            if (platform.HasFlag(TargetFrameworkMonikers.Netcoreapp))
                yield return new KeyValuePair<string, string>(XunitConstants.Category, XunitConstants.NonNetcoreappTest);
            if (platform.HasFlag(TargetFrameworkMonikers.UapNotUapAot))
                yield return new KeyValuePair<string, string>(XunitConstants.Category, XunitConstants.NonUapTest);
            if (platform.HasFlag(TargetFrameworkMonikers.UapAot))
                yield return new KeyValuePair<string, string>(XunitConstants.Category, XunitConstants.NonUapAotTest);
            if (platform.HasFlag(TargetFrameworkMonikers.NetcoreCoreRT))
                yield return new KeyValuePair<string, string>(XunitConstants.Category, XunitConstants.NonNetcoreCoreRTTest);
        }
    }
}
