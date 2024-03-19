@echo off
set DOTNET_WATCH_RESTART_ON_RUDE_EDIT=1
set DOTNET_WATCH_SUPPRESS_LAUNCH_BROWSER=1
start "Gateway Server" cmd /k "cd /D %~dp0\Backend\Plantmonitor.Server && dotnet watch run"
start chrome https://localhost:7005/swagger/index.html