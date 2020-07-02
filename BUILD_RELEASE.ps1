cd Lib
dotnet build --configuration Release --framework net462
dotnet build --configuration Release --framework net48
dotnet build --configuration Release --framework netcoreapp3.1
dotnet build --configuration Release --framework netstandard2.1
dotnet pack  --configuration Release --output ..\NuGetPackage