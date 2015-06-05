﻿using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace Birch.Swagger.ProxyGenerator
{
    public abstract class BaseProxy
    {
        private readonly Uri _baseUrl;

        protected BaseProxy(Uri baseUrl)
        {
            _baseUrl = baseUrl;
        }

        protected HttpClient BuildHttpClient()
        {
            var httpClient = new HttpClient();
            httpClient.BaseAddress = _baseUrl;
            return httpClient;
        }

        public async Task EnsureSuccessStatusCodeAsync(HttpResponseMessage response)
        {
            if (response.IsSuccessStatusCode)
            {
                return;
            }

            try
            {
                var content = await response.Content.ReadAsStringAsync().ConfigureAwait(false); ;
                throw new SimpleHttpResponseException(response.StatusCode, content);
            }
            finally
            {
                if (response.Content != null)
                    response.Content.Dispose();
            }
        }
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

