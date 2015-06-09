:: arguments
if "%~1"=="" (
  set version="1.0.0"
) else (
  set version=%1
)

:: Jump up a directory
cd ..

:: call build cmd located in root
call build.cmd

:: Jump back to build folder
cd build

:: remove all nupkg files
erase *.nupkg

:: pack everything in build folder
for /f %%l in ('dir /b *.nuspec') do (
    nuget pack %%l -version %version%
)

echo --------------------------------------------------------
echo Build NuGet Package Script completed...