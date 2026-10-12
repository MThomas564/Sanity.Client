# Sanity.Client

[![CI](https://github.com/MThomas564/Sanity.Client/actions/workflows/ci.yml/badge.svg)](https://github.com/MThomas564/Sanity.Client/actions/workflows/ci.yml)
[![CodeQL](https://github.com/MThomas564/Sanity.Client/actions/workflows/codeql.yml/badge.svg)](https://github.com/MThomas564/Sanity.Client/actions/workflows/codeql.yml)

A .NET SDK for querying [Sanity.io](https://www.sanity.io) content with
GROQ, with first-class Blazor WebAssembly support and optional Visual
Editing / Presentation tool integration.

> **This is an unofficial, community-maintained project.** It is not
> published, maintained, or endorsed by Sanity.io. For the official
> JavaScript/TypeScript client, see
> [`@sanity/client`](https://github.com/sanity-io/client).

Sanity.Client targets .NET 8, 9 and 10. If you need compatibility with
older runtimes (such as .NET Framework), take a look at
[sanity-linq](https://github.com/oslofjord/sanity-linq).

## Quickstart

```
dotnet add package Sanity.Client
```

```csharp
builder.Services.AddSanityClient(options =>
{
    options.ProjectId = "your-project-id";
    options.Dataset = "production";
});
```

```csharp
public sealed class Post : SanityDocument
{
    public string Title { get; set; } = string.Empty;
}

public sealed class PostsService(ISanityClient sanity)
{
    public Task<Post[]?> GetPostsAsync() =>
        sanity.FetchResultAsync<Post[]>("*[_type == \"post\"]{_id, _type, title}");
}
```

See `src/Sanity.Client/README.md` for the full guide (query parameters,
Content Source Maps, timeouts, strongly-typed documents, the Blazor WASM
token warning) and `src/Sanity.Client.Presentation/README.md` for Visual
Editing / click-to-edit / live preview support.

## Supported / not supported

| | |
|---|---|
| ✅ GROQ queries (`fetch`), with parameters | Full support, including automatic GET→POST fallback for very large queries |
| ✅ Content Source Maps / `perspective` | `SanityFetchOptions.ResultSourceMap`, `SanityFetchOptions.Perspective` |
| ✅ Private datasets (API token auth) | `SanityClientOptions.Token` |
| ✅ Dependency injection | `AddSanityClient(...)`, typed `HttpClient` via `IHttpClientFactory` |
| ✅ Blazor WebAssembly | First-class target; see the security note on tokens below |
| ✅ Presentation tool / Visual Editing | `Sanity.Client.Presentation`: stega-encoded click-to-edit + live preview refresh |
| ✅ LINQ querying (read-only) | `Sanity.Client.Linq`: `Where`/`Select`/`OrderBy`/`Skip`/`Take`/`Count`/`Any`/`First(OrDefault)`/`Single(OrDefault)` translated to GROQ -- see the package README's "Supported LINQ operators" table |
| ❌ Mutations (create/update/delete/patch) | Not implemented -- read-only client |
| ❌ Asset uploads | Not implemented |
| ❌ Listen API / real-time subscriptions (SSE) | Not implemented (the Live Content API used for preview refresh in `Sanity.Client.Presentation` is a different, simpler mechanism) |
| ❌ GROQ-based Studio tooling (schema, structure, etc.) | Out of scope -- this is a frontend data client, not a Studio/CLI replacement |

## Layout

- `src/Sanity.Client` — the core SDK (NuGet package `Sanity.Client`).
  Targets .NET 8, 9 and 10.
- `src/Sanity.Client.Presentation` — companion package adding Blazor
  support for Sanity's Presentation tool (stega-encoded click-to-edit +
  live preview). Ships the required JS vendored as static web assets --
  no third-party CDN at runtime.
- `tests/Sanity.Client.Tests` — xUnit tests (fake `HttpMessageHandler` for
  HTTP behavior, a real `ServiceProvider` for the DI registration path --
  no network calls).
- `samples/Sanity.Client.BlazorSample` — a Blazor WebAssembly app
  registering both packages via DI, with **Movies** (a live GROQ query
  against Sanity's public demo dataset) and **Presentation** (stega
  encoding + overlay wiring) demo pages.
- `LICENSE` / `THIRD-PARTY-NOTICES.md` — this repo's MIT license, and
  attributions/full license texts for the JavaScript vendored by
  `Sanity.Client.Presentation` (one of them, `@vercel/stega`, is
  MPL-2.0, not MIT -- see that file for details).

## Build & test

```
dotnet build
dotnet test tests/Sanity.Client.Tests/Sanity.Client.Tests.csproj
```

## Run the sample

```
dotnet run --project samples/Sanity.Client.BlazorSample
```

This runs on `http://localhost:3000` (see `Properties/launchSettings.json`)
deliberately -- the bundled public demo dataset's CORS allowlist only
permits a handful of origins used in Sanity's own tutorials, and
`localhost:3000` (Next.js's default dev port) happens to be one of them.
**If you point the sample at your own Sanity project**, add your dev
origin to its CORS origins first (Studio → API → CORS origins, or
`npx sanity cors add http://localhost:<port> --credentials false`) --
otherwise browser requests fail with a CORS error even though the same
query works fine from `curl`/server-side code (CORS is enforced by the
browser based on the responding server's allowlist, not by this SDK).

Then open the app and navigate to **Movies** or **Presentation**.

## ⚠️ Security: never put an API token in a Blazor WebAssembly app

`wwwroot/appsettings.json` (and everything else under `wwwroot/`) ships
as a plain static file to every visitor's browser in a WASM app -- there
is no server to keep it private. See the "Never put a Token in a Blazor
WebAssembly app's config" section of `src/Sanity.Client/README.md` before
using `SanityClientOptions.Token` outside a public, read-only dataset.

## Status

Early, `0.1.0-preview.1`. The core query client and its test coverage are
solid; the Presentation package's overlay/live-preview wiring has been
verified against the real vendored Sanity JS and a real headless browser
session, but not yet against an actual Sanity Studio Presentation iframe
in production. See `CONTRIBUTING.md` for how to get involved and
`SECURITY.md` for reporting vulnerabilities.
