// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.TemplatePackage;
using Microsoft.TemplateEngine.Edge.Settings;
using Microsoft.TemplateEngine.Utils;

namespace Microsoft.TemplateEngine.Cli
{
    /// <summary>
    /// The class represents template group. Templates in single group:<br/>
    /// - should same group identity
    /// - should have different template identity <br/>
    /// - same short name (however different short names are also supported) <br/>
    /// - the templates may have different languages and types <br/>
    /// - the templates should have different precedence value in case same language is used <br/>
    /// - the templates in the group may have different parameters and different choices for parameter symbols defined<br/>
    /// In case the template does not have group identity defined it represents separate template group with single template.
    /// </summary>
    internal sealed class TemplateGroup
    {
        /// <summary>
        /// Constructor of TemplateGroup.
        /// </summary>
        /// <param name="templates">the templates of the template group.</param>
        /// <exception cref="ArgumentNullException">when <paramref name="templates"/> is <c>null</c>.</exception>
        /// <exception cref="ArgumentException">when <paramref name="templates"/> is empty or don't have same <see cref="ITemplateMetadata.GroupIdentity"/> defined.</exception>
        internal TemplateGroup(IEnumerable<CliTemplateInfo> templates)
        {
            _ = templates ?? throw new ArgumentNullException(paramName: nameof(templates));
            if (!templates.Any())
            {
                throw new ArgumentException(paramName: nameof(templates), message: "The templates collection cannot be empty");
            }

            try
            {
                //all templates in the group should have same group identity
                GroupIdentity = templates.Select(t => string.IsNullOrWhiteSpace(t.GroupIdentity) ? null : t.GroupIdentity)
                                            .Distinct(StringComparer.OrdinalIgnoreCase)
                                            .Single();
            }
            catch (InvalidOperationException)
            {
                throw new ArgumentException(paramName: nameof(templates), message: "The templates should have same group identity");
            }
            Templates = templates.ToList();
        }

        /// <summary>
        /// Group identity of template group. The value can be null if the template does not have group identity set.
        /// </summary>
        internal string? GroupIdentity { get; private set; }

        /// <summary>
        /// Returns the list of short names defined for templates in the group.
        /// In theory, template group templates can have different short names but they are treated equally.
        /// </summary>
        internal IReadOnlyList<string> ShortNames
        {
            get
            {
                HashSet<string> shortNames = new(StringComparer.OrdinalIgnoreCase);
                foreach (ITemplateInfo template in Templates.OrderByDescending(t => t.Precedence))
                {
                    shortNames.UnionWith(template.ShortNameList);
                }
                return shortNames.ToList();
            }
        }

        /// <summary>
        /// Returns the list of languages defined for templates in the group.
        /// </summary>
        internal IReadOnlyList<string?> Languages
        {
            get
            {
                HashSet<string?> language = new(StringComparer.OrdinalIgnoreCase);
                foreach (ITemplateInfo template in Templates)
                {
                    language.Add(template.GetLanguage());
                }
                return language.ToList();
            }
        }

        /// <summary>
        /// Returns the list of types defined for templates in the group.
        /// </summary>
        internal IReadOnlyList<string?> Types
        {
            get
            {
                HashSet<string?> type = new(StringComparer.OrdinalIgnoreCase);
                foreach (ITemplateInfo template in Templates)
                {
                    type.Add(template.GetTemplateType());
                }
                return type.ToList();
            }
        }

        /// <summary>
        /// Returns the list of baselines defined for templates in the group.
        /// </summary>
        internal IReadOnlyList<string> Baselines
        {
            get
            {
                HashSet<string> baselines = new(StringComparer.OrdinalIgnoreCase);
                foreach (ITemplateInfo template in Templates)
                {
                    foreach (var baseline in template.BaselineInfo)
                    {
                        baselines.Add(baseline.Key);
                    }
                }
                return baselines.ToList();
            }
        }

