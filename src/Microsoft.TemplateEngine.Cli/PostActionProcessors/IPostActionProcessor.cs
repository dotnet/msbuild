using Microsoft.TemplateEngine.Abstractions;
using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.TemplateEngine.Cli.PostActionProcessors
{
    public interface IPostActionProcessor : IIdentifiedComponent
    {
        bool Process(IPostAction action, ICreationResult templateCreationResult, string outputBasePath);
    }
}
