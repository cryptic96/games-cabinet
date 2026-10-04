# GitHub repository settings the pull request gate and release pipeline rely on

The workflows assume a set of repository-side controls exist. None of them
can be set from inside the repository itself: they live in GitHub's own
settings. Every control below is applied with `gh api` and read back with
`gh api`, and `build/check-github-settings.sh` reads all of them back in one
run and prints one `PASS` or `FAIL` line per control. The check only ever
issues read requests, so it is safe to run at any time.

The public repository is `cryptic96/games-cabinet`. The commands use the
placeholders `{owner}` and `{repo}`; `gh` fills them in from the current
checkout's remote, or substitute the real values. To run the read-back check
against another slug, set `CABINET_GITHUB_REPO=<owner>/<repo>`.

The full list of apply commands should be reviewed once by the repository
owner before any of it is run. Nothing in this guide is applied
automatically.

## Order of application

The order matters: several controls cannot be chosen, or are created in the
wrong shape, if they are applied too early or too late.

1. **Account prerequisites.** Resolve the account-level commit metadata
   settings (last section) before the first pull request is merged through
   the web interface.
2. **Create the repository empty.** Public, with no generated README,
   licence or ignore file, so the local history and the remote history stay
   related. Add it as a remote.
3. **Push `main`** (its initial commit only), then apply the main branch
   ruleset **without** the required status checks.
4. **Push the working branch and open a pull request** so the `ci` workflow
   runs once. A required check can only be chosen after GitHub has seen it
   run.
5. **Read the check names** from that run and confirm they are exactly
   `build-test` and `lint` with the GitHub Actions app as the source, then
   update the main ruleset to require them.
6. **Apply every other control and read each one back**, in particular the
   `deploy` environment with its reviewer and tag policy. A workflow that
   names an environment that does not exist creates it with no protection, so
   the first tag would publish without approval. The environment must exist
   and be read back before the first tag is pushed.
7. **Enable immutable releases** before the first release is published.
8. **Merge the pull request through the web interface**, then check the
   author and committer identities on the merge commit
   (`git log --format='%an %ae %cn %ce' origin/main`) before tagging.
   An immutable release pins whatever commit is tagged.
9. **Run `build/check-github-settings.sh`**, expect every line to say `PASS`,
   and only then tag the merge commit on `main`.

## Main branch ruleset

**UI path:** Settings, Rules, Rulesets, New ruleset, New branch ruleset.

Why: `main` only changes through a pull request that has passed both CI
checks. The ruleset requires a pull request with zero approvals (a single
maintainer has nobody else to approve), forbids deleting the branch and
forbids force-pushes, has no bypass actors, and allows only merge commits
and squash merges. The merge method is chosen per pull request: a merge
commit for long-running branch pull requests, a squash for small ones.

Write the ruleset body to a file first, without the status checks:

```bash
cat > main-ruleset.json <<'JSON'
{
  "name": "Protect Main Branch",
  "target": "branch",
  "enforcement": "active",
  "bypass_actors": [],
  "conditions": { "ref_name": { "include": ["~DEFAULT_BRANCH"], "exclude": [] } },
  "rules": [
    { "type": "deletion" },
    { "type": "non_fast_forward" },
    {
      "type": "pull_request",
      "parameters": {
        "required_approving_review_count": 0,
        "dismiss_stale_reviews_on_push": true,
        "require_code_owner_review": false,
        "require_last_push_approval": false,
        "required_review_thread_resolution": false,
        "allowed_merge_methods": ["merge", "squash"]
      }
    }
  ]
}
JSON
```

Apply (first, without the checks):

```bash
gh api --method POST repos/{owner}/{repo}/rulesets --input main-ruleset.json
```

Read the check names and their source app from the pull request's head
commit once `ci` has run:

```bash
gh api repos/{owner}/{repo}/commits/<head-sha>/check-runs \
  --jq '.check_runs[] | {name, app_id: .app.id}'
```

The names must be `build-test` and `lint`, and the GitHub Actions app id is
`15368`. Then add the required checks and update the ruleset:

```bash
ruleset_id="$(gh api repos/{owner}/{repo}/rulesets --jq '.[] | select(.name == "Protect Main Branch") | .id')"

jq '.rules += [{
  "type": "required_status_checks",
  "parameters": {
    "strict_required_status_checks_policy": false,
    "required_status_checks": [
      { "context": "build-test", "integration_id": 15368 },
      { "context": "lint", "integration_id": 15368 }
    ]
  }
}]' main-ruleset.json > main-ruleset-with-checks.json

gh api --method PUT "repos/{owner}/{repo}/rulesets/$ruleset_id" --input main-ruleset-with-checks.json
```

