# Security Policy

This is an unofficial, community-maintained .NET client for Sanity.io. It
is not affiliated with or supported by Sanity.io -- please don't report
vulnerabilities in Sanity's own platform/API here; see
[Sanity's own security policy](https://www.sanity.io/security) for that.

## Reporting a vulnerability in this SDK

If you find a security issue in `Sanity.Client` or
`Sanity.Client.Presentation` (e.g. something that could leak an API
token, bypass CORS/auth expectations, or execute unexpected code via the
vendored JS in `Sanity.Client.Presentation`), please report it privately
rather than opening a public issue:

1. Use GitHub's [private vulnerability reporting](https://github.com/MThomas564/Sanity.Client/security/advisories/new)
   for this repository ("Security" tab → "Report a vulnerability"), or
2. If that's unavailable, open a regular issue with minimal detail
   ("I have a security concern, please contact me") and wait to be
   contacted before sharing specifics.

Please include:
- The affected package and version.
- Steps to reproduce, or a minimal repro project.
- The potential impact as you understand it.

## Response

This is a part-time, community-maintained project -- there's no SLA, but
security reports will be prioritized over feature work. Expect an initial
response within a few days.

## Supported versions

Only the latest published version of each package is supported. Given
the project is still pre-1.0 (`0.x`), breaking changes (including
security fixes that require an API change) may land in minor versions.

## Scope notes specific to this SDK

- `SanityClientOptions.Token` is a bearer credential. See
  `src/Sanity.Client/README.md`'s "Never put a Token in a Blazor
  WebAssembly app's config" section -- using it in a Blazor WASM app's
  `wwwroot/appsettings.json` ships it to every visitor. That's a usage
  risk this SDK can't prevent at the API level, but we try hard to
  document it clearly; suggestions for making it harder to misuse are
  welcome.
- `Sanity.Client.Presentation` vendors third-party JavaScript (see
  `THIRD-PARTY-NOTICES.md`) rather than loading it from a CDN. If you
  find a vulnerability in the vendored code itself, it's likely also
  present upstream (`@sanity/client`, `@vercel/stega`,
  `@sanity/visual-editing-standalone`) -- please consider reporting it to
  both that project and here.