        /// <summary>
        /// Returns the full name of template group
        /// Template group name is the name of highest precedence template in the group.
        /// If multiple templates have the maximum precedence, the name of first one is returned.
        /// </summary>
        internal string Name
        {
            get
            {
                return GetHighestPrecedenceTemplates().First().Name;
            }
        }

        /// <summary>
        /// Returns the description of template group.
        /// Template group description is the description of the template in the group with the highest precedence.
        /// If multiple templates have the maximum precedence, the description of the first one is returned.
        /// </summary>
        internal string Description
        {
            get
            {
                return GetHighestPrecedenceTemplates().First().Description ?? string.Empty;
            }
        }

        /// <summary>
        /// Returns the authors of template group.
        /// If different templates have different authors, lists all of them.
        /// </summary>
        internal IReadOnlyList<string> Authors
        {
            get
            {
                HashSet<string> authors = new(StringComparer.OrdinalIgnoreCase);
                foreach (ITemplateInfo template in Templates)
                {
                    if (!string.IsNullOrWhiteSpace(template.Author))
                    {
                        authors.Add(template.Author);
                    }
                }
                return authors.ToList();
            }
        }

        /// <summary>
        /// Returns true when <see cref="GroupIdentity"/> is not <c>null</c> or empty.
        /// </summary>
        internal bool HasGroupIdentity => !string.IsNullOrWhiteSpace(GroupIdentity);

        /// <summary>
        /// Returns true when the template group has single template.
        /// </summary>
        internal bool HasSingleTemplate => Templates.Count == 1;

        /// <summary>
        /// Returns the list of templates in the group.
        /// </summary>
        internal IReadOnlyList<CliTemplateInfo> Templates { get; private set; }

        internal static IEnumerable<TemplateGroup> FromTemplateList(IEnumerable<CliTemplateInfo> templates)
        {
            return templates
              .GroupBy(x => x.GroupIdentity, x => !string.IsNullOrEmpty(x.GroupIdentity), StringComparer.OrdinalIgnoreCase)
              .Select(group => new TemplateGroup(group.ToList()));
        }

        /// <summary>
        /// Gets the list of <b>managed</b> template packages which contain templates of template group.
        /// </summary>
        /// <remarks>
        /// The method might throw exceptions if <see cref="TemplatePackageManager.GetTemplatePackageAsync(ITemplateInfo, CancellationToken)"/> call throws.
        /// </remarks>
        internal async Task<IReadOnlyList<IManagedTemplatePackage>> GetManagedTemplatePackagesAsync(
            TemplatePackageManager templatePackageManager,
            CancellationToken cancellationToken)
        {
            var templatePackages = await GetTemplatePackagesAsync(templatePackageManager, cancellationToken).ConfigureAwait(false);

            return templatePackages.OfType<IManagedTemplatePackage>().ToArray();
        }

        /// <summary>
        /// Gets the list of template packages which contain templates of template group.
        /// </summary>
        /// <remarks>
        /// The method might throw exceptions if <see cref="TemplatePackageManager.GetTemplatePackageAsync(ITemplateInfo, CancellationToken)"/> call throws.
        /// </remarks>
        internal async Task<IReadOnlyList<ITemplatePackage>> GetTemplatePackagesAsync(
            TemplatePackageManager templatePackageManager,
            CancellationToken cancellationToken)
        {
            var templatePackages = await Task.WhenAll(Templates.Select(t => templatePackageManager.GetTemplatePackageAsync(t, cancellationToken))).ConfigureAwait(false);
            return templatePackages.Distinct().ToArray();
        }

        private IEnumerable<ITemplateInfo> GetHighestPrecedenceTemplates()
        {
            if (!Templates.Any())
            {
                throw new Exception($"{nameof(Templates)} cannot be empty collection");
            }

            int highestPrecedence = Templates.Max(t => t.Precedence);
            return Templates.Where(t => t.Precedence == highestPrecedence);
        }
    }
}