Read back:

```bash
gh api repos/{owner}/{repo}/rulesets --jq '.[] | select(.target == "branch")'
gh api "repos/{owner}/{repo}/rulesets/$ruleset_id"
```

`build/check-github-settings.sh` fails if the ruleset is missing or not
active, has a bypass actor, lacks the deletion or force-push rule, requires a
non-zero number of approvals, allows any merge method other than merge and
squash, or requires any set of checks other than exactly `build-test` and
`lint`. The workflow file `ci.yml` has no path filters, so a documentation
only pull request still reports both checks and can never be left waiting on
a check that was skipped.

Two events (a push and a pull request) can report the same check names for
one head commit. Merging stays blocked until the latest run of each required
check has succeeded; confirm this by watching the first pull request.

## Repository merge settings

**UI path:** Settings, General, Pull Requests.

Why: the ruleset limits the methods per pull request, and the repository
settings keep the merge button itself honest. Merge commits and squash merges
are allowed; rebase merges are disabled, because a rebase rewrites commit
identities and breaks the link between a pull request and its commits.

Apply:

```bash
gh api --method PATCH repos/{owner}/{repo} \
  -F allow_merge_commit=true \
  -F allow_squash_merge=true \
  -F allow_rebase_merge=false
```

Read back:

```bash
gh api repos/{owner}/{repo} --jq '{allow_merge_commit, allow_squash_merge, allow_rebase_merge}'
```

## Tag ruleset restricting tag creation, update and deletion

**UI path:** Settings, Rules, Rulesets, New ruleset, New tag ruleset.

Why: only the repository's admin role may create, move or delete a release
tag. That is what stops a tag from pointing anywhere the release workflow
did not expect.

Apply:

```bash
gh api --method POST repos/{owner}/{repo}/rulesets --input - <<'EOF'
{
  "name": "release tags",
  "target": "tag",
  "enforcement": "active",
  "bypass_actors": [
    { "actor_type": "RepositoryRole", "actor_id": 5, "bypass_mode": "always" }
  ],
  "conditions": { "ref_name": { "include": ["refs/tags/v*"], "exclude": [] } },
  "rules": [ { "type": "creation" }, { "type": "update" }, { "type": "deletion" } ]
}
EOF
```

Actor id 5 is the Admin repository role, the only bypass actor. The body is
sent as JSON because the API requires the `exclude` list, which the `-f` field
syntax cannot express as an empty array.

Read back:

```bash
gh api repos/{owner}/{repo}/rulesets --jq '.[] | select(.target == "tag")'
```

## Deploy environment with a required reviewer and a tag-only deployment policy

**UI path:** Settings, Environments, New environment (name it `deploy`).

Why: the `publish` job in the release workflow targets the `deploy`
environment, so nothing in that job runs until the reviewer approves it, and
the environment only accepts deployments from a tag matching a release,
never from a branch. Create it and read it back before the first tag.

`prevent_self_review` is deliberately `false`: with a single maintainer there
is nobody else to approve, so self-review must stay allowed or every release
would wait forever. No workflow step, bot or agent approves on the owner's
behalf; the owner approves in the Actions interface.

Apply (look up the numeric user id with `gh api users/<username> --jq .id`):

```bash
gh api --method PUT repos/{owner}/{repo}/environments/deploy \
  -F 'reviewers[][type]=User' \
  -F 'reviewers[][id]=<owner-user-id>' \
  -F 'prevent_self_review=false' \
  -F 'deployment_branch_policy[protected_branches]=false' \
  -F 'deployment_branch_policy[custom_branch_policies]=true'

gh api --method POST repos/{owner}/{repo}/environments/deploy/deployment-branch-policies \
  -f name='v*.*.*' \
  -f type='tag'
```

Read back:

```bash
gh api repos/{owner}/{repo}/environments/deploy
gh api repos/{owner}/{repo}/environments/deploy/deployment-branch-policies
```

## Approval required for all outside contributors' workflow runs

**UI path:** Settings, Actions, General, Fork pull request workflows.

Why: the default only asks for approval from first-time contributors. The
repository is public, so every workflow run triggered by an account without
write access should wait for an explicit approval, every time.

Apply:

```bash
gh api --method PUT repos/{owner}/{repo}/actions/permissions/fork-pr-contributor-approval \
  -f approval_policy='all_external_contributors'
```

Read back:

```bash
gh api repos/{owner}/{repo}/actions/permissions/fork-pr-contributor-approval
```

