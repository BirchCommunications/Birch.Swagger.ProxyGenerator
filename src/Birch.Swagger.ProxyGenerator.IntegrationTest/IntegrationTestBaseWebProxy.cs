using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Owin.Testing;
using Newtonsoft.Json;
using Shouldly;

namespace Birch.Swagger.ProxyGenerator.IntegrationTest
{
    public abstract class BaseIntegrationTestWebProxy :
        BaseProxy, IIntegrationTestWebProxy
    {
        public HttpStatusCode ExpectedHttpStatusCode { get; set; } = HttpStatusCode.OK;
        public bool ActionMethodVerificationByPassed { get; set; }
        public HttpStatusCode? ExpectedHttpStatusCodeOverride { get; set; }
        public bool NoResponseHttpStatusVerificationBypassed { get; set; }
        public bool ResponseBodyRequiredBypassed { get; set; }
        public bool ResponseShouldNotBeOfTypeObjectBypassed { get; set; }
        
        protected BaseIntegrationTestWebProxy(Uri baseUrl) : base(baseUrl)
        {
        }

        protected override HttpClient BuildHttpClient()
        {
            var buildHttpClient = TestServer.HttpClient;
            if (buildHttpClient == null)
            {
                throw new ArgumentNullException(nameof(TestServer));
            }
            return buildHttpClient;
        }

        public override async Task BeforeRequestAsync(BeforeRequestActionArgs actionArgs)
        {
            // Pre-Request convention verification
            RouteVerification(actionArgs);
            ActionMethodVerification(actionArgs);
            SetExpectedHttpStatusCode(actionArgs);

            await base.BeforeRequestAsync(actionArgs);
        }

        public override async Task AfterRequestAsync(WebProxyResponse webProxyResponse)
        {
            await base.AfterRequestAsync(webProxyResponse);

            // Post-Request convention verification
            HttpStatusCodeVerification(webProxyResponse);
            ProxyOutputTypeVerification(webProxyResponse);

            ResetOverrides();
        }

        public virtual TestServer TestServer { get; set; }

        protected virtual void ResetOverrides()
        {
            // reset overrides
            ActionMethodVerificationByPassed = false;
            ExpectedHttpStatusCodeOverride = null;
            NoResponseHttpStatusVerificationBypassed = false;
            ResponseBodyRequiredBypassed = false;
        }

        private void ProxyOutputTypeVerification(WebProxyResponse webProxyResponse)
        {
            if (!ResponseBodyRequiredBypassed)
            {
                const string errorMessage = "You should generally have a response body returned for all calls" +
                                            " you can bypass this verification for actions that do not have a response body by" +
                                            " using the .BypassResponseBodyRequiredVerification() extension method on the web proxy.";
                webProxyResponse.ExpectedResponseType.ShouldNotBeNull(errorMessage);
            }

            if (webProxyResponse.ExpectedResponseType == null && !NoResponseHttpStatusVerificationBypassed)
            {
                const HttpStatusCode expectedHttpStatusCode = HttpStatusCode.NoContent;
                const string errorMessage = "If you are not returning content the expected status code is NoContent," +
                                            " you can bypass the expected HTTP status code for actions that do not have a response by" +
                                            " using the .BypassNoResponseHttpStatusVerification() extension method on the web proxy.";
                webProxyResponse.Response.StatusCode.ShouldBe(expectedHttpStatusCode, errorMessage);
            }

            if (!ResponseShouldNotBeOfTypeObjectBypassed)
            {
                const string errorMessage = "The web proxy should not be returning object for this method," +
                                            " some types are not supported by swagger or there was no ResponseType attribute on the controller" +
                                            " you can bypass the response should not be of type object by" +
                                            " using the .BypassResponseShouldNotBeOfTypeObject() extension method on the web proxy.";
                var expected = typeof(object);
                webProxyResponse.ExpectedResponseType.ShouldNotBe(expected, errorMessage);
            }
        }

        #region REST convention verification methods

        private void SetExpectedHttpStatusCode(BeforeRequestActionArgs actionArgs)
        {
            if (ExpectedHttpStatusCodeOverride != null)
            {
                return;
            }
            var createVerbs = new[] { "Add", "Create", "Make", "New" };
            var actionNameLower = actionArgs.ActionName.ToLowerInvariant();

            if (createVerbs.Any(x => actionNameLower.StartsWith(x.ToLowerInvariant())
                                     || actionNameLower.EndsWith(x.ToLowerInvariant())))
            {
                ExpectedHttpStatusCodeOverride = HttpStatusCode.Created;
            }
        }

        private void HttpStatusCodeVerification(WebProxyResponse webProxyResponse)
        {
            HttpStatusCode? noContentOverride = null;
            if (webProxyResponse.ExpectedResponseType == null)
            {
                noContentOverride = HttpStatusCode.NoContent;
            }
            var expectedHttpStatusCode = noContentOverride ?? ExpectedHttpStatusCodeOverride ?? ExpectedHttpStatusCode;
            var errorMessage = new StringBuilder();
            errorMessage.Append("If you are sure this is what you want, you can override the default expected HTTP status code by");
            errorMessage.Append(" using the .OverrideExpectedHttpStatus(HttpStatusCode) extension method on the web proxy.");
            if (webProxyResponse.Exception != null)
            {
                ErrorViewModel exception = JsonConvert.DeserializeObject<ErrorViewModel>(webProxyResponse.Exception.Message);
                if (exception != null)
                {
                    errorMessage.AppendLine();
                    errorMessage.AppendLine();
                    errorMessage.AppendLine("Exception Details:");
                    errorMessage.AppendLine(exception.Message);
                    errorMessage.AppendLine();
                    errorMessage.AppendLine(exception.StackTrace);
                }
            }
            webProxyResponse.Response.StatusCode.ShouldBe(expectedHttpStatusCode, errorMessage.ToString());
        }

        [SuppressMessage("ReSharper", "ClassNeverInstantiated.Local")]
        [SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Local")]
        private class ErrorViewModel
        {
            public string StackTrace { get; set; }
            public string Message { get; set; }
        }

        private static void RouteVerification(BeforeRequestActionArgs actionArgs)
        {
            var uri = actionArgs.Uri;

            // global check for lowercase routes
            uri.ShouldBe(uri.ToLowerInvariant(),
                "Route URIs should be all lowercase.  Multiple words should be seperated with a dash (-).");
        }

        private void ActionMethodVerification(BeforeRequestActionArgs actionArgs)
        {
            if (ActionMethodVerificationByPassed)
            {
                return;
            }

            // verify http method against action names
            var createVerbs = new[] { "Add", "Create", "Make", "New" };
            var updateVerbs = new[] { "Update", "Edit", "Change", "Modify" };
            var deleteVerbs = new[] { "Delete", "Remove" };

            var actionName = actionArgs.ActionName;
            var webProxyHttpMethod = actionArgs.Method;
            ActionMethodVerification(createVerbs, actionName, webProxyHttpMethod, "POST");
            ActionMethodVerification(updateVerbs, actionName, webProxyHttpMethod, "PUT");
            ActionMethodVerification(deleteVerbs, actionName, webProxyHttpMethod, "DELETE");
        }

        private static void ActionMethodVerification(string[] verbsToCheck, string actionName, string webProxyHttpMethod, string expectedHttpMethod)
        {
            expectedHttpMethod = expectedHttpMethod.ToUpperInvariant();
            var actionNameLower = actionName.ToLowerInvariant();

            foreach (var verb in verbsToCheck)
            {
                // action name does not start with or end with 
                // a verb we want to check continues
                var verbLower = verb.ToLowerInvariant();
                if (!actionNameLower.StartsWith(verbLower) && !actionNameLower.EndsWith(verbLower))
                {
                    continue;
                }

                var customMessage = new StringBuilder();
                customMessage.AppendLine(
                    "Actions starting or ending with the following verbs should most likely be " +
                    $"{expectedHttpMethod}: {string.Join(", ", verbsToCheck)}");
                customMessage.AppendLine(
                    "Consider updating the name of this action or changing the HTTP method used.");
                customMessage.AppendLine();
                customMessage.AppendLine(
                    "If you are sure this is really what you want, you can override this check" +
                    " using the .ByPassActionMethodVerification() extension method on the WebProxy.");

                webProxyHttpMethod.ShouldBe(expectedHttpMethod, customMessage.ToString());
            }
        }

        #endregion
    }
}