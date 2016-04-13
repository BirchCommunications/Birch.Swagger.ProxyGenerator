using System;
using Xunit.Abstractions;

namespace Birch.Swagger.ProxyGenerator.IntegrationTest.XUnit
{
    public static class XUnitWebProxyExtensions
    {
        /// <summary>
        /// Registers the test output helper.
        /// </summary>
        /// <param name="proxy">The proxy.</param>
        /// <param name="testOutputHelper">The XUnit ITestOutputHelper.</param>
        /// <returns></returns>
        public static T RegisterXUnitOutputHelper<T>(this T proxy, ITestOutputHelper testOutputHelper)
            where T : IIntegrationTestWebProxy
        {
            return proxy.AddGlobalBeforeRequestAction(actionArgs =>
            {
                testOutputHelper.WriteLine($"[{DateTime.Now}] Calling test server: {actionArgs.Method} \"{actionArgs.Uri}\".");
            })
            .AddGlobalAfterRequestAction(webProxyResponse =>
            {
                testOutputHelper.WriteLine($"[{DateTime.Now}] Completed in {webProxyResponse.RequestDuration} with status \"{webProxyResponse.Response.StatusCode}\"");
            });
        }
    }
}
