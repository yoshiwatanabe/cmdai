@echo off
setlocal
set "CMDAI_NEXT_ROOT=%~dp0"
pushd "%CMDAI_NEXT_ROOT%" >nul
python -m cmdai_next %*
set "CMDAI_NEXT_EXIT=%ERRORLEVEL%"
popd >nul
exit /b %CMDAI_NEXT_EXIT%

