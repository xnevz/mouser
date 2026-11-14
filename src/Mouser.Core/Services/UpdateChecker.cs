using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Mouser.Core.Services;

public sealed record UpdateCheckResult(
    bool IsUpdateAvailable,
    string LatestVersion,
    string CurrentVersion,
    string ReleaseUrl,
    string ReleaseNotes,
    string? Error = null);

/// <summary>
/// Lightweight update checker querying GitHub Releases (Task H3).
/// Sends zero telemetry; includes custom proxy support and local test hook.
/// </summary>
public static class UpdateChecker
{
    public const string DefaultReleasesApiUrl = "https://api.github.com/repos/xnevz/mouser/releases/latest";

    public static async Task<UpdateCheckResult> CheckForUpdateAsync(
        string currentVersion,
        string? endpointUrl = null,
        string? proxyAddress = null,
        HttpClient? customClient = null,
        CancellationToken ct = default)
    {
        string targetUrl = string.IsNullOrWhiteSpace(endpointUrl) ? DefaultReleasesApiUrl : endpointUrl.Trim();

        try
        {
            string json;
            if (targetUrl.StartsWith("file://", StringComparison.OrdinalIgnoreCase) || File.Exists(targetUrl))
            {
                // Local test hook support
                string filePath = targetUrl.StartsWith("file://", StringComparison.OrdinalIgnoreCase)
                    ? new Uri(targetUrl).LocalPath
                    : targetUrl;
                json = await File.ReadAllTextAsync(filePath, ct);
            }
            else
            {
                HttpClient client;
                bool disposeClient = false;

                if (customClient != null)
                {
                    client = customClient;
                }
                else
                {
                    HttpClientHandler handler = new();
                    if (!string.IsNullOrWhiteSpace(proxyAddress))
                    {
                        handler.Proxy = new WebProxy(proxyAddress);
                        handler.UseProxy = true;
                    }
                    client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(10) };
                    disposeClient = true;
                }

                try
                {
                    using var request = new HttpRequestMessage(HttpMethod.Get, targetUrl);
                    request.Headers.UserAgent.Add(new ProductInfoHeaderValue("Mouser", "1.0"));
                    request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github.v3+json"));

                    using var response = await client.SendAsync(request, ct);
                    if (!response.IsSuccessStatusCode)
                    {
                        return new UpdateCheckResult(
                            false,
                            string.Empty,
                            currentVersion,
                            string.Empty,
                            string.Empty,
                            $"GitHub API returned {(int)response.StatusCode} {response.ReasonPhrase}");
                    }

                    json = await response.Content.ReadAsStringAsync(ct);
                }
                finally
                {
                    if (disposeClient) client.Dispose();
                }
            }

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            string tagName = root.TryGetProperty("tag_name", out var tagElem) ? tagElem.GetString() ?? "" : "";
            string htmlUrl = root.TryGetProperty("html_url", out var urlElem) ? urlElem.GetString() ?? "" : "";
            string body = root.TryGetProperty("body", out var bodyElem) ? bodyElem.GetString() ?? "" : "";

            if (string.IsNullOrWhiteSpace(tagName))
            {
                return new UpdateCheckResult(false, "", currentVersion, "", "", "Release tag missing from API response.");
            }

            bool isNewer = VersionComparer.IsNewer(tagName, currentVersion);

            return new UpdateCheckResult(
                IsUpdateAvailable: isNewer,
                LatestVersion: tagName,
                CurrentVersion: currentVersion,
                ReleaseUrl: htmlUrl,
                ReleaseNotes: body);
        }
        catch (Exception ex)
        {
            return new UpdateCheckResult(false, "", currentVersion, "", "", ex.Message);
        }
    }
}
