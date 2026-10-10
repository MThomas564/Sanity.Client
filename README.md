# Sanity.Client

.NET SDKs for querying [Sanity.io](https://www.sanity.io) content via GROQ
and supporting the Presentation tool / Visual Editing, with a Blazor
WebAssembly sample app.

## Layout

- `src/Sanity.Client` — the core SDK (NuGet package `Sanity.Client`).
  Targets .NET 8, 9 and 10. Read-only GROQ query client with DI support
  (`AddSanityClient`), API token auth for private datasets, Content Source
  Map / perspective support, and a `SanityDocument` base class /
  `ISanityDocument` interface for strongly typed documents.
- `src/Sanity.Client.Presentation` — companion package
  (`Sanity.Client.Presentation`) adding Blazor support for Sanity's
  **Presentation tool**: stega-encoded click-to-edit overlays via
  `ISanityPresentationService` / `<SanityVisualEditing>`. Ships the
  required JS (Sanity's own stega encoder +
  `@sanity/visual-editing-standalone`) vendored as static web assets --
  no third-party CDN at runtime. See its own README for details and
  what's verified vs. not.
- `tests/Sanity.Client.Tests` — xUnit tests (fake `HttpMessageHandler` for
  HTTP behavior, a real `ServiceProvider` for the DI registration path --
  no network calls).
- `samples/Sanity.Client.BlazorSample` — a Blazor WebAssembly app
  registering both packages via DI (configured from
  `wwwroot/appsettings.json`), with three demo pages: **Movies** (a live
  GROQ query against Sanity's public demo dataset), **Presentation**
  (stega encoding + overlay wiring), and the stock Counter/Home pages.

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

See `src/Sanity.Client/README.md` and
`src/Sanity.Client.Presentation/README.md` for each package's usage docs
(these also ship inside their NuGet packages).

## ⚠️ Security: never put an API token in a Blazor WebAssembly app

`wwwroot/appsettings.json` (and everything else under `wwwroot/`) ships
as a plain static file to every visitor's browser in a WASM app -- there
is no server to keep it private. See the "Never put a Token in a Blazor
WebAssembly app's config" section of `src/Sanity.Client/README.md` before
using `SanityClientOptions.Token` outside a public, read-only dataset.
