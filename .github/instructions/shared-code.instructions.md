---
applyTo: "src/Shared/**"
---

# Linked shared code

- Find actual `Compile` includes and their conditions before editing a shared file. Not every file is compiled into every assembly, and some former shared utilities now live in Framework.
- Preserve assembly/type identity, resource lookup, feature guards, and runtime-specific behavior when moving or changing linked source.
- Scope validation to affected consumers and configurations; do not run every test project merely because the path contains `Shared`.
- For node/task-host packets, trace read/write order and the supported peers' handshake/version behavior. An unframed translator cannot silently supply fields absent from an older packet.
- For mutable structs, avoid translating through a cast that boxes a copy; follow the existing constrained-generic translation pattern where applicable.
- Use the owning path/IO helper when it implements the required semantics, not as a blanket ban on `System.IO`. Check relative roots, separators, normalization, long/UNC paths, and filesystem effects for the specific operation.

For shared tests, also use the [test authoring instructions](tests.instructions.md). For binlog events, load the [binary-log skill](../skills/maintaining-binary-log-compatibility/SKILL.md); do not confuse it with IPC compatibility.
