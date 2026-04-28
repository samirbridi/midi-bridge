using System.Net.Http.Headers;

namespace Bridge.Core.Profiles.ProfileStore;

public sealed class ProfileStoreClient
{
    private readonly HttpClient _http;
    private readonly ProfileStoreOptions _options;

    public ProfileStoreClient(HttpClient httpClient, ProfileStoreOptions? options = null)
    {
        _http = httpClient;
        _options = options ?? ProfileStoreOptions.Default;
        _http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("UsbMidiBridge", "0.1"));
    }

    public async Task<string> DownloadManifestAsync(CancellationToken cancellationToken = default)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, _options.ManifestUri);
        using var resp = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
        var content = await resp.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        return content;
    }

    public async Task<string> DownloadProfileAsync(Uri profileUri, long maxSizeBytes, CancellationToken cancellationToken = default)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, profileUri);
        using var resp = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();

        if (resp.Content.Headers.ContentLength is long len && len > maxSizeBytes)
        {
            throw new InvalidOperationException("Profile too large");
        }

        var content = await resp.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (System.Text.Encoding.UTF8.GetByteCount(content) > maxSizeBytes)
        {
            throw new InvalidOperationException("Profile too large");
        }

        return content;
    }
}

