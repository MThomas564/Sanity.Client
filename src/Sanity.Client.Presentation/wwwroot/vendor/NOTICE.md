# Third-party JavaScript vendored here

Full license texts and attributions for the files in this directory
live in `THIRD-PARTY-NOTICES.md` at the repo root (also included inside
this package itself, since it's shipped as a static web asset alongside
these files -- see `staticwebassets/vendor/NOTICE.md` in the built
`.nupkg`). This file just covers what's here and how to rebuild it.

No third-party CDN is used at runtime -- everything here is shipped
inside the `Sanity.Client.Presentation` NuGet package and served from
your app's own origin.

## stega/sanity-stega.min.mjs

A dependency-trimmed, minified ESM bundle built with esbuild, combining:

- `@sanity/client` (pinned at **8.9.0**, MIT) -- `stegaEncodeSourceMap` /
  `encodeIntoResult`, from the package's own
  `dist/stegaEncodeSourceMap-*.js` chunk.
- `@vercel/stega` (pinned at **1.1.0**, **MPL-2.0** -- not MIT) --
  `vercelStegaCombine`, `vercelStegaClean`, `vercelStegaDecode`,
  `vercelStegaDecodeAll`, `vercelStegaSplit`.

Rebuild with:

```
npm install @sanity/client@8.9.0 @vercel/stega@1.1.0 esbuild
npx esbuild stega-wrapper.mjs --bundle --format=esm --platform=browser \
  --outfile=sanity-stega.min.mjs --minify
```

where `stega-wrapper.mjs` re-exports the functions above from each
package's dist files.

## visual-editing-standalone/*

Copied unmodified (no bundling/minification by us -- these are the
package's own build output files) from the `dist/` folder of
`@sanity/visual-editing-standalone` (pinned at **2.0.2**, MIT). All of
its dependencies (including React) are inlined by its own build, so
these files have no external imports besides each other.

## Updating

Bump the pinned versions above, re-run the build steps for the stega
bundle against the new `@sanity/client` / `@vercel/stega` versions,
re-download `@sanity/visual-editing-standalone`'s `dist/` folder at its
new version, replace the files here, and update both this file and
`THIRD-PARTY-NOTICES.md` at the repo root (version numbers and, if
changed, license text/copyright lines).
