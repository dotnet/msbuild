// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using Microsoft.TemplateEngine.Abstractions;
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
        private readonly IReadOnlyList<CliTemplateParameter> _parameters;

        internal CliTemplateInfo(ITemplateInfo templateInfo, HostSpecificTemplateData cliData)
        {
            _templateInfo = templateInfo ?? throw new ArgumentNullException(nameof(templateInfo));
            _cliData = cliData ?? throw new ArgumentNullException(nameof(cliData));

            HashSet<string> processedParameters = new HashSet<string>();
            List<CliTemplateParameter> parameters = new List<CliTemplateParameter>();

            foreach (ITemplateParameter parameter in Parameters.Where(param => param.Type == "parameter"))
            {
                if (!processedParameters.Add(parameter.Name))
                {
                    //TODO:
                    throw new Exception($"Template {Identity} defines {parameter.Name} twice.");
                }
                if (parameter.IsChoice())
                {
                    parameters.Add(new ChoiceTemplateParameter(parameter, CliData));
                }
                else
                {
                    parameters.Add(new CliTemplateParameter(parameter, CliData));
                }
            }
            _parameters = parameters;
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

        public IReadOnlyList<ITemplateParameter> Parameters => _templateInfo.Parameters;

        public string MountPointUri => _templateInfo.MountPointUri;

        public string ConfigPlace => _templateInfo.ConfigPlace;

        public string? LocaleConfigPlace => _templateInfo.LocaleConfigPlace;

        public string? HostConfigPlace => _templateInfo.HostConfigPlace;

        public string? ThirdPartyNotices => _templateInfo.ThirdPartyNotices;

        public IReadOnlyDictionary<string, IBaselineInfo> BaselineInfo => _templateInfo.BaselineInfo;

        [Obsolete]
        public bool HasScriptRunningPostActions { get => _templateInfo.HasScriptRunningPostActions; set => _templateInfo.HasScriptRunningPostActions = value; }

        public IReadOnlyList<string> ShortNameList => _templateInfo.ShortNameList;

        internal HostSpecificTemplateData CliData => _cliData;

        internal bool IsHidden => _cliData.IsHidden;

        internal IEnumerable<CliTemplateParameter> CliParameters => _parameters;

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
    }
}
