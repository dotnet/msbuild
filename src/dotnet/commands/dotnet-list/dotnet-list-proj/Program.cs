// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.DotNet.Cli.CommandLine;
using Microsoft.DotNet.Cli.Sln.Internal;
using Microsoft.DotNet.Tools.Common;
using System.Collections.Generic;
using Microsoft.DotNet.Tools.List;

namespace Microsoft.DotNet.Tools.List.ProjectsInSolution
{
    public class ListProjectsInSolution : IListSubCommand
    {
        private IList<string> _items = new List<string>();

        public ListProjectsInSolution(string fileOrDirectory)
        {
            SlnFile slnFile = SlnFileFactory.CreateFromFileOrDirectory(fileOrDirectory);
            foreach (var slnProject in slnFile.Projects)
            {
                _items.Add(slnProject.FilePath);
            }
        }

        public string LocalizedErrorMessageNoItemsFound => CommonLocalizableStrings.NoProjectsFound;
        public IList<string> Items => _items;
    }

    public class ListProjectsInSolutionCommand : ListSubCommandBase
    {
        protected override string CommandName => "projects";
        protected override string LocalizedDisplayName => LocalizableStrings.AppFullName;
        protected override string LocalizedDescription => LocalizableStrings.AppDescription;

        protected override IListSubCommand CreateIListSubCommand(string fileOrDirectory)
        {
            return new ListProjectsInSolution(fileOrDirectory);
        }

        internal static CommandLineApplication CreateApplication(CommandLineApplication parentApp)
        {
            var command = new ListProjectsInSolutionCommand();
            return command.Create(parentApp);
        }
    }
}
