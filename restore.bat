@echo off
setlocal
cd /d "%~dp0"

:: Chama o .exe (nao "node index.js" direto) para funcionar mesmo sem Node.js instalado -
:: o .exe e standalone e usa SEMPRE o motor nativo em C#, e ja cuida da propria elevacao de
:: Administrador (UAC), sem precisar da checagem "net session" + Start-Process -Verb RunAs
:: que este script fazia antes.
AdaptivePCOptimizer.exe --restore
