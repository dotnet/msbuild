# Wave 2 logbook - ConditionEvaluator

## Candidate under investigation

- Canonical candidate: `ConditionEvaluator` expression cache / pool locking
- Stage: project load and evaluation, with additional execution-time reuse

## Shared cache structure

- `ConditionEvaluator` is a static type with a process-wide cache:
  - `s_cachedExpressionTrees` maps `ParserOptions` (int) to per-options caches: `src\Build\Evaluation\ConditionEvaluator.cs:166-167`
  - each per-options cache maps raw condition text to `Stack<GenericExpressionNode>` pools: `src\Build\Evaluation\ConditionEvaluator.cs:138-163`
- This means sharing is keyed by:
  1. parser options
  2. exact condition string text
- Same text under different parser options does **not** share the same pool.

## Locking behavior

- The per-condition pool object is a plain `Stack<GenericExpressionNode>`, not a concurrent collection; access is serialized with `lock (expressionPool)`: `src\Build\Evaluation\ConditionEvaluator.cs:248-250`
- Inside that lock, the code:
  - decides whether to parse or pop: `src\Build\Evaluation\ConditionEvaluator.cs:252-269`
  - creates evaluation state: `src\Build\Evaluation\ConditionEvaluator.cs:273-281`
  - evaluates the expression tree: `src\Build\Evaluation\ConditionEvaluator.cs:283-302`
  - resets node state and pushes the tree back into the pool: `src\Build\Evaluation\ConditionEvaluator.cs:293-299`
- There is also an inner `lock (parsedExpression)`: `src\Build\Evaluation\ConditionEvaluator.cs:286-302`

## Most important finding

- **Evaluation happens while holding the shared per-condition pool lock.**
- That means the advertised pool-growth behavior in the comment is not achieved in practice.
  - Comment claims that “during high demand when all expression trees are busy evaluating, a new expression tree is created and added to the pool”: `src\Build\Evaluation\ConditionEvaluator.cs:134-137`
  - But because `expressionPool` remains locked across the full parse/evaluate/reset/push lifecycle, no competing thread can observe the pool empty while another tree is in use.
  - Result: for a given `(ParserOptions, condition string)` key, callers are effectively serialized and the pool cannot expand to absorb contention.

## Why the expression trees need exclusivity

- Expression nodes are explicitly stateful across one evaluation:
  - base contract requires nodes to clear cached state via `ResetState()`: `src\Build\Evaluation\Conditionals\GenericExpressionNode.cs:50-55`
  - `StringExpressionNode` caches expanded values and clears them in `ResetState()`: `src\Build\Evaluation\Conditionals\StringExpressionNode.cs:19-20`, `src\Build\Evaluation\Conditionals\StringExpressionNode.cs:71-115`, `src\Build\Evaluation\Conditionals\StringExpressionNode.cs:136-160`
  - `MultipleComparisonNode` tracks `_conditionedPropertiesUpdated` and clears it in `ResetState()`: `src\Build\Evaluation\Conditionals\MultipleComparisonExpressionNode.cs:17`, `src\Build\Evaluation\Conditionals\MultipleComparisonExpressionNode.cs:106-109`, `src\Build\Evaluation\Conditionals\MultipleComparisonExpressionNode.cs:115-139`
  - `OperatorExpressionNode.ResetState()` recursively resets children: `src\Build\Evaluation\Conditionals\OperatorExpressionNode.cs:57-66`
- So some kind of exclusivity per tree is required; the problem is that the current implementation holds the **pool** lock, not just the borrowed tree.

## What work is serialized under the shared lock

- Cold miss path parses under the pool lock: `src\Build\Evaluation\ConditionEvaluator.cs:255-265`
- Hot path still evaluates under the pool lock: `src\Build\Evaluation\ConditionEvaluator.cs:268-302`
- Evaluation can include non-trivial work:
  - property/item expansion through `ExpandIntoString` / `ExpandIntoTaskItems`: `src\Build\Evaluation\ConditionEvaluator.cs:455-483`
  - cached string-node expansion logic: `src\Build\Evaluation\Conditionals\StringExpressionNode.cs:71-115`, `src\Build\Evaluation\Conditionals\StringExpressionNode.cs:136-150`
  - function evaluation such as `Exists(...)`, which expands arguments, normalizes paths, and performs file existence checks: `src\Build\Evaluation\Conditionals\FunctionCallExpressionNode.cs:35-71`, `src\Build\Evaluation\Conditionals\FunctionCallExpressionNode.cs:115-182`

## How repeated conditions arise

- Project evaluation repeatedly feeds raw XML condition text into `ConditionEvaluator`:
  - `Evaluator.EvaluateCondition(element, condition, ...)`: `src\Build\Evaluation\Evaluator.cs:2432-2451`
  - design-time conditioned-property collection uses the same core cache path: `src\Build\Evaluation\Evaluator.cs:2463-2486`
- Execution also reuses the same static cache:
  - target conditions: `src\Build\BackEnd\Components\RequestBuilder\TargetEntry.cs:350-360`
  - task and intrinsic-task conditions: `src\Build\BackEnd\Components\RequestBuilder\TaskBuilder.cs:395-409`, `src\Build\BackEnd\Components\RequestBuilder\IntrinsicTasks\ItemGroupIntrinsicTask.cs:65-75`
  - using-task conditions during task registry initialization: `src\Build\Instance\TaskRegistry.cs:291-299`
  - public `ProjectInstance.EvaluateCondition`: `src\Build\Instance\ProjectInstance.cs:2347-2359`
- Repetition can happen in at least two important ways:
  1. **same imported/shared condition text across many projects** in one process
  2. **same condition evaluated repeatedly inside bucketed execution**, e.g. `ItemGroupIntrinsicTask` and `TaskBuilder.ExecuteBucket` evaluate one condition per bucket

## Cache segmentation that reduces impact somewhat

- Cache sharing is split by `ParserOptions`, not just by condition text: `src\Build\Evaluation\ConditionEvaluator.cs:240-243`
- That means the same textual condition used in different contexts may end up in different pools, reducing cross-stage collisions.

## Overall assessment

- Structural issue: **real**
- Strongest evidence: **the per-condition lock covers full evaluation, so concurrency collapses to one-at-a-time for identical conditions**
- Likelihood of this becoming a real bottleneck in one-process parallel builds: **medium**
  - moves upward when many parallel projects execute the same imported conditions or when bucketed execution repeats the same condition text
  - moves downward when condition strings are mostly unique, parser options differ, or conditions are trivial and fast

## Escalation decision

- **Escalate: yes**
- Reason: this is not just a vague “shared cache” suspicion; the source shows a concrete serialization scope that defeats the intended pooling behavior for each shared condition key.
