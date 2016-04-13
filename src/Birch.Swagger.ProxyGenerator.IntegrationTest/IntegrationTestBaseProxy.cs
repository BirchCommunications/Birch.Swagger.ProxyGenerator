using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace Birch.Swagger.ProxyGenerator.IntegrationTest
{
    // TODO: should be kept in sync with proxy generator BaseProxy somehow
    public abstract class BaseProxy
    {
        public class WebProxyResponse<T> : WebProxyResponse
        {
            public T Body { get; set; }
        }

        public class WebProxyResponse
        {
            public HttpResponseMessage Response { get; set; }
            public TimeSpan RequestDuration { get; set; }
            public Type ExpectedResponseType { get; set; }
            public SimpleHttpResponseException Exception { get; set; }
        }

        public class BeforeRequestActionArgs
        {
            public string Uri { get; set; }
            public string ActionName { get; set; }
            public string Method { get; set; }
        }

        public List<Action<BeforeRequestActionArgs>> BeforeRequestActions { get; set; }
            = new List<Action<BeforeRequestActionArgs>>();
        public List<Action<BeforeRequestActionArgs>> GlobalBeforeRequestActions { get; set; }
            = new List<Action<BeforeRequestActionArgs>>();

        public List<Action<WebProxyResponse>> AfterRequestActions { get; set; }
            = new List<Action<WebProxyResponse>>();
        public List<Action<WebProxyResponse>> GlobalAfterRequestActions { get; set; }
            = new List<Action<WebProxyResponse>>();

        protected readonly Uri BaseUrl;

        /// <summary>
        /// Initializes a new instance of the <see cref="BaseProxy"/> class.
        /// </summary>
        /// <param name="baseUrl">The base URL.</param>
        protected BaseProxy(Uri baseUrl)
        {
            BaseUrl = baseUrl;
        }

        /// <summary>
        /// Builds the HTTP client.
        /// </summary>
        /// <returns></returns>
        protected virtual HttpClient BuildHttpClient()
        {
            var httpClient = new HttpClient
            {
                BaseAddress = BaseUrl
            };
            return httpClient;
        }

        /// <summary>
        /// Runs before the request asynchronous.
        /// </summary>
        /// <param name="actionArgs">The action arguments.</param>
        /// <returns></returns>
        public virtual Task BeforeRequestAsync(BeforeRequestActionArgs actionArgs)
        {
            foreach (var globalBeforeRequestAction in GlobalBeforeRequestActions)
            {
                globalBeforeRequestAction.Invoke(actionArgs);
            }

            foreach (var beforeRequestAction in BeforeRequestActions)
            {
                beforeRequestAction.Invoke(actionArgs);
            }
            BeforeRequestActions.Clear();
            return Task.FromResult(0);
        }

        /// <summary>
        /// Runs After the request asynchronous.
        /// </summary>
        /// <param name="webProxyResponse">The web proxy response.</param>
        /// <returns></returns>
        public virtual async Task AfterRequestAsync(WebProxyResponse webProxyResponse)
        {
            foreach (var globalAfterRequestAction in GlobalAfterRequestActions)
            {
                globalAfterRequestAction.Invoke(webProxyResponse);
            }

            foreach (var afterRequestAction in AfterRequestActions)
            {
                afterRequestAction.Invoke(webProxyResponse);
            }
            AfterRequestActions.Clear();

            var response = webProxyResponse.Response;
            if (response.IsSuccessStatusCode)
            {
                return;
            }

            try
            {
                var content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                webProxyResponse.Exception = new SimpleHttpResponseException(response.StatusCode, content);
            }
            finally
            {
                response.Content?.Dispose();
            }
        }

        /// <summary>
        /// Appends the query.
        /// </summary>
        /// <param name="currentUrl">The current URL.</param>
        /// <param name="paramName">Name of the parameter.</param>
        /// <param name="value">The value.</param>
        /// <returns></returns>
        protected string AppendQuery(string currentUrl, string paramName, string value)
        {
            if (currentUrl.Contains("?"))
            {
                currentUrl += $"&{paramName}={Uri.EscapeUriString(value)}";
            }
            else
            {
                currentUrl += $"?{paramName}={Uri.EscapeUriString(value)}";
            }
            return currentUrl;
        }

        public class SimpleHttpResponseException : Exception
        {
            public HttpStatusCode StatusCode { get; private set; }

            public SimpleHttpResponseException(HttpStatusCode statusCode, string content)
            : base(content)
            {
                StatusCode = statusCode;
            }
        }
    }
}