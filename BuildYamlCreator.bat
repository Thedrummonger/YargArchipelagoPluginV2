@echo off
if not exist "Builds" mkdir "Builds"
cd "Yaml Creator\bin\Release"
ILRepack.exe /target:winexe /out:"..\..\..\Builds\Yaml Creator.exe" "Yaml Creator.exe" Newtonsoft.Json.dll YamlDotNet.dll YargArchipelagoPluginNightly.dll Archipelago.MultiClient.Net.dll YARG.Core.Package.dll
pause