import json
import sys

path = sys.argv[1] if len(sys.argv) > 1 else "-"
with open(path) if path != "-" else sys.stdin as f:
    data = json.load(f)

for run in data.get("runs", []):
    tool = run.get("tool", {}).get("driver", {}).get("name", "?")
    for r in run.get("results", []):
        msg = r.get("message", {}).get("text", "")
        rule = r.get("ruleId", "")
        if "redirect" not in msg.lower() and "redirect" not in rule.lower():
            continue
        locs = r.get("locations", [])
        if not locs:
            continue
        pl = locs[0].get("physicalLocation", {})
        uri = pl.get("artifactLocation", {}).get("uri", "")
        line = pl.get("region", {}).get("startLine", 0)
        print(f"{tool}|{rule}|{msg}|{uri}|{line}")
