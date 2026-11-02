using RIoT2.Core.Models;
using RIoT2.Core.Security;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace RIoT2.Core.Utils
{
    public static class Web
    {
        const int defaultTimeOutMilliseconds = 60000;

        // Shared HttpClient instances to avoid socket exhaustion (SocketException) caused by
        // creating/disposing an HttpClient per request. Per-request timeouts are applied via
        // CancellationTokenSource so the shared instance's Timeout is never mutated.
        // Every request passes the outbound URL policy first (design 7.5).
        private static readonly HttpClient _httpClient = new HttpClient(new PolicyHandler(new HttpClientHandler()));

        private static readonly HttpClient _insecureHttpClient = new HttpClient(new PolicyHandler(new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true
        }));

        private const string PurposeProperty = "RIoT2.OutboundPurpose";
        private static IOutboundUrlPolicy _outboundUrlPolicy = AllowAllOutboundUrlPolicy.Instance;

        /// <summary>
        /// The policy every request of these helpers is checked against, with purpose
        /// <see cref="OutboundPurposes.Device"/> unless a method takes another one. A refused request
        /// throws <see cref="OutboundUrlRejectedException"/> (wrapped like other errors by the methods
        /// that wrap them). The service host sets it; the default allows everything (design 7.5).
        /// </summary>
        public static IOutboundUrlPolicy OutboundUrlPolicy
        {
            get => _outboundUrlPolicy;
            set => _outboundUrlPolicy = value ?? throw new ArgumentNullException(nameof(value));
        }

        /// <summary>
        /// Sends <paramref name="request"/> like <see cref="SendAsync(HttpRequestMessage, int, CancellationToken)"/>,
        /// checked against <see cref="OutboundUrlPolicy"/> for <paramref name="purpose"/> (one of
        /// <see cref="OutboundPurposes"/>).
        /// </summary>
        public static Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, string purpose,
            int timeOutMilliseconds = defaultTimeOutMilliseconds, CancellationToken cancellationToken = default)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));
            if (string.IsNullOrEmpty(purpose))
                throw new ArgumentException("A purpose is required.", nameof(purpose));
            request.Properties[PurposeProperty] = purpose;
            return SendAsync(request, timeOutMilliseconds, cancellationToken);
        }

        /// <summary>
        /// Sends <paramref name="request"/> on the shared client, cancelled after
        /// <paramref name="timeOutMilliseconds"/> or by <paramref name="cancellationToken"/>.
        /// Unlike the other methods it doesn't wrap exceptions.
        /// </summary>
        public static async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            int timeOutMilliseconds = defaultTimeOutMilliseconds, CancellationToken cancellationToken = default)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));
            using (var cancel = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
            {
                cancel.CancelAfter(timeOutMilliseconds);
                return await _httpClient.SendAsync(request, cancel.Token).ConfigureAwait(false);
            }
        }

        public static Task<WebFile> DownloadFile(string url) => DownloadFile(url, OutboundPurposes.Device);

        /// <summary>Downloads <paramref name="url"/>, checked against <see cref="OutboundUrlPolicy"/> for <paramref name="purpose"/>.</summary>
        public static async Task<WebFile> DownloadFile(string url, string purpose)
        {
            try
            {
                using (var request = new HttpRequestMessage(HttpMethod.Get, url))
                using (var cancel = new CancellationTokenSource(defaultTimeOutMilliseconds))
                {
                    request.Properties[PurposeProperty] = purpose;
                    var response = await _httpClient.SendAsync(request, cancel.Token);
                    response.EnsureSuccessStatusCode();
                    var filename = response.Content.Headers.ContentDisposition != null && !String.IsNullOrEmpty(response.Content.Headers.ContentDisposition.FileName) ?
                        response.Content.Headers.ContentDisposition.FileName.Trim('"') :
                        GetFileNameFromUrl(url);

                    var content = await response.Content.ReadAsByteArrayAsync();
                    return new WebFile
                    {
                        Name = filename,
                        Url = url,
                        Content = content
                    };
                }
            }
            catch (Exception ex)
            {
                throw new Exception("Error in Web.DownloadFile", ex);
            }
        }

        public static async Task<HttpContentHeaders> GetUrlMetadata(string address)
        {
            try
            {
                using (var cancel = new CancellationTokenSource(defaultTimeOutMilliseconds))
                {
                    var response = await _httpClient.SendAsync(new HttpRequestMessage(HttpMethod.Head, address), cancel.Token);
                    response.EnsureSuccessStatusCode();
                    return response.Content.Headers;
                }
            }
            catch (Exception ex)
            {
                throw new Exception("Error in Web.GetHeaders", ex);
            }
        }

        public static async Task<HttpResponseMessage> GetWithBearerTokenAsync(
            string address,
            string? token = null,
            int timeOutMilliseconds = defaultTimeOutMilliseconds)
        {
            return await GetAsync(
                address,
                CreateBearerHeader(token),
                timeOutMilliseconds);
        }

        public static async Task<HttpResponseMessage> PostWithBearerTokenAsync(
            string address,
            string? postBody,
            string? token = null,
            int timeOutMilliseconds = defaultTimeOutMilliseconds)
        {
            return await PostAsync(
                address,
                postBody,
                CreateBearerHeader(token),
                timeOutMilliseconds);
        }

        public static async Task<HttpResponseMessage> GetAsync(
            string address,
            AuthenticationHeaderValue? authHeader = null,
            int timeOutMilliseconds = defaultTimeOutMilliseconds)
        {
            try
            {
                using (var request = new HttpRequestMessage(HttpMethod.Get, address))
                using (var cancel = new CancellationTokenSource(timeOutMilliseconds))
                {
                    if (authHeader != null)
                        request.Headers.Authorization = authHeader;

                    return await _httpClient.SendAsync(request, cancel.Token);
                }
            }
            catch (Exception ex)
            {
                throw new Exception("Error in Web.GetAsync", ex);
            }
        }

        public static async Task<HttpResponseMessage> GetAsync(
           string address)
        {
            try
            {
                using (var cancel = new CancellationTokenSource(defaultTimeOutMilliseconds))
                {
                    return await _httpClient.GetAsync(address, cancel.Token);
                }
            }
            catch (Exception ex)
            {
                throw new Exception("Error in Web.GetAsync", ex);
            }
        }

        /// <summary>
        /// Sends a GET request using the shared HTTP helper. This overload intentionally accepts
        /// self-signed TLS certificates for local device bridges such as Philips Hue; do not use it
        /// for Internet-facing or credential-bearing endpoints.
        /// </summary>
        public static async Task<HttpResponseMessage> GetAsync(
           string address,
           Dictionary<string, string>? headers = null)
        {
            try
            {
                using (var request = new HttpRequestMessage(HttpMethod.Get, address))
                using (var cancel = new CancellationTokenSource(defaultTimeOutMilliseconds))
                {
                    if (headers != null)
                    {
                        foreach (var header in headers)
                            request.Headers.TryAddWithoutValidation(header.Key, header.Value);
                    }

                    return await _insecureHttpClient.SendAsync(request, cancel.Token);
                }
            }
            catch (Exception ex)
            {
                throw new Exception("Error in Web.GetAsync", ex);
            }
        }

        public static async Task<HttpResponseMessage> PostAsync(
            string address,
            string? postBody,
            AuthenticationHeaderValue? authHeader,
            int timeOutMilliseconds = defaultTimeOutMilliseconds)
        {
            try
            {
                using (var request = new HttpRequestMessage(HttpMethod.Post, address))
                using (var cancel = new CancellationTokenSource(timeOutMilliseconds))
                {
                    if (authHeader != null)
                        request.Headers.Authorization = authHeader;
                    request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                    if (postBody != null)
                        request.Content = new StringContent(postBody, Encoding.UTF8, "application/json");

                    return await _httpClient.SendAsync(request, cancel.Token);
                }
            }
            catch (Exception ex)
            {
                throw new Exception("Error in Web.PostAsync", ex);
            }
        }

        public static async Task<HttpResponseMessage> PostAsync(
           string address,
           string postBody,
           Dictionary<string, string>? headers = null,
           int timeOutMilliseconds = defaultTimeOutMilliseconds)
        {
            try
            {
                using (var request = new HttpRequestMessage(HttpMethod.Post, address))
                using (var cancel = new CancellationTokenSource(timeOutMilliseconds))
                {
                    string contentType = "application/json";
                    if (headers != null && headers.ContainsKey("content-type"))
                    {
                        contentType = headers["content-type"];
                        headers.Remove("content-type");
                    }

                    request.Content = new StringContent(postBody, Encoding.UTF8, contentType);

                    if (headers != null)
                    {
                        foreach (var header in headers)
                            request.Headers.TryAddWithoutValidation(header.Key, header.Value);
                    }

                    return await _httpClient.SendAsync(request, cancel.Token);
                }
            }
            catch (Exception ex)
            {
                throw new Exception("Error in Web.PostAsync", ex);
            }
        }

        public static async Task<HttpResponseMessage> PostMultipartAsync(
           string address,
           byte[] data,
           int timeOutMilliseconds = defaultTimeOutMilliseconds)
        {
            try
            {
                using (var form = new MultipartFormDataContent())
                using (var request = new HttpRequestMessage(HttpMethod.Post, address))
                using (var cancel = new CancellationTokenSource(timeOutMilliseconds))
                {
                    form.Add(new ByteArrayContent(data, 0, data.Length));
                    request.Content = form;

                    return await _httpClient.SendAsync(request, cancel.Token);
                }
            }
            catch (Exception ex)
            {
                throw new Exception("Error in Web.PostMultipartAsync", ex);
            }
        }

        /// <summary>
        /// Sends a PUT request using the shared HTTP helper. This overload intentionally accepts
        /// self-signed TLS certificates for local device bridges such as Philips Hue; do not use it
        /// for Internet-facing or credential-bearing endpoints.
        /// </summary>
        public static async Task<HttpResponseMessage> PutAsync(
           string address,
           string? body,
           IReadOnlyDictionary<string, string>? headers = null,
           int timeOutMilliseconds = defaultTimeOutMilliseconds)
        {
            try
            {
                using (var request = new HttpRequestMessage(HttpMethod.Put, address))
                using (var cancel = new CancellationTokenSource(timeOutMilliseconds))
                {
                    if (body != null)
                        request.Content = new StringContent(body, Encoding.UTF8, "application/json");

                    if (headers != null)
                    {
                        foreach (var header in headers)
                            request.Headers.TryAddWithoutValidation(header.Key, header.Value);
                    }

                    return await _insecureHttpClient.SendAsync(request, cancel.Token);
                }
            }
            catch (Exception ex)
            {
                throw new Exception("Error in Web.PutAsync", ex);
            }
        }

        public static async Task<string> SendRequestAsync(string uri, string? userName, string? password, HttpMethod method)
        {
            try
            {
                using (var request = new HttpRequestMessage(method, uri))
                using (var cancel = new CancellationTokenSource(defaultTimeOutMilliseconds))
                {
                    if (userName != null && password != null)
                    {
                        string encodedAuth = Convert.ToBase64String(Encoding.GetEncoding("ISO-8859-1").GetBytes(userName + ":" + password));
                        request.Headers.Add("Authorization", "Basic " + encodedAuth);
                    }
                    request.Content = new StringContent("");
                    request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/x-www-form-urlencoded");
                    request.Content.Headers.ContentLength = 0;

                    var response = await _httpClient.SendAsync(request, cancel.Token);
                    return await response.Content.ReadAsStringAsync();
                }
            }
            catch (Exception ex)
            {
                throw new Exception("Error in Web.SendRequestAsync", ex);
            }
        }

        public static async Task<HttpResponseMessage> GetResponseAsync(string uri, string token = "", int timeOutMilliseconds = defaultTimeOutMilliseconds)
        {
            try
            {
                using (var request = new HttpRequestMessage(HttpMethod.Get, uri))
                using (var cancel = new CancellationTokenSource(timeOutMilliseconds))
                {
                    if (!String.IsNullOrEmpty(token))
                        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

                    return await _httpClient.SendAsync(request, cancel.Token);
                }
            }
            catch (Exception ex)
            {
                throw new Exception("Error in Web.GetResponseAsync", ex);
            }
        }

        private sealed class PolicyHandler : DelegatingHandler
        {
            public PolicyHandler(HttpMessageHandler inner) : base(inner)
            {
            }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                var purpose = request.Properties.TryGetValue(PurposeProperty, out var value) && value is string text ? text : OutboundPurposes.Device;
                OutboundUrlPolicyHandler.Check(_outboundUrlPolicy, request.RequestUri, purpose);
                return base.SendAsync(request, cancellationToken);
            }
        }

        // Fix #9: avoid emitting a malformed "Authorization: Bearer" header when the token is null/empty.
        private static AuthenticationHeaderValue? CreateBearerHeader(string? token)
        {
            return String.IsNullOrEmpty(token) ? null : new AuthenticationHeaderValue("Bearer", token);
        }

        // Fix #10: derive a safe filename that ignores query strings and trailing slashes.
        private static string GetFileNameFromUrl(string url)
        {
            try
            {
                var uri = new Uri(url, UriKind.RelativeOrAbsolute);
                var path = uri.IsAbsoluteUri ? uri.AbsolutePath : url;
                var name = path.TrimEnd('/').Split('/').Last();
                return String.IsNullOrEmpty(name) ? "download" : Uri.UnescapeDataString(name);
            }
            catch
            {
                return "download";
            }
        }
    }
}
