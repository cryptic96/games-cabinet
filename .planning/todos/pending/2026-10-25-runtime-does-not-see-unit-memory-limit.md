---
created: 2026-10-25T20:00:00.000Z
title: Make the .NET runtime see the unit's memory limit
area: operations
severity: minor
resolves_phase: 8
files:
  - deploy/systemd/cabinet.service
  - Cabinet.Service/Hosting/HostLimits.cs
---

## Problem

After provisioning the measurement release with `MemoryHigh`/`MemoryMax` on `cabinet.service`, the start-up report logs "The runtime sees 15594 MB of memory and 1 processors; server GC: False" (plan 08-12). The processor count follows the CPU quota, but the memory figure (`GC.GetGCMemoryInfo().TotalAvailableMemoryBytes`) is neither the unit's 600 MiB limit (the service cgroup's `memory.max` is 629145600 and readable from inside the process) nor the container's 1 GiB. The kernel still enforces `MemoryHigh`/`MemoryMax`, but the GC does not size its heap budget to them, so it can run into `MemoryHigh` throttling or the `MemoryMax` OOM kill instead of collecting earlier.

## Solution

When the final unit numbers are written (plan 08-17), set an explicit absolute heap limit in the unit, `Environment=DOTNET_GCHeapHardLimit=<hex bytes>` sized below `MemoryHigh` (a percentage limit would be a percentage of the wrong 15 GB figure), and have the selfcheck or the start-up report confirm the runtime's figure matches. Optionally find out why cgroup v2 detection misses the service cgroup inside the LXC under the unit's sandboxing directives. Verify on the container after provisioning: the start-up line reports about the configured limit.
