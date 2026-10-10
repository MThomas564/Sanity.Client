# Releasing

## Versioning: MinVer, not a hand-set property

`Version` is no longer set by hand in `Directory.Build.props`. It's
computed by [MinVer](https://github.com/adamralph/minver) from git tags
at build time:

- Tag the repo `v<semver>` (e.g. `v0.1.0`) for a release -- that tag
  becomes the exact package version.
- Every commit after a tag, until the next tag, gets a version
  automatically: `{major}.{minor}.{patch+1}-preview.0.<height>` after a
  stable tag (e.g. `0.1.1-preview.0.3`), or `<tag>.<height>` appended if
  the last tag was itself already a prerelease (e.g. tagging
  `v0.1.0-preview.1` then committing twice more gives
  `0.1.0-preview.1.2`).
- This needs the full git history (tags included) to work -- every CI
  checkout step uses `fetch-depth: 0` for exactly this reason. A shallow
  clone doesn't error, it just silently computes the wrong (effectively
  `0.0.0`-rooted) version, so don't "optimize" that away.
- `Sanity.Client` and `Sanity.Client.Presentation` share one version
  (both read `Directory.Build.props`), and are always published
  together, core first (Presentation depends on it).

## Tag naming convention

- `v1.2.3` -- a real release (GA or patch). Goes out as a published,
  immutable version.
- `v1.2.3-preview.N` -- an ad-hoc preview of work in progress. Use
  sparingly; each one is a real, permanent, immutable entry once
  published anywhere (especially nuget.org, which is append-only -- see
  below).
- `v1.2.3-rc.N` -- reserved for when you're deliberately cutting a
  release candidate shortly before a GA tag, not for routine builds.
  Don't call every build an RC.

Untagged commits on `main` are never manually versioned -- that's the
whole point of MinVer. You only ever need to think about version numbers
at the moment you push a tag.

## Where packages go

| Trigger | Destination | Why |
|---|---|---|
| Every push to `main` (after CI passes) | GitHub Packages (`nuget.pkg.github.com/MThomas564`) | Cheap, append-only-but-not-public-facing feed for every build -- lets you install a specific commit's build for testing without touching the permanent public record. |
| A `v*` tag, after manual approval | nuget.org | nuget.org is append-only and public: every version pushed there is permanent and visible to every consumer forever. Reserved for things you've decided are worth that permanence. |

This is why there's no "preview on every main push" on nuget.org -- that
would mean a permanent, public version for literally every merge.

## The release pipeline (`.github/workflows/release.yml`)

1. Push a `v*` tag.
2. The `build` job runs once: restore, build (all 3 TFMs), test, pack.
   The resulting `.nupkg`/`.snupkg` files are uploaded as a build
   artifact.
3. The `publish-nuget` job downloads *that exact artifact* (it is never
   rebuilt) and pushes it to nuget.org -- but only after a human
   approves the run, because the job's `environment: nuget-production`
   has a required reviewer configured (Settings -> Environments ->
   nuget-production). Until approved, the run sits in "Waiting" in the
   Actions tab.
4. Core (`Sanity.Client`) is pushed before `Sanity.Client.Presentation`,
   since the latter depends on the former.

## One-time setup this repo needs (not automatable from here)

- **`NUGET_API_KEY` repo secret**: an API key from your nuget.org
  account, scoped to just the `Sanity.Client` and
  `Sanity.Client.Presentation` package IDs (nuget.org -> API Keys ->
  Create -> restrict to "Push" + those two package IDs/glob). Add it via
  Settings -> Secrets and variables -> Actions, or `gh secret set
  NUGET_API_KEY`. Never paste the key itself into chat/a PR/an issue.
- **Trusted Publishing (OIDC)**: nuget.org now supports GitHub Actions
  OIDC trusted publishing, which avoids a long-lived API key entirely.
  If/when your nuget.org account has this enabled for these package
  IDs, swap `release.yml`'s `publish-nuget` job to use it instead:
  add `permissions: id-token: write` to that job, and replace the
  `--api-key "${{ secrets.NUGET_API_KEY }}"` push with nuget.org's
  documented trusted-publishing login step (check nuget.org's current
  docs for the exact action/step, since this is a newer feature its
  recommended wiring is still evolving). Until that's configured on the
  nuget.org side, the API-key path above is what's actually implemented.
- **`nuget-production` environment + required reviewer**: created via
  `gh api` as part of setting this pipeline up, with you as the required
  reviewer. If you ever add collaborators who shouldn't be able to
  single-handedly publish to nuget.org, add them as required reviewers
  too (Settings -> Environments -> nuget-production -> Required
  reviewers).

## Doing an actual release

```
git tag -a v1.2.3 -m "Release 1.2.3"
git push origin v1.2.3
```

Then go to the Actions tab, find the `Release` run for that tag, and
approve the `publish-nuget` job when you're ready for it to actually go
out. Nothing reaches nuget.org before you click approve.
