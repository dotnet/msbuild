# Investigating a selected health problem

Use after the overview identifies a problem and deeper investigation is within
the requested scope. Do not launch a separate agent for every red row; several
rows may share one failing build or service dependency.

## Route the question

| Evidence/question | Appropriate available workflow |
|---|---|
| Individual Azure DevOps build/check failure | CI analysis, then pipeline or Helix investigation for the identified leg |
| Crash rather than a test assertion failure | Crash-dump investigation, only when a dump is relevant and accessible |
| Source-flow freshness, subscriptions, stuck codeflow PR | Flow analysis |
| Whether a specific commit reached a repository or SDK | Flow tracing |
| A local or downloaded binary log | Binlog failure analysis using an already available compatible tool |

Discover capabilities before using them. A named installed skill/tool may not be
loaded into this session. Do not download a pinned MCP package or another skill
from a public repository just because an old procedure names it.

## Pipeline failure

Start with the actual failed build URL, definition, repository/ref, source SHA,
first relevant failed stage/job/task, and error/log links. Compare the last
successful and current failing inputs only as needed.

Classify evidence, not just keywords:

- Compilation errors or an assertion failure can indicate a product/test regression. Inspect the changed area and relevant revision.
- Authentication, certificate/signing, or permissions failures can indicate configuration/access problems. A message containing `NuGet` alone does not establish an auth failure; package resolution and downgrade errors may be code/configuration defects.
- Queue outages, agent loss, timeouts, or resource exhaustion can indicate infrastructure trouble, but may also be caused by product behavior. Compare independent failures before asserting an outage.
- A failed job may have no failed task: inspect job/stage issues and agent initialization/cleanup records.

For build errors, inspect the first relevant errors and locate a useful binary
log in the actual build artifacts. Download only the needed artifact when that
local action is authorized. Keep binlogs local unless sharing is authorized;
they can contain credentials, environment values, task inputs, and project files.
Use available binlog tooling rather than installing a historically pinned server.

## VS insertion failures and staleness

Read actual policy evaluations and current iteration evidence. Separate blocking
policy failures, optional check failures, pending checks, merge conflicts, and
unavailable evidence. Follow the failing check's actual build URL through any
dependent pipeline only when it is necessary to reach the cause.

For apparent stale insertion, establish the intended target branch, schedule,
lookback window, last merge time, and active PRs. No recent PR could mean no new
input needed insertion, an untriggered pipeline, or a collection gap. Pending
checks alone do not prove the pipeline is stuck. Weekends and holidays are not
automatically business-time failures.

## VMR failures and upstream hypotheses

First establish that the failed run belongs to the correct repository, pipeline,
PR, and current revision. Then resolve the actual flowed commit range and the
affected files before blaming a particular upstream change.

Useful search leads, not automatic diagnoses:

| Symptom | Follow-up |
|---|---|
| MSB4216 / task-host launch failure | Task host availability, node launch, runtime/architecture, and SDK layout |
| BinaryToolTask failure in a confirmed source-only leg | Compare bootstrap/source-built SDK contents and task-host expectations; the task name alone does not prove source-build context |
| MetadataLoadContext disposed error | Lifetime/host behavior at the failing call chain |
| CS/VB/FS compilation diagnostic | Exact file, symbols, generated inputs, and relevant diff |
| MSB3073 nonzero command | Follow the child tool/test's own error, not the wrapper exit code |
| Feed or signing error | Specific status, access/configuration, or service evidence |
| Timeout or memory pressure | Timeline, allocation/resource evidence, and repeatability |

PR-title keywords and references in comments are only search hints. A current
retry may change the observed status, but a green retry alone does not explain
the earlier failure or prove it was infrastructure.

## Optional ownership context

Use already authorized service/ownership documentation first. If an available
M365/WorkIQ capability is explicitly appropriate, scope the query to the needed
service and minimize returned private information. Do not install it, accept an
EULA, bypass an organization registry, or publish raw tenant responses. Report
missing ownership evidence as such.

## Report and stop

Provide the observed failure, likely cause with evidence and confidence, relevant
URLs, and the next authorized action. Keep hypotheses separate from findings.
Suggest a retry, fix, escalation, or waiting only when supported by evidence;
actually retrying, canceling, posting, reverting, or changing subscriptions needs
separate authority. Stop when the selected problem is explained or its precise
remaining evidence/access blocker is identified.
