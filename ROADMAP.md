# Roadmap

Sanity.Client is a modern, read-first .NET client for [Sanity.io](https://www.sanity.io),
with first-class support for Blazor and Sanity's Presentation tool / Visual Editing.
This roadmap is a statement of intent, not a promise of dates. Priorities follow
real usage, so please open an issue or discussion if something here matters to you.

> Unofficial community project, not affiliated with or endorsed by Sanity.

## Principles

- **Modern .NET only.** Targets .NET 8, 9 and 10.
- **System.Text.Json native.** No Newtonsoft.Json dependency.
- **Small, honest surface.** Ship less, and document exactly what works and what has not been verified.
- **Blazor WebAssembly friendly.** Small dependency footprint, trimming in mind, and loud warnings about never shipping API tokens to the browser.
- **Raw GROQ always works.** Higher-level helpers (LINQ, generators) are optional layers on top, never a requirement.

## Where we are

| Area | Status |
|---|---|
| GROQ query client (`FetchAsync` / `FetchResultAsync`) with DI | Available |
| Perspectives, Content Source Maps, CDN and token handling | Available |
| Strongly typed documents (`SanityDocument`) | Available |
| Automatic GET to POST fallback for long queries, configurable timeout | Available |
| `Sanity.Client.Presentation`: stega encoding, visual-editing overlays, refresh bridge for Blazor | Available (see the package README for what is verified vs not) |
| Unit tests (fake `HttpMessageHandler`, DI registration) | Available |
| Blazor WebAssembly sample | Available |
| LICENSE, third-party notices, `SECURITY.md`, `CONTRIBUTING.md` | Available |

## Milestones

### 0.1: first public preview (`0.1.0-preview.1`)
- [x] LICENSE and third-party notices
- [x] `SECURITY.md`, `CONTRIBUTING.md`
- [ ] CI: build, test and pack on every push and pull request
- [ ] Issue and pull request templates
- [ ] README: supported / not-yet-supported table and a comparison with other .NET clients
- [ ] First NuGet preview publish

### 0.2: hardening
- Retry and error-model improvements (`SanityException` detail, rate limits)
- `JsonTypeInfo<T>` overloads for source-generated JSON (trimming / AOT friendly)
- Opt-in integration tests against a public dataset
- Image URL builder and asset URL helpers

### 0.3: Portable Text
- Portable Text object model
- Blazor renderer component with per-block-type custom renderers
- HTML string renderer for non-Blazor use

### 0.4: `Sanity.Client.Linq` (read-only)
- Expression-tree translation to GROQ: `Where`, `OrderBy` / `ThenBy`, `Skip` / `Take`, `Select`, `First` / `FirstOrDefault`, `Count` / `Any`, string `Contains` / `StartsWith`, reference dereferencing
- All values sent as GROQ parameters, never inlined
- `ToGroq()` for inspecting the generated query
- Golden-file tests mapping expressions to GROQ
- Documented limits: expression trees and reflection are not trimming or AOT friendly, so raw GROQ remains the AOT-safe path

### 0.5: writes
- Mutations (create, createOrReplace, patch, delete) and transactions
- File and image upload
- Prominent guidance: use server-side only, never from Blazor WebAssembly

### 1.0: stability
- API review and freeze
- Integration tests against a live dataset in CI
- Documentation site and more samples
- Migration guide from other .NET Sanity clients

## Exploring

- Live Content API / Listen for real-time updates
- A C# model generator driven by Sanity's extracted schema (similar in spirit to Sanity's TypeGen for TypeScript)
- Caching helpers for ASP.NET Core

## Not planned

- Wrapping or embedding Sanity Studio

## How to help

Try the preview, open issues for anything surprising, and tell us which milestone would unblock you.
Pull requests are welcome; please read [CONTRIBUTING.md](CONTRIBUTING.md) first.
