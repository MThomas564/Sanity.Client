# Contributing

Thanks for considering a contribution! This is a small, community-run
project, so please keep PRs focused -- it's much easier to review (and
merge) three small PRs than one that touches everything.

## Before you start

For anything beyond a typo fix, please open an issue first describing
what you want to change and why. That avoids wasted effort if the
approach needs discussion, or if something's already in progress.

## Development setup

Requires the .NET SDK (10.0.x; the solution multi-targets net8.0/net9.0/
net10.0, which restore automatically via NuGet reference packs -- you
don't need the 8/9 SDKs installed separately to build or test).

```
git clone https://github.com/MThomas564/Sanity.Client.git
cd Sanity.Client
dotnet build
dotnet test tests/Sanity.Client.Tests/Sanity.Client.Tests.csproj
```

To run the Blazor sample (see the root README for why it binds to port
3000 specifically):

```
dotnet run --project samples/Sanity.Client.BlazorSample
```

## Making changes

- **Core SDK (`src/Sanity.Client`)**: no network calls in tests -- use
  `FakeHttpMessageHandler` in `tests/Sanity.Client.Tests` (it records the
  last request, including body, and supports simulating a slow response
  for timeout tests). Add a test alongside any behavior change.
- **`Sanity.Client.Presentation`**: the vendored JS under
  `wwwroot/vendor/` should never be hand-edited -- see
  `wwwroot/vendor/NOTICE.md` for how it's built/rebuilt from the
  unmodified upstream packages. If you need to change the JS glue layer
  itself, that's `wwwroot/sanity-presentation-interop.js`.
- **Public API changes**: this project is pre-1.0 but already has real
  usage; please avoid gratuitous breaking changes. If a breaking change
  is genuinely necessary, call it out explicitly in the PR description.
- Keep `README.md` / each package's own `README.md` in sync with any
  behavior change that affects how consumers use the library.

## Verifying changes against the real Sanity API

Where practical, verify against the real Sanity API rather than only a
fake handler -- the project's public demo dataset (`ppsg7ml5` / `movies`,
no auth required) is used throughout the sample and is safe to query
freely for this. If you're touching `Sanity.Client.Presentation`'s JS
interop, a real browser check (not just a .NET-side test) is strongly
preferred, since JS interop failures often don't show up any other way.

## Pull requests

- Reference the issue it addresses, if any.
- Describe what you tested and how (unit tests, manual run, against the
  real API, etc.) -- "builds" is not the same as "works".
- Keep commit messages descriptive; this repo doesn't squash-merge by
  default, so individual commits show up in history.

## Code of conduct

Be respectful and assume good faith. Disagreements about technical
approach are fine and expected; personal attacks aren't.
