"""Fold a CPU profile of MSBuild project evaluation into cost categories.

Reads the JSON produced by::

    dotnet-trace collect --format speedscope -- <evaluation workload>

``dotnet-trace`` emits *evented* profiles (open/close frame pairs), so this
reconstructs the stack over time and attributes each interval to the category of
the innermost frame that matches a rule.

Only time underneath a project evaluation is counted, so process start-up, the
benchmark harness itself, and background threads are excluded.

Usage::

    python fold-evaluation-profile.py <profile.speedscope.json>
"""

import json
import re
import sys
from collections import defaultdict

# Ordered. Searching a stack from the leaf upwards, the first frame that matches a rule decides the category,
# so the more specific rules must come first.
CATEGORY_RULES = [
    ("OS: file attribute query", r"GetFileAttributes|FillAttributeInfo|FileStatus|GetFileInformationByHandle|GetFileTypeCore"),
    ("OS: open/close file handle", r"CreateFile|CloseHandle|ReleaseHandle|OSFileStreamStrategy\.\.ctor|SafeFileHandle"),
    ("OS: read bytes", r"ReadFile|OSFileStreamStrategy\.Read|^System\.IO\.RandomAccess"),
    ("OS: directory enumeration", r"FileSystemEnumerator|FindFirstFile|FindNextFile"),
    ("Path normalization", r"^System\.IO\.PathHelper|^System\.IO\.Path\.|^Microsoft\.Build\.Shared\.FileUtilities"),
    ("Text decoding (StreamReader)", r"^System\.IO\.StreamReader|^System\.Text\.(UTF8Encoding|Encoding|Decoder|ASCIIEncoding|UnicodeEncoding)"),
    ("XML: tokenizing", r"^System\.Xml\.(XmlTextReaderImpl|XmlTextReader)\."),
    ("XML: element location tracking", r"^Microsoft\.Build\.Construction\.(XmlDocumentWithLocation|XmlElementWithLocation|XmlAttributeWithLocation|ElementLocation)"),
    ("XML: DOM construction", r"^System\.Xml\.(XmlDocument|XmlNode|XmlElement|XmlAttribute|XmlLoader|XmlName|XmlNameTable|XmlNamedNodeMap|XmlLinkedNode)"),
    ("SDK resolution", r"Microsoft\.Build\.BackEnd\.SdkResolution|MSBuildSdkResolver|WorkloadSdkResolver|WorkloadManifestReader|Microsoft\.DotNet\.MSBuild"),
    ("Construction model (ProjectParser)", r"^Microsoft\.Build\.Evaluation\.ProjectParser|^Microsoft\.Build\.Construction\.Project\w+Element"),
    ("Import resolution and probing", r"ExpandAndLoadImports|SearchPaths|^Microsoft\.Build\.Internal\.EngineFileUtilities|ProjectRootElementCache"),
    ("Globbing / FileMatcher", r"^Microsoft\.Build\.Shared\.FileMatcher"),
    ("Property expansion (Expander)", r"^Microsoft\.Build\.Evaluation\.Expander"),
    ("Condition evaluation", r"^Microsoft\.Build\.Evaluation\.(ConditionEvaluator|Conditionals)"),
    ("Item evaluation (lazy items)", r"^Microsoft\.Build\.Evaluation\.(LazyItemEvaluator|ItemSpec)|^Microsoft\.Build\.Execution\.ProjectItem"),
    ("Target registration", r"^Microsoft\.Build\.Execution\.(ProjectTargetInstance|ProjectTaskInstance|ProjectOnErrorInstance)|ReadNewTargetElement|ReadTargetElement|AddBeforeAndAfterTargetMappings"),
    ("Task registry (UsingTask)", r"^Microsoft\.Build\.Execution\.TaskRegistry"),
    ("Property storage / collections", r"^Microsoft\.Build\.Collections|^Microsoft\.Build\.Execution\.ProjectPropertyInstance|^Microsoft\.Build\.Evaluation\.ProjectProperty"),
    ("String interning (StringTools)", r"^Microsoft\.NET\.StringTools"),
    ("File system probing (managed)", r"^Microsoft\.Build\.Shared\.FileSystem|^Microsoft\.Build\.FileSystem|^System\.IO\.(File|Directory|FileSystem)\b"),
    ("Environment variables", r"GetEnvironmentVariable"),
    ("Logging", r"^Microsoft\.Build\.BackEnd\.Logging|^Microsoft\.Build\.Framework\..*EventArgs"),
    ("Reflection", r"^System\.Reflection|^System\.RuntimeType|^System\.Signature|^System\.Activator"),
    ("Regex", r"^System\.Text\.RegularExpressions"),
    ("GC", r"GCHeap|gc_heap|WKS::|SVR::|^System\.GC\.|JIT_New|AllocateObject|PollGC"),
    ("JIT", r"!Jit|PrestubWorker|ThePreStub|MethodDesc::|ReadyToRun"),
    ("Other MSBuild", r"^Microsoft\.Build\."),
]

