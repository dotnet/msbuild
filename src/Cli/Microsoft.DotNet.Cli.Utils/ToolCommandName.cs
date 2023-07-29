// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Cli.Utils
{
    public struct ToolCommandName : IEquatable<ToolCommandName>
    {
        public ToolCommandName(string name)
        {
            if (name == null)
            {
                throw new ArgumentNullException(nameof(name));
            }

            if (name == string.Empty)
            {
                throw new ArgumentException(message: "cannot be empty", paramName: nameof(name));
            }

            Value = name;
        }

        public string Value { get; }

        public override string ToString() => Value;

        public bool Equals(ToolCommandName other)
        {
            return string.Equals(Value, other.Value, StringComparison.OrdinalIgnoreCase);
        }

        public override bool Equals(object obj)
        {
            return obj is ToolCommandName name && Equals(name);
        }

        public override int GetHashCode()
        {
            return StringComparer.OrdinalIgnoreCase.GetHashCode(Value);
        }

        public static bool operator ==(ToolCommandName name1, ToolCommandName name2)
        {
            return name1.Equals(name2);
        }

        public static bool operator !=(ToolCommandName name1, ToolCommandName name2)
        {
            return !name1.Equals(name2);
        }

        public static ToolCommandName[] Convert(string[] toolCommandNameStringArray)
        {
            var toolCommandNames = new ToolCommandName[toolCommandNameStringArray.Length];
            for (int i = 0; i < toolCommandNameStringArray.Length; i++)
            {
                toolCommandNames[i] = new ToolCommandName(toolCommandNameStringArray[i]);
            }

            return toolCommandNames;
        }
    }
}
