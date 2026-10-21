@echo off
setlocal
cd /d "%~dp0"
title Strike Kids (Unity) - Gerar APK
rem Gera o APK da TV Box do STRIKE KIDS feito na UNITY (OpenGL ES 2.0,
rem Mali-400/450): mostra cada passo nesta janela e guarda o historico em
rem Build\GERACAO-COMPLETA.log. Opcoes (opcionais):
rem   GERAR_APK.bat -Giro -1         (topo do jogo na direita do HDMI)
rem   GERAR_APK.bat -Giro 0          (TV deitada, sem giro)
rem   GERAR_APK.bat -Instalar        (instala na TV Box pelo adb)
rem   GERAR_APK.bat -Windows         (gera tambem a versao de PC, para testar)
rem   GERAR_APK.bat -Calibrar        (refaz a calibracao das jogadas)
rem   GERAR_APK.bat -Versao 6.1
if exist "%~dp0ProjectSettings\ProjectVersion.txt" goto projeto_ok
echo.
echo ERRO: extraia o ZIP inteiro antes e rode o GERAR_APK.bat de dentro
echo da pasta do projeto (junto das pastas Assets e ProjectSettings).
echo.
pause
exit /b 1
:projeto_ok
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0GERAR_APK.ps1" %*
set "RESULTADO=%ERRORLEVEL%"
echo.
if "%RESULTADO%"=="0" goto sucesso
echo ============================================================
echo  NAO FOI POSSIVEL GERAR O APK. Envie o arquivo:
echo  Build\GERACAO-COMPLETA.log
echo ============================================================
goto fim
:sucesso
echo ============================================================
echo  PRONTO: APK-Pronto\StrikeKids-Unity.apk
echo ============================================================
:fim
echo.
pause
exit /b %RESULTADO%
