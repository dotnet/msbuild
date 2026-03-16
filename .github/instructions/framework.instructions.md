---
applyTo: "src/Framework/**"
---

# MSBuild Framework Instructions

`Microsoft.Build.Framework` defines MSBuild's public API contracts: interfaces, base types, event args, and extensibility points. Referenced by every task and logger author.

## API Surface Discipline (Critical)

* Default to `internal`. Every `public` member is a permanent commitment.
* New public API **must** be recorded in `PublicAPI.Unshipped.txt`.
* Add XML doc comments to all public members.
* Seal classes unless explicitly designed for inheritance. Prefer interfaces for extensibility.

## Public API Compatibility

* Never remove or change signatures of existing public members — add new overloads.
* Interface additions require default interface method implementations to avoid breaking implementors.
* Binary compatibility matters — task assemblies compiled against older Framework versions must continue to work.

## Event Args & Build Events

* Event args are serialized in binary logs — adding fields requires backward-compatible serialization. See [Binary Log](../../documentation/wiki/Binary-Log.md).
* New event types must integrate with `IEventSource`, forwarding loggers, and binary log reader/writer.
* `MessageImportance` levels: `High` = user-critical, `Normal` = standard, `Low` = verbose.

## BuildCheck Contracts

* Analyzer interfaces define the contract with third-party analyzers — treat as public API.
* See [BuildCheck Architecture](../../documentation/specs/BuildCheck/BuildCheck-Architecture.md).

## Serialization Stability

* Types serialized across IPC or persisted in binary logs must maintain format stability.
* When adding fields, handle the case where the field is missing (backward compat with older writers).
* Use `ITranslatable` for IPC serialization; follow existing patterns.

## Interface Design

* `IBuildEngine` versions follow a progression pattern — new task capabilities go in the next numbered interface.
* Ensure new interfaces are implemented on `TaskExecutionHost`.

## Related Documentation

* [Microsoft.Build.Framework](../../documentation/wiki/Microsoft.Build.Framework.md)
* [BuildCheck specs](../../documentation/specs/BuildCheck/BuildCheck.md)
