# API Coverage — GitHub REST API (with Sigstore through `gh attestation`)

> Full coverage by default. Opt-outs are explicit, reasoned decisions.

Scope: the capabilities of the GitHub platform API that bear on this phase's need, which is distributing attested releases to a pull-based server and governing the public repository. The BGG API is not integrated in this phase.

| capability | decision | reason |
|---|---|---|
| releases: get latest published release (unauthenticated poll) | INTEGRATE | |
| releases: get release by tag (published-release audit, immutable flag) | INTEGRATE | |
| releases: create draft, upload assets, publish (release workflow via gh) | INTEGRATE | |
| releases: edit draft notes before approval | INTEGRATE | |
| releases: list all releases | OPT-OUT | not needed: the poller compares the latest release against active and rejected versions with a strict greater-than |
| release assets: unauthenticated public download | INTEGRATE | |
| commits: compare (attested commit is on main) | INTEGRATE | |
| attestations: verify a shipped Sigstore bundle with gh attestation verify | INTEGRATE | |
| attestations: fetch attestations by digest from the API | OPT-OUT | explicitly out of scope: the bundle ships with the release so the server needs no GitHub credential and no authenticated API call |
| rate-limit headers: log x-ratelimit-remaining, quiet 403/429 handling | INTEGRATE | |
| conditional requests (ETag / If-None-Match) | OPT-OUT | not needed: a 304 is exempt from the rate limit only for authorised requests and the server is unauthenticated by design |
| authenticated API calls from the server | OPT-OUT | explicitly out of scope: no GitHub credential may exist on the server |
| repository rulesets: create, update, read (main and v* tags) | INTEGRATE | |
| classic branch protection | OPT-OUT | not needed: rulesets replace it and are what the read-back script verifies |
| repository merge settings (merge and squash on, rebase off) | INTEGRATE | |
| environments: create deploy, required reviewer, tag deployment policy, read back | INTEGRATE | |
| pending deployments: approve or reject through the API | OPT-OUT | explicitly out of scope: approval is the owner's own action and is never automated |
| actions permissions: fork PR approval, default token read-only, SHA pinning | INTEGRATE | |
| security: secret scanning, push protection, Dependabot security updates | INTEGRATE | |
| vulnerability alerts | INTEGRATE | |
| immutable releases setting | INTEGRATE | |
| self-hosted runners: list (must be zero) | INTEGRATE | |
| self-hosted runners: register | OPT-OUT | explicitly out of scope: no runner may exist; GitHub never executes anything on the server |
| check runs: read names and app id for required checks | INTEGRATE | |
| pull requests: create, read checks, merge (merge or squash) | INTEGRATE | |
| webhooks: inbound release or push events | OPT-OUT | explicitly out of scope: pull-based deploy, no inbound connection from GitHub |
| deployments and deployment statuses reported back from the server | OPT-OUT | not needed yet: deploy outcomes are visible in the journal and loopback health; failure notification is a later requirement |
| packages and container registry | OPT-OUT | not needed: releases ship as attested zip assets |
