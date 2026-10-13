# Sanity.Client.Presentation

Blazor support for Sanity's **Presentation tool** (Visual Editing):
stega-encoded click-to-edit overlays plus live preview updates, wired into
your app via dependency injection. Depends on `Sanity.Client` for
fetching data.

All required JavaScript (Sanity's own `stegaEncodeSourceMap` plus
`@sanity/visual-editing-standalone`) is **vendored inside this package**
and served from your app's own origin as a static web asset
(`_content/Sanity.Client.Presentation/...`) -- nothing is loaded from a
third-party CDN at runtime. See `THIRD-PARTY-NOTICES.md` at the repo
root for full license texts and attributions, and
`wwwroot/vendor/NOTICE.md` for exact versions and how to rebuild them.

## Two separate concerns -- don't confuse them

Stega encoding and overlays are **not** what makes the preview update
live as an editor types. They're separate mechanisms:

| Concern | What it does | API |
|---|---|---|
| **Click-to-edit** | Lets an editor click rendered text in the preview and jump straight to that field in the Studio. Works by embedding invisible metadata into strings (stega) that the overlay script decodes. | `StegaEncodeAsync`, `<SanityVisualEditing>` |
| **Live preview updates** | Makes the preview re-render in place when content changes in the Studio, without a page reload. | the `OnRefresh` callback, below |

If you only wire stega + overlays, click-to-edit works but the preview
will *not* update when content changes -- you also need the refresh
callback.

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

`Sanity.Client` automatically routes a `Drafts`/`Raw` perspective request
off the CDN for you, regardless of how the client itself is configured --
Sanity's CDN flatly rejects the `perspective` parameter (confirmed
against the real API, which returns the error
`"perspective" is not allowed for CDN requests`), and drafts are never
cached there anyway. You don't need to also set `UseCdn = false` by hand.

For a **private** dataset, drafts also require a read token -- and since
this is a Blazor **WASM** app, that token cannot live in `wwwroot/`
config (see the root `Sanity.Client` README's Blazor WASM token
warning). Sanity's own recommended pattern is a secret-gated, server-side
"enable draft mode" endpoint that validates a one-time secret the Studio
passes, then sets a cookie your frontend checks -- the token itself never
reaches the browser. See
[Sanity's Draft Mode docs](https://www.sanity.io/docs/visual-editing/implementing-draft-mode)
for the full flow. (This repo's own Blazor WASM sample queries a public
demo dataset that happens to allow anonymous draft reads, so it can
demonstrate the `Drafts` perspective switch itself without a token --
but that's specific to that dataset, not something to rely on for your
own.)

## Enable overlays AND live updates together

Drop the component in your layout, bound to whatever flag tracks whether
you're being previewed by the Presentation tool (e.g. a query string or
cookie your app sets when Sanity opens it in the Presentation iframe).
`OnRefresh` is what makes the preview actually update when content
changes -- without it you only get click-to-edit, which is the gap you'll
notice if you skip this part:

```razor
<SanityVisualEditing Enabled="@IsPreviewMode" OnRefresh="HandleRefreshAsync" />

@code {
    private async Task<bool> HandleRefreshAsync(SanityRefreshPayload payload)
    {
        // payload.Source is "mutation" (a document was edited -- payload.Document
        // has _id/_type/_rev) or "manual" (editor clicked Studio's refresh button).
        await ReloadPostAsync(); // re-fetch with SanityPerspective.Drafts
        StateHasChanged();
        return true; // tells Sanity this was handled; omit/return false to
                      // let Sanity fall back to its own default behavior.
    }
}
```

Or call the service directly for more control:

```csharp
@inject ISanityPresentationService Presentation

var overlays = await Presentation.EnableVisualEditingAsync(onRefresh: HandleRefreshAsync);
// later:
await overlays.DisposeAsync();
```

## What's verified vs. what isn't

Verified end to end in a real headless-Chrome session against the live
public Sanity API (see the Blazor sample's `/presentation` page):

- **Stega encoding** (`StegaEncodeAsync` / `StegaCleanAsync`): output
  matches the real, unmodified `@sanity/client` + `@vercel/stega` code --
  same edit-intent URL shape, and cleans back to the exact original
  string.
- **Overlay loading** (`EnableVisualEditingAsync`): the vendored
  `@sanity/visual-editing-standalone` loads and `enableVisualEditing()`
  runs with zero console errors, creating its `<sanity-visual-editing>`
  element.
- **The refresh bridge**: simulating a Studio mutation event and invoking
  the registered `refresh` callback directly (the same call path
  `@sanity/visual-editing-standalone` uses internally) round-trips
  correctly through `DotNetObjectReference` -> `OnRefreshAsync` -> your
  C# delegate -> back to JS as a boolean, and the sample's UI updates in
  response.

**Not verified**: the actual Comlink/postMessage wiring between a real
Sanity Studio's Presentation iframe and this code -- that requires a live
Studio deployment, which this SDK doesn't ship. Everything up to the
point where Sanity's own code would call our `refresh` option has been
exercised with Sanity's real, unmodified JS; what hasn't been exercised
is Sanity's Studio-side code that decides *when* to call it. Test that
part against your own Studio before relying on it in production.
