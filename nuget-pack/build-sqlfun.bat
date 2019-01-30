msbuild ..\SqlFun.sln /p:Configuration=release
rd /S /Q .\lib
echo f | xcopy ..\SqlFun\bin\release\SqlFun.dll .\lib\net461\SqlFun.dll
echo f | xcopy ..\SqlFun\bin\release\SqlFun.XML .\lib\net461\SqlFun.XML
nuget pack .\SqlFun.nuspec
