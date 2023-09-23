// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

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
                    StringBuilder stringBuilder = new();
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
