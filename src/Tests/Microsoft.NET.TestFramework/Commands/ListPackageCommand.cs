using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.DotNet.Cli.Utils;
using Xunit.Abstractions;

namespace Microsoft.NET.TestFramework.Commands
{
    public class ListPackageCommand : DotnetCommand
    {
        private string _projectName = null;

        public ListPackageCommand(ITestOutputHelper log, params string[] args) : base(log, args)
        {
        }

        public override CommandResult Execute(IEnumerable<string> args)
        {
            List<string> newArgs = new List<string>();
            newArgs.Add("list");
            if (!string.IsNullOrEmpty(_projectName))
            {
                newArgs.Add(_projectName);
            }
            newArgs.Add("package");
            newArgs.AddRange(args);

            return base.Execute(newArgs);
        }


        public ListPackageCommand WithProject(string projectName)
        {
            _projectName = projectName;
            return this;
        }
    }
}
