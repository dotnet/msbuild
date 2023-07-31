// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.NET.Sdk.Razor.Tool.CommandLineUtils;

namespace Microsoft.NET.Sdk.Razor.Tool
{
    internal abstract class CommandBase : CommandLineApplication
    {
        public const int ExitCodeSuccess = 0;
        public const int ExitCodeFailure = 1;
        public const int ExitCodeFailureRazorError = 2;

        protected CommandBase(Application parent, string name)
            : base(throwOnUnexpectedArg: true)
        {
            if (parent == null)
            {
                throw new ArgumentNullException(nameof(parent));
            }

            base.Parent = parent;
            Name = name;
            Out = parent.Out ?? Out;
            Error = parent.Error ?? Error;

            Help = HelpOption("-?|-h|--help");
            OnExecute((Func<Task<int>>)ExecuteAsync);
        }

        protected new Application Parent => (Application)base.Parent;

        protected CancellationToken Cancelled => Parent?.CancellationToken ?? default;

        protected CommandOption Help { get; }

        protected virtual bool ValidateArguments()
        {
            return true;
        }

        protected abstract Task<int> ExecuteCoreAsync();

        private async Task<int> ExecuteAsync()
        {
            if (!ValidateArguments())
            {
                ShowHelp();
                return ExitCodeFailureRazorError;
            }

            return await ExecuteCoreAsync();
        }
    }
}
