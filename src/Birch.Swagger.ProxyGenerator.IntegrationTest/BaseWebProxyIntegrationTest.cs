namespace Birch.Swagger.ProxyGenerator.IntegrationTest
{
    public abstract class BaseWebProxyIntegrationTest<TWebProxy>
        where TWebProxy : IIntegrationTestWebProxy
    {
        public TWebProxy TestWebProxy { get; set; }

        protected BaseWebProxyIntegrationTest()
        {
            TestWebProxy = WebProxyIntegrationTestExtensions.GetWebProxy<TWebProxy>();
        }
    }
}
