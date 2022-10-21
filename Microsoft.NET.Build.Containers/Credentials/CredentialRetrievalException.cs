using System;

namespace Microsoft.NET.Build.Containers.Credentials;

internal class CredentialRetrievalException : Exception
{
	public CredentialRetrievalException(string registry, Exception innerException)
		: base($"Failed retrieving credentials for \"{registry}\": {innerException.Message}", innerException)
	{ }
}