COMPILED_RULES = [(name, re.compile(pattern)) for name, pattern in CATEGORY_RULES]

# Frames that mark the boundary of a project evaluation.
EVALUATION_ROOT = re.compile(
    r"Microsoft\.Build\.Evaluation\.Evaluator.*Evaluate|Microsoft\.Build\.Execution\.ProjectInstance\.Initialize")

# dotnet-trace inserts these synthetic leaves to distinguish running from blocked time.
PSEUDO_FRAMES = {"CPU_TIME", "UNMANAGED_CODE_TIME", "BLOCKED_TIME", "READIED_BY"}


def clean(name):
    """Strip the ``module!`` prefix and the parameter list from a frame name."""
    if "!" in name:
        name = name.split("!", 1)[1]

    return name.split("(", 1)[0]


def categorize(stack_names):
    for name in reversed(stack_names):
        for category, pattern in COMPILED_RULES:
            if pattern.search(name):
                return category

    return "Non-MSBuild / runtime"


def main(path):
    with open(path, "r", encoding="utf-8") as handle:
        document = json.load(handle)

    frames = [clean(frame.get("name", "")) for frame in document["shared"]["frames"]]
    is_evaluation_frame = [bool(EVALUATION_ROOT.search(name)) for name in frames]

    totals = defaultdict(float)
    running_totals = defaultdict(float)
    leaf_totals = defaultdict(float)
    grand_total = 0.0
    evaluation_total = 0.0
    evaluation_cpu = 0.0

    for profile in document["profiles"]:
        if profile.get("type") != "evented":
            continue

        stack = []
        evaluation_depth = 0
        last = profile.get("startValue", 0.0)

        for event in profile["events"]:
            at = event["at"]
            delta = at - last
            last = at

            if delta > 0:
                grand_total += delta

                if evaluation_depth > 0 and stack:
                    evaluation_total += delta
                    names = [frames[index] for index in stack]
                    category = categorize(names)
                    totals[category] += delta

                    if names[-1] == "CPU_TIME":
                        evaluation_cpu += delta
                        running_totals[category] += delta

                    real = [name for name in names if name not in PSEUDO_FRAMES]

                    if real:
                        leaf_totals[real[-1]] += delta

            index = event["frame"]

            if event["type"] == "O":
                stack.append(index)

                if is_evaluation_frame[index]:
                    evaluation_depth += 1
            else:
                if stack and stack[-1] == index:
                    stack.pop()
                elif index in stack:
                    # Tolerate a malformed pair rather than losing the rest of the profile.
                    position = len(stack) - 1 - stack[::-1].index(index)
                    del stack[position:]

                if is_evaluation_frame[index] and evaluation_depth > 0:
                    evaluation_depth -= 1

    if evaluation_total == 0:
        print("No project evaluation frames found in the profile.")
        return 1

    print(f"Total thread time in the trace: {grand_total:.0f} ms")
    print(f"Thread time inside project evaluation: {evaluation_total:.0f} ms "
          f"({evaluation_total * 100 / grand_total:.1f}% of the process)")
    print(f"  running on CPU:                  {evaluation_cpu:.0f} ms "
          f"({evaluation_cpu * 100 / evaluation_total:.1f}%)")
    print(f"  blocked or in unmanaged code:    {evaluation_total - evaluation_cpu:.0f} ms "
          f"({(evaluation_total - evaluation_cpu) * 100 / evaluation_total:.1f}%)")
    print()
    print(f"{'Category':<40}{'total ms':>10}{'share':>8}{'cpu ms':>10}")
    print("-" * 68)

    for category, value in sorted(totals.items(), key=lambda kv: -kv[1]):
        print(f"{category:<40}{value:>10.0f}{value * 100 / evaluation_total:>7.1f}%{running_totals[category]:>10.0f}")

    print()
    print("Hottest individual methods inside evaluation (self time):")
    print()

    for name, value in sorted(leaf_totals.items(), key=lambda kv: -kv[1])[:25]:
        print(f"{value:>8.0f} ms {value * 100 / evaluation_total:>5.1f}%  {name}")

    return 0


if __name__ == "__main__":
    if len(sys.argv) != 2:
        print(__doc__)
        sys.exit(2)

    sys.exit(main(sys.argv[1]))
