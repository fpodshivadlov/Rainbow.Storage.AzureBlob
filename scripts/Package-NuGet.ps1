$scriptRoot = $PSScriptRoot;
$ErrorActionPreference = "Stop"

function Ensure-NuGet {
	if (-Not (Test-Path -Path $nuGet)) {
		(New-Object System.Net.WebClient).DownloadFile("https://dist.nuget.org/win-x86-commandline/latest/nuget.exe", $nuGet)
	}
}

function Resolve-MsBuild {
	$msb2017 = Resolve-Path "${env:ProgramFiles(x86)}\Microsoft Visual Studio\*\*\MSBuild\*\bin\msbuild.exe" -ErrorAction SilentlyContinue
	if ($msb2017) {
		Write-Host "Found MSBuild 2017 (or later)."
		Write-Host $msb2017
		return $msb2017
	}

	$msBuild2015 = "${env:ProgramFiles(x86)}\MSBuild\14.0\bin\msbuild.exe"

	if(-not (Test-Path $msBuild2015)) {
		throw 'Could not find MSBuild 2015 or later.'
	}

	Write-Host "Found MSBuild 2015."
	Write-Host $msBuild2015

	return $msBuild2015
}

$msBuild = Resolve-MsBuild
$nuGet = "$scriptRoot\..\tools\NuGet.exe"
$solution = "$scriptRoot\..\src\Rainbow.Storage.AzureBlob.sln"

Ensure-NuGet
& $nuGet restore $solution
& $msBuild $solution /p:Configuration=Release /t:Rebuild /m

$synthesisAssembly = Get-Item "$scriptRoot\..\src\Rainbow.Storage.AzureBlob\bin\Release\Rainbow.Storage.AzureBlob.dll" | Select-Object -ExpandProperty VersionInfo
$targetAssemblyVersion = $synthesisAssembly.ProductVersion

& $nuGet pack "$scriptRoot\..\src\Rainbow.Storage.AzureBlob\Rainbow.Storage.AzureBlob.csproj" -Symbols -Prop Configuration=Release
