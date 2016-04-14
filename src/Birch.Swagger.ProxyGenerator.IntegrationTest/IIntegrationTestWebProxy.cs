using System;
using System.Collections.Generic;
using System.Net;
using Microsoft.Owin.Testing;

namespace Birch.Swagger.ProxyGenerator.IntegrationTest
{
    public interface IIntegrationTestWebProxy
    {
        TestServer TestServer { get; set; }
        HttpStatusCode ExpectedHttpStatusCode { get; set; }

        // Bypasses and overrrides
        HttpStatusCode? ExpectedHttpStatusCodeOverride { get; set; }
        bool ActionMethodVerificationByPassed { get; set; }
        bool NoResponseHttpStatusVerificationBypassed { get; set; }
        bool ResponseBodyRequiredBypassed { get; set; }
        bool ResponseShouldNotBeOfTypeObjectBypassed { get; set; }

        // Actions
        List<Action<IntegrationTestBaseProxy.BeforeRequestActionArgs>> BeforeRequestActions { get; set; }
        List<Action<IntegrationTestBaseProxy.BeforeRequestActionArgs>> GlobalBeforeRequestActions { get; set; }
        List<Action<IntegrationTestBaseProxy.IWebProxyResponse>> AfterRequestActions { get; set; }
        List<Action<IntegrationTestBaseProxy.IWebProxyResponse>> GlobalAfterRequestActions { get; set; }
    }
}