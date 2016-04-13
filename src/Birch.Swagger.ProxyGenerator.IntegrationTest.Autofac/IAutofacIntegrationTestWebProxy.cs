using System;
using System.Collections.Generic;
using System.Web.Http;
using Autofac;
using Microsoft.Owin.Testing;
using Owin;

namespace Birch.Swagger.ProxyGenerator.IntegrationTest.Autofac
{
    public interface IAutofacIntegrationTestWebProxy : IIntegrationTestWebProxy
    {
        List<object> DefaultFakes { get; set; }
        List<object> FakedObjects { get; set; }
        Action<IAppBuilder, HttpConfiguration, Action<ContainerBuilder>> StartUpAction { get; set; }
        TestServer TestServerOverride { get; set; }
    }
}