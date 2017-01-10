// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Cli.Utils.CommandParsing
{
    public class CommandGrammar : Grammar
    {
        private CommandGrammar(Func<string, string> variable, bool preserveSurroundingQuotes)
        {
            var environmentVariablePiece = Ch('%').And(Rep(Ch().Not(Ch('%')))).And(Ch('%')).Left().Down().Str()
                .Build(key => variable(key) ?? "%" + key + "%");

            var escapeSequencePiece =
                Ch('%').And(Ch('%')).Build(_=>"%")
                    .Or(Ch('^').And(Ch('^')).Build(_ => "^"))
                    .Or(Ch('\\').And(Ch('\\')).Build(_ => "\\"))
                    .Or(Ch('\\').And(Ch('\"')).Build(_ => "\""))
                ;

            var specialPiece = environmentVariablePiece.Or(escapeSequencePiece);

            var unquotedPiece = Rep1(Ch().Not(specialPiece).Not(Ch(' '))).Str();

            var quotedPiece = Rep1(Ch().Not(specialPiece).Not(Ch('\"'))).Str();

            var unquotedTerm = Rep1(unquotedPiece.Or(specialPiece)).Str();

            var quotedTerm = Ch('\"').And(Rep(quotedPiece.Or(specialPiece)).Str()).And(Ch('\"')).Left().Down();
            if (preserveSurroundingQuotes)
            {
                // Str() value assigned to quotedTerm does not include quotation marks surrounding the quoted or
                // special piece. Add those quotes back if requested.
                quotedTerm = quotedTerm.Build(str => "\"" + str + "\"");
            }

            var whitespace = Rep(Ch(' '));

            var term = whitespace.And(quotedTerm.Or(unquotedTerm)).And(whitespace).Left().Down();

            Parse = Rep(term);
        }

        public readonly Parser<IList<string>> Parse;

        public static string[] Process(string text, Func<string, string> variables, bool preserveSurroundingQuotes)
        {
            var grammar = new CommandGrammar(variables, preserveSurroundingQuotes);
            var cursor = new Cursor(text, 0, text.Length);

            var result = grammar.Parse(cursor);
            if (!result.Remainder.IsEnd)
            {
                throw new ArgumentException(string.Format(LocalizableStrings.MalformedText, nameof(text)));
            }
            return result.Value.ToArray();
        }
    }
}