---
schema_version: 1
open_count: 1
waived_count: 0
fixed_count: 0
total_count: 1
last_updated: 2026-10-25T22:37:20.308Z
---

# Broken Windows Ledger

> Cross-phase defect register. With `workflow.windows_enforce` enabled, `/gsd-ship` blocks while `open_count > 0`.
> Waive with `gsd-tools windows waive <id> "<reason>"` (reason required).
> Mark fixed with `gsd-tools windows fixed <id>`.

| id | phase | kind | file | line | description | status | reason | recorded_at | resolved_at |
|----|-------|------|------|------|-------------|--------|--------|-------------|-------------|
| 1 | 08 | unmet-truth | .planning/phases/08-public-hardening-go-public/08-MEASUREMENTS.md |  | Flood: in-flight refusal point (C0, C20), A2 and the in-flight half of A3 not measured; the stop rule ended every unthrottled step before the in-flight cap could be reached | open |  | 2026-10-25T22:37:20.308Z |  |

````json
[
  {
    "id": 1,
    "kind": "unmet-truth",
    "phase": "08",
    "file": ".planning/phases/08-public-hardening-go-public/08-MEASUREMENTS.md",
    "line": null,
    "description": "Flood: in-flight refusal point (C0, C20), A2 and the in-flight half of A3 not measured; the stop rule ended every unthrottled step before the in-flight cap could be reached",
    "status": "open",
    "reason": "",
    "recorded_at": "2026-10-25T22:37:20.308Z",
    "resolved_at": null
  }
]
````
