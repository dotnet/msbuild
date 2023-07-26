// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.RegularExpressions;

namespace Microsoft.NET.Build.Containers;

/// <summary>
/// This class is a port of the regex patterns described in the regexp.go file in the OCI Registry spec repo distribution/distribution.
/// It is current as of SHA 78b9c98c5c31c30d74f9acb7d96f98552f2cf78f.
/// <see href="https://github.com/distribution/distribution/blob/78b9c98c5c31c30d74f9acb7d96f98552f2cf78f/reference/regexp.go">regexp.go</see> is the direct file link.
/// Comments on each member are lifted directly from this source.
/// Names of each member are deliberately non-.NET-standard, as they were kept aligned with their golang versions for easier comparison.
/// Visibility of each member is determined by golang rules - lowercase is private, uppercase is public. The exception is when a private member is used inside the golang module.
/// </summary>
internal static class ReferenceParser
{

    /// <summary>
    /// alphaNumeric defines the alpha numeric atom, typically a
    /// component of names. This only allows lower case characters and digits.
    /// </summary>
    private static readonly string alphaNumeric = @"[a-z0-9]+";

    /// <summary>
    /// separator defines the separators allowed to be embedded in name
    /// components. This allow one period, one or two underscore and multiple
    /// dashes. Repeated dashes and underscores are intentionally treated
    /// differently. In order to support valid hostnames as name components,
    /// supporting repeated dash was added. Additionally double underscore is
    /// now allowed as a separator to loosen the restriction for previously
    /// supported names.
    /// </summary>
    private static readonly string separator = @"(?:[._]|__|[-]*)";

    /// <summary>
    /// nameComponent restricts registry path component names to start
    /// with at least one letter or number, with following parts able to be
    /// separated by one period, one or two underscore and multiple dashes.
    /// </summary>
    private static readonly string nameComponent = expression(alphaNumeric, optional(repeated(separator, alphaNumeric)));

    /// <summary>
    /// domainNameComponent restricts the registry domain component of a
    /// repository name to start with a component as defined by DomainRegexp.
    /// </summary>
    private static readonly string domainNameComponent = @"(?:[a-zA-Z0-9]|[a-zA-Z0-9][a-zA-Z0-9-]*[a-zA-Z0-9])";

    /// <summary>
    /// ipv6address are enclosed between square brackets and may be represented
    /// in many ways, see rfc5952. Only IPv6 in compressed or uncompressed format
    /// are allowed, IPv6 zone identifiers (rfc6874) or Special addresses such as
    /// IPv4-Mapped are deliberately excluded.
    /// </summary>
    private static readonly string ipv6address = expression(
        literal("["),
        @"(?:[a-fA-F0-9:]+)",
        literal("]")
    );

    /// <summary>
    /// domainName defines the structure of potential domain components
    /// that may be part of image names. This is purposely a subset of what is
    /// allowed by DNS to ensure backwards compatibility with Docker image
    /// names. This includes IPv4 addresses on decimal format.
    /// </summary>
    private static readonly string domainName = expression(
        domainNameComponent,
        // Require two name domainNameComponents to prevent matching a domain when parsing the short containername: 'ubuntu/runtime'.
        literal("."), domainNameComponent,
        optional(repeated(literal("."), domainNameComponent))
    );

    /// <summary>
    /// host defines the structure of potential domains based on the URI
    /// Host subcomponent on rfc3986. It may be a subset of DNS domain name,
    /// or an IPv4 address in decimal format, or an IPv6 address between square
    /// brackets (excluding zone identifiers as defined by rfc6874 or special
    /// addresses such as IPv4-Mapped).
    /// </summary>
    private static readonly string host = $"(?:{domainName}|{ipv6address}|localhost)";

    /// <summary>
    /// allowed by the URI Host subcomponent on rfc3986 to ensure backwards
    /// compatibility with Docker image names.
    /// </summary>
    private static readonly string domain = expression(
        host,
        optional(literal(":"), "[0-9]+")
    );

