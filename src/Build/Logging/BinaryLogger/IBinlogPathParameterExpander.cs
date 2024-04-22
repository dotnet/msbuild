// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Build.Logging;

/// <summary>
/// Interface for expanding LogFile parameter wildcard(s) in a binary log.
/// Wildcards can be used in the LogFile parameter in a form for curly brackets ('{}', '{[param]}').
/// Currently, the only supported wildcard is '{}', the optional parameters within the curly brackets
///  are not currently supported, however the string parameter to the <see cref="ExpandParameter"/> method
/// is reserved for this purpose.
/// </summary>
public interface IBinlogPathParameterExpander
{
    /// <summary>
    /// Expands the wildcard parameter in a binlog path parameter.
    /// </summary>
    /// <param name="parameters">
    /// Reserved for future use, currently not used.
    /// </param>
    /// <returns>Replacement for the wildcard.</returns>
    string ExpandParameter(string parameters);
}
