echo Restoring dotnet tools...
dotnet tool restore

dotnet run -p ./build/build.fsproj -- -t %*
