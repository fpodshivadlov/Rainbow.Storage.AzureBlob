$scriptRoot = $PSScriptRoot;

$apiKey = Get-Content -Path "$scriptRoot\NugetApiKey.txt"

Get-ChildItem *.nupkg -exclude *.symbols.nupkg | % { 
	& "$scriptRoot\..\tools\NuGet.exe" push $_ -Source https://www.nuget.org/api/v2/package -ApiKey $apiKey
}
