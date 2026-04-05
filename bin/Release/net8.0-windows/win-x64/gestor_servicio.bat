@echo off
:: Verifica permisos de administrador
net session >nul 2>&1
if %errorLevel% == 0 (
    echo Permisos de administrador verificados.
) else (
    echo ===========================================================
    echo Error: Este script requiere permisos de Administrador.
    echo Por favor, haz clic derecho en el archivo y selecciona
    echo "Ejecutar como administrador".
    echo ===========================================================
    pause
    exit /b 1
)

set SERVICENAME=QPrintBridge Service
set EXENAME=QPrintBridge.exe
set EXEPATH=%~dp0%EXENAME%

:MENU
cls
echo =======================================================
echo          GESTOR DEL SERVICIO QPrintBridge
echo =======================================================
echo.
echo Asegurate de compilar el proyecto antes de ejecutar esto.
echo Ruta actual detectada: %EXEPATH%
echo.
echo 1. Instalar (Crear) el servicio
echo 2. Iniciar el servicio
echo 3. Detener el servicio
echo 4. Desinstalar (Eliminar) el servicio
echo 5. Salir
echo.
set /p option="Elige una opcion (1-5): "

if "%option%"=="1" goto INSTALAR
if "%option%"=="2" goto INICIAR
if "%option%"=="3" goto DETENER
if "%option%"=="4" goto DESINSTALAR
if "%option%"=="5" goto SALIR

echo Opcion invalida.
pause
goto MENU

:INSTALAR
if not exist "%EXEPATH%" (
    echo ======================================================================
    echo ERROR: No se encuentra el archivo %EXENAME% en esta carpeta.
    echo Asegurate de compilar y colocar este .bat junto al .exe publicado.
    echo ======================================================================
    pause
    goto MENU
)

echo Instalando el servicio "%SERVICENAME%" desde "%EXEPATH%"...
sc create "%SERVICENAME%" binPath= "%EXEPATH%" start= auto
if %errorLevel% == 0 (
    echo Servicio instalado correctamente.
    sc description "%SERVICENAME%" "Provee un puente HTTP local al puerto 19100 a impresoras de Windows."
) else (
    echo Hubo un problema instalando el servicio.
)
pause
goto MENU

:INICIAR
echo Iniciando el servicio...
sc start "%SERVICENAME%"
pause
goto MENU

:DETENER
echo Deteniendo el servicio...
sc stop "%SERVICENAME%"
pause
goto MENU

:DESINSTALAR
echo Deteniendo el servicio antes de eliminarlo...
sc stop "%SERVICENAME%" >nul 2>&1
echo Eliminando el servicio...
sc delete "%SERVICENAME%"
if %errorLevel% == 0 (
    echo Servicio eliminado correctamente.
) else (
    echo Hubo un problema eliminando el servicio (quiza ya estaba eliminado).
)
pause
goto MENU

:SALIR
exit /b 0
