"""
Script 1: Parse PerfView ETW CSV and compute task host overhead.

Uses DURATION_MSEC from Stop events (auto-calculated by ETW Start/Stop pattern).
Aggregates dispatch vs execute durations per task type, validates count parity,
and flags mismatches.

Input:  PerfViewData.csv (from PerfView export)
Output: task_host_summary.csv (per-task-type metrics)
"""

import pandas as pd

INPUT_CSV = r"D:\perf-view\PerfViewData.csv"
OUTPUT_CSV = r"D:\perf-view\task_host_summary.csv"


def main():
    df = pd.read_csv(INPUT_CSV, encoding="utf-8-sig")
    print(f"Total events: {len(df)}")

    # Extract fields from the Rest column
    df["task_name"] = df["Rest"].str.extract(r'taskName="([^"]*)"')
    duration_str = df["Rest"].str.extract(r'DURATION_MSEC="([^"]*)"')[0]
    df["duration_ms"] = pd.to_numeric(duration_str.str.replace(",", ""), errors="coerce")

    # Only need Stop events — they have DURATION_MSEC
    dispatch_stops = df[df["Event Name"] == "Microsoft-Build/TaskHostDispatch/Stop"].copy()
    execute_stops = df[df["Event Name"] == "Microsoft-Build/TaskExecuteInHost/Stop"].copy()

    # Validate: drop rows with missing task_name or duration
    dispatch_stops = dispatch_stops.dropna(subset=["task_name", "duration_ms"])
    execute_stops = execute_stops.dropna(subset=["task_name", "duration_ms"])

    print(f"Dispatch stops: {len(dispatch_stops)}, Execute stops: {len(execute_stops)}")

    # Aggregate dispatch durations by task name
    dispatch_by_task = (
        dispatch_stops.groupby("task_name")["duration_ms"]
        .agg(dispatch_count="size", total_dispatch_ms="sum")
        .reset_index()
    )

    # Aggregate execute durations by task name
    execute_by_task = (
        execute_stops.groupby("task_name")["duration_ms"]
        .agg(execute_count="size", total_execute_ms="sum")
        .reset_index()
    )

    # Outer join to detect mismatches in both directions
    summary = dispatch_by_task.merge(execute_by_task, on="task_name", how="outer")
    summary["dispatch_count"] = summary["dispatch_count"].fillna(0).astype(int)
    summary["execute_count"] = summary["execute_count"].fillna(0).astype(int)
    summary["total_dispatch_ms"] = summary["total_dispatch_ms"].fillna(0)
    summary["total_execute_ms"] = summary["total_execute_ms"].fillna(0)

    # Flag count mismatches
    mismatched = summary[summary["dispatch_count"] != summary["execute_count"]]
    if len(mismatched) > 0:
        print(f"\nWARNING: {len(mismatched)} task type(s) have dispatch/execute count mismatch:")
        for _, row in mismatched.iterrows():
            print(f"  {row['task_name']}: {row['dispatch_count']} dispatches, {row['execute_count']} executes")
        print("Overhead numbers for these tasks may be inaccurate.\n")

    # Compute averages using each event type's own count (not cross-dividing)
    summary["avg_dispatch_ms"] = (
        summary["total_dispatch_ms"] / summary["dispatch_count"]
    ).where(summary["dispatch_count"] > 0, 0)
    summary["avg_execute_ms"] = (
        summary["total_execute_ms"] / summary["execute_count"]
    ).where(summary["execute_count"] > 0, 0)

    # Compute overhead from totals
    summary["total_overhead_ms"] = summary["total_dispatch_ms"] - summary["total_execute_ms"]
    summary["avg_overhead_ms"] = (
        summary["total_overhead_ms"] / summary["dispatch_count"]
    ).where(summary["dispatch_count"] > 0, 0)
    summary["overhead_pct"] = (
        summary["total_overhead_ms"] / summary["total_dispatch_ms"] * 100
    ).where(summary["total_dispatch_ms"] > 0, 0).round(2)

    # Use dispatch_count as the primary count
    summary = summary.rename(columns={"dispatch_count": "dispatch_count"})

    # Round all float columns
    float_cols = ["total_dispatch_ms", "total_execute_ms", "total_overhead_ms",
                  "avg_dispatch_ms", "avg_execute_ms", "avg_overhead_ms"]
    summary[float_cols] = summary[float_cols].round(3)

    # Select and order columns for output
    output_cols = ["task_name", "dispatch_count", "execute_count",
                   "total_dispatch_ms", "total_execute_ms", "total_overhead_ms",
                   "avg_dispatch_ms", "avg_execute_ms", "avg_overhead_ms", "overhead_pct"]
    summary = summary[output_cols].sort_values("total_overhead_ms", ascending=False)
    summary.to_csv(OUTPUT_CSV, index=False)
    print(f"Written: {OUTPUT_CSV}")

    # Print totals
    total_dispatch = summary["total_dispatch_ms"].sum()
    total_exec = summary["total_execute_ms"].sum()
    total_overhead = summary["total_overhead_ms"].sum()
    overhead_pct = total_overhead / total_dispatch * 100 if total_dispatch > 0 else 0

    print(f"""
{'=' * 60}
TOTALS
{'=' * 60}
Total Dispatch time:      {total_dispatch:>10.1f} ms
Total Execute time:       {total_exec:>10.1f} ms
Total Overhead:           {total_overhead:>10.1f} ms
Overhead %:               {overhead_pct:>10.1f} %
Task invocations:         {summary['dispatch_count'].sum():>10}
Unique task types:        {len(summary):>10}
""")

    # Print top 10
    print("TOP 10 TASKS BY TOTAL OVERHEAD")
    print("-" * 95)
    print(f"{'Task Name':<60} {'Count':>6} {'Overhead ms':>12} {'Overhead %':>10}")
    print("-" * 95)
    for _, row in summary.head(10).iterrows():
        count_note = f"{row['dispatch_count']}"
        if row['dispatch_count'] != row['execute_count']:
            count_note += f"/{row['execute_count']}"
        print(f"{row['task_name']:<60} {count_note:>6} {row['total_overhead_ms']:>12.1f} {row['overhead_pct']:>9.1f}%")


if __name__ == "__main__":
    main()
