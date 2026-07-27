@echo off
chcp 65001
setlocal enabledelayedexpansion
set "total=0"
echo ==============================================
for /r %%f in (*.cs) do (
    for /f %%a in ('find /v /c "" ^< "%%f"') do (
        set /a total+=%%a
    )
)
echo ==============================================
echo !total!
pause