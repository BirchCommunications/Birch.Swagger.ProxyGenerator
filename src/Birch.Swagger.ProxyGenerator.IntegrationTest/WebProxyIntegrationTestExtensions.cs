using System;
using System.Collections.Generic;
using System.Net;
using System.Web.Http;
using Owin;

namespace Birch.Swagger.ProxyGenerator.IntegrationTest
{
    public static class WebProxyIntegrationTestExtensions
    {
        public static T AddBeforeRequestAction<T>(this T proxy, Action<BaseProxy.BeforeRequestActionArgs> action)
            where T : IIntegrationTestWebProxy
        {
            proxy.BeforeRequestActions.Add(action);
            return proxy;
        }

        public static T AddBeforeRequestActions<T>(this T proxy, IEnumerable<Action<BaseProxy.BeforeRequestActionArgs>> actions)
            where T : IIntegrationTestWebProxy
        {
            proxy.BeforeRequestActions.AddRange(actions);
            return proxy;
        }

        public static T AddAfterRequestAction<T>(this T proxy, Action<BaseProxy.IWebProxyResponse> action)
            where T : IIntegrationTestWebProxy
        {
            proxy.AfterRequestActions.Add(action);
            return proxy;
        }

        public static T AddAfterRequestActions<T>(this T proxy, IEnumerable<Action<BaseProxy.IWebProxyResponse>> actions)
            where T : IIntegrationTestWebProxy
        {
            proxy.AfterRequestActions.AddRange(actions);
            return proxy;
        }

        public static T AddGlobalBeforeRequestAction<T>(this T proxy, Action<BaseProxy.BeforeRequestActionArgs> action)
            where T : IIntegrationTestWebProxy
        {
            proxy.GlobalBeforeRequestActions.Add(action);
            return proxy;
        }

        public static T AddGlobalBeforeRequestActions<T>(this T proxy, IEnumerable<Action<BaseProxy.BeforeRequestActionArgs>> actions)
            where T : IIntegrationTestWebProxy
        {
            proxy.GlobalBeforeRequestActions.AddRange(actions);
            return proxy;
        }

        public static T AddGlobalAfterRequestAction<T>(this T proxy, Action<BaseProxy.IWebProxyResponse> action)
            where T : IIntegrationTestWebProxy
        {
            proxy.GlobalAfterRequestActions.Add(action);
            return proxy;
        }

        public static T AddGlobalAfterRequestActions<T>(this T proxy, IEnumerable<Action<BaseProxy.IWebProxyResponse>> actions)
            where T : IIntegrationTestWebProxy
        {
            proxy.GlobalAfterRequestActions.AddRange(actions);
            return proxy;
        }

        /// <summary>
        /// Configures the web proxy test server.
        /// </summary>
        /// <param name="proxy">The web proxy.</param>
        /// <param name="startupAction">The web proxy owin startup action.</param>
        /// <returns></returns>
        public static T ConfigureTestServer<T>(this T proxy, Action<IAppBuilder, HttpConfiguration> startupAction)
            where T : IIntegrationTestWebProxy
        {
            proxy.TestServer = WebProxyTestServerHelpers.GetTestServer(startupAction);
            return proxy;
        }

        /// <summary>
        /// Overrides the expected HTTP status code for the next request.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="proxy">The web proxy.</param>
        /// <param name="statusCode">The expected status code.</param>
        /// <returns></returns>
        public static T OverrideExpectedHttpStatus<T>(this T proxy, HttpStatusCode statusCode)
            where T : IIntegrationTestWebProxy
        {
            proxy.ExpectedHttpStatusCodeOverride = statusCode;
            return proxy;
        }

        /// <summary>
        /// Bypass action method verification for the next request.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="proxy">The proxy.</param>
        /// <returns></returns>
        public static T ByPassActionMethodVerification<T>(this T proxy)
            where T : IIntegrationTestWebProxy
        {
            proxy.ActionMethodVerificationByPassed = true;
            return proxy;
        }

        /// <summary>
        /// Bypass the no response HTTP status verification for the next request.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="proxy">The proxy.</param>
        /// <returns></returns>
        public static T BypassNoResponseHttpStatusVerification<T>(this T proxy)
            where T : IIntegrationTestWebProxy
        {
            proxy.NoResponseHttpStatusVerificationBypassed = true;
            return proxy;
        }

        /// <summary>
        /// Bypass the response body required verification for the next request.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="proxy">The proxy.</param>
        /// <returns></returns>
        public static T BypassResponseBodyRequiredVerification<T>(this T proxy)
            where T : IIntegrationTestWebProxy
        {
            proxy.ResponseBodyRequiredBypassed = true;
            return proxy;
        }

        /// <summary>
        /// Bypass the response should not be of type object verification for the next request.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="proxy">The proxy.</param>
        /// <returns></returns>
        public static T BypassResponseShouldNotBeOfTypeObject<T>(this T proxy)
            where T : IIntegrationTestWebProxy
        {
            proxy.ResponseShouldNotBeOfTypeObjectBypassed = true;
            return proxy;
        }

        internal static T GetWebProxy<T>()
            where T : IIntegrationTestWebProxy
        {
            var webProxy = (T)Activator.CreateInstance(typeof(T), new Uri("http://fake.com"));
            return webProxy;
        }
    }
}
