
# Threat model of MSBuild BuildCheck feature

## BuildCheck Feature Description

The infrastructure within MSBuild allowing pluggability and execution of
Analyzers and their Rules previously known as "warning waves" and
"MSBuild Analyzers".

The feature is meant to help customers to improve and understand quality of their MSBuild scripts via rules violations reporting. It will allow MSBuild to gradually roll out additional rules, as users will be capable to configure their opt-in and severity of reports – preventing unwanted build breakages. And to equip powerusers to roll out their own quality checks – whether for general community or internal enterprise usage.

[Design
Spec](https://github.com/dotnet/msbuild/blob/main/documentation/specs/proposed/BuildCheck.md)

[Architecture](https://github.com/dotnet/msbuild/blob/main/documentation/specs/proposed/BuildCheck-Architecture.md)

# Threats Identification

This feature does not alter existing nor create any new trust boundaries.

It is assumed to rely on only trusted sources, be managed by trusted operators, and operated on trusted machines.

For this document, we do not address any threats that result from violating these conditions.

## Acquisition

### Threat: Supply chain attack on custom analyzer

Custom BuildCheck analyzers are executed during build. If bad external actors inject malicious code into it by supply chain attack or somehow else, such code can run on build machine, mostly build agent or develop box.

#### Mitigation

Custom analyzers are delivered as regular nuget packages by MSBuild `<PackageReference />` element. This way custom analyzer packages will be included in the generated SBOM and Component Government can detect and warn about known malicious custom analyzers.

It is identical to Roslyn analyzers or any other nuget package, for that matter.

## Execution

### Threat: Supply chain attack by custom analyzer

Custom BuildCheck analyzers are executed during build. If bad external actors inject malicious code into it by supply chain attack or somehow else, such code can run on build machine, mostly build agent or develop box, with intent to inject malicious behavior into build artifacts.

#### Mitigation

Identical to mitigation of threat [Supply chain attack on custom analyzer](#threat-supply-chain-attack-on-custom-analyzer).

### Threat: Third-Party Vulnerabilities
Vulnerabilities in custom analyzer or its dependencies.

#### Mitigation

Custom analyzers are delivered as regular NuGet packages by MSBuild `<PackageReference />` element. This way custom analyzer packages will be included in the generated SBOM and Component Government can detect and warn about known malicious custom analyzers.

## Configuration

### Threat: Malicious configuration value

Although .editorconfig shall be part of trusted sources, and hence not malicious, .editorconfig is looked up in parent folders up to the root. This can allow attacked to store malicious editor config up in parent folders with intent of disabling an analyzer or cause build malfunction for any reason.

#### Mitigation

This problem is identical to existing .editorconfig for Roslyn analyzers and since we share code for parsing it, we adopt same mitigation strategy, which is:

- default template for editor config has `root = true` stopping parent config traversing
- code is unit tested to verify and sanitize .editorconfig values

### Threat: Intentional analyzer ID conflict or misleading ID

Malicious actors can define analyzer ID to be identical or like existing well known analyzer ID to increase probability of executing malicious analyzer code.

#### Mitigation

Main mitigation relays on nuget packages component governance.

BuildCheck also disallow duplicated analyzer IDs and do not allow well known prefixes, for example `microsoft-\*`, in custom analyzers.

## Declaration

### Threat: Malicious analyzer registration property function

Threat actor can write malicious analyzer registration property function in project files, with intent to run code from non-governed assemblies.

#### Mitigation

This threat is out of scope of this document, as this requires malicious modification of source code (repository) making these sources untrusted.

It is mentioned here, as a note that we have thought about it.
