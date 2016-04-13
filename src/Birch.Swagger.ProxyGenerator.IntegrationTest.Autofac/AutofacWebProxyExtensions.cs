using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Http;
using Autofac;
using Microsoft.Owin.Testing;
using Owin;

namespace Birch.Swagger.ProxyGenerator.IntegrationTest.Autofac
{
    public static class AutofacWebProxyExtensions
    {
        /// <summary>
        /// Configures the web proxy test server.
        /// </summary>
        /// <param name="proxy">The web proxy.</param>
        /// <param name="startupAction">The web proxy owin startup action.</param>
        /// <returns></returns>
        public static T ConfigureTestServer<T>(this T proxy, Action<IAppBuilder, HttpConfiguration, Action<ContainerBuilder>> startupAction)
            where T : IAutofacIntegrationTestWebProxy
        {
            proxy.StartUpAction = startupAction;
            return proxy;
        }
        /// <summary>
        /// Adds default fake object to the WebProxy for the lifetime of the proxy
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="proxy">The proxy.</param>
        /// <param name="fakeObject">The fake object.</param>
        /// <returns></returns>
        public static T AddDefaultFake<T>(this T proxy, object fakeObject)
            where T : IAutofacIntegrationTestWebProxy
        {
            proxy.DefaultFakes.Add(fakeObject);
            return proxy;
        }

        /// <summary>
        /// Adds default fake objects to the WebProxy for the lifetime of the proxy
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="proxy">The proxy.</param>
        /// <param name="fakeObjects">The fake objects.</param>
        /// <returns></returns>
        public static T AddDefaultFakes<T>(this T proxy, IEnumerable<object> fakeObjects)
            where T : IAutofacIntegrationTestWebProxy
        {
            proxy.DefaultFakes.AddRange(fakeObjects);
            return proxy;
        }
        /// <summary>
        /// Adds fake object to the WebProxy for the lifetime of the proxy
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="proxy">The proxy.</param>
        /// <param name="fakeObject">The fake object.</param>
        /// <returns></returns>
        public static T AddFake<T>(this T proxy, object fakeObject)
            where T : IAutofacIntegrationTestWebProxy
        {
            proxy.FakedObjects.Add(fakeObject);
            return proxy;
        }

        /// <summary>
        /// Adds fake objects to the WebProxy for the lifetime of the proxy
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="proxy">The proxy.</param>
        /// <param name="fakeObjects">The fake objects.</param>
        /// <returns></returns>
        public static T AddFakes<T>(this T proxy, IEnumerable<object> fakeObjects)
            where T : IAutofacIntegrationTestWebProxy
        {
            proxy.FakedObjects.AddRange(fakeObjects);
            return proxy;
        }

        /// <summary>
        /// Overrides the fake objects used by the WebProxy for the next request proxy.
        /// If default fakes have been specified these will be included also.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="proxy">The proxy.</param>
        /// <param name="fakeObjects">The fake objects.</param>
        /// <returns></returns>
        public static T OverrideFakes<T>(this T proxy, params object[] fakeObjects)
            where T : IAutofacIntegrationTestWebProxy
        {
            proxy.TestServerOverride = proxy.GetTestServerWithFakes(proxy.DefaultFakes.Concat(fakeObjects).ToList());
            return proxy;
        }

        internal static TestServer GetTestServerWithFakes<T>(this T input, List<object> objects = null)
            where T : IAutofacIntegrationTestWebProxy
        {
            var startupAction = input.StartUpAction;
            if (startupAction == null)
            {
                throw new ArgumentNullException(nameof(startupAction));
            }

            var fakeObjects = input.DefaultFakes.Concat(objects ?? input.FakedObjects);
            var key = fakeObjects.GetHashCode();

            var testServer = TestServer.Create(appBuilder =>
            {
                startupAction.Invoke(appBuilder, new HttpConfiguration(), containerBuilder =>
                {
                    // register user porvided fakes
                    foreach (var fakeObject in fakeObjects)
                    {
                        containerBuilder.RegisterInstance(fakeObject).AsImplementedInterfaces();
                    }
                });
            });

            return WebProxyTestServerHelpers.TestServerDictionary.GetOrAdd(key, testServer);
        }
    }
}
