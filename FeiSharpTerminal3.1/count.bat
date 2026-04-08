@echo off
:: 核心：启用延迟扩展，让total变量能实时更新
setlocal enabledelayedexpansion
set "total=0"

echo 正在统计当前文件夹及子文件夹下所有.cs文件行数...
echo ==============================================

:: 遍历所有.cs文件（/r 表示递归子文件夹，想只统计当前目录就删掉 /r）
for /r %%f in (*.cs) do (
    :: 统计单个文件的行数
    for /f %%a in ('find /v /c "" ^< "%%f"') do (
        echo %%~nxf : %%a 行
        set /a total+=%%a
    )
)

echo ==============================================
echo 统计完成！所有.cs文件总行数：!total!
pause