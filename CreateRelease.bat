@echo off

set "MSBUILD=C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe"
set "LINUX_PATH=\\tsclient\_home_thedrummonger\Documents\Yarg AP Release\V2"

if not exist "Builds" mkdir "Builds"
echo Building projects...
"%MSBUILD%" "YargArchipelagoPluginNightly\YargArchipelagoPluginNightly.csproj" /p:Configuration=Release /v:minimal
"%MSBUILD%" "YargArchipelagoPluginMain\YargArchipelagoPluginStable.csproj" /p:Configuration=Release /v:minimal
"%MSBUILD%" "Yaml Creator\Yaml Creator.csproj" /p:Configuration=Release /v:minimal

echo Packing executable...
cd "Yaml Creator\bin\Release"
ILRepack.exe /target:winexe /out:"..\..\..\Builds\Yaml Creator.exe" "Yaml Creator.exe" Newtonsoft.Json.dll YamlDotNet.dll YargArchipelagoPluginNightly.dll Archipelago.MultiClient.Net.dll YARG.Core.Package.dll
cd ..\..\..

echo Creating Nightly plugin zip...
cd "YargArchipelagoPluginNightly\bin\Release"
copy /Y "YargArchipelagoPluginNightly.dll" "%LINUX_PATH%\"
powershell -command "Compress-Archive -Path 'YargArchipelagoPluginNightly.dll','Archipelago.MultiClient.Net.dll' -DestinationPath '..\..\..\Builds\YargArchipelagoPluginNightly.zip' -Force"
cd ..\..\..

echo Creating Stable plugin zip...
cd "YargArchipelagoPluginMain\bin\Release"
copy /Y "YargArchipelagoPluginStable.dll" "%LINUX_PATH%\"
powershell -command "Compress-Archive -Path 'YargArchipelagoPluginStable.dll','Archipelago.MultiClient.Net.dll' -DestinationPath '..\..\..\Builds\YargArchipelagoPluginStable.zip' -Force"
cd ..\..\..

if exist "%LINUX_PATH%" (
    copy /Y "Builds\Yaml Creator.exe" "%LINUX_PATH%\"
    copy /Y "Builds\YargArchipelagoPluginNightly.zip" "%LINUX_PATH%\"
    copy /Y "Builds\YargArchipelagoPluginStable.zip" "%LINUX_PATH%\"
)

pause