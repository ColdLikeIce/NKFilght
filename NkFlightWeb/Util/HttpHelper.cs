using DtaAccess.Domain.Models;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.OpenApi.Models;
using Microsoft.Playwright;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NkFlightWeb.Service.Dto;
using Serilog;
using System.Diagnostics.Metrics;
using System.Drawing.Drawing2D;
using System.Net;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text;

namespace NkFlightWeb.Util
{
    public class HttpHelper
    {
        public static bool AcceptAllCertifications(object sender, System.Security.Cryptography.X509Certificates.X509Certificate certification, System.Security.Cryptography.X509Certificates.X509Chain chain, System.Net.Security.SslPolicyErrors sslPolicyErrors)
        {
            return true;
        }

        private static readonly HttpClient HttpClient;

        static HttpHelper()
        {
            HttpClient = new HttpClient();
            HttpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/102.0.0.0 Safari/537.36");
        }

        public static string HttpPostFromData(string url, List<KeyValuePair<string, string>> paraList, string cookie = "", int timeOut = 30, Dictionary<string, string> headers = null)
        {
            using (HttpClient client = new HttpClient(new HttpClientHandler() { UseCookies = false }))//若想手动设置Cookie则必须设置UseCookies = false
            {
                MultipartFormDataContent postContent = new MultipartFormDataContent();
                string boundary = string.Format("--{0}", DateTime.Now.Ticks.ToString("x"));
                if (headers != null)
                {
                    foreach (var header in headers)
                        client.DefaultRequestHeaders.Add(header.Key, header.Value);
                }
                else
                {
                    client.DefaultRequestHeaders.Clear();
                }
                // postContent.Headers.ContentType = MediaTypeHeaderValue.Parse("multipart/form-data");
                postContent.Headers.Add("ContentType", $"multipart/form-data, boundary={boundary}");
                if (string.IsNullOrWhiteSpace(cookie))
                {
                    postContent.Headers.Add("Cookie", cookie);
                }
                foreach (var keyValuePair in paraList)
                {
                    postContent.Add(new StringContent(keyValuePair.Value),
                        String.Format("\"{0}\"", keyValuePair.Key));
                }
                var response = client.PostAsync(url, postContent).Result;
                return response.Content.ReadAsStringAsync().Result;
            }
        }

        /// <summary>
        /// 发起POST异步请求
        /// </summary>
        /// <param name="url"></param>
        /// <param name="postData"></param>
        /// <param name="contentType">application/xml、application/json、application/text、application/x-www-form-urlencoded</param>
        /// <param name="headers">填充消息头</param>
        /// <returns></returns>
        public static async Task<string> HttpPostAsync(string url, string postData, string contentType, int timeOut = 30, Dictionary<string, string>? headers = null, string cookie = "")
        {
            postData = postData ?? "";
            using (HttpClient client = new HttpClient(new HttpClientHandler() { UseCookies = false }))
            {
                client.Timeout = new TimeSpan(0, 0, timeOut);
                if (headers != null)
                {
                    foreach (var header in headers)
                    {
                        if (header.Key.Contains("content-type"))
                        {
                            continue;
                        }
                        client.DefaultRequestHeaders.Add(header.Key, header.Value);
                    }
                }
                if (!string.IsNullOrWhiteSpace(cookie))
                {
                    client.DefaultRequestHeaders.Add("Cookie", cookie);
                }
                using (HttpContent httpContent = new StringContent(postData, Encoding.UTF8))
                {
                    if (contentType != null)
                        httpContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);

                    HttpResponseMessage response = HttpClient.PostAsync(url, httpContent).Result;
                    return response.Content.ReadAsStringAsync().Result;
                }
            }
        }

        /// <summary>
        /// 发起POST同步请求
        ///
        /// </summary>
        /// <param name="url"></param>
        /// <param name="postData"></param>
        /// <param name="contentType">application/xml、application/json、application/text、application/x-www-form-urlencoded</param>
        /// <param name="headers">填充消息头</param>
        /// <returns></returns>
        public static string HttpPost(string url, string postData, string contentType, int timeOut = 30, Dictionary<string, string>? headers = null, string cookie = "")
        {
            postData = postData ?? "";
            using (HttpContent httpContent = new StringContent(postData, Encoding.Default))
            {
                return HttpPost(url, httpContent, contentType, timeOut, headers, cookie);
            }
        }

