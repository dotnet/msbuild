// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;

namespace Microsoft.TemplateEngine.Cli.Commands
{
    internal class TemplateOption : IEquatable<TemplateOption>
    {
        private CliOption _option;

        internal TemplateOption(
            CliTemplateParameter parameter,
            IReadOnlySet<string> aliases)
        {
            TemplateParameter = parameter;
            Aliases = aliases;
            _option = TemplateParameter.GetOption(Aliases);
        }

        internal CliTemplateParameter TemplateParameter { get; private set; }

        internal IReadOnlySet<string> Aliases { get; private set; }

        internal CliOption Option => _option;

        public bool Equals(TemplateOption? other)
        {
            if (other == null)
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            if (!string.Equals(TemplateParameter.Name, other.TemplateParameter.Name, StringComparison.Ordinal))
            {
                return false;
            }

            if (TemplateParameter.Type != other.TemplateParameter.Type)
            {
                return false;
            }

            if (Aliases.Count != other.Aliases.Count)
            {
                return false;
            }

            foreach (string alias in other.Aliases)
            {
                if (!Aliases.Contains(alias))
                {
                    return false;
                }
            }
            return true;
        }

        public override bool Equals(object? obj) => Equals(obj as TemplateOption);

        public override int GetHashCode()
        {
            return new { a = TemplateParameter.Name, b = TemplateParameter.Type, c = Aliases.Aggregate(0, (sum, next) => sum ^ next.GetHashCode()) }.GetHashCode();
        }

        internal void MergeChoices(ChoiceTemplateParameter choiceParam)
        {
            if (TemplateParameter is not ChoiceTemplateParameter currentChoiceParam)
            {
                return;
            }

            if (TemplateParameter is CombinedChoiceTemplateParameter combinedParam)
            {
                combinedParam.MergeChoices(choiceParam);
            }
            else
            {
                var combinedChoice = new CombinedChoiceTemplateParameter(currentChoiceParam);
                combinedChoice.MergeChoices(choiceParam);
                TemplateParameter = combinedChoice;
            }
            _option = TemplateParameter.GetOption(Aliases);
        }
    }
}
