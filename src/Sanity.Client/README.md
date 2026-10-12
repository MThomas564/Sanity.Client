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

## LINQ querying

As an alternative to hand-writing GROQ, the `Sanity.Client.Linq`
namespace (part of this same package, not a separate one) translates a
subset of LINQ into GROQ:

```csharp
using Sanity.Client.Linq;

var recent = await client.Query<Post>()
    .Where(p => p.PublishedAt != null && p.Title.Contains("Sanity"))
    .OrderByDescending(p => p.PublishedAt)
    .Take(5)
    .ToListAsync();

var titles = await client.Query<Post>()
    .OrderByDescending(p => p.PublishedAt)
    .Select(p => p.Title)
    .ToListAsync();
```

`Query<T>()` filters to `_type == "<name>"`, where `<name>` is the
camelCased class name (`BlogPost` -> `"blogPost"`) unless overridden with
`[SanityType("post")]`. Field names follow the same convention the rest
of the SDK uses for deserialization: camelCased property name, or
`[JsonPropertyName]` if present.

**This is read-only** -- there is no LINQ surface for mutations, joins/
reference dereferencing, or server-side projections (`Select` always
fetches the full matched documents, then projects in memory; it costs
some bandwidth but guarantees correct semantics for any C# expression,
even ones GROQ has no equivalent for).

Call `.ToGroqQuery()` on any query (before an async terminal) to see
exactly what GROQ it translates to, e.g. for logging/debugging.

> This is a clean-room implementation written directly against GROQ's
> published syntax and standard .NET `IQueryable<T>`/`IQueryProvider`
> conventions -- not a port of
> [sanity-linq](https://github.com/oslofjord/sanity-linq) (or any other
> library)'s code, though "LINQ over Sanity" as a concept is naturally
> inspired by it and by Entity Framework. Scope, API shape, and the
> `Select`-is-client-side design choice are all intentionally different.

### Supported LINQ operators

| Operator | Translates to |
|---|---|
| `Where(predicate)` | `&&`-combined GROQ filter; chaining multiple `Where` calls ANDs them |
| `==`, `!=`, `>`, `>=`, `<`, `<=` | the same GROQ operators (member must appear on at least one side; `5 < p.X` correctly flips to `x > 5`) |
| `&&`, `\|\|`, unary `!` | GROQ `&&`, `\|\|`, `!` |
| `== null` / `!= null` | GROQ `== null` / `!= null` |
| `p.A.B` (nested member access) | dotted GROQ path `a.b` -- only for real nested objects, not CLR-only properties like `.Length`/`.Year` (those throw `NotSupportedException`) |
| `p.Title.Contains(x)` / `StartsWith(x)` / `EndsWith(x)` | GROQ `match "*x*"` / `"x*"` / `"*x"` |
| `list.Contains(p.Field)` (closed-over collection) | GROQ `field in [...]` |
| `OrderBy` / `OrderByDescending` / `ThenBy` / `ThenByDescending` | GROQ `\| order(...)` |
| `Skip(n)` / `Take(n)` | GROQ slice `[n...n+take]` |
| `Select(selector)` | fetched server-side as the full document, projected client-side (see above) |
| `CountAsync()` / `LongCountAsync()` | GROQ `count(...)` (a real server-side count, not "fetch everything and count") |
| `AnyAsync()` / `AnyAsync(predicate)` | GROQ `count(...) > 0` |
| `First/FirstOrDefaultAsync([predicate])` | same filter + a `[0...1]` slice, picking the first result (or throwing/returning default per standard LINQ semantics) |
| `Single/SingleOrDefaultAsync([predicate])` | same filter + a `[0...2]` slice, to detect and throw on more-than-one-match like standard LINQ |

Anything else (closure-captured variables and most constant-ish method
calls like `DateTime.Today` work automatically by evaluating the
non-member side of an expression; genuinely unsupported shapes) throws
`NotSupportedException` naming the exact unsupported expression, rather
than silently producing a wrong query. Synchronous enumeration
(`foreach`, `.ToList()` instead of `.ToListAsync()`) works but blocks on
the HTTP call -- prefer the `...Async` methods.

## Notes

- Read-only: this SDK targets the GROQ query endpoint only (no mutations,
  asset uploads, or the Listen API).
- By default queries route through Sanity's CDN (`apicdn.sanity.io`) for
  speed; set `UseCdn = false` for strongly-consistent reads, or supply a
  `Token` for private datasets (which automatically bypasses the CDN unless
  `UseCdnWithToken` is set).
- Large queries automatically switch from GET to POST once the encoded
  query string would exceed 11,264 characters (the same threshold
  `@sanity/client` itself uses), so you don't need to think about URL
  length limits even for very large GROQ queries or parameter sets.
- Set `SanityClientOptions.Timeout` for a per-client request timeout
  (applied to the underlying `HttpClient`). A timeout surfaces as a
  `SanityException` with "timed out" in the message, distinct from your
  own `CancellationToken` being triggered (which propagates as
  `OperationCanceledException`, unwrapped).

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
