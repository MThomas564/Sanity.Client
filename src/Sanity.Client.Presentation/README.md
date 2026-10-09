# Sanity.Client.Presentation

Blazor support for Sanity's **Presentation tool** (Visual Editing):
stega-encoded click-to-edit overlays, wired into your app via dependency
injection. Depends on `Sanity.Client` for fetching data.

All required JavaScript (Sanity's own `stegaEncodeSourceMap` plus
`@sanity/visual-editing-standalone`) is **vendored inside this package**
and served from your app's own origin as a static web asset
(`_content/Sanity.Client.Presentation/...`) -- nothing is loaded from a
third-party CDN at runtime. See `wwwroot/vendor/NOTICE.md` for exact
versions and how to rebuild them.

## Install

```
dotnet add package Sanity.Client.Presentation
```

## Register

```csharp
builder.Services.AddSanityClient(options =>
{
    options.ProjectId = "your-project-id";
    options.Dataset = "production";
});

builder.Services.AddSanityVisualEditing(options =>
{
    options.StudioUrl = "https://your-project.sanity.studio"; // or "/studio"
});
```

Or bind both from configuration the same way as `Sanity.Client` (see its
README), using the `"SanityPresentation"` section for the latter.

## Fetch with a Content Source Map, then stega-encode

```csharp
public sealed class PostsService(ISanityClient sanity, ISanityPresentationService presentation)
{
    public async Task<Post?> GetPostForPreviewAsync(string slug)
    {
        var result = await sanity.FetchAsync<Post>(
            "*[_type == \"post\" && slug.current == $slug][0]{_id, _type, title, body}",
            new { slug },
            options: new SanityFetchOptions
            {
                Perspective = SanityPerspective.Drafts,
                ResultSourceMap = SanitySourceMapMode.WithKeyArraySelector,
            });

        // Embeds invisible edit-intent metadata into every string field
        // that resolves to a real document field, using Sanity's own
        // stega encoding -- this is what makes click-to-edit work.
        return await presentation.StegaEncodeAsync(result.Result, result.ResultSourceMap);
    }
}
```

Only call this (and request drafts/source maps) while your app is in
preview/draft mode -- never on a normal production request, both for
performance and because it leaks invisible metadata into rendered text if
left on by accident.

## Enable overlays

Drop the component in your layout, bound to whatever flag tracks whether
you're being previewed by the Presentation tool (e.g. a query string or
cookie your app sets when Sanity opens it in the Presentation iframe):

```razor
<SanityVisualEditing Enabled="@IsPreviewMode" />
```

Or call the service directly for more control:

```csharp
@inject ISanityPresentationService Presentation

var overlays = await Presentation.EnableVisualEditingAsync();
// later:
await overlays.DisposeAsync();
```

## What's verified vs. what isn't

- The stega encoding path (`StegaEncodeAsync` / `StegaCleanAsync`) is
  verified end to end against the real, unmodified `@sanity/client` +
  `@vercel/stega` code -- the exact output (including the edit-intent
  URL shape) matches what Sanity's own client produces.
- The overlay path (`EnableVisualEditingAsync`) calls
  `@sanity/visual-editing-standalone`'s `enableVisualEditing()` exactly as
  documented, unmodified. It hasn't been exercised inside a real
  Presentation iframe talking to a live Studio (that requires a Studio
  deployment this SDK doesn't ship) -- test that part against your own
  Studio before relying on it in production.
