"""Compares a mutation run with the one before it (see .github/workflows/mutation.yml).

Reads the JSON reports of both runs, one folder per run with the artifacts in it (mutation-report-website/mutation.json
and mutation-report-config-math/mutation-report.json), and writes a Markdown table of every file's score and how it
moved. When a file has survivors it didn't have, it also writes a report of that for an issue.

A survivor is known by its file, mutator, replacement and the code it replaces, not by its line: lines move when code
above them changes, and a survivor that only moved isn't new. Nor is one that the run before didn't measure: Stryker
gives a mutant a RuntimeError now and then, and the next run may find it surviving although nothing changed. A score
that fell without new survivors (killed mutants of code that was removed) only shows in the table.

python Compare-Mutation.py --current DIR [--previous DIR] [--workspace DIR] --summary FILE --drop FILE
"""

import argparse
import collections
import json
import os
import posixpath
import sys

# (name, report in the artifact folder)
PARTS = [
    ("Share code and preview", "mutation-report-website/mutation.json"),
    ("Config math", "mutation-report-config-math/mutation-report.json"),
]

KILLED = {"Killed", "Timeout"}
NOT_KILLED = {"Survived", "NoCoverage"}

# At most this many new survivors are listed per file, so a large drop still fits in one issue comment
MAX_LISTED = 25


def file_name(path, workspace):
    """The file's path in the repository: Stryker.NET writes absolute paths like /home/runner/work/.../Shared/../Config/X.cs"""
    path = posixpath.normpath(path.replace("\\", "/"))
    if workspace and path.startswith(workspace.rstrip("/") + "/"):
        path = path[len(workspace.rstrip("/")) + 1 :]
    return path


def snippet(source, location):
    """The code a mutant replaces"""
    lines = source.split("\n")
    start, end = location["start"], location["end"]
    if start["line"] == end["line"]:
        return lines[start["line"] - 1][start["column"] - 1 : end["column"] - 1]
    parts = [lines[start["line"] - 1][start["column"] - 1 :]]
    parts += lines[start["line"] : end["line"] - 1]
    parts.append(lines[end["line"] - 1][: end["column"] - 1])
    return "\n".join(parts)


def read_report(path, workspace):
    """{file: (killed, not killed, Counter of survivor keys, {survivor key: first line}, Counter of unmeasured keys)},
    or None without a report; unmeasured mutants are those with any other status (RuntimeError, CompileError, Ignored)"""
    if not os.path.isfile(path):
        return None
    with open(path, encoding="utf-8") as stream:
        report = json.load(stream)
    files = {}
    for path_in_report, entry in report["files"].items():
        killed = not_killed = 0
        survivors = collections.Counter()
        unmeasured = collections.Counter()
        lines = {}
        for mutant in entry["mutants"]:
            status = mutant["status"]
            key = (mutant["mutatorName"], mutant.get("replacement", ""), snippet(entry.get("source", ""), mutant["location"]))
            if status in KILLED:
                killed += 1
            elif status in NOT_KILLED:
                not_killed += 1
                survivors[key] += 1
                lines.setdefault(key, mutant["location"]["start"]["line"])
            else:
                unmeasured[key] += 1
        files[file_name(path_in_report, workspace)] = (killed, not_killed, survivors, lines, unmeasured)
    return files


def score(killed, not_killed):
    total = killed + not_killed
    return 100.0 * killed / total if total else None


def shown(value):
    return "–" if value is None else f"{value:.2f} %"


def code(text, limit=80):
    """text in a Markdown code span, on one line"""
    text = " ".join(text.split())
    if len(text) > limit:
        text = text[: limit - 1] + "…"
    return "`" + text.replace("`", "'") + "`" if text else "(nothing)"


def compare(current_dir, previous_dir, workspace):
    """(summary lines, drop lines): the table for the run's summary, and what fell, empty when nothing did"""
    summary = ["# Mutation scores", ""]
    drop = []
    if previous_dir is None:
        summary += ["No earlier run to compare with: this is the first, or its reports have expired.", ""]
    for name, report in PARTS:
        current = read_report(os.path.join(current_dir, report), workspace)
        previous = read_report(os.path.join(previous_dir, report), workspace) if previous_dir else None
        if current is None:
            summary += [f"## {name}", "", "No report: the job failed or was cut short.", ""]
            continue
        total_now = score(sum(f[0] for f in current.values()), sum(f[1] for f in current.values()))
        total_before = score(sum(f[0] for f in previous.values()), sum(f[1] for f in previous.values())) if previous else None
        summary += [f"## {name}: {shown(total_now)}" + (f" (was {shown(total_before)})" if previous else ""), ""]
        summary += ["| File | Score | Before | Change | Survivors | New |", "|---|---:|---:|---:|---:|---:|"]
        for path in sorted(current):
            killed, not_killed, survivors, lines, _ = current[path]
            now = score(killed, not_killed)
            before_entry = previous.get(path) if previous else None
            before = score(before_entry[0], before_entry[1]) if before_entry else None
            change = now - before if now is not None and before is not None else None
            # A file that wasn't in the earlier run is new code: all its survivors are new. A mutant the earlier run
            # didn't measure isn't, whatever it is now.
            known = before_entry[2] + before_entry[4] if before_entry else collections.Counter()
            new = survivors - known if previous else collections.Counter()
            if now is None:
                continue
            summary.append(
                f"| {path} | {shown(now)} | {shown(before)} | {'' if change is None else f'{change:+.2f}'} | "
                f"{not_killed} | {sum(new.values()) if previous else ''} |"
            )
            if new:
                count_new = sum(new.values())
                drop += [f"### {path}", ""]
                if change is not None:
                    drop += [f"Score {shown(before)} → {shown(now)} ({change:+.2f})", ""]
                drop += [f"{count_new} new survivor{'' if count_new == 1 else 's'}:", ""]
                listed = sorted(new.items(), key=lambda item: lines[item[0]])
                for (mutator, replacement, original), count in listed[:MAX_LISTED]:
                    times = f" ({count}×)" if count > 1 else ""
                    drop.append(f"- line {lines[(mutator, replacement, original)]}, {mutator}: {code(original)} → {code(replacement)}{times}")
                if len(listed) > MAX_LISTED:
                    drop.append(f"- … and {len(listed) - MAX_LISTED} more, see the report")
                drop.append("")
        summary.append("")
    return summary, drop


def main():
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument("--current", required=True)
    parser.add_argument("--previous")
    parser.add_argument("--workspace", default=os.environ.get("GITHUB_WORKSPACE", ""))
    parser.add_argument("--summary", required=True, help="where the table goes, e.g. $GITHUB_STEP_SUMMARY")
    parser.add_argument("--drop", required=True, help="where what fell goes; not written when nothing did")
    args = parser.parse_args()

    previous = args.previous if args.previous and os.path.isdir(args.previous) else None
    summary, drop = compare(args.current, previous, args.workspace)
    with open(args.summary, "a", encoding="utf-8") as stream:
        stream.write("\n".join(summary) + "\n")
    if drop:
        with open(args.drop, "w", encoding="utf-8") as stream:
            stream.write("\n".join(drop) + "\n")
    print("Something fell, see " + args.drop if drop else "Nothing fell")
    return 0


if __name__ == "__main__":
    sys.exit(main())
