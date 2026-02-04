$ErrorActionPreference = "Stop"
try{
   cd Lib
   dotnet build --configuration Release --framework net8.0
   dotnet build --configuration Release --framework net6.0
   dotnet build --configuration Release --framework net462
   dotnet build --configuration Release --framework net48
   dotnet build --configuration Release --framework netcoreapp3.1
   dotnet build --configuration Release --framework netstandard2.1
   dotnet pack  --configuration Release --output ..\NuGetPackage
   cd $PSScriptRoot
} catch {
   $errorMsg = $_.Exception.Message
   Write-Host "`nExiting script with return code 999 due to:" -ForegroundColor Red
   Write-Host "$($errorMsg)`n" -ForegroundColor Red
   cd $PSScriptRoot
   exit 999
}
exit 0