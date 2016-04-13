using System;
using System.Diagnostics;
using System.Linq;
using System.Reactive.Linq;
using System.Text;
using Birch.Swagger.ProxyGenerator.IntegrationTest.Autofac;
using Serilog;
using Serilog.Events;

namespace Birch.Swagger.ProxyGenerator.IntegrationTest.Serilog
{
    public static class SerilogWebProxyExtensions
    {
        static readonly Func<LogEvent, string> DefaultLoggingFunc = logEvent =>
        {
            string logEventTitle = $"[{DateTime.Now}] EVENT LOGGED: {logEvent.Level}";
            var divider = new string('-', logEventTitle.Length);

            var eventMessage = new StringBuilder();
            eventMessage.AppendLine(logEventTitle);
            eventMessage.AppendLine(divider);
            eventMessage.AppendLine("Title");
            eventMessage.AppendLine($"\t{logEvent.RenderMessage()}");
            eventMessage.AppendLine();

            if (logEvent.Properties.Any())
            {
                eventMessage.AppendLine("Properties");
                foreach (var keyValuePair in logEvent.Properties)
                {
                    eventMessage.AppendLine($"\t{keyValuePair.Key}: {keyValuePair.Value}");
                }
            }

            if (logEvent.Exception != null)
            {
                eventMessage.AppendLine();
                eventMessage.AppendLine("Has Exception");
                eventMessage.AppendLine($"\tType: {logEvent.Exception.GetType()}");
                eventMessage.AppendLine($"\tMessage: {logEvent.Exception.Message}");
            }

            eventMessage.AppendLine(divider);
            return eventMessage.ToString();
        };
        private static readonly Action<string> DefaultLoggingOutput = s => Debug.WriteLine(s);
        
        public static T ConfigureLogging<T>(this T proxy, Action<string> loggingOutputAction = null, Func<LogEvent, string> loggingFunc = null)
            where T : IAutofacIntegrationTestWebProxy
        {
            var action = loggingOutputAction ?? DefaultLoggingOutput;
            var func = loggingFunc ?? DefaultLoggingFunc;

            var loggerConfiguration = new LoggerConfiguration()
                .WriteTo.Observers(
                    logEvents => logEvents.Do(
                        logEvent =>
                        {
                            action.Invoke(func.Invoke(logEvent));
                        })
                        .Subscribe())
                .Enrich.WithProperty("ApplicationName", "IntegrationTest")
                .Enrich.WithProperty("ApplicationVersion", "0.0.0");

            proxy.AddDefaultFake(loggerConfiguration.CreateLogger());
            return proxy;
        }
    }
}