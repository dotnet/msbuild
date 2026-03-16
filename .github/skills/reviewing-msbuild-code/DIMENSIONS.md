# Review Dimension Checklists

Detailed checklists for all 24 review dimensions. Use these for systematic review verification.

For an overview of each dimension and its rules, see [SKILL.md](SKILL.md).

---

## 1. Backwards Compatibility Vigilance

**Severity: BLOCKING**

MSBuild's backward compatibility contract is near-inviolable. Any change that could alter existing build behavior must be justified, gated, or introduced as opt-in.

**Rules:**
1. **Gate breaking behavior changes behind a ChangeWave.** Any change to property evaluation, target execution order, or error/warning behavior must use the ChangeWave mechanism. See `../../../documentation/wiki/ChangeWaves.md`.
2. **New warnings are breaking changes** for builds using `-WarnAsError` or `<TreatWarningsAsErrors>`. Gate behind a ChangeWave or emit as `Message` importance instead.
3. **Make behavioral changes opt-in by default.** Users must explicitly choose new behavior; it should never silently activate.
4. **SDK target changes must preserve backward compatibility** with existing project files. Changed property defaults break existing projects.
5. **Never remove CLI switches or aliases.** Deprecate with warnings first; maintain the old form indefinitely.

**CHECK — Flag if:**
- [ ] A behavioral change has no ChangeWave gate
- [ ] New warnings are emitted without considering `WarnAsError` impact
- [ ] A property default value has changed
- [ ] A CLI switch or alias has been removed rather than deprecated
- [ ] Output format changes could break downstream consumers

---

## 2. ChangeWave Discipline

**Severity: BLOCKING**

ChangeWave is MSBuild's mechanism for safely introducing behavioral changes. See `../../../documentation/wiki/ChangeWaves.md` and `../../../documentation/wiki/ChangeWaves-Dev.md`.

**Rules:**
1. **Use the correct ChangeWave version** — it must match the upcoming MSBuild release version, not the current one.
2. **Test both enabled and disabled paths** — the opt-out via `MSBuildDisableFeaturesFromVersion` must work correctly.
3. **Document the ChangeWave** in error messages, specs, and the ChangeWaves tracking file.

**CHECK — Flag if:**
- [ ] A behavioral change is not gated behind a ChangeWave
- [ ] The ChangeWave version number does not match the next release
- [ ] Only the enabled path is tested; no test for the disabled/opt-out path
- [ ] The ChangeWave is not documented in `../../../documentation/wiki/ChangeWaves.md`

---

## 3. Performance & Allocation Awareness

**Severity: MAJOR**

MSBuild evaluates and builds thousands of projects in enterprise scenarios. Hot paths include `Evaluator.cs`, `Expander.cs`, and file I/O operations.

**Rules:**
1. **Minimize allocations on hot paths.** Avoid LINQ in tight loops, prefer `Span<T>`/`stackalloc` for small buffers, avoid `string.Format` where interpolation or `StringBuilder` suffices.
2. **Cache computed values** to avoid redundant work — especially in evaluation and expression expansion.
3. **Choose appropriate collection types** for the access pattern. Don't use `Dictionary` for small N where linear scan suffices; don't use `List` for large-N lookup.
4. **Profile before optimizing.** Claims of "this is faster" require evidence.
5. **Avoid string allocations in formatting on hot paths.** Use `string.Create`, `ReadOnlySpan<char>`, or cached strings.

**CHECK — Flag if:**
- [ ] LINQ (`.Where`, `.Select`, `.Any`) is used inside a loop on a hot path
- [ ] A method allocates strings that could be reused or avoided
- [ ] A value is recomputed on every call when it could be cached
- [ ] A `Dictionary` is used for <10 items, or a `List` for frequent lookups of >100 items
- [ ] An optimization claim is made without profiling data

---

## 4. Test Coverage & Completeness

**Severity: MAJOR**

This is one of the most important review concerns.

