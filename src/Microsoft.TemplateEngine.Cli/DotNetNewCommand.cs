// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.TemplateEngine.Cli
{
    internal class DotNetNewCommand
    {
        private string _commandDescription;

        public DotNetNewCommand(string commandDescription) => _commandDescription = commandDescription;
    }
}