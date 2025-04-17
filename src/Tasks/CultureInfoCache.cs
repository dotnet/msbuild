﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Globalization;
#if NET
using System.Linq;
#else
using System.Collections.Generic;
using Microsoft.Build.Shared;
#endif

#nullable disable

namespace Microsoft.Build.Tasks
{
    /// <summary>
    /// Provides read-only cached instances of <see cref="CultureInfo"/>.
    /// <remarks>
    /// Original source:
    /// https://raw.githubusercontent.com/aspnet/Localization/dev/src/Microsoft.Framework.Globalization.CultureInfoCache/CultureInfoCache.cs
    /// </remarks>
    /// </summary>
    internal static class CultureInfoCache
    {
#if !NET
        private static readonly Lazy<HashSet<string>> ValidCultureNames = new Lazy<HashSet<string>>(() => InitializeValidCultureNames());
#endif

        // https://docs.microsoft.com/en-gb/windows/desktop/Intl/using-pseudo-locales-for-localization-testing
        // These pseudo-locales are available in versions of Windows from Vista and later.
        // However, from Windows 10, version 1803, they are not returned when enumerating the
        // installed cultures, even if the registry keys are set. Therefore, add them to the list manually.
        private static readonly string[] pseudoLocales = ["qps-ploc", "qps-ploca", "qps-plocm", "qps-Latn-x-sh"];

#if !NET
        private static HashSet<string> InitializeValidCultureNames()
        {
#if !FEATURE_CULTUREINFO_GETCULTURES
            if (!AssemblyUtilities.CultureInfoHasGetCultures())
            {
                return HardcodedCultureNames;
            }
#endif
            HashSet<string> validCultureNames = new(StringComparer.OrdinalIgnoreCase);
            foreach (CultureInfo cultureName in AssemblyUtilities.GetAllCultures())
            {
                validCultureNames.Add(cultureName.Name);
            }

            // Account for pseudo-locales (see above)
            foreach (string pseudoLocale in pseudoLocales)
            {
                validCultureNames.Add(pseudoLocale);
            }

            return validCultureNames;
        }
#endif