    /// <summary>
    /// DomainRegexp defines the structure of potential domain components
    /// that may be part of image names. This is purposely a subset of what is
    /// allowed by DNS to ensure backwards compatibility with Docker image
    /// names.
    /// </summary>
    public static readonly Regex DomainRegexp = new(domain);

    /// <summary>
    /// This is a custom addition - we needed domain-part validation for a string we _knew_ was anchored,
    /// so this was included as a slight addition to the source material.
    /// </summary>
    public static readonly Regex AnchoredDomainRegexp = new(anchored(domain));

    /// <summary>
    /// valid tags are a word character followed by 0-127 subsequent word, dot, or dash characters.
    /// </summary>
    private static readonly string tag = @"[\w][\w.-]{0,127}";

    /// <summary>
    /// TagRegexp matches valid tag names. From docker/docker:graph/tags.go.
    /// </summary>
    public static readonly Regex TagRegexp = new(tag);

    /// <summary>
    // anchoredTag matches valid tag names, anchored at the start and
    // end of the matched string.
    /// </summary>
    private static readonly string anchoredTag = anchored(tag);

    /// <summary>
    /// anchoredTagRegexp matches valid tag names, anchored at the start and
    /// end of the matched string.
    /// </summary>
    public static readonly Regex anchoredTagRegexp = new(anchoredTag);

    /// <summary>
    /// needed because the original golang used `[[:xdigit:]]` which .Net doesn't support.
    /// `[[:xdigit:]]` is the set of valid hex digits, which is 0-9, a-f, and A-F.
    /// </summary>
    private static readonly string hexDigit = "[0-9A-Fa-f]";

    /// <summary>
    /// digestPat matches valid digests.
    /// </summary>
    private static readonly string digestPat = $"[A-Za-z][A-Za-z0-9]*(?:[-_+.][A-Za-z][A-Za-z0-9]*)*[:]{hexDigit}{{32,}}";

    /// <summary>
    /// DigestRegexp matches valid digests.
    /// </summary>
    public static readonly Regex DigestRegexp = new(digestPat);

    /// <summary>
    /// anchoredDigest matches valid digests, anchored at the start and
    /// end of the matched string.
    /// </summary>
    private static readonly string anchoredDigest = anchored(digestPat);

    /// <summary>
    /// anchoredDigestRegexp matches valid digests, anchored at the start and
    /// end of the matched string.
    /// </summary>
    private static readonly Regex anchoredDigestRegexp = new(anchoredDigest);

    /// <summary>
    /// namePat is the format for the name component of references. The
    /// regexp has capturing groups for the domain and name part omitting
    /// the separating forward slash from either.
    /// </summary>
    private static readonly string namePat = expression(
        optional(domain, literal("/")),
        nameComponent,
        optional(repeated(literal("/"), nameComponent))
    );

    /// <summary>
    /// NameRegexp is the format for the name component of references. Logically this consists of
    /// a domain portion followed by one or more /-delimited name components.
    /// </summary>
    public static readonly Regex NameRegexp = new(namePat);

    /// <summary>
    /// anchoredNameRegexp is used to parse a name value, capturing the
    /// domain and trailing components.
    /// </summary>
    private static readonly string anchoredName = anchored(
        optional(capture(domain), literal("/")),
        capture(nameComponent, optional(repeated(literal("/"), nameComponent)))
    );

    /// <summary>
    /// anchoredNameRegexp is used to parse a name value, capturing the
    /// domain and trailing components.
    /// </summary>
    public static readonly Regex anchoredNameRegexp = new(anchoredName);

    /// <summary>
    /// referencePat is the full supported format of a reference. The regexp
    /// is anchored and has capturing groups for name, tag, and digest
    /// components.
    /// </summary>
    private static string referencePat = anchored(
        capture(namePat),
        optional(literal(":"), capture(tag)),
        optional(literal("@"), capture(digestPat))
    );

