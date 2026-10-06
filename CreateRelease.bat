@echo off

cd /d "%~dp0"

set "MSBUILD=C:\Program Files\Microsoft Visual Studio\18\Insiders\MSBuild\Current\Bin\MSBuild.exe"
set "LINUX_PATH=\\tsclient\_home_thedrummonger\Documents\Yarg AP Release\V2"

if not exist "Builds" mkdir "Builds"
echo Building projects...
"%MSBUILD%" "YargArchipelagoPluginNightly\YargArchipelagoPluginNightly.csproj" /p:Configuration=Release /v:minimal
"%MSBUILD%" "YargArchipelagoPluginMain\YargArchipelagoPluginStable.csproj" /p:Configuration=Release /v:minimal
"%MSBUILD%" "Yaml Creator\Yaml Creator.csproj" /p:Configuration=Release /v:minimal
"%MSBUILD%" "CloneHeroPlugin\CloneHeroPlugin.csproj" /restore /p:Configuration=Release /v:minimal
"%MSBUILD%" "Clone Hero Yaml Creator\Clone Hero Yaml Creator.csproj" /p:Configuration=Release /v:minimal

echo Packing YARG executable...
cd "Yaml Creator\bin\Release"
ILRepack.exe /target:winexe /out:"..\..\..\Builds\YAYARG Yaml Creator.exe" "Yaml Creator.exe" Newtonsoft.Json.dll YamlDotNet.dll Archipelago.MultiClient.Net.dll
cd ..\..\..

echo Packing Clone Hero executable...
cd "Clone Hero Yaml Creator\bin\Release"
ILRepack.exe /target:winexe /out:"..\..\..\Builds\Clone Hero Yaml Creator.exe" "Clone Hero Yaml Creator.exe" "Yaml Creator.exe" Newtonsoft.Json.dll YamlDotNet.dll Archipelago.MultiClient.Net.dll
cd ..\..\..

echo Creating Nightly plugin zip...
cd "YargArchipelagoPluginNightly\bin\Release"
powershell -command "Compress-Archive -Path 'YargArchipelagoPluginNightly.dll','Archipelago.MultiClient.Net.dll' -DestinationPath '..\..\..\Builds\YargArchipelagoPluginNightly.zip' -Force"
cd ..\..\..

echo Creating Stable plugin zip...
cd "YargArchipelagoPluginMain\bin\Release"
powershell -command "Compress-Archive -Path 'YargArchipelagoPluginStable.dll','Archipelago.MultiClient.Net.dll' -DestinationPath '..\..\..\Builds\YargArchipelagoPluginStable.zip' -Force"
cd ..\..\..

echo Creating Clone Hero plugin zip...
cd "CloneHeroPlugin\bin\Release\net6.0"
powershell -command "Compress-Archive -Path 'CloneHeroPlugin.dll','Archipelago.MultiClient.Net.dll' -DestinationPath '..\..\..\..\Builds\CloneHeroPlugin.zip' -Force"
cd ..\..\..\..

if exist "%LINUX_PATH%" (
    copy /Y "YargArchipelagoPluginNightly\bin\Release\YargArchipelagoPluginNightly.dll" "%LINUX_PATH%\"
    copy /Y "YargArchipelagoPluginMain\bin\Release\YargArchipelagoPluginStable.dll" "%LINUX_PATH%\"
    copy /Y "CloneHeroPlugin\bin\Release\net6.0\CloneHeroPlugin.dll" "%LINUX_PATH%\"
    copy /Y "Builds\YAYARG Yaml Creator.exe" "%LINUX_PATH%\"
    copy /Y "Builds\Clone Hero Yaml Creator.exe" "%LINUX_PATH%\"
    copy /Y "Builds\YargArchipelagoPluginNightly.zip" "%LINUX_PATH%\"
    copy /Y "Builds\YargArchipelagoPluginStable.zip" "%LINUX_PATH%\"
    copy /Y "Builds\CloneHeroPlugin.zip" "%LINUX_PATH%\"
)

pause
