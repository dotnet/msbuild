// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Edge.Settings;

namespace Microsoft.TemplateEngine.Cli.TemplateResolution
{
    internal abstract class BaseTemplateResolver<T> : BaseTemplateResolver
    {
        internal BaseTemplateResolver(TemplatePackageManager templatePackageManager, IHostSpecificDataLoader hostSpecificDataLoader)
            : base(templatePackageManager, hostSpecificDataLoader) { }

        internal BaseTemplateResolver(IEnumerable<ITemplateInfo> templateList, IHostSpecificDataLoader hostSpecificDataLoader)
            : base(templateList, hostSpecificDataLoader) { }

        internal abstract Task<TemplateResolutionResult> ResolveTemplatesAsync(T args, string? defaultLanguage, CancellationToken cancellationToken);
    }

    internal abstract class BaseTemplateResolver
    {
        private readonly IReadOnlyList<ITemplateInfo>? _templateList;
        private readonly TemplatePackageManager? _templatePackageManager;

        internal BaseTemplateResolver(TemplatePackageManager templatePackageManager, IHostSpecificDataLoader hostSpecificDataLoader)
        {
            _templatePackageManager = templatePackageManager ?? throw new ArgumentNullException(nameof(templatePackageManager));
            HostSpecificDataLoader = hostSpecificDataLoader ?? throw new ArgumentNullException(nameof(hostSpecificDataLoader));
        }

        internal BaseTemplateResolver(IEnumerable<ITemplateInfo> templateList, IHostSpecificDataLoader hostSpecificDataLoader)
        {
            _templateList = templateList?.ToList() ?? throw new ArgumentNullException(nameof(templateList));
            HostSpecificDataLoader = hostSpecificDataLoader ?? throw new ArgumentNullException(nameof(hostSpecificDataLoader));
        }

        protected IHostSpecificDataLoader HostSpecificDataLoader { get; }

        protected async Task<IEnumerable<TemplateGroup>> GetTemplateGroupsAsync(CancellationToken cancellationToken)
        {
            IEnumerable<ITemplateInfo> templates;
            if (_templateList != null)
            {
                templates = _templateList;
            }
            else if (_templatePackageManager != null)
            {
                templates = await _templatePackageManager.GetTemplatesAsync(cancellationToken).ConfigureAwait(false);
            }
            else
            {
                throw new Exception($"Both {nameof(_templateList)} and {nameof(_templatePackageManager)} cannot be null.");
            }
            templates = templates.Where(x => !x.IsHiddenByHostFile(HostSpecificDataLoader));
            return TemplateGroup.FromTemplateList(CliTemplateInfo.FromTemplateInfo(templates, HostSpecificDataLoader));
        }

    }
}
