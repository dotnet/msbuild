# When should we use LiveLogger

The LiveLogger presents the user with the build's most relevant information at the time, automatically hiding all the information that is no longer relevant (as to prevent huge log outputs). However, many users might find this not very useful (or even counterproductive) such as those using a terminal without proper ANSI support or when redirecting the output to a file. For that reason, the users must be able to turn this feature on/off at will.

# Proposal
Using the `/livelogger` or `/ll` command line switches, users are able to opt-in and use the LiveLogger, EXCEPT when:
- The terminal does not support ANSI codes or color
 - Output is redirected to a file or pipe

For early development stages, an environment variable `$MSBUILDLIVELOGGER` should be enabled to prevent accidental access to an unfinished feature. 

In cases where the LiveLogger should not be enabled, the default ConsoleLogger should be used instead.

# Considerations
## Should LiveLogger be used with other loggers (eg, BinaryLogger, FileLogger, custom loggers)?
LiveLogger should only replace the current ConsoleLogger for the aforementioned cases. Additionally, other loggers can be used in conjunction. 

## Should output be ignored with the `/noconsolelogger` flag enabled?
LiveLogger serves as a replacement for ConsoleLogger, so it should behave similarly. When attaching the `/noconsolelogger` flag, it should not output anything.