**Rules:**
1. **Add test coverage** for all new functionality and bug fixes. Bug fixes require a regression test that fails without the fix.
2. **Name test methods** to describe scenario and expected outcome (e.g., `PropertyOverride_LastWriteWins_InImportedProject`).
3. **Ensure tests are deterministic.** No dependency on file system ordering, timing, or environment variables without explicit setup.
4. **Write focused tests** with clear assertion failure messages.
5. **Test both positive and negative paths.** Include edge cases: empty collections, null values, concurrent access, very large inputs.

**CHECK — Flag if:**
- [ ] New behavior has no test coverage
- [ ] A bug fix has no regression test
- [ ] Test method names are opaque (e.g., `Test1`, `TestBug`)
- [ ] Tests depend on implicit environment state or ordering
- [ ] Only the happy path is tested; no edge case or error path coverage

---

## 5. Error Message Quality

**Severity: MAJOR**

Error and warning messages directly impact millions of developers. See `../../../documentation/assigning-msb-error-code.md`.

**Rules:**
1. **Messages must be actionable.** State what happened, why, and what the user should do to fix it.
2. **Use proper MSBuild error/warning codes** in `MSBxxxx` format. Each code must be unique across the codebase.
3. **Use `ResourceUtilities.FormatResourceStringStripCodeAndKeyword`** for error formatting — resource strings must include embedded error codes at the start.
4. **Use correct severity levels.** Warnings must be distinguishable from errors. Don't emit errors for non-fatal conditions.

**CHECK — Flag if:**
- [ ] An error message does not explain what the user should do
- [ ] A new error/warning lacks an `MSBxxxx` code
- [ ] A hardcoded string is used instead of a resource string
- [ ] An error is emitted for a condition that should be a warning, or vice versa
- [ ] A message is user-facing but not localization-ready (not in `.resx`)

---

## 6. Logging & Diagnostics Rigor

**Severity: MODERATE**

See `../../../documentation/wiki/Binary-Log.md` and `../../../documentation/wiki/Logging-Internals.md`.

**Rules:**
1. **Changes must be captured in the binary log.** Any alteration to build behavior should produce corresponding binary log entries.
2. **Use appropriate `MessageImportance` levels.** `High` = always shown, `Normal` = default verbosity, `Low` = detailed verbosity, `Diagnostic` = debugging only.
3. **Add diagnostic logging** for complex code paths to aid debugging.
4. **Use structured logging events** with sufficient context (project path, target name, item identity).
5. **Binary log format changes require backward compatibility.** Readers of older binlogs must not crash on new data.

**CHECK — Flag if:**
- [ ] A behavioral change produces no binary log output
- [ ] `MessageImportance.High` is used for verbose/debugging information
- [ ] A complex code path has no diagnostic logging
- [ ] Log events lack context (e.g., which project, which target)
- [ ] A binary log format change would break older readers

---

## 7. String Comparison Correctness

**Severity: MAJOR**

**Rules:**
1. **Use `MSBuildNameIgnoreCaseComparer`** for property, item, and target name comparisons.
2. **Use `StringComparison.OrdinalIgnoreCase`** for MSBuild identifier comparisons. Never use `CurrentCulture` for MSBuild identifiers.
3. **File path comparisons must be OS-appropriate:** case-sensitive on Linux, case-insensitive on Windows/macOS. Use `FileUtilities` helpers.
4. **Suffix `DateTime` fields with `Utc`** to indicate timezone.

**CHECK — Flag if:**
- [ ] `ToLower()`/`ToUpper()` is used for comparison instead of a `StringComparer`
- [ ] `String.Equals` without a `StringComparison` parameter
- [ ] `CurrentCulture` comparison is used for MSBuild names
- [ ] File path comparison does not account for OS case sensitivity
- [ ] A `DateTime` field lacks the `Utc` suffix when it stores UTC

---

## 8. API Surface Discipline

**Severity: MAJOR**

See `../../../documentation/wiki/Microsoft.Build.Framework.md`.

**Rules:**
1. **Default to `internal`.** Only make members `public` with strong justification. Every public API is a long-term commitment.
2. **Use interfaces for extensibility points.** Prefer `interface` over `abstract class` for plug-in contracts.
3. **New public API additions must be recorded** in `PublicAPI.Unshipped.txt`.
4. **Never remove public API members.** Add new ones alongside and deprecate old ones with `[Obsolete]`.
5. **Add XML doc comments** to all public API members.

