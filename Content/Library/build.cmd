SET FAKE_TOOL_PATH=.fake

IF NOT EXIST "%FAKE_TOOL_PATH%\fake.exe" (
  dotnet tool install fake-cli --tool-path ./%FAKE_TOOL_PATH%
)

SET PAKET_TOOL_PATH=.paket

IF NOT EXIST "%PAKET_TOOL_PATH%\paket.exe" (
  dotnet tool install paket --tool-path ./%PAKET_TOOL_PATH%
)

"%FAKE_TOOL_PATH%/fake.exe" build -t %*
