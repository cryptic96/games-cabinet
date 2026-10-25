# Proposed caps for the container and the service

Draft for the owner. Built on the measurements in `08-MEASUREMENTS.md` ("Sync and picture rebuild"). Numbers only. The
proposal is tried at run time first and only written into the unit file after that.

Status: the measurements are complete and valid. The picture rebuild's owner-data comparison stopped on a difference that
a person still has to decide on (see the measurements); that does not change the numbers below.

## What was measured

| Load | CPU used | Highest one-second CPU share (one core) | Memory peak of the service |
|---|---|---|---|
| Idle since start (context) | 28.16 s over about 1 h 40 min | not sampled | 99.1 MiB |
| Start-up only | 0.72 s | 0.37 | 48.6 MiB |
| Normal sync (about 7 s) | 1.08 s | 0.37 | 56.2 MiB |
| Rebuild run one (102 s) | 13.62 s | 0.41 | 284.4 MiB |
| Rebuild run two (37 s) | 4.31 s | 0.32 | 314.1 MiB (whole-life peak) |

No run touched the high or max memory line, no swap was used, memory pressure stayed at 0.00, CPU pressure peaked at
9.7 (10 s average), and the CPU quota throttled 1 % of the time at most (1.14 s in 102 s).

## Proposal per cap

| Cap | Today | Proposed | Basis |
|---|---|---|---|
| Service `MemoryMax` | 600 MiB | keep 600 MiB | Measured peak 314 MiB plus one worst-case 36-megapixel decode (about 144 MiB) is 458 MiB; 600 MiB leaves about 140 MiB (30 %) of margin on top of that. |
| Service `MemoryHigh` | 450 MiB | raise to 480 MiB | The worst case above (458 MiB) sits just over today's 450 MiB, so the worst picture would be slowed by reclaim; 480 MiB lets it pass, and it stays 120 MiB under the max line so the kernel still slows the service well before it kills it. |
| Service swap | 0 | keep 0 | No swap was used; a service that cannot swap fails fast instead of thrashing the shared host. |
| Service `CPUQuota` | 80 % of one core | keep 80 % | The busiest second used 41 % of a core and the rebuild was throttled for about 1 % of its time; 20 % of the core stays free for SSH and the deploy timer, which is the stated minimum of a fifth. |
| Service `CPUWeight` | 50 | keep 50 | Already below a default weight of 100; it only matters when the core is contended. |
| Service `TasksMax` | 512 | keep 512 | Not measured as a constraint; the unit is far below it. |
| Container memory | 1 GiB | keep 1 GiB | Service max 600 MiB plus SSH, journald, the deploy timer and page cache fits in 1 GiB with about 400 MiB for everything else; the service's whole-life peak was 314 MiB, so in practice the margin is larger. |
| Container swap | 512 MiB | keep 512 MiB | Small relative to memory and unused in every run; it is a safety net for the rest of the container, not for the service. |
| Container cores | 1 | keep 1 | CPU pressure never passed 9.7; the highest second used 41 % of the one core. A second core would not change any measured peak. |
| Container CPU weight | not read | owner reads the reverse proxy's weight with `pct config` on the host and sets this container's weight below it | A lower weight only matters when the host's cores are contended; it keeps the public site from ever starving the reverse proxy. Needs the proxy's number, which only the host shows. |

## One finding worth a decision

The runtime reports 15.6 GB of memory and one processor at start, not the service's 600 MiB, so its garbage collector does
not size itself against the limit. That is why the whole-life peak grew to 314 MiB although the second rebuild run fetched
fewer pictures than the first. The caps above bound the service correctly regardless. If a lower resident size is wanted
later, a heap limit for the runtime is the lever; it was not measured and is not proposed here.

## Order of use

The values are tried at run time first (`systemctl set-property --runtime`), then written into the unit file in the
release plan, so a wrong number never survives a restart.
