@echo off
setlocal enabledelayedexpansion
set "total=0"
echo 姝ｅ湪缁熻褰撳墠鏂囦欢澶瑰強瀛愭枃浠跺す涓嬫墍鏈?cs鏂囦欢琛屾暟...
echo ==============================================
for /r %%f in (*.cs) do (
    for /f %%a in ('find /v /c "" ^< "%%f"') do (
        echo %%~nxf : %%a 琛?
        set /a total+=%%a
    )
)
echo ==============================================
echo 缁熻瀹屾垚锛佹墍鏈?cs鏂囦欢鎬昏鏁帮細!total!
pause