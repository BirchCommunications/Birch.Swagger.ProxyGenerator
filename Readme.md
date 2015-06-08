Birch.Swagger.ProxyGenerator
===================================
This package will consume a swagger 2.0 spec and output a C# proxy that can be used to consume the swagger based service.
Most generators only allow you to point at a swagger.json file on disk or hosted on the web.  With the Birch.Swagger.ProxyGenerator you can specify a BaseUrl or an Owin Web API Assembly to generate the proxy from.

Note: If you specify an assembly BaseUrl will be ignored.


NuGet
-----------------------------------
https://www.nuget.org/packages/Birch.Swagger.ProxyGenerator/


General Setup
-----------------------------------
* Create a new project in your solution. For example: MyDomain.MyServiceName.Proxy
* Install the Birch.Swagger.ProxyGenerator nuget package
* Modify the example `Birch.Swagger.ProxyGenerator.config.json` (added to the project) to fit your requirements.
* A BeforeBuild task will be auto registered to generate the proxy code before build.
* Optional: Set this project to not build.  Generating the proxy will take some time depending on the number of endpoints and the power of your machine, it is recomended to only build the proxy when changes have been made to the source API.


General Usage
-----------------------------------
After you have completed the general setup you are ready to generate your proxy.
All you have to do to generate the proxy is build the project.


Advanced Usage
-----------------------------------
`Birch.Swagger.ProxyGenerator.exe` will look for its configuration file `Birch.Swagger.ProxyGenerator.config.json` in the folder it is executed in, you can override this behavior by providing command line switch `-SettingsFile <path to file>`.

Once you have specified a settings file, you can override specific settings by using its corresponding commandline switch.
*Available Switches:*
`-BaseDirectory <path to baseDir>`
`-BaseUrl <url>`
`-WebApiAssembly <path to file>`
`-WebApiConfig <path to file>`
`-ProxyOutputFile <path to file>`