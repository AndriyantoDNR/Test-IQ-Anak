@echo off
setlocal EnableExtensions

set "PG_CTL=C:\Users\62210770\devtools\postgresql\bin\pg_ctl.exe"
set "PG_DATA=C:\Users\62210770\devdata\postgresql\data"
set "ROOT=C:\andri\Source Code\SiapSD-Cognitive"
set "API_PROJECT=%ROOT%\src\SiapSD.Cognitive.Api\SiapSD.Cognitive.Api.csproj"
set "WEB_DIR=%ROOT%\src\siap-sd-cognitive-web"

if not exist "%PG_CTL%" (
  echo PostgreSQL launcher was not found: %PG_CTL%
  exit /b 1
)

call :port_listening 5432
if errorlevel 1 (
  echo Starting PostgreSQL...
  "%PG_CTL%" -D "%PG_DATA%" -w start
  if errorlevel 1 (
    echo PostgreSQL failed to start.
    exit /b 1
  )
) else (
  echo PostgreSQL is already listening on port 5432.
)

call :wait_for_port 5432 30
if errorlevel 1 goto :not_ready

call :port_listening 5138
if errorlevel 1 (
  echo Starting API in a separate window...
  start "SiapSD Cognitive API" cmd /k "cd /d \"%ROOT%\" ^&^& dotnet run --project \".\src\SiapSD.Cognitive.Api\SiapSD.Cognitive.Api.csproj\""
) else (
  echo API is already listening on port 5138.
)

call :port_listening 5173
if errorlevel 1 (
  echo Starting frontend in a separate window...
  start "SiapSD Cognitive Frontend" cmd /k "cd /d \"%WEB_DIR%\" ^&^& npm run dev"
) else (
  echo Frontend is already listening on port 5173.
)

call :wait_for_port 5138 90
if errorlevel 1 goto :not_ready
call :wait_for_port 5173 90
if errorlevel 1 goto :not_ready

echo.
echo ========================================
echo SiapSD Cognitive Local Environment
echo ========================================
echo PostgreSQL : RUNNING
echo API        : http://localhost:5138
echo Frontend   : http://127.0.0.1:5173
echo ========================================
start "SiapSD Cognitive" "http://127.0.0.1:5173"
exit /b 0

:port_listening
netstat -ano | findstr /R /C:":%~1 .*LISTENING" >nul 2>&1
exit /b %errorlevel%

:wait_for_port
set "WAIT_PORT=%~1"
set /a WAIT_REMAINING=%~2
:wait_loop
call :port_listening %WAIT_PORT%
if not errorlevel 1 exit /b 0
set /a WAIT_REMAINING-=1
if %WAIT_REMAINING% LEQ 0 exit /b 1
timeout /t 1 /nobreak >nul
goto :wait_loop

:not_ready
echo Timed out waiting for a required service to listen.
exit /b 1