**CHECK — Flag if:**
- [ ] A member is made `public` without justification
- [ ] A new public API is missing from `PublicAPI.Unshipped.txt`
- [ ] A public member has no XML doc comment
- [ ] A public API member has been removed instead of deprecated
- [ ] An `abstract class` is used where an `interface` would suffice for extensibility

---

## 9. MSBuild Target Authoring Conventions

**Severity: MAJOR**

**Rules:**
1. **Follow target ordering conventions.** Use `DependsOnTargets` for required predecessors. Use `BeforeTargets`/`AfterTargets` sparingly.
2. **Use proper MSBuild conditions and properties.** Conditions are evaluated at the point they appear, not deferred. Use `'$(Prop)' == ''` patterns correctly.
3. **Respect SDK import ordering.** SDK `.props` import before user code; SDK `.targets` import after. See `../../../documentation/ProjectReference-Protocol.md`.
4. **Incremental build targets require precise `Inputs` and `Outputs`** declarations.

**CHECK — Flag if:**
- [ ] `BeforeTargets`/`AfterTargets` is used where `DependsOnTargets` would be clearer
- [ ] A target could break the SDK import ordering contract
- [ ] `Inputs`/`Outputs` are missing on a target that should support incremental builds
- [ ] Property/item conditions reference properties not yet defined at that evaluation point

---

## 10. Design Before Implementation

**Severity: MAJOR**

**Rules:**
1. **Discuss design tradeoffs** before implementation for non-trivial features. Probing questions are expected.
2. **Complex features require a written spec** — see `../../../documentation/specs/`.
3. **Make incremental, reviewable commits.** Large monolithic PRs are rejected.
4. **Follow established design patterns.** Do not introduce new patterns without discussion.
5. **Document design decisions in specs.** Include problem statement, goals, non-goals, and trade-off analysis.

**CHECK — Flag if:**
- [ ] A large feature PR has no linked design document or spec
- [ ] A PR introduces a new architectural pattern without discussion
- [ ] A single PR mixes multiple unrelated concerns
- [ ] Design trade-offs are not articulated in the PR description or spec

---

## 11. Cross-Platform Correctness

**Severity: MAJOR**