## Read-only default token permissions, no pull request approvals

**UI path:** Settings, Actions, General, Workflow permissions.

Why: every workflow's token should start read-only unless a job asks for
more, as the release workflow's jobs do, scoped to exactly what each needs.
Workflows must also never approve a pull request.

Apply:

```bash
gh api --method PUT repos/{owner}/{repo}/actions/permissions/workflow \
  -f default_workflow_permissions='read' \
  -F can_approve_pull_request_reviews=false
```

Read back:

```bash
gh api repos/{owner}/{repo}/actions/permissions/workflow
```

## Actions must be pinned to a full-length commit SHA

**UI path:** Settings, Actions, General, "Require actions to be pinned to a
full-length commit SHA".

Why: this is the repository-side backstop behind the lint suite's own
hash-pin policy. Even a change that slipped past the local checks cannot use
a mutable-tag action reference.

Apply (read the current settings first so `allowed_actions` is not reset by
accident):

```bash
gh api repos/{owner}/{repo}/actions/permissions

gh api --method PUT repos/{owner}/{repo}/actions/permissions \
  -F enabled=true \
  -f allowed_actions='all' \
  -F sha_pinning_required=true
```

Read back:

```bash
gh api repos/{owner}/{repo}/actions/permissions
```

## Secret scanning, push protection and Dependabot security updates

**UI path:** Settings, Code security.

Why: GitHub's own scanners are a second line behind the repository's local
secret and personal-data checks.

Apply:

```bash
gh api --method PATCH repos/{owner}/{repo} \
  -F 'security_and_analysis[secret_scanning][status]=enabled' \
  -F 'security_and_analysis[secret_scanning_push_protection][status]=enabled' \
  -F 'security_and_analysis[dependabot_security_updates][status]=enabled'
```

Read back:

```bash
gh api repos/{owner}/{repo} --jq .security_and_analysis
```

## Dependabot alerts

**UI path:** Settings, Code security, Dependabot alerts.

Why: this is the alert feed itself (a vulnerable dependency is reported),
distinct from the security updates above (an automatic pull request that
fixes it).

Apply:

```bash
gh api --method PUT repos/{owner}/{repo}/vulnerability-alerts
```

Read back (a `204` response means enabled, a `404` means not):

```bash
gh api repos/{owner}/{repo}/vulnerability-alerts
```

## Immutable releases

**UI path:** Settings, General, "Enable immutable releases".

Why: once a release is published, its tag, assets and attestation can no
longer be changed or deleted, only superseded by a newer release. That is
what makes verifying a release later trustworthy. Enable it before the first
release is published, because it cannot be applied retroactively to a release
that is already public. It also means release notes must be final before the
release is approved; see `docs/releasing.md`.

Apply:

```bash
gh api --method PUT repos/{owner}/{repo}/immutable-releases
```

Read back:

```bash
gh api repos/{owner}/{repo}/immutable-releases
```

## No registered self-hosted runners

**UI path:** Settings, Actions, Runners.

Why: every job in every workflow targets a GitHub-hosted runner, and no
GitHub-executed code runs on the server. There is nothing to switch off; this
is a standing fact to re-check, because a self-hosted runner registered later,
even by accident, would reintroduce exactly the risk the pipeline avoids.

Read back (always an empty list):

```bash
gh api repos/{owner}/{repo}/actions/runners --jq '.runners'
```

## Account-level commit metadata

**UI path:** account Settings, Public profile and Emails.

Every commit, merge commit and tag in a public repository shows its author
metadata to anyone who clones it, and a merge made through the web interface
takes its identity from the account profile. Before the first merge:

- set the profile display name to the account handle, or clear it;
- turn on "Keep my email addresses private";
- turn on "Block command line pushes that expose my email";
- use the resulting noreply address as the local `git config user.email`.

After the first merge, verify what landed:

```bash
git log --format='%an %ae %cn %ce' origin/main
```

Every line should show only the handle and a noreply address. Do this before
tagging: an immutable release pins the tagged commit forever.

## Reading everything back

```bash
build/check-github-settings.sh
```

This prints one line per control (the main ruleset, the repository merge
settings, the tag ruleset, the `deploy` environment reviewer and tag policy,
outside contributor approval, the default token permissions, SHA pinning,
secret scanning and push protection, Dependabot security updates,
Dependabot alerts, immutable releases and the runner list) and exits with a
non-zero status if any of them is wrong. `build/tests/check-github-settings-test.sh`
exercises the checker offline against synthetic responses.
