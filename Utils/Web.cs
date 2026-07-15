using RIoT2.Core.Models;
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
        private static readonly HttpClient _httpClient = new HttpClient();

        private static readonly HttpClient _insecureHttpClient = new HttpClient(new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true
        });

        public static async Task<WebFile> DownloadFile(string url)
        {
            try
            {
                using (var cancel = new CancellationTokenSource(defaultTimeOutMilliseconds))
                {
                    var response = await _httpClient.GetAsync(url, cancel.Token);
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
            string token = null,
            int timeOutMilliseconds = defaultTimeOutMilliseconds)
        {
            return await GetAsync(
                address,
                CreateBearerHeader(token),
                timeOutMilliseconds);
        }

        public static async Task<HttpResponseMessage> PostWithBearerTokenAsync(
            string address,
            string postBody,
            string token = null,
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
            AuthenticationHeaderValue authHeader = null,
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

        public static async Task<HttpResponseMessage> GetAsync(
           string address,
           Dictionary<string, string> headers = null)
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
            string postBody,
            AuthenticationHeaderValue authHeader,
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
           Dictionary<string, string> headers = null,
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

        public static async Task<HttpResponseMessage> PutAsync(
           string address,
           string body,
           IReadOnlyDictionary<string, string> headers = null,
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

        public static async Task<string> SendRequestAsync(string uri, string userName, string password, HttpMethod method)
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

        // Fix #9: avoid emitting a malformed "Authorization: Bearer" header when the token is null/empty.
        private static AuthenticationHeaderValue CreateBearerHeader(string token)
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

    internal class InsecureHttpClient : IDisposable
    {
        private HttpClientHandler _httpClientHandler;
        private HttpClient _httpClient;
        public InsecureHttpClient()
        {
            _httpClientHandler = new HttpClientHandler();
            _httpClientHandler.ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => { return true; };
            _httpClient = new HttpClient(_httpClientHandler);
        }

        internal HttpClient Client { get { return _httpClient; } }

        public void Dispose()
        {
            if (_httpClientHandler != null)
                _httpClientHandler.Dispose();

            if (_httpClient != null)
                _httpClient.Dispose();
        }
    }
}
