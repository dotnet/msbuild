# MSBuild binary log overview

Starting with MSBuild 15.3 a new binary log format is introduced, to complement the existing file and console loggers.

Goals:
 * completeness (more information than the most detailed file log)
 * build speed (doesn't slow the build down nearly as much as the diagnostic-level file log)
 * smaller disk size (10-20x more compact than a file log)
 * structure (preserves the exact build event args that can later be replayed to reconstruct the exact events and information as if a real build was running). File logs erase structure and are harder to parse (especially for multicore /m builds).

# Creating a binary log during a build

# Replaying a binary log

# Using MSBuild Structured Log Viewer

# Binary log file format