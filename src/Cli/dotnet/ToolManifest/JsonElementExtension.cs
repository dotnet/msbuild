// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json;

namespace Microsoft.DotNet.ToolManifest
{
    internal static class JsonElementExtension
    {
        // this is needed due to https://github.com/dotnet/corefx/issues/36109

        internal static bool TryGetStringValue(this JsonElement element, string name, out string value)
        {
            value = null;
            if (element.TryGetProperty(name, out JsonElement jsonValue))
            {
                if (jsonValue.ValueKind != JsonValueKind.String)
                {
                    throw new ToolManifestException(
                        string.Format(
                            LocalizableStrings.UnexpectedTypeInJson,
                            JsonValueKind.String.ToString(),
                            name));
                }
                value = jsonValue.GetString();
                return true;
            }

            return false;
        }

        internal static bool TryGetInt32Value(this JsonElement element, string name, out int value)
        {
            value = default;
            if (element.TryGetProperty(name, out JsonElement jsonValue))
            {
                if (jsonValue.ValueKind != JsonValueKind.Number)
                {
                    throw new ToolManifestException(
                        string.Format(
                            LocalizableStrings.UnexpectedTypeInJson,
                            JsonValueKind.Number.ToString(),
                            name));
                }
                value = jsonValue.GetInt32();
                return true;
            }

            return false;
        }

        internal static bool TryGetBooleanValue(this JsonElement element, string name, out bool value)
        {
            value = default;
            if (element.TryGetProperty(name, out JsonElement jsonValue))
            {
                if (!(jsonValue.ValueKind == JsonValueKind.True || jsonValue.ValueKind == JsonValueKind.False))
                {
                    throw new ToolManifestException(
                        string.Format(
                            LocalizableStrings.UnexpectedTypeInJson,
                            JsonValueKind.True.ToString() + "|" + JsonValueKind.False.ToString(),
                            name));
                }
                value = jsonValue.GetBoolean();
                return true;
            }

            return false;
        }
    }
}
