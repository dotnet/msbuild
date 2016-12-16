// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.Evaluation;
using Microsoft.DotNet.Cli.CommandLine;
using System.Collections.Generic;
using Microsoft.DotNet.Tools.List;

namespace Microsoft.DotNet.Tools.List.ProjectToProjectReferences
{
    public class ListProjectToProjectReferences : IListSubCommand
    {
        private string _fileOrDirectory = null;
        private IList<string> _items = new List<string>();

        public ListProjectToProjectReferences(string fileOrDirectory)
        {
            _fileOrDirectory = fileOrDirectory;
            var msbuildProj = MsbuildProject.FromFileOrDirectory(new ProjectCollection(), fileOrDirectory);

            var p2ps = msbuildProj.GetProjectToProjectReferences();
            foreach (var p2p in p2ps)
            {
                _items.Add(p2p.Include);
            }
        }

        public string LocalizedErrorMessageNoItemsFound => string.Format(
            LocalizableStrings.NoReferencesFound, 
            CommonLocalizableStrings.P2P,
            _fileOrDirectory);
        
        public IList<string> Items => _items;
    }

    public class ListProjectToProjectReferencesCommand : ListSubCommandBase
    {
        protected override string CommandName => "p2ps";
        protected override string LocalizedDisplayName => LocalizableStrings.AppFullName;
        protected override string LocalizedDescription => LocalizableStrings.AppDescription;

        protected override IListSubCommand CreateIListSubCommand(string fileOrDirectory)
        {
            return new ListProjectToProjectReferences(fileOrDirectory);
        }

        internal static CommandLineApplication CreateApplication(CommandLineApplication parentApp)
        {
            var command = new ListProjectToProjectReferencesCommand();
            return command.Create(parentApp);
        }
    }
}
