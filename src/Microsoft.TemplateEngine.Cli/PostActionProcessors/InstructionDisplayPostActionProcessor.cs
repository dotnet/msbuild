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

        public bool Process(IPostAction actionConfig, ICreationResult templateCreationResult, string outputBasePath)
        {
            Reporter.Output.WriteLine(actionConfig.Description);
            Reporter.Output.WriteLine(actionConfig.ManualInstructions);
            Reporter.Output.WriteLine();
            return true;
        }
    }
}
