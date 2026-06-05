# ConditionEvaluator

## Why shared

`ConditionEvaluator` uses a static, process-wide cache of parsed expression trees. The top-level cache `s_cachedExpressionTrees` is keyed by `ParserOptions`, and each per-options entry contains a dictionary from raw condition text to a pooled `Stack<GenericExpressionNode>` (`src\Build\Evaluation\ConditionEvaluator.cs:138-167`, `src\Build\Evaluation\ConditionEvaluator.cs:240-248`). That makes the cache effectively shared by all project evaluations and request-execution paths running in the same MSBuild process.

## Why it might bottleneck

For a given `(ParserOptions, condition string)` key, the code takes `lock (expressionPool)` and holds it across parse/pop, evaluation, reset, and push-back (`src\Build\Evaluation\ConditionEvaluator.cs:250-305`). This means callers with the same shared condition key do not merely serialize cache maintenance; they serialize the full condition evaluation. The design comment says the pool should grow when demand is high (`src\Build\Evaluation\ConditionEvaluator.cs:134-137`), but the outer lock prevents other threads from observing an empty pool while one tree is in use, so the pool cannot actually scale out under contention.

## Evidence

- Shared cache and per-condition pools:
  - `src\Build\Evaluation\ConditionEvaluator.cs:138-167`
  - `src\Build\Evaluation\ConditionEvaluator.cs:240-248`
- Full evaluation under the per-condition shared lock:
  - `src\Build\Evaluation\ConditionEvaluator.cs:250-305`
- Intended pool-growth behavior stated in comment:
  - `src\Build\Evaluation\ConditionEvaluator.cs:134-137`
- Expression trees are stateful across one evaluation, so reuse requires reset/exclusivity:
  - `src\Build\Evaluation\Conditionals\GenericExpressionNode.cs:50-55`
  - `src\Build\Evaluation\Conditionals\OperatorExpressionNode.cs:61-66`
  - `src\Build\Evaluation\Conditionals\StringExpressionNode.cs:19-20`
  - `src\Build\Evaluation\Conditionals\StringExpressionNode.cs:71-115`
  - `src\Build\Evaluation\Conditionals\StringExpressionNode.cs:157-160`
  - `src\Build\Evaluation\Conditionals\MultipleComparisonExpressionNode.cs:17`
  - `src\Build\Evaluation\Conditionals\MultipleComparisonExpressionNode.cs:106-109`
- Evaluation can include more than cheap boolean logic:
  - string/item/property expansion through evaluation state: `src\Build\Evaluation\ConditionEvaluator.cs:455-483`
  - function work such as `Exists(...)` calling expansion and filesystem checks: `src\Build\Evaluation\Conditionals\FunctionCallExpressionNode.cs:35-71`, `src\Build\Evaluation\Conditionals\FunctionCallExpressionNode.cs:115-182`

## Likelihood

**Medium.** The structural serialization is definite, but contention only happens when multiple threads hit the same exact condition text with the same parser options. In homogeneous SDK-style or import-heavy builds, that is plausible; in builds where conditions are mostly unique or fragmented across parser-option buckets, impact drops.

## Expected contention mode

- **Primary mode:** per-key monitor contention on `lock (expressionPool)`
- **Key granularity:** exact `(ParserOptions, condition string)` match
- **Impact shape:** serialized hot-path evaluation, not just serialized cold parsing
- **Cold-miss behavior:** first parse also happens under the same shared lock
- **Scaling consequence:** the current implementation effectively caps concurrency at one active evaluator per shared condition key

The inner `lock (parsedExpression)` (`src\Build\Evaluation\ConditionEvaluator.cs:286-302`) does not materially improve throughput because the outer pool lock is already held for the entire borrowed-tree lifetime.

## Where it is used

- Project evaluation:
  - `src\Build\Evaluation\Evaluator.cs:2432-2451`
  - `src\Build\Evaluation\Evaluator.cs:2463-2486`
  - `src\Build\Evaluation\LazyItemEvaluator.cs:82-92`
- Build execution:
  - `src\Build\BackEnd\Components\RequestBuilder\TargetEntry.cs:350-360`
  - `src\Build\BackEnd\Components\RequestBuilder\TaskBuilder.cs:401-409`
  - `src\Build\BackEnd\Components\RequestBuilder\IntrinsicTasks\ItemGroupIntrinsicTask.cs:65-75`
- Other shared engine paths:
  - `src\Build\Instance\TaskRegistry.cs:291-299`
  - `src\Build\Instance\ProjectInstance.cs:2351-2359`

These call sites show that the same static cache is reused across both evaluation and execution work inside one process.

## Why it may or may not matter in practice

Why it **may** matter:

- many projects in one process often reuse the same imported XML and therefore the same condition text
- bucketed execution can reevaluate one condition string repeatedly (`TaskBuilder.ExecuteBucket`, `ItemGroupIntrinsicTask`)
- some conditions are not cheap because they expand properties/items or call filesystem-sensitive functions like `Exists(...)`
- the current locking prevents the intended pool from absorbing parallel demand

Why it **may not** matter:

- the lock is per condition key, not global
- parser options split the cache, so identical text in different contexts may not collide
- many conditions may be fast enough that serialization is negligible
- large builds can still distribute contention across many different condition strings

Net: this looks more like a **selective but real contention point** than a universal top bottleneck.

## How to validate

1. Add lightweight instrumentation around `ConditionEvaluator.EvaluateConditionCollectingConditionedProperties` to measure:
   - wait time to enter `lock (expressionPool)`
   - time spent inside the lock
   - key = `(ParserOptions, condition string)`
2. Count distinct pool sizes actually observed per key; if they remain effectively `1` under parallel load, that confirms the source-level analysis.
3. Capture top contended keys in a one-process multi-project build, especially SDK-style solutions with many similar projects.
4. Compare:
   - total condition evaluations
   - lock wait distribution
   - hot keys by frequency and aggregate blocked time
5. If needed, prototype a narrower synchronization design that:
   - holds the pool lock only while borrowing/returning a tree
   - evaluates outside the shared pool lock
   - keeps exclusivity per borrowed tree rather than per pool

If profiling shows low wait time even for hot keys, downgrade the finding. If a small set of repeated condition strings dominates blocked time, upgrade it.
