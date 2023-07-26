// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.


#nullable enable

namespace Microsoft.DotNet.Cli.Utils;

/// <summary>
/// Parses property key value pairs that have already been forwarded through the PropertiesOption class.
/// Does not parse -p and etc. formats, (this is done by PropertiesOption) but does parse property values separated by =, ;, and using quotes.
/// </summary>
public static class MSBuildPropertyParser {
    public static IEnumerable<(string key, string value)> ParseProperties(string input) {
        var currentPos = 0;
        StringBuilder currentKey = new StringBuilder();
        StringBuilder currentValue = new StringBuilder();
        
        (string key, string value) EmitAndReset() {
            var key = currentKey.ToString();
            var value= currentValue.ToString();
            currentKey.Clear();
            currentValue.Clear();
            return (key, value);
        }

        char? Peek() => currentPos < input.Length ? input[currentPos] : null;

        bool TryConsume(out char? consumed) {
            if(input.Length > currentPos) {
                consumed = input[currentPos];
                currentPos++;
                return true;
            } else {
                consumed = null;
                return false;
            }
        }
        
        void ParseKey() {
            while (TryConsume(out var c) && c != '=') {
                currentKey.Append(c);
            }
        }

        void ParseQuotedValue() {
            TryConsume(out var leadingQuote); // consume the leading quote, which we know is there
            currentValue.Append(leadingQuote);
            while(TryConsume(out char? c)) {
                currentValue.Append(c);
                if (c == '"') {
                    // we're done
                    return;
                }
                if (c == '\\' && Peek() == '"')
                {
                    // consume the escaped quote
                    TryConsume(out var c2); 
                    currentValue.Append(c2);
                }
            }
        }

        void ParseUnquotedValue() {
            while(TryConsume(out char? c) && c != ';') {
                currentValue.Append(c);
            }
            // we're either at the end or 
            if (AtEnd()) return;
            // we're just past a semicolon
            // if semicolon, we need to check if there are any other = in the string (signifying property pairs)
            if (input.IndexOf('=', currentPos) != -1) {
                // there are more = in the string, so eject and let a new key/value pair be parsed
                return;
            } else {
                currentValue.Append(';');
                // there are no more = in the string, so consume the remainder of the string
                while(TryConsume(out char? c))
                {
                    currentValue.Append(c);
                }
            }
        }

        void ParseValue() {
            if (Peek() == '"') {
                ParseQuotedValue();
            } else {
                ParseUnquotedValue();
            }
        }

        (string key, string value) ParseKeyValue() {
            ParseKey();
            ParseValue();
            return EmitAndReset();
        }

        bool AtEnd() => currentPos == input.Length;

        while (!(AtEnd())) {
            yield return ParseKeyValue();
            if (Peek() is char c && (c == ';' || c== ',')) {
                TryConsume(out _); // swallow the next semicolon or comma delimiter
            }
        }
    }
}
