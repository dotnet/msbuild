// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.TemplatePackage;
using Microsoft.TemplateEngine.Edge.Settings;

namespace Microsoft.TemplateEngine.Cli
{
    /// <summary>
    /// The class provides ITemplateInfo extension methods.
    /// </summary>
    internal static class TemplateInfoExtensions
    {
        /// <summary>
        /// Helper method that returns <see cref="ITemplatePackage"/> that contains <paramref name="template"/>.
        /// </summary>
        public static async Task<ITemplatePackage> GetTemplatePackageAsync(this ITemplateInfo template, TemplatePackageManager templatePackagesManager)
        {
            IReadOnlyList<ITemplatePackage> templatePackages = await templatePackagesManager.GetTemplatePackagesAsync().ConfigureAwait(false);
            return templatePackages.Single(s => s.MountPointUri == template.MountPointUri);
        }
    }
}
