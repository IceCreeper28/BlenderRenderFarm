using System;
using System.IO;
using System.Linq.Expressions;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Lazy;

namespace BlenderRenderFarm.Extensions {
    internal static class HttpClientExtensions {
        public static async Task GetIntoStreamAsync(this HttpClient client, string? requestUri, Stream destination, IProgress<float>? progress = null, CancellationToken cancellationToken = default) {
            using var response = await GetResponseAsync(client, requestUri, cancellationToken).ConfigureAwait(false);
            await CopyContentToStream(response.Content, destination, progress, cancellationToken).ConfigureAwait(false);
        }

        public static Task<Stream> GetStreamAsync(this HttpClient client, string? requestUri, IProgress<float>? progress = null, CancellationToken cancellationToken = default) {
            // fallback to original method if no progress is used.
            if (progress is null)
                return client.GetStreamAsync(requestUri, cancellationToken);

            return Core();

            async Task<Stream> Core() {
                using var response = await GetResponseAsync(client, requestUri, cancellationToken).ConfigureAwait(false);
                return await CopyContentIntoStream(response.Content, progress, cancellationToken).ConfigureAwait(false);
            }
        }

        [Lazy]
        private static Func<ArraySegment<byte>, HttpContentHeaders, string>? ReadBufferAsStringFunc {
            get {
                var bufferParameter = Expression.Parameter(typeof(ArraySegment<byte>), "buffer");
                var headersParameter = Expression.Parameter(typeof(HttpContentHeaders), "headers");
                var method = typeof(HttpContent).GetMethod("ReadBufferAsString", BindingFlags.NonPublic | BindingFlags.Static, null, new Type[] { typeof(ArraySegment<byte>), typeof(HttpContentHeaders) }, null);
                if (method is null)
                    return null;
                var callExpression = Expression.Call(method, bufferParameter, headersParameter);
                var lambda = Expression.Lambda<Func<ArraySegment<byte>, HttpContentHeaders, string>>(callExpression, bufferParameter, headersParameter);
                return lambda.Compile();
            }
        }
        public static Task<string> GetStringAsync(this HttpClient client, string? requestUri, IProgress<float>? progress = null, CancellationToken cancellationToken = default) {
            // fallback to original method if no progress is used.
            if (progress is null)
                return client.GetStringAsync(requestUri, cancellationToken);

            return Core();

            async Task<string> Core() {
                var readBufferAsString = ReadBufferAsStringFunc;
                if (readBufferAsString is null) {
                    // fallback to original method if method could not be found.
                    var result = await client.GetStringAsync(requestUri, cancellationToken).ConfigureAwait(false);
                    progress.Report(1);
                    return result;
                }

                using var response = await GetResponseAsync(client, requestUri, cancellationToken).ConfigureAwait(false);
                using var contentStream = await CopyContentIntoStream(response.Content, progress, cancellationToken).ConfigureAwait(false);

                return readBufferAsString(contentStream.ToArray(), response.Content.Headers);
            }
        }

        private static async Task CopyContentToStream(HttpContent content, Stream destination, IProgress<float>? progress = null, CancellationToken cancellationToken = default) {
            using var contentStream = await content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            var contentLength = content.Headers.ContentLength;

            if (contentLength is long totalBytes && progress != null) {
                var progressHandler = new Progress<long>(bytesReceived => {
                    progress.Report((float)bytesReceived / totalBytes);
                });
                await contentStream.CopyToAsync(destination, progressHandler, cancellationToken).ConfigureAwait(false);
            } else {
                await contentStream.CopyToAsync(destination, cancellationToken).ConfigureAwait(false);
            }
            progress?.Report(1);
        }

        private static async Task<MemoryStream> CopyContentIntoStream(HttpContent content, IProgress<float>? progress = null, CancellationToken cancellationToken = default) {
            var memoryStream = new MemoryStream();
            try {
                await CopyContentToStream(content, memoryStream, progress, cancellationToken).ConfigureAwait(false);
                memoryStream.Seek(0, SeekOrigin.Begin);
                return memoryStream;
            } catch (Exception) {
                await memoryStream.DisposeAsync().ConfigureAwait(false);
                throw;
            }
        }

        private static async Task<HttpResponseMessage> GetResponseAsync(this HttpClient client, string? requestUri, CancellationToken cancellationToken = default) {
            HttpResponseMessage? response = null;
            try {
                response = await client.GetAsync(requestUri, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();
                return response;
            } catch {
                response?.Dispose();
                throw;
            }
        }
    }
}
