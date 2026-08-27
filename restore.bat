@echo off
setlocal
cd /d "%~dp0"

:: Check for Administrator privileges
net session >nul 2>&1
if %errorlevel% == 0 (
    node index.js --restore
) else (
    echo [INFO] Solicitando privilegios de Administrador para restaurar configuracoes...
    powershell -Command "Start-Process cmd -ArgumentList '/c cd /d \"%~dp0\" && node index.js --restore && pause' -Verb RunAs"
)
