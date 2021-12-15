using System.CommandLine.Invocation;
using System.Threading.Tasks;
using System;
using System.CommandLine.Parsing;

namespace Microsoft.DotNet.Cli
{
    internal class ParseResultCommandHandler : ICommandHandler
    {
        private Func<ParseResult, int> _action;

        internal ParseResultCommandHandler(Func<ParseResult, int> action) {
            _action = action;
        }
        public Task<int> InvokeAsync(InvocationContext context) => Task.FromResult(_action(context.ParseResult));
    }
}