**Rules:**
1. **File operations must use cross-platform APIs.** No hardcoded backslashes. Use `Path.Combine`, `Path.DirectorySeparatorChar`, or MSBuild's `FileUtilities`.
2. **Handle .NET Framework vs .NET Core differences** explicitly.
3. **Use appropriate file I/O APIs.** Handle UNC paths, long paths (`\\?\`), and symlinks correctly.
4. **Never change output assembly behavior based on build machine OS.**

**CHECK — Flag if:**
- [ ] Hardcoded path separators (`\\` or `/`) instead of `Path.DirectorySeparatorChar`
- [ ] Windows-only APIs used without a cross-platform fallback
- [ ] File path case sensitivity is not considered
- [ ] Code assumes `Environment.NewLine` is `\r\n`
- [ ] `.NET Framework`-only API used without `#if` guard

---

## 12. Code Simplification

**Severity: MODERATE**

**Rules:**
1. **Simplify without losing clarity.** Remove unnecessary conditions, flatten nested logic, collapse redundant branches.
2. **Prefer simpler implementations** when two approaches are equally correct.
3. **Prefer clear, linear control flow** over deep nesting. Use early returns, guard clauses, and `switch` expressions.
4. **Use existing helpers and shared utilities** — don't reinvent `FileUtilities`, `MSBuildNameIgnoreCaseComparer`, or `ResourceUtilities`.
5. **Remove dead code** and unused references proactively.

**CHECK — Flag if:**
- [ ] Code has >3 levels of nesting where guard clauses would flatten it
- [ ] A custom implementation exists for something `FileUtilities` or `Shared/` already provides
- [ ] Dead code or unused variables are present
- [ ] A complex expression could be a simple `is` pattern match or ternary
- [ ] An `if/else` chain could be a `switch` expression

---

## 13. Concurrency & Thread Safety

**Severity: BLOCKING**

See `../../../documentation/wiki/Nodes-Orchestration.md`.

**Rules:**
1. **Shared mutable state must be thread-safe.** Use `ConcurrentDictionary`, `Interlocked`, or explicit locking.
2. **Synchronize access** to any state accessed from multiple threads or nodes.
3. **Handle in-proc vs out-of-proc node differences.** Behavior must be correct in both modes.
4. **Consider concurrency implications in IPC.** Message ordering, reentrancy, and serialization must be correct.

**CHECK — Flag if:**
- [ ] A shared field is read/written without synchronization
- [ ] A `static` mutable field is introduced without thread-safety analysis
- [ ] Code assumes single-threaded execution in a path reachable from parallel builds
- [ ] In-proc and out-of-proc behavior differences are not tested

---

## 14. Naming Precision

**Severity: NIT**

**Rules:**
1. **Use clear, descriptive names.** Avoid abbreviations unless universally understood (e.g., `PRE` for `ProjectRootElement`).
2. **Use precise, consistent naming** throughout a change. Match existing conventions.
3. **Name test methods** to describe scenario and expected outcome: `MethodUnderTest_Scenario_ExpectedResult`.

**CHECK — Flag if:**
- [ ] A variable name is ambiguous (e.g., `data`, `result`, `temp`, `flag`)
- [ ] Naming is inconsistent with adjacent code (e.g., `projectFile` vs existing `projectPath`)
- [ ] A boolean parameter's meaning is unclear without reading docs
- [ ] Test method names don't describe what they test

---

## 15. SDK Integration Boundaries

**Severity: MAJOR**

See `../../../documentation/ProjectReference-Protocol.md`.

**Rules:**
1. **SDK changes must respect MSBuild's evaluation/execution phase separation.**
2. **Property defaults in SDK must not override user-specified values.** Use `Condition="'$(Prop)' == ''"`.
3. **Project reference protocol changes require cross-stack coordination.**
4. **NuGet restore and Build must not run in the same evaluation.**
5. **Evaluation-time decisions in SDK targets have long-term architectural impact.**

**CHECK — Flag if:**
- [ ] An SDK property default overwrites user values (missing `Condition`)
- [ ] Evaluation-time and execution-time concerns are mixed
- [ ] A project reference protocol change is not coordinated with SDK/NuGet teams
- [ ] Restore and Build are conflated in a single evaluation
- [ ] A design-time build contract is violated

---

## 16. Idiomatic C# Patterns

**Severity: NIT**

**Rules:**
1. **Use modern C# features** where the target framework supports them.
2. **Follow MSBuild coding conventions.** Match surrounding code style.
3. **Handle nullability explicitly.** Use nullable annotations, `is null`/`is not null`.
4. **Track .NET version constraints.** Add `// TODO` for unavailable APIs.

**CHECK — Flag if:**
- [ ] `using` block is used where `using` declaration would work and surrounding code uses declarations
- [ ] `== null` is used where `is null` is the convention in surrounding code
- [ ] Nullable warnings are suppressed (`!`) without explanation
- [ ] An older C# pattern is used where a newer one is idiomatic in the codebase

---

## 17. File I/O & Path Handling

**Severity: MAJOR**

**Rules:**
1. **Use `FileUtilities` helpers** for path normalization, comparison, and manipulation.
2. **Handle UNC paths and long paths** (`\\?\` prefix on Windows).
3. **Globbing patterns must handle excludes correctly.** See `../../../documentation/WhenGlobbingReturnsOriginalFilespec.md`.

**CHECK — Flag if:**
- [ ] Custom path normalization logic instead of `FileUtilities`
- [ ] File path comparison using `==` instead of OS-appropriate comparison
- [ ] UNC paths or long paths are not considered
- [ ] Globbing does not account for exclude patterns

---

## 18. Documentation Accuracy

**Severity: MODERATE**

**Rules:**
1. **Include relevant context** in code comments. Explain _why_, not just _what_.
2. **Add XML doc comments** to public and complex internal code.
3. **Use `learn.microsoft.com` URLs**, not legacy `docs.microsoft.com`.
4. **Document non-obvious behavior and design decisions.**

**CHECK — Flag if:**
- [ ] A `docs.microsoft.com` URL is used instead of `learn.microsoft.com`
- [ ] A complex method has no doc comment explaining its purpose
- [ ] A spec lacks problem statement, non-goals, or examples
- [ ] Technical inaccuracies in documentation
- [ ] A design decision is made without documenting the rationale

---

## 19. Build Infrastructure Care

**Severity: MAJOR**

See `../../../documentation/wiki/Bootstrap.md`.

**Rules:**
1. **Dependency versions must be pinned** via Darc/Maestro. Manual `eng/Versions.props` edits require justification.
2. **Verify compatibility with all build entry points:** Arcade CLI, Visual Studio, `dotnet build`, and bootstrap builds.
3. **CI/CD pipeline changes require validation** before merge.
4. **Follow VS servicing and branching conventions.**

**CHECK — Flag if:**
- [ ] `eng/Versions.props` is manually edited without Darc justification
- [ ] A CI YAML change has not been validated
- [ ] Bootstrap build compatibility is not verified
- [ ] A change works in one build entry point but may fail in others

---

## 20. Scope & PR Discipline

**Severity: MODERATE**

**Rules:**
1. **Track follow-up work explicitly.** Create issues for deferred improvements.
2. **Make incremental, reviewable commits.** Don't mix refactoring with behavioral changes.
3. **Cross-reference related issues and PRs** for traceability.
4. **Address reviewer concerns before merging.**

**CHECK — Flag if:**
- [ ] A PR contains unrelated changes (formatting fixes mixed with logic changes)
- [ ] Follow-up work is mentioned but no issue is created
- [ ] The PR lacks references to related issues or specs
- [ ] Reviewer feedback is unresolved at merge time

---

## 21. Evaluation Model Integrity

**Severity: BLOCKING**

See `../../../documentation/High-level-overview.md`.

**Rules:**
1. **Respect the established evaluation order.** Environment variables → global properties → project-level properties (in file order with imports) → item definitions → items.
2. **`Directory.Build.props` imports before SDK props; `Directory.Build.targets` imports after SDK targets.**
3. **Evaluation-time decisions have long-term architectural impact.** Changes to evaluation order are extremely hard to reverse.
4. **Undefined metadata and empty-string metadata must be treated equivalently.**

**CHECK — Flag if:**
- [ ] A change alters property evaluation order
- [ ] `Directory.Build.props`/`.targets` import ordering is violated
- [ ] Evaluation-time side effects are introduced
- [ ] Undefined metadata is treated differently from empty-string metadata

---

## 22. Correctness & Edge Cases

**Severity: MAJOR**

**Rules:**
1. **Verify correctness for edge cases.** Empty collections, null values, concurrent access, very large inputs, Unicode paths.
2. **Verify behavior matches documented/expected semantics.**
3. **Validate fixes against original repro steps.**
4. **Validate inputs early.** Fail fast with clear errors.

**CHECK — Flag if:**
- [ ] A new code path does not handle null/empty inputs
- [ ] A bug fix does not include the original repro as a test case
- [ ] Boundary conditions are not considered (off-by-one, max-length, empty)
- [ ] Input validation is missing at public API boundaries

---

## 23. Dependency Management

**Severity: MODERATE**

**Rules:**
1. **Manage dependencies carefully.** Minimize unnecessary references.
2. **Use Darc/Maestro for dependency updates.** Manual version bumps require justification.
3. **Binding redirect changes have high downstream impact.** Test thoroughly.

**CHECK — Flag if:**
- [ ] A new package reference is added without justification
- [ ] `eng/Versions.props` or `eng/Version.Details.xml` is edited without Darc context
- [ ] Binding redirects are changed without impact analysis

---

## 24. Security Awareness

**Severity: BLOCKING**

**Rules:**
1. **Security-relaxing parameters** must only be applied with explicit user opt-in.
2. **Task type loading must unify to currently-running MSBuild assemblies.**
3. **Consider security implications of file operations.** Path traversal, symlink following, temp file creation.

**CHECK — Flag if:**
- [ ] A security-relaxing flag is applied unconditionally
- [ ] Task assembly loading accepts paths from untrusted sources without validation
- [ ] File operations could be exploited via symlinks or path traversal
- [ ] Credentials or tokens are logged or stored insecurely
