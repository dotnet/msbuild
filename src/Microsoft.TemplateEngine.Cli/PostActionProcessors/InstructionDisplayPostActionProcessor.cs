using System;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.TemplateEngine.Abstractions;

namespace Microsoft.TemplateEngine.Cli.PostActionProcessors
{
    public class InstructionDisplayPostActionProcessor : IPostActionProcessor
    {
        private static readonly Guid ActionProcessorId = new Guid("AC1156F7-BB77-4DB8-B28F-24EEBCCA1E5C");

        public Guid Id => ActionProcessorId;

        public InstructionDisplayPostActionProcessor()
        {
        }

        public bool Process(IEngineEnvironmentSettings settings, IPostAction actionConfig, ICreationResult templateCreationResult, string outputBasePath)
        {
            Reporter.Output.WriteLine($"{LocalizableStrings.PostActionDescription} {actionConfig.Description}");
            Reporter.Output.WriteLine($"{LocalizableStrings.PostActionInstructions} {actionConfig.ManualInstructions}");

            if (actionConfig.Args != null && actionConfig.Args.TryGetValue("executable", out string executable))
            {
                Reporter.Output.Write($"{LocalizableStrings.PostActionCommand} ");
                if (actionConfig.Args.TryGetValue("args", out string commandArgs))
                {
                    Reporter.Output.WriteLine($"{executable} {commandArgs}".Bold().Red());
                }
                else
                {
                    Reporter.Output.WriteLine(executable.Bold().Red());
                }
            }

            return true;
        }
    }
}
