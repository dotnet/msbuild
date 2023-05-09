// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.Constraints;
using Microsoft.TemplateEngine.Abstractions.Parameters;
using Microsoft.TemplateEngine.Abstractions.TemplatePackage;
using Microsoft.TemplateEngine.Edge.Settings;
using Microsoft.TemplateEngine.Utils;

namespace Microsoft.TemplateEngine.Cli
{
    /// <summary>
    /// <see cref="ITemplateInfo"/> + <see cref="HostSpecificTemplateData"/>.
    /// </summary>
    internal class CliTemplateInfo : ITemplateInfo
    {
        private readonly ITemplateInfo _templateInfo;
        private readonly HostSpecificTemplateData _cliData;
        private IReadOnlyDictionary<string, CliTemplateParameter>? _parameters;

        internal CliTemplateInfo(ITemplateInfo templateInfo, HostSpecificTemplateData cliData)
        {
            _templateInfo = templateInfo ?? throw new ArgumentNullException(nameof(templateInfo));
            _cliData = cliData ?? throw new ArgumentNullException(nameof(cliData));
        }

        public string? Author => _templateInfo.Author;

        public string? Description => _templateInfo.Description;

        public IReadOnlyList<string> Classifications => _templateInfo.Classifications;

        public string? DefaultName => _templateInfo.DefaultName;

        public string Identity => _templateInfo.Identity;

        public Guid GeneratorId => _templateInfo.GeneratorId;

        public string? GroupIdentity => _templateInfo.GroupIdentity;

        public int Precedence => _templateInfo.Precedence;

        public string Name => _templateInfo.Name;

        [Obsolete]
        public string ShortName => _templateInfo.ShortName;

        [Obsolete]
        public IReadOnlyDictionary<string, ICacheTag> Tags => _templateInfo.Tags;

        public IReadOnlyDictionary<string, string> TagsCollection => _templateInfo.TagsCollection;

        [Obsolete]
        public IReadOnlyDictionary<string, ICacheParameter> CacheParameters => _templateInfo.CacheParameters;

        public IParameterDefinitionSet ParameterDefinitions => _templateInfo.ParameterDefinitions;

        [Obsolete("Use ParameterDefinitionSet instead.")]
        public IReadOnlyList<ITemplateParameter> Parameters => ParameterDefinitions;

        public string MountPointUri => _templateInfo.MountPointUri;

        public string ConfigPlace => _templateInfo.ConfigPlace;

        public string? LocaleConfigPlace => _templateInfo.LocaleConfigPlace;

        public string? HostConfigPlace => _templateInfo.HostConfigPlace;

        public string? ThirdPartyNotices => _templateInfo.ThirdPartyNotices;

        public bool PreferDefaultName => _templateInfo.PreferDefaultName;

        public IReadOnlyDictionary<string, IBaselineInfo> BaselineInfo => _templateInfo.BaselineInfo;

        [Obsolete]
        public bool HasScriptRunningPostActions { get => _templateInfo.HasScriptRunningPostActions; set => _templateInfo.HasScriptRunningPostActions = value; }

        public IReadOnlyList<string> ShortNameList => _templateInfo.ShortNameList;

        public IReadOnlyList<Guid> PostActions => _templateInfo.PostActions;

        public IReadOnlyList<TemplateConstraintInfo> Constraints => _templateInfo.Constraints;

        internal HostSpecificTemplateData CliData => _cliData;

        internal bool IsHidden => _cliData.IsHidden;

        internal IReadOnlyDictionary<string, CliTemplateParameter> CliParameters
        {
            get
            {
                if (_parameters == null)
                {
                    Dictionary<string, CliTemplateParameter> parameters = new();
                    foreach (ITemplateParameter parameter in ParameterDefinitions.Where(param => param.Type == "parameter"))
                    {
                        if (parameters.ContainsKey(parameter.Name))
                        {
                            //runnable projects generator ensures the symbols are unique, so no error handling here.
                            //in case there is a duplicate, the logic is broken.
                            throw new Exception($"Template {Identity} defines {parameter.Name} twice.");
                        }
                        if (parameter.IsChoice())
                        {
                            parameters[parameter.Name] = new ChoiceTemplateParameter(parameter, CliData);
                        }
                        else
                        {
                            parameters[parameter.Name] = new CliTemplateParameter(parameter, CliData);
                        }
                    }
                    _parameters = parameters;
                }
                return _parameters;
            }
        }

        internal static IEnumerable<CliTemplateInfo> FromTemplateInfo(IEnumerable<ITemplateInfo> templateInfos, IHostSpecificDataLoader hostSpecificDataLoader)
        {
            if (templateInfos is null)
            {
                throw new ArgumentNullException(nameof(templateInfos));
            }

            if (hostSpecificDataLoader is null)
            {
                throw new ArgumentNullException(nameof(hostSpecificDataLoader));
            }

            return templateInfos.Select(templateInfo => new CliTemplateInfo(templateInfo, hostSpecificDataLoader.ReadHostSpecificTemplateData(templateInfo)));
        }

        /// <summary>
        /// Gets the <b>managed</b> template package which contain the template, <see langword="null"/> otherwise.
        /// </summary>
        /// <remarks>
        /// The method might throw exceptions if <see cref="TemplatePackageManager.GetTemplatePackageAsync(ITemplateInfo, CancellationToken)"/> call throws.
        /// </remarks>
        internal async Task<IManagedTemplatePackage?> GetManagedTemplatePackageAsync(
            TemplatePackageManager templatePackageManager,
            CancellationToken cancellationToken)
        {
            ITemplatePackage templatePackage = await GetTemplatePackageAsync(templatePackageManager, cancellationToken).ConfigureAwait(false);
            return templatePackage as IManagedTemplatePackage;
        }

        /// <summary>
        /// Gets the template package which contains the template.
        /// </summary>
        /// <remarks>
        /// The method might throw exceptions if <see cref="TemplatePackageManager.GetTemplatePackageAsync(ITemplateInfo, CancellationToken)"/> call throws.
        /// </remarks>
        internal Task<ITemplatePackage> GetTemplatePackageAsync(
            TemplatePackageManager templatePackageManager,
            CancellationToken cancellationToken)
        {
            return templatePackageManager.GetTemplatePackageAsync(this, cancellationToken);
        }
    }
}
