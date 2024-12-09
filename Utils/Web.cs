using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using System.Threading;

namespace RIoT2.Core.Utils
{
    public static class Web
    {
        const int defaultTimeOutMilliseconds = 12000;

        public static async Task<HttpResponseMessage> GetWithBearerTokenAsync(
            string address,
            string token = null,
            int timeOutMilliseconds = defaultTimeOutMilliseconds)
        {
            return await GetAsync(
                address,
                new AuthenticationHeaderValue("Bearer", token),
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
                new AuthenticationHeaderValue("Bearer", token),
                timeOutMilliseconds);
        }

        public static async Task<HttpResponseMessage> GetAsync(
            string address,
            AuthenticationHeaderValue authHeader = null,
            int timeOutMilliseconds = defaultTimeOutMilliseconds)
        {
            try
            {
                using (var httpClient = new HttpClient())
                using (var cancel = new CancellationTokenSource(timeOutMilliseconds))
                {
                    if (authHeader != null)
                        httpClient.DefaultRequestHeaders.Authorization = authHeader;

                    return await httpClient.GetAsync(address, cancel.Token);
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
                using (var httpClient = new HttpClient())
                using (var cancel = new CancellationTokenSource(defaultTimeOutMilliseconds))
                {
                    return await httpClient.GetAsync(address, cancel.Token);
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
                using (var httpClient = new InsecureHttpClient().Client)
                using (var cancel = new CancellationTokenSource(defaultTimeOutMilliseconds))
                {
                    if (headers != null)
                    {
                        foreach (var header in headers)
                        {
                            if (httpClient.DefaultRequestHeaders.Contains(header.Key))
                                httpClient.DefaultRequestHeaders.Remove(header.Key);

                            httpClient.DefaultRequestHeaders.Add(header.Key, header.Value);
                        }
                    }

                    return await httpClient.GetAsync(address, cancel.Token);
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
                using (var httpClient = new HttpClient())
                using (var cancel = new CancellationTokenSource(timeOutMilliseconds))
                {
                    httpClient.DefaultRequestHeaders.Authorization = authHeader;
                    httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                    return await httpClient.PostAsync(
                        address,
                        new StringContent(postBody, Encoding.UTF8, "application/json"),
                        cancel.Token);
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
                using (var httpClient = new HttpClient())
                using (var cancel = new CancellationTokenSource(timeOutMilliseconds))
                {
                    StringContent httpContent = null;
                    if (headers != null && headers.ContainsKey("content-type"))
                    {
                        httpContent = new StringContent(postBody, Encoding.UTF8, headers["content-type"]);
                        headers.Remove("content-type");
                    }
                    else
                    {
                        httpContent = new StringContent(postBody, Encoding.UTF8, "application/json");
                    }

                    if (headers != null)
                    {
                        foreach (var header in headers)
                        {
                            if (httpClient.DefaultRequestHeaders.Contains(header.Key))
                                httpClient.DefaultRequestHeaders.Remove(header.Key);

                            httpClient.DefaultRequestHeaders.Add(header.Key, header.Value);
                        }
                    }

                    return await httpClient.PostAsync(
                        address,
                        httpContent,
                        cancel.Token);
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
                using (var httpClient = new HttpClient())
                using (var cancel = new CancellationTokenSource(timeOutMilliseconds))
                {
                    MultipartFormDataContent form = new MultipartFormDataContent();
                    form.Add(new ByteArrayContent(data, 0, data.Length));

                    return await httpClient.PostAsync(
                        address,
                        form,
                        cancel.Token);
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
                using (var httpClient = new InsecureHttpClient().Client)
                using (var cancel = new CancellationTokenSource(timeOutMilliseconds))
                {
                    var httpContent = new StringContent(body, Encoding.UTF8, "application/json");
                    if (headers != null)
                    {
                        foreach (var header in headers)
                        {
                            if (httpClient.DefaultRequestHeaders.Contains(header.Key))
                                httpClient.DefaultRequestHeaders.Remove(header.Key);

                            httpClient.DefaultRequestHeaders.Add(header.Key, header.Value);
                        }
                    }

                    var response = await httpClient.PutAsync(
                        address,
                        httpContent,
                        cancel.Token);

                    return response;
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
                using (var httpClient = new HttpClient())
                {
                    var request = new HttpRequestMessage(method, uri);
                    if (userName != null && password != null)
                    {
                        string encodedAuth = Convert.ToBase64String(Encoding.GetEncoding("ISO-8859-1").GetBytes(userName + ":" + password));
                        request.Headers.Add("Authorization", "Basic " + encodedAuth);
                    }
                    request.Content = new StringContent("");
                    request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/x-www-form-urlencoded");
                    request.Content.Headers.ContentLength = 0;

                    var response = await httpClient.SendAsync(request);
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
                using (var httpClient = new HttpClient())
                using (var cancel = new CancellationTokenSource(timeOutMilliseconds))
                {
                    if (!String.IsNullOrEmpty(token))
                        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

                    return await httpClient.GetAsync(uri, cancel.Token);
                }
            }
            catch (Exception ex)
            {
                throw new Exception("Error in Web.GetResponseAsync", ex);
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
