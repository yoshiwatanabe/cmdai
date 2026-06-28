@echo off
setlocal
set "CMDAI_ROOT=%~dp0"
pushd "%CMDAI_ROOT%" >nul
python -m cmdai %*
set "CMDAI_EXIT=%ERRORLEVEL%"
popd >nul
exit /b %CMDAI_EXIT%

