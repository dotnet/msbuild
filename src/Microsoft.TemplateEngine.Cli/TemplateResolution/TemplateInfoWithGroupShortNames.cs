// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Edge.Settings;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Cli.TemplateResolution
{
    /// <summary>
    /// In addition to <see cref="ITemplateInfo"/> the class contains <see cref="GroupShortNameList"/> property which contains the short names of other templates in the template group.
    /// The class is used for template filtering using specific TemplateResolver.CliNameFilter which takes into account the short names of template group when matching names.
    /// </summary>
    internal class TemplateInfoWithGroupShortNames : ITemplateInfo, ITemplateInfoHostJsonCache
    {
        private ITemplateInfo _parent;

        internal TemplateInfoWithGroupShortNames(ITemplateInfo source, IEnumerable<string> groupShortNameList)
        {
            _parent = source;
            GroupShortNameList = groupShortNameList.ToList();
        }

        public string? Author => _parent.Author;

        public string? Description => _parent.Description;

        public IReadOnlyList<string> Classifications => _parent.Classifications;

        public string? DefaultName => _parent.DefaultName;

        public string Identity => _parent.Identity;

        public Guid GeneratorId => _parent.GeneratorId;

        public string? GroupIdentity => _parent.GroupIdentity;

        public int Precedence => _parent.Precedence;

        public string Name => _parent.Name;

        [Obsolete]
        public string ShortName => _parent.ShortName;

        public IReadOnlyList<string> ShortNameList => _parent.ShortNameList;

        public IReadOnlyList<string> GroupShortNameList { get; } = new List<string>();

        [Obsolete]
        public IReadOnlyDictionary<string, ICacheTag> Tags => _parent.Tags;

        [Obsolete]
        public IReadOnlyDictionary<string, ICacheParameter> CacheParameters => _parent.CacheParameters;

        public IReadOnlyList<ITemplateParameter> Parameters => _parent.Parameters;

        public string MountPointUri => _parent.MountPointUri;

        public string ConfigPlace => _parent.ConfigPlace;

        public string? LocaleConfigPlace => _parent.LocaleConfigPlace;

        public string? HostConfigPlace => _parent.HostConfigPlace;

        public string? ThirdPartyNotices => _parent.ThirdPartyNotices;

        public IReadOnlyDictionary<string, IBaselineInfo> BaselineInfo => _parent.BaselineInfo;

        public IReadOnlyDictionary<string, string> TagsCollection => _parent.TagsCollection;

        public JObject? HostData => (_parent as ITemplateInfoHostJsonCache)?.HostData;

        bool ITemplateInfo.HasScriptRunningPostActions { get; set; }

    }
}
