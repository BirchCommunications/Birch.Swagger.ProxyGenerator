param($installPath, $toolsPath, $package, $project)
$version = $package.Version;
$targetFile = $installPath + "\build\Birch.Swagger.ProxyGenerator.targets";
Write-Host "Updating Birch.Swagger.ProxyGenerator.targets to have the correct version ($version)";
(Get-Content $targetFile | ForEach-Object { $_ -replace '\$version\$', $version } ) | set-content $targetFile
Write-Host "Updating Birch.Swagger.ProxyGenerator.targets completed..."