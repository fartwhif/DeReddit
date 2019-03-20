@cd %~dp0
@if not exist "DeReddit.dll" (echo please run the copy of this file residing in the output folder: .\bin\Debug\XXXXX\netcoreapp2.2\
pause)
dotnet DeReddit.dll