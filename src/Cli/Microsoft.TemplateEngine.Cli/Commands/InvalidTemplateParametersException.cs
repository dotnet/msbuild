// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System.Text;

namespace Microsoft.TemplateEngine.Cli.Commands
{
    internal class InvalidTemplateParametersException : Exception
    {
        private string? _message;

        public InvalidTemplateParametersException(CliTemplateInfo template, IReadOnlyDictionary<CliTemplateParameter, IReadOnlyList<string>> parameterErrors)
        {
            ParameterErrors = parameterErrors;
            Template = template;
        }

        public override string Message
        {
            get
            {
                if (_message == null)
                {
                    StringBuilder stringBuilder = new StringBuilder();
                    stringBuilder.Append(string.Format(LocalizableStrings.Exception_InvalidTemplateParameters_MessageHeader, Template.Identity, Template.ShortNameList[0]));

                    foreach (var error in ParameterErrors)
                    {
                        stringBuilder.AppendLine().Append(error.Key.Name.Indent(1));
                        foreach (var message in error.Value)
                        {
                            stringBuilder.AppendLine().Append(message.Indent(2));
                        }
                    }
                    _message = stringBuilder.ToString();
                }
                return _message;
            }
        }

        public CliTemplateInfo Template { get; }

        internal IReadOnlyDictionary<CliTemplateParameter, IReadOnlyList<string>> ParameterErrors { get; }
    }
}
