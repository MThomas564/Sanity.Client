// Glue module for Sanity.Client.Presentation. Loaded on demand via
// IJSRuntime.InvokeAsync<IJSObjectReference>("import", "./_content/Sanity.Client.Presentation/sanity-presentation-interop.js").
// Everything it needs is vendored locally under ./vendor — nothing here
// ever fetches from a third-party CDN.

import { stegaEncodeSourceMap, vercelStegaClean } from "./vendor/stega/sanity-stega.min.mjs";

let visualEditingModulePromise = null;
let activeDisableFns = new Map();
let nextHandle = 1;

function loadVisualEditingModule() {
    visualEditingModulePromise ??= import("./vendor/visual-editing-standalone/index.js");
    return visualEditingModulePromise;
}

/**
 * Embeds the Content Source Map returned alongside a GROQ query result as
 * invisible stega-encoded metadata into the result's string values, using
 * Sanity's own `stegaEncodeSourceMap`. `result` and `resultSourceMap` are
 * plain JSON values (as produced by System.Text.Json on the .NET side);
 * `studioUrl` is the base URL of the Sanity Studio to deep-link into.
 */
export function stegaEncode(result, resultSourceMap, studioUrl, options) {
    if (!resultSourceMap) {
        return result;
    }

    return stegaEncodeSourceMap(result, resultSourceMap, {
        enabled: true,
        studioUrl,
        omitCrossDatasetReferenceData: options?.omitCrossDatasetReferenceData ?? false,
    });
}

/** Strips any stega-encoded invisible metadata back out of a value. */
export function stegaClean(value) {
    return vercelStegaClean(value);
}

/**
 * Dynamically imports `@sanity/visual-editing-standalone` and calls
 * `enableVisualEditing()`. Returns an opaque handle; pass it to
 * `disableVisualEditing` to tear the overlays down again.
 */
export async function enableVisualEditing(options) {
    const { enableVisualEditing: enable } = await loadVisualEditingModule();
    const disable = enable(options ?? {});
    const handle = nextHandle++;
    activeDisableFns.set(handle, disable);
    return handle;
}

/** Tears down the overlays previously enabled with the given handle. */
export function disableVisualEditing(handle) {
    const disable = activeDisableFns.get(handle);
    if (disable) {
        disable();
        activeDisableFns.delete(handle);
    }
}
