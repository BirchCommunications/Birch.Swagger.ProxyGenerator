Birch.Swagger.ProxyGenerator
===================================
This application will consume a swagger 2.0 spec and output a C# proxy that can be used to consume the swagger based service.
Most generators only allow you to point at a swagger.json file on disk or hosted on the web.  With the Birch.Swagger.ProxyGenerator you can specify a BaseUrl or an Owin Web API Assembly to generate the proxy from.

Note: If you specify an assembly BaseUrl will be ignored.

General Setup
-----------------------------------
* Create a new project in your solution. For example: MyDomain.MyServiceName.Proxy
* Install the Birch.Swagger.ProxyGenerator nuget package
* Modify the example `Birch.Swagger.ProxyGenerator.config.json` (added to the project in the ProxyGenerator folder) to fit your requirements.
* Set the proxy generator to run before build of your proxy project by right clicking on your project and going to properties.  Then put the follwing in your pre-build events:
`
"$(SolutionDir)packages\Birch.Swagger.ProxyGenerator.$version$\tools\ProxyGenerator\Birch.Swagger.ProxyGenerator.exe" -SettingsFile "$(projectDir)ProxyGenerator\Birch.Swagger.ProxyGenerator.config.json
` (Make sure to replace $version$ with the appropriate package version, also keep in mind if you update the package)
* Optional: Set this project to not build.  Generating the proxy will take some time depending on the number of endpoints and the power of your machine, it is recomended to only build the proxy when changes have been made to the source API.


General Usage
-----------------------------------
After you have completed the general setup you are ready to generate your proxy.
In the ProxyGenerator folder of your project you will find an executable named `Birch.Swagger.ProxyGenerator.exe`, running this executable will generate the proxy.
Optional: To clean up the Solution Explorer view of your proxy project, you can hide the `Birch.Swagger.ProxyGenerator.exe.config` and any  `*.dll` files in the ProxyGen folder.  Right click the file and choose `Exclude from project`


Advanced Usage
-----------------------------------
`Birch.Swagger.ProxyGenerator.exe` will look for its configuration file `Birch.Swagger.ProxyGenerator.config.json` in the folder it is executed in, you can override this behavior by providing command line switch `-SettingsFile <path to file>`.

Once you have specified a settings file, you can override specific settings by using its corresponding commandline switch.
*Available Switches:*
`-BaseUrl <url>`
`-WebApiAssembly <path to file>`
`-WebApiConfig <path to file>`
`-ProxyOutputFile <path to file>`