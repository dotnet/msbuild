# TerminalLogger Opt-in

## When should we use TerminalLogger

The TerminalLogger presents the user with the build's most relevant information at the time, automatically hiding all the information that is no longer relevant (as to prevent huge log outputs). However, many users might find this not very useful (or even counterproductive) such as those using a terminal without proper ANSI support or when redirecting the output to a file. For that reason, the users must be able to turn this feature on/off at will.

## Proposal

### Enabling for a single build

Using the `/terminallogger` or `/tl` command line switches, users are able to opt-in and use the TerminalLogger, EXCEPT when:

- The terminal does not support ANSI codes or color
- Output is redirected to a file or pipe

### Enabling for all builds

Users can set the `MSBUILDTERMINALLOGGER` environment variable to enable TerminalLogger without adding a swtich to all build invocations.

### TerminalLogger parameters

Both methods accept parameters:

- `true` forces TerminalLogger to be used even wwhen it would be disabled
- `false` forces TerminalLogger to not be used even when it would be enabled
- `auto` enables TerminalLogger when the terminal supports it and the session doesn't have redirected stdout/stderr

In cases where the TerminalLogger should not be enabled, the default ConsoleLogger should be used instead.

## Considerations

### Should TerminalLogger be used with other loggers (eg, BinaryLogger, FileLogger, custom loggers)?

TerminalLogger should only replace the current ConsoleLogger for the aforementioned cases. Additionally, other loggers can be used in conjunction.

### Should output be ignored with the `/noconsolelogger` flag enabled?

TerminalLogger serves as a replacement for ConsoleLogger, so it should behave similarly. When attaching the `/noconsolelogger` flag, it should not output anything.
