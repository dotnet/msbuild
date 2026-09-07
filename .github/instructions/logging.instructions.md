---
applyTo: "src/Build/Logging/**"
---

# Logging

- Distinguish node IPC, task-host forwarding, and the persisted binlog format. They have different serialization and compatibility mechanisms.
- Use [binary-log compatibility](../skills/maintaining-binary-log-compatibility/SKILL.md) for reader/writer changes. Preserve supported old-log reading and follow the actual record/version contract; not every old reader can read every newer log.
- Trace event subscriptions and forwarding configuration. A central logger sees what reaches it, not automatically every event generated anywhere.
- Check filtering in the actual logger. [BaseConsoleLogger](../../src/Build/Logging/BaseConsoleLogger.cs) maps `High` to minimal verbosity, `Normal` to normal, and `Low` to detailed; `High` is not visible at quiet verbosity by definition.
- Preserve structured event payloads and available locations through round trips. A text message is not equivalent to preserving event type and fields.
- For terminal output, cover redirected/non-TTY output, narrow/resized terminals, concurrency, and supported platforms when affected.
- Use `RenderImmediateMessage`, not direct terminal writes, for messages that must appear outside the normal node-status rendering cycle, including user-intervention messages and long-delay explanations. Direct writes bypass node-display coordination and can corrupt rendered output.
- Assess changed output or importance as a compatibility surface. Do not add logging to every internal change merely to satisfy a generic checklist.

Read [logging internals](../../documentation/wiki/Logging-Internals.md) or [binary logs](../../documentation/wiki/Binary-Log.md) for the relevant boundary, then confirm the source behavior.
