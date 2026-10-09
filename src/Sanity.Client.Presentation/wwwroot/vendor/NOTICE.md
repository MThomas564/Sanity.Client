# Third-party JavaScript vendored in this package

These files are redistributed as-is from their official npm packages (no
CDN is used at runtime; everything is shipped inside this NuGet package and
served from your app's own origin). They are only ever loaded in the
browser when you call into `Sanity.Client.Presentation`'s services.

## vendor/stega/sanity-stega.min.mjs

A dependency-trimmed, minified ESM bundle built with esbuild from:

- `@sanity/client` (MIT License, Sanity.io) — `stegaEncodeSourceMap` /
  `encodeIntoResult`, from the package's own `dist/stegaEncodeSourceMap-*.js`
  chunk.
- `@vercel/stega` (MIT License, Vercel Inc.) — `vercelStegaCombine`,
  `vercelStegaClean`, `vercelStegaDecode`, `vercelStegaDecodeAll`,
  `vercelStegaSplit`.

Rebuild with:

```
npm install @sanity/client @vercel/stega esbuild
npx esbuild stega-wrapper.mjs --bundle --format=esm --platform=browser \
  --outfile=sanity-stega.min.mjs --minify
```

where `stega-wrapper.mjs` re-exports the functions above. See
`src/Sanity.Client.Presentation/README.md` for versions used.

## vendor/visual-editing-standalone/*

Copied unmodified from the `dist/` folder of
`@sanity/visual-editing-standalone` (MIT License, Sanity.io) — "a
self-contained ESM build of Sanity Visual Editing for non-React
applications". All of its dependencies (including React) are inlined by
its own build, so these files have no external imports besides each other.

## Updating

Bump the version in `src/Sanity.Client.Presentation/README.md`, re-run the
build steps above against the new package versions, and replace the files
in `wwwroot/vendor/`.
