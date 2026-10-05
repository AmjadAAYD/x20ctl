"""Make CodeQL findings a release gate rather than only uploading alerts."""
import json
from pathlib import Path
import sys


def check(directory):
    reports = list(Path(directory).rglob("*.sarif"))
    if not reports: raise ValueError("No CodeQL SARIF report produced")
    findings = []
    for report in reports:
        data = json.loads(report.read_text())
        for run in data.get("runs", []):
            findings.extend(result.get("ruleId", "unknown rule") for result in run.get("results", [])
                            if result.get("kind", "fail") == "fail")
    if findings: raise ValueError("CodeQL findings block release: " + ", ".join(findings))
    print("CodeQL produced no findings")


if __name__ == "__main__": check(sys.argv[1])
