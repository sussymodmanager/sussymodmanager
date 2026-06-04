using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using SussyModManager.Core;

namespace SussyModManager.Core.Helpers
{
    public sealed class ETagResult
    {
        public bool NotModified { get; set; }
        public string Content { get; set; }
        public string ETag { get; set; }
    }

    /// <summary>
    /// Thin wrapper over a single shared <see cref="HttpClient"/> with GitHub-friendly headers,
    /// ETag-aware string fetches, and streamed file downloads with progress.
    /// </summary>
    public static class Http
    {
        private static readonly HttpClient Client = CreateClient();

        private static HttpClient CreateClient()
        {
            var client = new HttpClient(new HttpClientHandler
            {
                AutomaticDecompression = System.Net.DecompressionMethods.GZip | System.Net.DecompressionMethods.Deflate
            });
            client.Timeout = TimeSpan.FromMinutes(10);
            // A realistic UA keeps CDNs (Thunderstore/Cloudflare) happy; GitHub only needs any UA.
            client.DefaultRequestHeaders.UserAgent.ParseAdd($"Mozilla/5.0 (compatible; SussyModManager/{AppInfo.Version})");
            client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
            return client;
        }

        public static async Task<string> GetStringAsync(string url, CancellationToken ct = default)
        {
            using var resp = await Client.GetAsync(url, ct).ConfigureAwait(false);
            resp.EnsureSuccessStatusCode();
            return await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        }

        public static async Task<ETagResult> GetStringWithETagAsync(string url, string etag, CancellationToken ct = default)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            if (!string.IsNullOrEmpty(etag))
            {
                request.Headers.TryAddWithoutValidation("If-None-Match", etag);
            }

            using var resp = await Client.SendAsync(request, ct).ConfigureAwait(false);

            if (resp.StatusCode == System.Net.HttpStatusCode.NotModified)
            {
                return new ETagResult { NotModified = true, ETag = etag };
            }

            resp.EnsureSuccessStatusCode();
            var content = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            return new ETagResult
            {
                NotModified = false,
                Content = content,
                ETag = resp.Headers.ETag?.Tag
            };
        }

        public static async Task DownloadFileAsync(string url, string destinationPath, IProgress<int> progress = null, CancellationToken ct = default)
        {
            var dir = Path.GetDirectoryName(destinationPath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            using var resp = await Client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
            resp.EnsureSuccessStatusCode();

            var total = resp.Content.Headers.ContentLength ?? -1L;
            using var source = await resp.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
            using var target = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true);

            var buffer = new byte[81920];
            long readSoFar = 0;
            int read;
            int lastPercent = -1;

            while ((read = await source.ReadAsync(buffer, 0, buffer.Length, ct).ConfigureAwait(false)) > 0)
            {
                await target.WriteAsync(buffer, 0, read, ct).ConfigureAwait(false);
                readSoFar += read;

                if (total > 0 && progress != null)
                {
                    var percent = (int)(readSoFar * 100 / total);
                    if (percent != lastPercent)
                    {
                        lastPercent = percent;
                        progress.Report(percent);
                    }
                }
            }
        }
    }
}
