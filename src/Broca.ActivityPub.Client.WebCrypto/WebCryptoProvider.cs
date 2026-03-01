using Glihm.JSInterop.Browser.WebCryptoAPI.Cryptography;
using Broca.ActivityPub.Core.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace Broca.ActivityPub.Client.WebCrypto;

public class WebCryptoProvider : ICryptoProvider, IAsyncDisposable
{
    private const string ModulePath = "./_content/Broca.ActivityPub.Client.WebCrypto/rsaSigning.js";

    private readonly IJSRuntime _jsRuntime;
    private readonly ILogger<WebCryptoProvider> _logger;
    private IJSObjectReference? _module;

    public WebCryptoProvider(IJSRuntime jsRuntime, ILogger<WebCryptoProvider> logger)
    {
        _jsRuntime = jsRuntime ?? throw new ArgumentNullException(nameof(jsRuntime));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    private async ValueTask<IJSObjectReference> GetModuleAsync()
    {
        _module ??= await _jsRuntime.InvokeAsync<IJSObjectReference>("import", ModulePath);
        return _module;
    }

    /// <inheritdoc/>
    public async Task<byte[]> RsaSignDataAsync(string privateKeyPem, byte[] bytesToSign, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(privateKeyPem);
        ArgumentNullException.ThrowIfNull(bytesToSign);

        if (!privateKeyPem.Contains("BEGIN PRIVATE KEY") && !privateKeyPem.Contains("BEGIN RSA PRIVATE KEY"))
            throw new ArgumentException("privateKeyPem does not appear to contain a valid PEM-encoded private key", nameof(privateKeyPem));

        byte[]? key = PemHelper.KeyExtract(privateKeyPem);
        if (key is null || key.Length == 0)
            throw new InvalidOperationException("Failed to extract key from PEM - PemHelper.KeyExtract returned null or empty array");

        _logger.LogDebug("RsaSignDataAsync: extracted {KeyLength} bytes from PEM, data length {DataLength}", key.Length, bytesToSign.Length);

        try
        {
            var module = await GetModuleAsync();
            var signatureBase64 = await module.InvokeAsync<string>(
                "signRsa",
                Convert.ToBase64String(key),
                Convert.ToBase64String(bytesToSign));

            var signature = Convert.FromBase64String(signatureBase64);
            _logger.LogDebug("RsaSignDataAsync: sign succeeded, signature length {SigLength}", signature.Length);
            return signature;
        }
        catch (JSException ex)
        {
            _logger.LogError(ex, "WebCrypto Sign failed. JSException: {JsError}", ex.Message);
            throw new InvalidOperationException($"Failed to sign data with private key - WebCrypto error: {ex.Message}", ex);
        }
    }

    /// <inheritdoc/>
    public async Task<bool> RsaVerifyDataAsync(string publicKeyPem, byte[] signatureBytes, byte[] signedDataBytes, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(publicKeyPem);
        ArgumentNullException.ThrowIfNull(signatureBytes);
        ArgumentNullException.ThrowIfNull(signedDataBytes);

        byte[]? key = PemHelper.KeyExtract(publicKeyPem);
        if (key is null)
            return false;

        try
        {
            var module = await GetModuleAsync();
            return await module.InvokeAsync<bool>(
                "verifyRsa",
                Convert.ToBase64String(key),
                Convert.ToBase64String(signatureBytes),
                Convert.ToBase64String(signedDataBytes));
        }
        catch (JSException ex)
        {
            _logger.LogWarning(ex, "RsaVerifyDataAsync failed. JSException: {JsError}", ex.Message);
            return false;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_module is not null)
            await _module.DisposeAsync();
    }
}
