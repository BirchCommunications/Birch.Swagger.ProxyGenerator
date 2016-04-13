using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Web.Http;
using Autofac;
using Microsoft.Owin.Testing;
using Owin;

namespace Birch.Swagger.ProxyGenerator.IntegrationTest.Autofac
{
    public class AutofacBaseIntegrationTestProxy :
        BaseIntegrationTestWebProxy, IAutofacIntegrationTestWebProxy
    {
        public AutofacBaseIntegrationTestProxy(Uri baseUrl) : base(baseUrl)
        {
        }

        /// <summary>
        /// The default fakes to be provided to every test server even if WebProxy.OverrideFakes() is specified.
        /// </summary>
        public List<object> DefaultFakes { get; set; } = new List<object>();

        /// <summary>
        /// Fake objects to be added for the lifetime of the proxy in addition to the DefaultFakes
        /// Calling WebProxy.OverrideFakes() will cause the next call to only use the DefaultFakes.
        /// </summary>
        public List<object> FakedObjects { get; set; } = new List<object>();

        /// <summary>
        /// Sets the start up action.
        /// </summary>
        public Action<IAppBuilder, HttpConfiguration, Action<ContainerBuilder>> StartUpAction { get; set; }

        /// <summary>
        /// Allows faked objects to be injected for the next webproxy call.
        /// By calling WebProxy.OverrideFakes() extension method.
        /// </summary>
        public TestServer TestServerOverride { get; set; }

        protected override void ResetOverrides()
        {
            base.ResetOverrides();
            TestServerOverride = null;
        }

        protected override HttpClient BuildHttpClient()
        {
            if (TestServer == null)
            {
                TestServer = this.GetTestServerWithFakes(DefaultFakes.Concat(FakedObjects).ToList());
            }

            var buildHttpClient = TestServerOverride?.HttpClient ?? TestServer?.HttpClient;
            if (buildHttpClient == null)
            {
                throw new ArgumentNullException(nameof(TestServer));
            }
            return buildHttpClient;
        }
    }
}