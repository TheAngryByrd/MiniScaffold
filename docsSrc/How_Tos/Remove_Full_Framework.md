# Removing .NET Full/Mono Framework

There are times where your library isn't appropriate for [Full](https://dotnet.microsoft.com/download/dotnet-framework) or [Mono](https://www.mono-project.com/).

- Remove `net461` from all `TargetFrameworks` section in your `fsproj`/`csproj`/`vbproj` files in your repository.

The resulting fsproj should look like:

    [lang=xml]
    <?xml version="1.0" encoding="utf-8"?>
    <Project Sdk="Microsoft.NET.Sdk">
    <PropertyGroup>
        <TargetFrameworks>netstandard2.1</TargetFrameworks>
        <GenerateDocumentationFile>true</GenerateDocumentationFile>
    </PropertyGroup>
    <PropertyGroup>
        <Title>MyLib.1</Title>
        <Description>MyLib.1 does the thing!</Description>

    </PropertyGroup>
    <PropertyGroup Condition="'$(Configuration)'=='Release'">
        <Optimize>true</Optimize>
        <Tailcalls>true</Tailcalls>

    </PropertyGroup>
    <ItemGroup>
        <Compile Include="AssemblyInfo.fs" />
        <Compile Include="Library.fs" />
    </ItemGroup>
    <Import Project="..\..\.paket\Paket.Restore.targets" />
    </Project>


