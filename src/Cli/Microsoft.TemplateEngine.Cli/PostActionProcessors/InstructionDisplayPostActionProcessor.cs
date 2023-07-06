// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Utils;
using Microsoft.TemplateEngine.Abstractions;

namespace Microsoft.TemplateEngine.Cli.PostActionProcessors
{
    internal class InstructionDisplayPostActionProcessor : PostActionProcessorBase
    {
        private static readonly Guid ActionProcessorId = new Guid("AC1156F7-BB77-4DB8-B28F-24EEBCCA1E5C");

        public override Guid Id => ActionProcessorId;

        protected override bool ProcessInternal(IEngineEnvironmentSettings environment, IPostAction actionConfig, ICreationEffects creationEffects, ICreationResult templateCreationResult, string outputBasePath)
        {
            Reporter.Output.WriteLine(LocalizableStrings.PostActionDescription, actionConfig.Description);
            Reporter.Output.WriteLine(LocalizableStrings.PostActionInstructions, actionConfig.ManualInstructions);

            if (actionConfig.Args != null && actionConfig.Args.TryGetValue("executable", out string? executable))
            {
                actionConfig.Args.TryGetValue("args", out string? commandArgs);
                Reporter.Output.WriteLine(string.Format(LocalizableStrings.PostActionCommand, $"{executable} {commandArgs}").Bold().Red());
            }

            return true;
        }
    }
}
