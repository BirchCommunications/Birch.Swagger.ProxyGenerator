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
    nuget pack %%l -version 1.0.0
)

echo --------------------------------------------------------
echo Build NuGet Package Script completed...