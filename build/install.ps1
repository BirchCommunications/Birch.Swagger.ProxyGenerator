param($installPath, $toolsPath, $package, $project)
Write-Host "Starting package install script..."
$version = $package.Version;
$projectDir = Split-Path $project.FileName;
$targetFile = $installPath + "\build\Birch.Swagger.ProxyGenerator.targets";
$propsFile = $projectDir + "\build\Birch.Swagger.ProxyGenerator.props";

Write-Host "Updating $targetsFile to have the correct version ($version)";
(Get-Content $targetFile | ForEach-Object { $_ -replace '\$version\$', $version } ) | set-content $targetFile
Write-Host "Updating Birch.Swagger.ProxyGenerator.targets completed..."


Write-Host "Updating $propsFile to have the correct version ($version)";
(Get-Content $propsFile | ForEach-Object { $_ -replace "<ProxyGeneratorVersion>.+</ProxyGeneratorVersion>", "<ProxyGeneratorVersion>$version</ProxyGeneratorVersion>" } ) | set-content $propsFile
Write-Host "Updating Birch.Swagger.ProxyGenerator.props completed..."

Write-Host "Package install script completed..."