    /// <summary>
    /// ReferenceRegexp is the full supported format of a reference. The regexp
    /// is anchored and has capturing groups for name, tag, and digest
    /// components.
    /// </summary>
    public static readonly Regex ReferenceRegexp = new(referencePat);

    /// <summary>
    /// identifier is the format for string identifier used as a
    /// content addressable identifier using sha256. These identifiers
    /// are like digests without the algorithm, since sha256 is used.
    /// </summary>
    private static readonly string identifier = @"([a-f0-9]{64})";

    /// <summary>
    /// IdentifierRegexp is the format for string identifier used as a
    /// content addressable identifier using sha256. These identifiers
    /// are like digests without the algorithm, since sha256 is used.
    /// </summary>
    public static readonly Regex IdentifierRegexp = new(identifier);

    /// <summary>
    /// shortIdentifier is the format used to represent a prefix
    /// of an identifier. A prefix may be used to match a sha256 identifier
    /// within a list of trusted identifiers.
    /// </summary>
    private static readonly string shortIdentifier = @"([a-f0-9]{6,64})";

    /// <summary>
    /// ShortIdentifierRegexp is the format used to represent a prefix
    /// of an identifier. A prefix may be used to match a sha256 identifier
    /// within a list of trusted identifiers.
    /// </summary>
    public static readonly Regex ShortIdentifierRegexp = new(shortIdentifier);

    /// <summary>
    /// anchoredIdentifier is used to check or match an
    /// identifier value, anchored at start and end of string.
    /// </summary>
    private static readonly string anchoredIdentifier = anchored(identifier);

    /// <summary>
    /// anchoredIdentifierRegexp is used to check or match an
    /// identifier value, anchored at start and end of string.
    /// </summary>
    private static readonly Regex anchoredIdentifierRegexp = new(anchoredIdentifier);

    /// <summary>
    /// anchoredShortIdentifier is used to check if a value
    /// is a possible identifier prefix, anchored at start and end
    /// of string.
    /// </summary>
    private static readonly string anchoredShortIdentifier = anchored(shortIdentifier);

    /// <summary>
    /// anchoredShortIdentifierRegexp is used to check if a value
    /// is a possible identifier prefix, anchored at start and end
    /// of string.
    /// </summary>
    private static readonly Regex anchoredShortIdentifierRegexp = new(anchoredShortIdentifier);

    /// <summary>
    /// literal compiles s into a literal regular expression, escaping any regexp
    /// reserved characters.
    /// </summary>
    /// <remarks>we use a simpler implementation than the golang source since Regex.Escape seems to do the job</remarks>
    private static string literal(string s) => Regex.Escape(s);

    /// <summary>
    /// expression defines a full expression, where each regular expression must
    /// follow the previous.
    /// </summary>
    private static string expression(params string[] segments)
    {
        var b = new StringBuilder();
        foreach (var s in segments)
        {
            b.Append(s);
        }
        return b.ToString();
    }

    /// <summary>
    /// optional wraps the expression in a non-capturing group and makes the
    /// production optional.
    /// </summary>
    private static string optional(params string[] segments) => $"{group(expression(segments))}?";

    /// <summary>
    /// repeated wraps the regexp in a non-capturing group to get one or more
    /// matches.
    /// </summary>
    private static string repeated(params string[] segments) => $"{group(expression(segments))}+";

    /// <summary>
    /// group wraps the regexp in a non-capturing group.
    /// </summary>
    private static string group(params string[] segments) => $"(?:{expression(segments)})";

    /// <summary>
    /// capture wraps the expression in a capturing group.
    /// </summary>
    private static string capture(params string[] segments) => $"({expression(segments)})";

    /// <summary>
    /// anchored anchors the regular expression by adding start and end delimiters.
    /// </summary>
    private static string anchored(params string[] segments) => $"^{expression(segments)}$";
}
