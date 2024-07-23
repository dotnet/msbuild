
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

Detecting unsecure packages is not MSBuild responsibility and is currently out of scope of this feature.

Custom analyzers are delivered as regular nuget packages by MSBuild `<PackageReference />` element.
Users is expected to implement process to detect and warn about known malicious custom analyzers.

#### Important Notice:
Users should not add untrusted build analyzers to their projects. The use of untrusted or unverified 3rd party analyzers can introduce security risks and vulnerabilities into the build process. Always ensure that any build analyzers integrated into your projects come from reputable sources and have been thoroughly vetted.

To ensure a secure and reliable build environment, the following steps should be taken:

#### Use Dedicated Security Tools:
Utilize specialized security tools and services to scan and monitor 3rd party analyzer packages and their dependencies. 
#### Regular Updates:
Ensure that all 3rd party packages and dependencies are regularly updated to the latest versions, which often include security patches and vulnerability fixes.

#### Vendor Documentation and Support:
Refer to the official documentation and support channels provided by the developers of the 3rd party analyzer packages. They may offer guidance and tools for managing security and addressing vulnerabilities.

#### Internal Security Policies:
Implement internal policies and processes for the assessment and mitigation of security risks associated with using 3rd party packages. This can include regular security audits, dependency management protocols, and automated vulnerability scanning during the build process.

## Execution

### Threat: Supply chain attack by custom analyzer

Custom BuildCheck analyzers are executed during build. If bad external actors inject malicious code into it by supply chain attack or somehow else, such code can run on build machine, mostly build agent or develop box, with intent to inject malicious behavior into build artifacts.

#### Mitigation

Detecting unsecure packages is not MSBuild responsibility and is currently out of scope of this feature.

### Threat: Third-Party Vulnerabilities
Vulnerabilities in custom analyzer or its dependencies.

#### Mitigation

Detecting unsecure packages is not MSBuild responsibility and is currently out of scope of this feature.

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

Detecting unsecure packages is not MSBuild responsibility and is currently out of scope of this feature.

## Declaration

### Threat: Malicious analyzer registration property function

Threat actor can write malicious analyzer registration property function in project files, with intent to run code from non-governed assemblies.

#### Mitigation

This threat is out of scope of this document, as this requires malicious modification of source code (repository) making these sources untrusted.
