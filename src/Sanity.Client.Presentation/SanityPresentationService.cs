using System.Text.Json;
using Microsoft.JSInterop;

namespace Sanity.Client.Presentation;

/// <summary>
/// Default <see cref="ISanityPresentationService"/> implementation, backed
/// by the vendored JS module shipped in this package's static web assets
/// (<c>_content/Sanity.Client.Presentation/sanity-presentation-interop.js</c>).
/// </summary>
public sealed class SanityPresentationService : ISanityPresentationService
{
    private const string ModulePath = "./_content/Sanity.Client.Presentation/sanity-presentation-interop.js";

    private readonly IJSRuntime _jsRuntime;
    private readonly SanityPresentationOptions _options;
    private readonly JsonSerializerOptions _jsonOptions;
    private Task<IJSObjectReference>? _modulePromise;
    private long? _activeOverlayHandle;

    public SanityPresentationService(IJSRuntime jsRuntime, Microsoft.Extensions.Options.IOptions<SanityPresentationOptions> options)
        : this(jsRuntime, options.Value)
    {
    }

    public SanityPresentationService(IJSRuntime jsRuntime, SanityPresentationOptions options)
    {
        ArgumentNullException.ThrowIfNull(jsRuntime);
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();

        _jsRuntime = jsRuntime;
        _options = options;
        _jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);
    }

    /// <inheritdoc />
    public async ValueTask<T> StegaEncodeAsync<T>(
        T result,
        JsonElement? resultSourceMap,
        CancellationToken cancellationToken = default)
    {
        if (resultSourceMap is null)
        {
            return result;
        }

        var module = await GetModuleAsync(cancellationToken).ConfigureAwait(false);
        return await module.InvokeAsync<T>(
            "stegaEncode",
            cancellationToken,
            result,
            resultSourceMap,
            _options.StudioUrl,
            new { omitCrossDatasetReferenceData = _options.OmitCrossDatasetReferenceData }).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask<string> StegaCleanAsync(string value, CancellationToken cancellationToken = default)
    {
        var module = await GetModuleAsync(cancellationToken).ConfigureAwait(false);
        return await module.InvokeAsync<string>("stegaClean", cancellationToken, value).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IAsyncDisposable> EnableVisualEditingAsync(CancellationToken cancellationToken = default)
    {
        var module = await GetModuleAsync(cancellationToken).ConfigureAwait(false);
        var handle = await module.InvokeAsync<long>("enableVisualEditing", cancellationToken, (object?)null).ConfigureAwait(false);
        _activeOverlayHandle = handle;
        return new OverlayHandle(this, handle);
    }

    /// <inheritdoc />
    public async Task DisableVisualEditingAsync(CancellationToken cancellationToken = default)
    {
        if (_activeOverlayHandle is not { } handle || _modulePromise is null)
        {
            return;
        }

        var module = await _modulePromise.ConfigureAwait(false);
        await module.InvokeVoidAsync("disableVisualEditing", cancellationToken, handle).ConfigureAwait(false);
        _activeOverlayHandle = null;
    }

    private Task<IJSObjectReference> GetModuleAsync(CancellationToken cancellationToken)
    {
        return _modulePromise ??= _jsRuntime.InvokeAsync<IJSObjectReference>(
            "import",
            cancellationToken,
            ModulePath).AsTask();
    }

    public async ValueTask DisposeAsync()
    {
        await DisableVisualEditingAsync().ConfigureAwait(false);

        if (_modulePromise is not null)
        {
            try
            {
                var module = await _modulePromise.ConfigureAwait(false);
                await module.DisposeAsync().ConfigureAwait(false);
            }
            catch (JSDisconnectedException)
            {
                // The circuit/runtime is already gone; nothing to clean up.
            }
        }
    }

    private sealed class OverlayHandle(SanityPresentationService owner, long handle) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            if (owner._activeOverlayHandle == handle)
            {
                await owner.DisableVisualEditingAsync().ConfigureAwait(false);
            }
        }
    }
}
