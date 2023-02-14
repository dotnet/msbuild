// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.NET.Build.Containers.Credentials;

internal sealed class CredentialRetrievalException : Exception
{
	public CredentialRetrievalException(string registry, Exception innerException)
		: base($"Failed retrieving credentials for \"{registry}\": {innerException.Message}", innerException)
	{ }
}
