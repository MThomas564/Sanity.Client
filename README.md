# Sanity.Client

A .NET SDK for querying [Sanity.io](https://www.sanity.io) content via GROQ,
with a Blazor WebAssembly sample app.

## Layout

- `src/Sanity.Client` — the SDK (NuGet package `Sanity.Client`). Targets
  .NET 8, 9 and 10. Read-only GROQ query client with DI support
  (`AddSanityClient`), API token auth for private datasets, and a
  `SanityDocument` base class / `ISanityDocument` interface for strongly
  typed documents.
- `tests/Sanity.Client.Tests` — xUnit tests (fake `HttpMessageHandler`, no
  network calls).
- `samples/Sanity.Client.BlazorSample` — a Blazor WebAssembly app that
  registers `ISanityClient` via DI (configured from
  `wwwroot/appsettings.json`) and renders a live GROQ query against
  Sanity's public demo "movies" dataset on the `/movies` page.

## Build & test

```
dotnet build
dotnet test tests/Sanity.Client.Tests/Sanity.Client.Tests.csproj
```

## Run the sample

```
dotnet run --project samples/Sanity.Client.BlazorSample
```

Then open the printed localhost URL and navigate to **Movies**.

See `src/Sanity.Client/README.md` for the SDK's own usage docs (this is
also what ships inside the NuGet package).
