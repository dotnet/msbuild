// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;

namespace Microsoft.TemplateEngine.Cli.Commands
{
    internal class InstallCommandArgs : GlobalArgs
    {
        public InstallCommandArgs(BaseInstallCommand installCommand, ParseResult parseResult) : base(installCommand, parseResult)
        {
            var nameResult = parseResult.GetResult(InstallCommand.NameArgument);
            if (nameResult is null || nameResult.Errors.Any())
            {
                throw new ArgumentException($"{nameof(parseResult)} should contain at least one argument for {nameof(InstallCommand.NameArgument)}", nameof(parseResult));
            }

            TemplatePackages = parseResult.GetValue(InstallCommand.NameArgument)!;

            //workaround for --install source1 --install source2 case
            if (installCommand is LegacyInstallCommand && (TemplatePackages.Contains(installCommand.Name) || installCommand.Aliases.Any(alias => TemplatePackages.Contains(alias))))
            {
                TemplatePackages = TemplatePackages.Where(package => installCommand.Name != package && !installCommand.Aliases.Contains(package)).ToList();
            }

            if (!TemplatePackages.Any())
            {
                throw new ArgumentException($"{nameof(parseResult)} should contain at least one argument for {nameof(InstallCommand.NameArgument)}", nameof(parseResult));
            }

            Interactive = parseResult.GetValue(installCommand.InteractiveOption);
            AdditionalSources = parseResult.GetValue(installCommand.AddSourceOption);
            Force = parseResult.GetValue(BaseInstallCommand.ForceOption);
        }

        public IReadOnlyList<string> TemplatePackages { get; }

        public bool Interactive { get; }

        public IReadOnlyList<string>? AdditionalSources { get; }

        public bool Force { get; }
    }
}
