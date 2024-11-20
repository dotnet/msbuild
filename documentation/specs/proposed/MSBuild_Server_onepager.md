## MSBuild Server

MSBuild server aims to create a persistent entry node for the MSBuild
that we would communicate with via a thin client. We want to get from
the current state of “spawn a complete process for every CLI invocation”
to “we have a server process in the background and we only spawn a small
CLI handler that will tell the server what to build”.

### Goals and Motivation

Currently all the MSBuild processes are persistent, except for the entry
point process which lives only for the duration of the build. Restarting
this process with each and every build leads to some overhead due to
startup costs like jitting. It also leads to a loss of continuity mainly
due to the absence of caching.

The primary aim of the MSBuild server is to reduce this startup
overhead.

The secondary aim of this project is to enable us to introduce more
advanced caching and potentially some other performance optimizations
further down the line. However these aren’t in the current scope.

### Impact

Small performance improvement in the short term. Enabling further
optimizations in the long term. (these improvements are for the Dev Kit
and inner loop CLI scenarios)

Getting closer to the possibility of decoupling from Visual Studio.

### Stakeholders

Tomas Bartonek, Rainer Sigwald. Successful handover means turning on the
feature, dogfooding it for long enough to ensure we have reasonable
certainty that nothing breaks and then rolling it out.

### Risks

The project was already attempted once, however it was postponed because
it surfaced a group of bugs that weren’t previously visible due to the
processes not being persistent. Most of those bugs should be solved by
now, however we can run into some new ones.

### Cost

A week to figure out how to turn on the MSBuild Server in a way that
will enable us to dogfood it properly **plus** some overhead for the
review loop.

A month of developer time for bugfixes assuming that nothing goes
terribly wrong.

Some PM time to communicate with appropriate teams to ask them for help
with dogfooding.

### Plan

- In a first month we should aim to get the MSBuild server dogfooded for
  our internal time. (Coding + review + setting up)

- Second month we will monitor it and fix anything that crops up.

- After that we start dogfooding internally for as long as we feel
  necessary to ensure everything works as intended. I would give this
  period one to three months of monitoring + bugfixing when necessary.
