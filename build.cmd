@echo off
cls

.paket\paket.bootstrapper.exe
if errorlevel 1 (
  exit /b %errorlevel%
)

.paket\paket.exe restore
if errorlevel 1 (
  exit /b %errorlevel%
)

IF NOT EXIST build.fsx (
  .paket\paket.exe update
  packages\build\FAKE\tools\FAKE.exe init.fsx
)
pushd Content\
call build.cmd
if errorlevel 1 (
  exit /b %errorlevel%
)
call build.cmd Clean
if errorlevel 1 (
  exit /b %errorlevel%
)
popd

packages\build\FAKE\tools\FAKE.exe build.fsx %*
