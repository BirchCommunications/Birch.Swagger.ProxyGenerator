using System.IO;
using System.Xml.Serialization;

namespace Birch.Swagger.ProxyGenerator.Generator
{
    public static class Settings
    {
        public static SwaggerApiProxySettings GetSettings(string path)
        {
            using (var settingStream = File.OpenRead(Path.Combine(path, "SwaggerApiProxy.tt.settings.xml")))
            {
                var serializer = new XmlSerializer(typeof(SwaggerApiProxySettings));
                return (SwaggerApiProxySettings)serializer.Deserialize(settingStream);
            }
        }
    }
}