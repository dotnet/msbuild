---
applyTo: "src/MSBuild/**"
---

# MSBuild command line

- Preserve accepted switch names/aliases, exit codes, response-file behavior, and existing project discovery unless an intentional compatibility change is in scope.
- Switch aliases are explicitly listed and compared in [CommandLineSwitches](../../src/MSBuild/CommandLine/CommandLineSwitches.cs); do not invent shortest-unique-prefix matching.
- Check the specific switch's parameter rules. Not every switch accepts bare, `:true`, and `:false` forms.
- Trace `XMake` parsing through server selection and build parameters. A `dotnet` verb may have a separate SDK parser/forwarding path; use the [SDK integration skill](../skills/integrating-sdk-and-msbuild/SKILL.md) at that boundary.
- Avoid unnecessary startup initialization. Server-mode changes must account for repeated builds, environment changes, and state reset.
- Preserve the existing exception boundary. Do not catch every exception or turn an internal failure into an apparently successful invocation.
- Use resource-based diagnostics and existing exit behavior. Assess user-visible differences with the [compatibility skill](../skills/assessing-breaking-changes/SKILL.md), rather than requiring a ChangeWave for every edit.
- Validate the affected combinations of explicit arguments, response files, environment defaults, and host/mode. Confirm which executable handled the command.

Relevant references: [environment variables](../../documentation/wiki/MSBuild-Environment-Variables.md), [threading](../../documentation/specs/threading.md), and [apphost](../../documentation/specs/msbuild-apphost.md). Read the one matching the changed path.
