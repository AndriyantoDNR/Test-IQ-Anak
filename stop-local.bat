@echo off
setlocal EnableExtensions DisableDelayedExpansion
title SiapSD Local Shutdown

set "PG_CTL=C:\Users\62210770\devtools\postgresql\bin\pg_ctl.exe"
set "PG_DATA=C:\Users\62210770\devdata\postgresql\data"

echo Stopping process trees listening on :5138 and :5173...
call :StopPort 5138
call :StopPort 5173

if exist "%PG_CTL%" (
  "%PG_CTL%" status -D "%PG_DATA%" >nul 2>&1
  if not errorlevel 1 (
    echo Stopping PostgreSQL portable instance...
    "%PG_CTL%" stop -D "%PG_DATA%" -m fast -w
  ) else (
    echo PostgreSQL portable instance not running.
  )
) else (
  echo PostgreSQL pg_ctl not found; skipped.
)

echo.
echo Local stack stopped.
goto :end

:StopPort
for /f "tokens=5" %%P in ('netstat -ano ^| findstr /R /C:":%~1 .*LISTENING"') do (
  echo Stopping PID %%P on :%~1...
  taskkill /PID %%P /T >nul 2>&1
)
exit /b 0

:end
pause
endlocal