        public static string HttpPost(string url, HttpContent postData, string contentType, int timeOut = 30, Dictionary<string, string>? headers = null, string cookie = "")
        {
            using (HttpClient httpClient = new HttpClient(new HttpClientHandler() { UseCookies = false }))
            {
                if (headers != null)
                {
                    httpClient.DefaultRequestHeaders.Clear();
                    foreach (var header in headers)
                    {
                        if (header.Key.Contains("content-type"))
                        {
                            continue;
                        }
                        httpClient.DefaultRequestHeaders.Add(header.Key, header.Value);
                    }
                }
                else
                {
                    httpClient.DefaultRequestHeaders.Clear();
                }
                if (!string.IsNullOrEmpty(contentType))
                    postData.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);

                if (!string.IsNullOrWhiteSpace(cookie))
                {
                    httpClient.DefaultRequestHeaders.Add("Cookie", cookie);
                }

                httpClient.Timeout = new TimeSpan(0, 0, timeOut);
                HttpResponseMessage response = httpClient.PostAsync(url, postData).Result;
                foreach (var item in response.Headers)
                {
                    if (item.Key == "Set-Cookie")
                    {
                        var coo = item.Value;
                        foreach (var key in coo)
                        {
                            Log.Information($"{key}");
                        }
                    }
                }
                return response.Content.ReadAsStringAsync().Result;
            }
        }

        public static HttpResponseMessage HttpDelete(string url, int timeOut = 30)
        {
            HttpResponseMessage response = HttpClient.DeleteAsync(url).Result;
            return response;
        }

        public static string HttpPut(string url, string putData, int timeOut = 30)
        {
            using (HttpContent httpContent = new StringContent(putData, Encoding.UTF8))
            {
                HttpResponseMessage response = HttpClient.PutAsync(url, httpContent).Result;
                return response.Content.ReadAsStringAsync().Result;
            }
        }

        public static string PostAjaxData(string url, string param, Encoding encoding, Dictionary<string, string>? headers = null)
        {
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
            request.Method = "POST";
            request.ContentType = "application/json;charet=utf-8";
            request.Headers.Add("dataType", "json");
            request.Headers.Add("type", "post");
            foreach (var header in headers)
            {
                request.Headers.Add(header.Key, header.Value);
            }
            byte[] data = encoding.GetBytes(param);

            using (BinaryWriter reqStream = new BinaryWriter(request.GetRequestStream()))
            {
                reqStream.Write(data, 0, data.Length);
            }
            using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
            {
                StreamReader reader = new StreamReader(response.GetResponseStream(), encoding);
                string result = reader.ReadToEnd();
                return result;
            }
        }

        public static string Send(HttpRequestMessage httpRequestMessage)
        {
            using (HttpClient client = new HttpClient())
            {
                HttpResponseMessage response = client.SendAsync(httpRequestMessage).Result;
                return response.Content.ReadAsStringAsync().Result;
            }
        }

        /// <summary>
        /// 发起GET同步请求
        /// </summary>
        /// <param name="url"></param>
        /// <param name="headers"></param>
        /// <param name="contentType"></param>
        /// <returns></returns>
        public static string HttpGet(string url, Dictionary<string, string>? headers = null)
        {
            using (HttpClient client = new HttpClient())
            {
                if (headers != null)
                {
                    foreach (var header in headers)
                        client.DefaultRequestHeaders.Add(header.Key, header.Value);
                }
                HttpResponseMessage response = client.GetAsync(url).Result;
                return response.Content.ReadAsStringAsync().Result;
            }
        }

        /// <summary>
        /// 发起GET异步请求
        /// </summary>
        /// <param name="url"></param>
        /// <param name="headers"></param>
        /// <param name="contentType"></param>
        /// <returns></returns>
        public static async Task<string> HttpGetAsync(string url, Dictionary<string, string>? headers = null)
        {
            using (HttpClient client = new HttpClient())
            {
                if (headers != null)
                {
                    foreach (var header in headers)
                        client.DefaultRequestHeaders.Add(header.Key, header.Value);
                }
                HttpResponseMessage response = await client.GetAsync(url);
                return await response.Content.ReadAsStringAsync();
            }
        }

        public static string HttpPostRetry(string url, string postData, string contentType, int retry = 5, int timeOut = 30, Dictionary<string, string>? headers = null, string cookie = "")
        {
            HttpMessageHandler handler = new TimeoutHandler(retry, timeOut * 1000, false);
            using (HttpClient httpClient = new HttpClient(handler))
            {
                using (HttpContent httpContent = new StringContent(postData, Encoding.Default))
                {
                    if (headers != null)
                    {
                        foreach (var header in headers)
                        {
                            if (header.Key.Contains("content-type"))
                            {
                                continue;
                            }
                            httpClient.DefaultRequestHeaders.Add(header.Key, header.Value);
                        }
                    }
                    else
                    {
                        httpClient.DefaultRequestHeaders.Clear();
                    }
                    if (!string.IsNullOrEmpty(contentType))
                        httpContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);

                    if (!string.IsNullOrWhiteSpace(cookie))
                    {
                        httpClient.DefaultRequestHeaders.Add("Cookie", cookie);
                    }

                    // httpClient.Timeout = new TimeSpan(0, 0, timeOut);
                    HttpResponseMessage response = httpClient.PostAsync(url, httpContent).Result;
                    return response.Content.ReadAsStringAsync().Result;
                }
            }
        }
    }

    public class TimeoutHandler : DelegatingHandler
    {
        private int _timeout;
        private int _max_count;

        ///
        /// 超时重试
        ///
        ///重试次数
        ///超时时间
        public TimeoutHandler(int max_count = 3, int timeout = 5000, bool userCookies = false)
        {
            base.InnerHandler = new HttpClientHandler();
            if (!userCookies)
            {
                base.InnerHandler = new HttpClientHandler() { UseCookies = userCookies };
            }
            _timeout = timeout;
            _max_count = max_count;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            HttpResponseMessage response = null;
            for (int i = 1; i <= _max_count + 1; i++)
            {
                var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(_timeout);
                try
                {
                    response = await base.SendAsync(request, cts.Token);
                    Log.Error($"第{i}次请求返回状态码{response.IsSuccessStatusCode}");
                    if (response.IsSuccessStatusCode)
                    {
                        Log.Error($"第{i}次请求成功");
                        return response;
                    }
                }
                catch (Exception ex)
                {
                    //请求超时
                    if (ex is TaskCanceledException)
                    {
                        if (i > _max_count)
                        {
                            return new HttpResponseMessage(HttpStatusCode.OK)
                            {
                                Content = new StringContent("{\"code\":-1,\"data\":\"\",\"msg\":\"接口请求超时\"}", Encoding.UTF8, "text/json")
                            };
                        }
                        Log.Error($"接口第{i}次重新请求");
                    }
                    else
                    {
                        return new HttpResponseMessage(HttpStatusCode.OK)
                        {
                            Content = new StringContent("{\"code\":-1,\"data\":\"\",\"msg\":\"接口请求出错\"}", Encoding.UTF8, "text/json")
                        };
                    }
                }
            }
            return response;
        }
    }
}