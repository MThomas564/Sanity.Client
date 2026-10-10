// Glue module for Sanity.Client.Presentation. Loaded on demand via
// IJSRuntime.InvokeAsync<IJSObjectReference>("import", "./_content/Sanity.Client.Presentation/sanity-presentation-interop.js").
// Everything it needs is vendored locally under ./vendor — nothing here
// ever fetches from a third-party CDN.

import { stegaEncodeSourceMap, vercelStegaClean } from "./vendor/stega/sanity-stega.min.mjs";

let visualEditingModulePromise = null;
let activeDisableFns = new Map();
let activeRefreshFns = new Map();
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
 *
 * `onRefreshDotNetRef`, if given, is a .NET object reference with an
 * `OnRefreshAsync(payloadJson: string): Task<bool>` `[JSInvokable]`
 * method. It's wired as the `refresh` option: when the Studio reports a
 * mutation (or a manual refresh request), we call into .NET and await the
 * result. Returning `true` tells Sanity the refresh was handled (no
 * further action); returning `false` lets Sanity fall back to its own
 * default behavior (eventually a full reload) after its internal timeout.
 */
export async function enableVisualEditing(onRefreshDotNetRef, options) {
    const { enableVisualEditing: enable } = await loadVisualEditingModule();

    const mergedOptions = { ...(options ?? {}) };
    let refreshFn = null;
    if (onRefreshDotNetRef) {
        refreshFn = async (payload) => {
            try {
                const handled = await onRefreshDotNetRef.invokeMethodAsync(
                    "OnRefreshAsync",
                    JSON.stringify(payload),
                );
                return handled === true;
            } catch (e) {
                console.error("Sanity.Client.Presentation: refresh callback threw", e);
                return false;
            }
        };
        mergedOptions.refresh = refreshFn;
    }

    const disable = enable(mergedOptions);
    const handle = nextHandle++;
    activeDisableFns.set(handle, disable);
    if (refreshFn) {
        activeRefreshFns.set(handle, refreshFn);
    }
    return handle;
}

/** Tears down the overlays previously enabled with the given handle. */
export function disableVisualEditing(handle) {
    const disable = activeDisableFns.get(handle);
    if (disable) {
        disable();
        activeDisableFns.delete(handle);
        activeRefreshFns.delete(handle);
    }
}

/**
 * TESTING ONLY. Invokes the `refresh` callback wired for `handle` exactly
 * as Sanity's own overlay code would when the Studio reports a change,
 * without needing a real Presentation iframe/Studio. Used to verify the
 * JS-to-.NET refresh bridge end to end. Not part of the public API.
 */
export async function __testInvokeRefresh(handle, payload) {
    const refreshFn = activeRefreshFns.get(handle);
    if (!refreshFn) {
        throw new Error(`No refresh callback registered for handle ${handle}`);
    }
    return await refreshFn(payload);
}
