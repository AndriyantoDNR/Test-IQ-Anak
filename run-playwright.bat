@echo off
setlocal EnableExtensions

call :port_listening 5138
if errorlevel 1 (
  echo API is not listening on http://localhost:5138
  exit /b 1
)

call :port_listening 5173
if errorlevel 1 (
  echo Frontend is not listening on http://127.0.0.1:5173
  exit /b 1
)

cd /d "C:\andri\Source Code\SiapSD-Cognitive\src\siap-sd-cognitive-web"
npx playwright test --workers=1 --reporter=line
exit /b %errorlevel%

:port_listening
netstat -ano | findstr /R /C:":%~1 .*LISTENING" >nul 2>&1
exit /b %errorlevel%
