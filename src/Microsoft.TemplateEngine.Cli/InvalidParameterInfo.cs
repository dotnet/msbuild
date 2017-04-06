using System;
using System.Collections.Generic;

namespace Microsoft.TemplateEngine.Cli
{
    internal class InvalidParameterInfo
    {
        public InvalidParameterInfo(string inputFormat, string specifiedValue, string canonical, bool invalidDefault = false)
        {
            InputFormat = inputFormat;
            SpecifiedValue = specifiedValue;
            Canonical = canonical;
            InvalidDefault = invalidDefault;
        }

        public string InputFormat { get; }
        public string SpecifiedValue { get; }
        public string Canonical { get; }
        public bool InvalidDefault { get; }

        public static string InvalidParameterListToString(IReadOnlyList<InvalidParameterInfo> invalidParameterList)
        {
            if (invalidParameterList.Count == 0)
            {
                return string.Empty;
            }

            string invalidParamsErrorText = LocalizableStrings.InvalidTemplateParameterValues;

            foreach (InvalidParameterInfo invalidParam in invalidParameterList)
            {
                if (!invalidParam.InvalidDefault)
                {
                    invalidParamsErrorText += Environment.NewLine + string.Format(LocalizableStrings.InvalidParameterDetail, invalidParam.InputFormat, invalidParam.SpecifiedValue, invalidParam.Canonical);
                }
                else
                {
                    invalidParamsErrorText += Environment.NewLine + string.Format(LocalizableStrings.InvalidParameterDefault, invalidParam.Canonical, invalidParam.SpecifiedValue);
                }
            }

            return invalidParamsErrorText;
        }

        public static IDictionary<string, InvalidParameterInfo> IntersectWithExisting(IDictionary<string, InvalidParameterInfo> existing, IReadOnlyList<InvalidParameterInfo> newInfo)
        {
            Dictionary<string, InvalidParameterInfo> intersection = new Dictionary<string, InvalidParameterInfo>();

            foreach (InvalidParameterInfo info in newInfo)
            {
                if (existing.ContainsKey(info.Canonical))
                {
                    intersection.Add(info.Canonical, info);
                }
            }

            return intersection;
        }
    }
}
