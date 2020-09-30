
#if CODE_ANALYSIS
[module: SuppressMessage("Microsoft.Design", "CA2210:AssembliesShouldHaveValidStrongNames")]
[module: SuppressMessage("Microsoft.MSInternal", "CA905:SystemAndMicrosoftNamespacesRequireApproval", Scope="namespace", Target="Microsoft.Build.CommandLine", Justification="This is an approved namespace.")]
[module: SuppressMessage("Microsoft.Naming","CA1709:IdentifiersShouldBeCasedCorrectly", MessageId="STA", Scope="type", Target="Microsoft.Build.Framework.RunInSTAAttribute", Justification="Not worth breaking custormers because of case.")]
#endif
