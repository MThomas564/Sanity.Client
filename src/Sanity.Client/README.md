# Sanity.Client

A lightweight .NET client SDK for querying [Sanity.io](https://www.sanity.io)
content via GROQ. Works anywhere .NET runs, including Blazor WebAssembly.
Targets .NET 8, 9 and 10.

## Install

```
dotnet add package Sanity.Client
```

## Register (ASP.NET Core / Blazor, via DI)

```csharp
builder.Services.AddSanityClient(options =>
{
    options.ProjectId = "your-project-id";
    options.Dataset = "production";
    options.ApiVersion = "v2024-01-01";
    // options.Token = "sanity-api-token"; // only needed for private datasets
});
```

Or bind from configuration (e.g. `appsettings.json`, with a `"Sanity"` section):

```json
{
  "Sanity": {
    "ProjectId": "your-project-id",
    "Dataset": "production",
    "ApiVersion": "v2024-01-01"
  }
}
```

```csharp
builder.Services.AddSanityClient(builder.Configuration);
```

## Use

```csharp
public sealed class Post : SanityDocument // picks up _id, _type, _rev, _createdAt, _updatedAt
{
    public string Title { get; set; } = string.Empty;
    public string? Body { get; set; }
}

public sealed class PostsService(ISanityClient sanity)
{
    public Task<Post[]?> GetPostsAsync() =>
        sanity.FetchResultAsync<Post[]>("*[_type == \"post\"]{_id, _type, title, body}");

    public Task<Post?> GetPostBySlugAsync(string slug) =>
        sanity.FetchResultAsync<Post>(
            "*[_type == \"post\" && slug.current == $slug][0]{_id, _type, title, body}",
            new { slug });
}
```

`FetchAsync<T>` returns the full Sanity response envelope (`Query`, `Result`,
`Ms`); `FetchResultAsync<T>` is a convenience wrapper that unwraps `Result`
directly. `T` can be any type you like — plain POCOs, records, `JsonElement`,
etc. — the client only needs it to be deserializable by `System.Text.Json`.

### Strongly-typed documents

Inherit your document models from `SanityDocument` (or implement
`ISanityDocument` yourself) to get Sanity's common system fields mapped
automatically:

```csharp
public abstract class SanityDocument : ISanityDocument
{
    public string Id { get; set; }          // _id
    public string Type { get; set; }        // _type
    public string? Rev { get; set; }         // _rev
    public DateTimeOffset? CreatedAt { get; set; } // _createdAt
    public DateTimeOffset? UpdatedAt { get; set; } // _updatedAt
}
```

This is optional — any plain class works as `T` — but it saves you from
re-declaring `[JsonPropertyName("_id")]` etc. on every model, and gives you
a common `ISanityDocument` contract to code against (e.g. in generic
repository/service helpers) regardless of the concrete document type.

## Without DI

```csharp
var options = new SanityClientOptions { ProjectId = "abc123", Dataset = "production" };
using var http = new HttpClient();
var client = new SanityClient(http, options);
var posts = await client.FetchResultAsync<Post[]>("*[_type == \"post\"]");
```

## Notes

- Read-only: this SDK targets the GROQ query endpoint only (no mutations,
  asset uploads, or the Listen API).
- By default queries route through Sanity's CDN (`apicdn.sanity.io`) for
  speed; set `UseCdn = false` for strongly-consistent reads, or supply a
  `Token` for private datasets (which automatically bypasses the CDN unless
  `UseCdnWithToken` is set).

## ⚠️ Never put a Token in a Blazor WebAssembly app's config

In Blazor Server, Blazor Hybrid, or any ASP.NET Core app, `appsettings.json`
lives on the server and `options.Token` stays private.

**In Blazor WebAssembly, there is no server.** Everything under `wwwroot/`
-- including `wwwroot/appsettings.json` -- is downloaded as a plain static
file straight to the browser. Anyone can open dev tools (or just
`view-source:`) and read it. If you bind `SanityClientOptions.Token` from
configuration in a WASM app, **you are shipping that token to every
visitor**, and with it write access to your dataset if the token has write
permissions.

For a Blazor WASM app:
- Only query **public** datasets without a token (as the sample app does),
  or
- Put the token behind a thin server-side endpoint (an ASP.NET Core
  minimal API, Azure Function, etc.) that your WASM app calls instead of
  talking to Sanity directly, keeping the token server-side where it
  belongs.

This applies to `Sanity.Client.Presentation` too: a `Token` used to fetch
drafts for preview is exactly as exposed as any other config value in a
WASM app's `wwwroot`.
