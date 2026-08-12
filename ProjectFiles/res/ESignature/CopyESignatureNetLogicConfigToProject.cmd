@echo off
setlocal enabledelayedexpansion
echo ######################################################
echo # Automated Project Template Deployment Tool #
echo ######################################################
echo.
echo This tool automatically detects the project name and deploys project template content.
echo It copies .dll files and renames .csproj files to match the project name.
echo.
pause
set "TARGET_NETSOLUTION_RELATIVE_PATH=..\..\NetSolution"
set "SOURCE_PATH=%~dp0"
set "SCRIPT_FILENAME=%~nx0"
for %%i in ("%SOURCE_PATH%%TARGET_NETSOLUTION_RELATIVE_PATH%") do set "TARGET_NETSOLUTION_DIR=%%~fi\"
if not exist "%TARGET_NETSOLUTION_DIR%" (
   echo Error: Target NetSolution folder "%TARGET_NETSOLUTION_DIR%" does not exist.
   echo Please verify the 'TARGET_NETSOLUTION_RELATIVE_PATH' configuration.
   pause
   exit /b 1
)
for %%i in ("%TARGET_NETSOLUTION_DIR%..\..\") do set "PROJECT_FILES_DIR=%%~dpi"
for %%i in ("%PROJECT_FILES_DIR:~0,-1%") do set "NEW_PROJECT_NAME=%%~nxi"
echo Detected project name: **%NEW_PROJECT_NAME%**
echo Preparing to copy project template content...
echo Source Path: "%SOURCE_PATH%"
echo Target Path: "%TARGET_NETSOLUTION_DIR%"
echo Excluding script file: "%SCRIPT_FILENAME%"
echo Only copying .dll files and .csproj files (renamed to project name)
set "COPY_SUCCESS=0"
:: Copy .dll files (unchanged)
for %%F in ("%SOURCE_PATH%*.dll") do (
   if /i not "%%~nxF"=="%SCRIPT_FILENAME%" (
       echo Copying file: %%~nxF
       copy "%%F" "%TARGET_NETSOLUTION_DIR%" >nul
       if errorlevel 1 set "COPY_SUCCESS=1"
   )
)
:: Copy and rename .csproj file
for %%F in ("%SOURCE_PATH%*.csproj") do (
   if /i not "%%~nxF"=="%SCRIPT_FILENAME%" (
       echo Renaming and copying: %%~nxF to %NEW_PROJECT_NAME%.csproj
       copy "%%F" "%TARGET_NETSOLUTION_DIR%%NEW_PROJECT_NAME%.csproj" >nul
       if errorlevel 1 set "COPY_SUCCESS=1"
   )
)
pause
if "%COPY_SUCCESS%"=="0" (
   echo Project '%NEW_PROJECT_NAME%' template content successfully deployed.
   echo .dll files copied as-is
   echo .csproj file renamed to %NEW_PROJECT_NAME%.csproj
   echo Please check the '%TARGET_NETSOLUTION_DIR%' folder.
) else (
   echo Error: Project template deployment failed or had warnings.
   echo Please check paths, permissions, or if files are in use.
)
pause
endlocal