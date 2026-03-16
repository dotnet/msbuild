---
applyTo: "src/Framework/**"
---

# MSBuild Framework Instructions

`Microsoft.Build.Framework` defines MSBuild's public API contracts: interfaces, base types, event args, and extensibility points. This assembly is referenced by every task and logger author.

## API Surface Discipline (Critical)

* Default to `internal` visibility. Every `public` member is a permanent commitment that can never be removed.
* New public API additions **must** be recorded in `PublicAPI.Unshipped.txt`.
* Add XML doc comments to all public members — task and logger authors depend on these docs.
* Seal classes unless explicitly designed for inheritance. Prefer interfaces for extensibility.
* Do not widen visibility (e.g., `internal` → `public`) without strong justification and team discussion.

## Public API Compatibility

* Never remove or change the signature of existing public members. Add new overloads alongside existing ones.
* Interface additions require default interface method implementations to avoid breaking existing implementors.
* Enum additions are generally safe but verify no `switch` exhaustiveness assumptions exist downstream.
* Binary compatibility matters — task assemblies compiled against older Framework versions must continue to work.

## Event Args & Build Events

* Build event args classes are serialized in binary logs. Adding fields requires backward-compatible serialization — see [Binary Log docs](../../documentation/wiki/Binary-Log.md).
* New event types must integrate with the logging infrastructure: `IEventSource`, forwarding loggers, and binary log reader/writer.
* Use appropriate `MessageImportance` levels: `High` for user-critical, `Normal` for standard output, `Low` for verbose, `Diagnostic` for debugging.

## BuildCheck Contracts

* BuildCheck analyzer interfaces in this assembly define the contract between MSBuild and third-party analyzers.
* Changes to analyzer base classes or interfaces break third-party analyzers — treat as public API.
* See [BuildCheck Architecture](../../documentation/specs/BuildCheck/BuildCheck-Architecture.md).

## Serialization Stability

* Types serialized across IPC (node communication) or persisted in binary logs must maintain format stability.
* When adding serializable fields, always handle the case where the field is missing (backward compat with older writers).
* Use `ITranslatable` for IPC serialization; follow existing patterns in the codebase.

## Interface Design

* `IBuildEngine` versions (`IBuildEngine2` through `IBuildEngine10+`) follow a progression pattern. New task capabilities go in the next numbered interface.
* Task authors check for interface support via `is` checks — ensure new interfaces are implemented on `TaskExecutionHost`.

## Related Documentation

* [Microsoft.Build.Framework](../../documentation/wiki/Microsoft.Build.Framework.md)
* [BuildCheck specs](../../documentation/specs/BuildCheck/BuildCheck.md)
