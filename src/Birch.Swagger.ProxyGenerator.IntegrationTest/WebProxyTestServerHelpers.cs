using System;
using System.Collections.Concurrent;
using System.Web.Http;
using Microsoft.Owin.Testing;
using Owin;

namespace Birch.Swagger.ProxyGenerator.IntegrationTest
{
    public static class WebProxyTestServerHelpers
    {
        public static readonly ConcurrentDictionary<int, TestServer> TestServerDictionary =
            new ConcurrentDictionary<int, TestServer>();

        internal static TestServer GetTestServer(Action<IAppBuilder, HttpConfiguration> startupAction)
        {
            if (startupAction == null)
            {
                throw new ArgumentNullException(nameof(startupAction));
            }

            return TestServerDictionary.GetOrAdd(0, TestServer.Create(appBuilder =>
            {
                startupAction.Invoke(appBuilder, new HttpConfiguration());
            }));
        }
    }
}