        /// <summary>
        /// Determine if a culture string represents a valid <see cref="CultureInfo"/> instance.
        /// </summary>
        /// <param name="name">The culture name.</param>
        /// <returns>True if the culture is determined to be valid.</returns>
        internal static bool IsValidCultureString(string name)
        {
#if NET
            try
            {
                // GetCultureInfo throws if the culture doesn't exist
                CultureInfo.GetCultureInfo(name, predefinedOnly: true);
                return true;
            }
            catch
            {
                // Second attempt: try pseudolocales (see above)
                return pseudoLocales.Contains(name, StringComparer.OrdinalIgnoreCase);
            }
#else
            return ValidCultureNames.Value.Contains(name);
#endif
        }

#if !NET && !FEATURE_CULTUREINFO_GETCULTURES
        // Copied from https://github.com/aspnet/Localization/blob/5e1fb16071affd15f15b9c732833f3ae2ac46e10/src/Microsoft.Framework.Globalization.CultureInfoCache/CultureInfoList.cs
        // Regenerated using the tool (removed by https://github.com/aspnet/Localization/pull/130)
        //   * Removed the empty string from the list
        //   * Any cultures not present when regenerated were retained
        //   * Added the Windows pseudo-locales
        private static readonly HashSet<string> HardcodedCultureNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "aa",
            "aa-DJ",
            "aa-ER",
            "aa-ET",
            "af",
            "af-NA",
            "af-ZA",
            "agq",
            "agq-CM",
            "ak",
            "ak-GH",
            "am",
            "am-ET",
            "ar",
            "ar-001",
            "ar-AE",
            "ar-BH",
            "ar-DJ",
            "ar-DZ",
            "ar-EG",
            "ar-ER",
            "ar-IL",
            "ar-IQ",
            "ar-JO",
            "ar-KM",
            "ar-KW",
            "ar-LB",
            "ar-LY",
            "ar-MA",
            "ar-MR",
            "ar-OM",
            "ar-PS",
            "ar-QA",
            "ar-SA",
            "ar-SD",
            "ar-SO",
            "ar-SS",
            "ar-SY",
            "ar-TD",
            "ar-TN",
            "ar-YE",
            "arn",
            "arn-CL",
            "as",
            "as-IN",
            "asa",
            "asa-TZ",
            "ast",
            "ast-ES",
            "az",
            "az-Cyrl",
            "az-Cyrl-AZ",
            "az-Latn",
            "az-Latn-AZ",
            "ba",
            "ba-RU",
            "bas",
            "bas-CM",
            "be",
            "be-BY",
            "bem",
            "bem-ZM",
            "bez",
            "bez-TZ",
            "bg",
            "bg-BG",
            "bin",
            "bin-NG",
            "bm",
            "bm-Latn",
            "bm-Latn-ML",
            "bn",
            "bn-BD",
            "bn-IN",
            "bo",
            "bo-CN",
            "bo-IN",
            "br",
            "br-FR",
            "brx",
            "brx-IN",
            "bs",
            "bs-Cyrl",
            "bs-Cyrl-BA",
            "bs-Latn",
            "bs-Latn-BA",
            "byn",
            "byn-ER",
            "ca",
            "ca-AD",
            "ca-ES",
            "ca-ES-valencia",
            "ca-FR",
            "ca-IT",
            "ce",
            "ce-RU",
            "cgg",
            "cgg-UG",
            "chr",
            "chr-Cher",
            "chr-Cher-US",
            "co",
            "co-FR",
            "cs",
            "cs-CZ",
            "cu",
            "cu-RU",
            "cy",
            "cy-GB",
            "da",
            "da-DK",
            "da-GL",
            "dav",
            "dav-KE",
            "de",
            "de-AT",
            "de-BE",
            "de-CH",
            "de-DE",
            "de-IT",
            "de-LI",
            "de-LU",
            "dje",
            "dje-NE",
            "dsb",
            "dsb-DE",
            "dua",
            "dua-CM",
            "dv",
            "dv-MV",
            "dyo",
            "dyo-SN",
            "dz",
            "dz-BT",
            "ebu",
            "ebu-KE",
            "ee",
            "ee-GH",
            "ee-TG",
            "el",
            "el-CY",
            "el-GR",
            "en",
            "en-001",
            "en-029",
            "en-150",
            "en-AG",
            "en-AI",
            "en-AS",
            "en-AT",
            "en-AU",
            "en-BB",
            "en-BE",
            "en-BI",
            "en-BM",
            "en-BS",
            "en-BW",
            "en-BZ",
            "en-CA",
            "en-CC",
            "en-CH",
            "en-CK",
            "en-CM",
            "en-CX",
            "en-CY",
            "en-DE",
            "en-DK",
            "en-DM",
            "en-ER",
            "en-FI",
            "en-FJ",
            "en-FK",
            "en-FM",
            "en-GB",
            "en-GD",
            "en-GG",
            "en-GH",
            "en-GI",
            "en-GM",
            "en-GU",
            "en-GY",
            "en-HK",
            "en-ID",
            "en-IE",
            "en-IL",
            "en-IM",
            "en-IN",
            "en-IO",
            "en-JE",
            "en-JM",
            "en-KE",
            "en-KI",
            "en-KN",
            "en-KY",
            "en-LC",
            "en-LR",
            "en-LS",
            "en-MG",
            "en-MH",
            "en-MO",
            "en-MP",
            "en-MS",
            "en-MT",
            "en-MU",
            "en-MW",
            "en-MY",
            "en-NA",
            "en-NF",
            "en-NG",
            "en-NL",
            "en-NR",
            "en-NU",
            "en-NZ",
            "en-PG",
            "en-PH",
            "en-PK",
            "en-PN",
            "en-PR",
            "en-PW",
            "en-RW",
            "en-SB",
            "en-SC",
            "en-SD",
            "en-SE",
            "en-SG",
            "en-SH",
            "en-SI",
            "en-SL",
            "en-SS",
            "en-SX",
            "en-SZ",
            "en-TC",
            "en-TK",
            "en-TO",
            "en-TT",
            "en-TV",
            "en-TZ",
            "en-UG",
            "en-UM",
            "en-US",
            "en-VC",
            "en-VG",
            "en-VI",
            "en-VU",
            "en-WS",
            "en-ZA",
            "en-ZM",
            "en-ZW",
            "eo",
            "eo-001",
            "es",
            "es-419",
            "es-AR",
            "es-BO",
            "es-BR",
            "es-BZ",
            "es-CL",
            "es-CO",
            "es-CR",
            "es-CU",
            "es-DO",
            "es-EC",
            "es-ES",
            "es-GQ",
            "es-GT",
            "es-HN",
            "es-MX",
            "es-NI",
            "es-PA",
            "es-PE",
            "es-PH",
            "es-PR",
            "es-PY",
            "es-SV",
            "es-US",
            "es-UY",
            "es-VE",
            "et",
            "et-EE",
            "eu",
            "eu-ES",
            "ewo",
            "ewo-CM",
            "fa",
            "fa-IR",
            "ff",
            "ff-CM",
            "ff-GN",
            "ff-Latn",
            "ff-Latn-SN",
            "ff-MR",
            "ff-NG",
            "fi",
            "fi-FI",
            "fil",
            "fil-PH",
            "fo",
            "fo-DK",
            "fo-FO",
            "fr",
            "fr-029",
            "fr-BE",
            "fr-BF",
            "fr-BI",
            "fr-BJ",
            "fr-BL",
            "fr-CA",
            "fr-CD",
            "fr-CF",
            "fr-CG",
            "fr-CH",
            "fr-CI",
            "fr-CM",
            "fr-DJ",
            "fr-DZ",
            "fr-FR",
            "fr-GA",
            "fr-GF",
            "fr-GN",
            "fr-GP",
            "fr-GQ",
            "fr-HT",
            "fr-KM",
            "fr-LU",
            "fr-MA",
            "fr-MC",
            "fr-MF",
            "fr-MG",
            "fr-ML",
            "fr-MQ",
            "fr-MR",
            "fr-MU",
            "fr-NC",
            "fr-NE",
            "fr-PF",
            "fr-PM",
            "fr-RE",
            "fr-RW",
            "fr-SC",
            "fr-SN",
            "fr-SY",
            "fr-TD",
            "fr-TG",
            "fr-TN",
            "fr-VU",
            "fr-WF",
            "fr-YT",
            "fur",
            "fur-IT",
            "fy",
            "fy-NL",
            "ga",
            "ga-IE",
            "gd",
            "gd-GB",
            "gl",
            "gl-ES",
            "gn",
            "gn-PY",
            "gsw",
            "gsw-CH",
            "gsw-FR",
            "gsw-LI",
            "gu",
            "gu-IN",
            "guz",
            "guz-KE",
            "gv",
            "gv-IM",
            "ha",
            "ha-Latn",
            "ha-Latn-GH",
            "ha-Latn-NE",
            "ha-Latn-NG",
            "haw",
            "haw-US",
            "he",
            "he-IL",
            "hi",
            "hi-IN",
            "hr",
            "hr-BA",
            "hr-HR",
            "hsb",
            "hsb-DE",
            "hu",
            "hu-HU",
            "hy",
            "hy-AM",
            "ia",
            "ia-001",
            "ia-FR",
            "ibb",
            "ibb-NG",
            "id",
            "id-ID",
            "ig",
            "ig-NG",
            "ii",
            "ii-CN",
            "is",
            "is-IS",
            "it",
            "it-CH",
            "it-IT",
            "it-SM",
            "it-VA",
            "iu",
            "iu-Cans",
            "iu-Cans-CA",
            "iu-Latn",
            "iu-Latn-CA",
            "ja",
            "ja-JP",
            "jgo",
            "jgo-CM",
            "jmc",
            "jmc-TZ",
            "jv",
            "jv-Java",
            "jv-Java-ID",
            "jv-Latn",
            "jv-Latn-ID",
            "ka",
            "ka-GE",
            "kab",
            "kab-DZ",
            "kam",
            "kam-KE",
            "kde",
            "kde-TZ",
            "kea",
            "kea-CV",
            "khq",
            "khq-ML",
            "ki",
            "ki-KE",
            "kk",
            "kk-KZ",
            "kkj",
            "kkj-CM",
            "kl",
            "kl-GL",
            "kln",
            "kln-KE",
            "km",
            "km-KH",
            "kn",
            "kn-IN",
            "ko",
            "ko-KP",
            "ko-KR",
            "kok",
            "kok-IN",
            "kr",
            "kr-NG",
            "ks",
            "ks-Arab",
            "ks-Arab-IN",
            "ks-Deva",
            "ks-Deva-IN",
            "ksb",
            "ksb-TZ",
            "ksf",
            "ksf-CM",
            "ksh",
            "ksh-DE",
            "ku",
            "ku-Arab",
            "ku-Arab-IQ",
            "ku-Arab-IR",
            "kw",
            "kw-GB",
            "ky",
            "ky-KG",
            "la",
            "la-001",
            "lag",
            "lag-TZ",
            "lb",
            "lb-LU",
            "lg",
            "lg-UG",
            "lkt",
            "lkt-US",
            "ln",
            "ln-AO",
            "ln-CD",
            "ln-CF",
            "ln-CG",
            "lo",
            "lo-LA",
            "lrc",
            "lrc-IQ",
            "lrc-IR",
            "lt",
            "lt-LT",
            "lu",
            "lu-CD",
            "luo",
            "luo-KE",
            "luy",
            "luy-KE",
            "lv",
            "lv-LV",
            "mas",
            "mas-KE",
            "mas-TZ",
            "mer",
            "mer-KE",
            "mfe",
            "mfe-MU",
            "mg",
            "mg-MG",
            "mgh",
            "mgh-MZ",
            "mgo",
            "mgo-CM",
            "mi",
            "mi-NZ",
            "mk",
            "mk-MK",
            "ml",
            "ml-IN",
            "mn",
            "mn-Cyrl",
            "mn-MN",
            "mn-Mong",
            "mn-Mong-CN",
            "mn-Mong-MN",
            "mni",
            "mni-IN",
            "moh",
            "moh-CA",
            "mr",
            "mr-IN",
            "ms",
            "ms-BN",
            "ms-MY",
            "ms-SG",
            "mt",
            "mt-MT",
            "mua",
            "mua-CM",
            "my",
            "my-MM",
            "mzn",
            "mzn-IR",
            "naq",
            "naq-NA",
            "nb",
            "nb-NO",
            "nb-SJ",
            "nd",
            "nd-ZW",
            "nds",
            "nds-DE",
            "nds-NL",
            "ne",
            "ne-IN",
            "ne-NP",
            "nl",
            "nl-AW",
            "nl-BE",
            "nl-BQ",
            "nl-CW",
            "nl-NL",
            "nl-SR",
            "nl-SX",
            "nmg",
            "nmg-CM",
            "nn",
            "nn-NO",
            "nnh",
            "nnh-CM",
            "no",
            "nqo",
            "nqo-GN",
            "nr",
            "nr-ZA",
            "nso",
            "nso-ZA",
            "nus",
            "nus-SS",
            "nyn",
            "nyn-UG",
            "oc",
            "oc-FR",
            "om",
            "om-ET",
            "om-KE",
            "or",
            "or-IN",
            "os",
            "os-GE",
            "os-RU",
            "pa",
            "pa-Arab",
            "pa-Arab-PK",
            "pa-Guru",
            "pa-IN",
            "pap",
            "pap-029",
            "pl",
            "pl-PL",
            "prg",
            "prg-001",
            "prs",
            "prs-AF",
            "ps",
            "ps-AF",
            "pt",
            "pt-AO",
            "pt-BR",
            "pt-CH",
            "pt-CV",
            "pt-GQ",
            "pt-GW",
            "pt-LU",
            "pt-MO",
            "pt-MZ",
            "pt-PT",
            "pt-ST",
            "pt-TL",
            "qps-ploc",
            "qps-ploca",
            "qps-plocm",
            "qps-Latn-x-sh",
            "quc",
            "quc-Latn",
            "quc-Latn-GT",
            "qut",
            "qut-GT",
            "quz",
            "quz-BO",
            "quz-EC",
            "quz-PE",
            "rm",
            "rm-CH",
            "rn",
            "rn-BI",
            "ro",
            "ro-MD",
            "ro-RO",
            "rof",
            "rof-TZ",
            "ru",
            "ru-BY",
            "ru-KG",
            "ru-KZ",
            "ru-MD",
            "ru-RU",
            "ru-UA",
            "rw",
            "rw-RW",
            "rwk",
            "rwk-TZ",
            "sa",
            "sa-IN",
            "sah",
            "sah-RU",
            "saq",
            "saq-KE",
            "sbp",
            "sbp-TZ",
            "sd",
            "sd-Arab",
            "sd-Arab-PK",
            "sd-Deva",
            "sd-Deva-IN",
            "se",
            "se-FI",
            "se-NO",
            "se-SE",
            "seh",
            "seh-MZ",
            "ses",
            "ses-ML",
            "sg",
            "sg-CF",
            "shi",
            "shi-Latn",
            "shi-Latn-MA",
            "shi-Tfng",
            "shi-Tfng-MA",
            "si",
            "si-LK",
            "sk",
            "sk-SK",
            "sl",
            "sl-SI",
            "sma",
            "sma-NO",
            "sma-SE",
            "smj",
            "smj-NO",
            "smj-SE",
            "smn",
            "smn-FI",
            "sms",
            "sms-FI",
            "sn",
            "sn-Latn",
            "sn-Latn-ZW",
            "so",
            "so-DJ",
            "so-ET",
            "so-KE",
            "so-SO",
            "sq",
            "sq-AL",
            "sq-MK",
            "sq-XK",
            "sr",
            "sr-Cyrl",
            "sr-Cyrl-BA",
            "sr-Cyrl-CS",
            "sr-Cyrl-ME",
            "sr-Cyrl-RS",
            "sr-Cyrl-XK",
            "sr-Latn",
            "sr-Latn-BA",
            "sr-Latn-CS",
            "sr-Latn-ME",
            "sr-Latn-RS",
            "sr-Latn-XK",
            "ss",
            "ss-SZ",
            "ss-ZA",
            "ssy",
            "ssy-ER",
            "st",
            "st-LS",
            "st-ZA",
            "sv",
            "sv-AX",
            "sv-FI",
            "sv-SE",
            "sw",
            "sw-CD",
            "sw-KE",
            "sw-TZ",
            "sw-UG",
            "syr",
            "syr-SY",
            "ta",
            "ta-IN",
            "ta-LK",
            "ta-MY",
            "ta-SG",
            "te",
            "te-IN",
            "teo",
            "teo-KE",
            "teo-UG",
            "tg",
            "tg-Cyrl",
            "tg-Cyrl-TJ",
            "th",
            "th-TH",
            "ti",
            "ti-ER",
            "ti-ET",
            "tig",
            "tig-ER",
            "tk",
            "tk-TM",
            "tn",
            "tn-BW",
            "tn-ZA",
            "to",
            "to-TO",
            "tr",
            "tr-CY",
            "tr-TR",
            "ts",
            "ts-ZA",
            "tt",
            "tt-RU",
            "twq",
            "twq-NE",
            "tzm",
            "tzm-Arab",
            "tzm-Arab-MA",
            "tzm-Latn",
            "tzm-Latn-DZ",
            "tzm-Latn-MA",
            "tzm-Tfng",
            "tzm-Tfng-MA",
            "ug",
            "ug-CN",
            "uk",
            "uk-UA",
            "ur",
            "ur-IN",
            "ur-PK",
            "uz",
            "uz-Arab",
            "uz-Arab-AF",
            "uz-Cyrl",
            "uz-Cyrl-UZ",
            "uz-Latn",
            "uz-Latn-UZ",
            "vai",
            "vai-Latn",
            "vai-Latn-LR",
            "vai-Vaii",
            "vai-Vaii-LR",
            "ve",
            "ve-ZA",
            "vi",
            "vi-VN",
            "vo",
            "vo-001",
            "vun",
            "vun-TZ",
            "wae",
            "wae-CH",
            "wal",
            "wal-ET",
            "wo",
            "wo-SN",
            "xh",
            "xh-ZA",
            "xog",
            "xog-UG",
            "yav",
            "yav-CM",
            "yi",
            "yi-001",
            "yo",
            "yo-BJ",
            "yo-NG",
            "zgh",
            "zgh-Tfng",
            "zgh-Tfng-MA",
            "zh",
            "zh-CN",
            "zh-Hans",
            "zh-Hans-HK",
            "zh-Hans-MO",
            "zh-Hant",
            "zh-HK",
            "zh-MO",
            "zh-SG",
            "zh-TW",
            "zu",
            "zu-ZA",
            "zh-CHS",
            "zh-CHT"
        };
#endif
    }
}
