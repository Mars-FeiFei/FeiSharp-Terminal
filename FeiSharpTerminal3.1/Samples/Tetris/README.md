# FeiSharp Tetris / 俄罗斯方块

一个完全运行在终端中的 FeiSharp 俄罗斯方块示例，不使用 HTML、JavaScript 或浏览器。

## 运行

在仓库根目录执行：

```powershell
dotnet run --project FeiSharp.csproj -- Samples/Tetris/Tetris.feiproj
```

已经安装 `feisharp` 时也可以执行：

```powershell
feisharp Samples/Tetris/Tetris.feiproj
```

## 操作

- `A`：左移
- `D`：右移
- `W`：顺时针旋转
- `S`：立即下落一格（方块也会自动下落）
- `Space`：硬降并立即锁定
- `Q`：退出

游戏会自动下落，约每 500 毫秒受到一次重力影响；按键可以随时移动或旋转方块，不需要点击 `S` 才能继续游戏。

## 示例内容

- `tetromino` 类封装方块类型、坐标和旋转状态。
- `gameStats` 类管理分数、消行数、等级和落地方块数。
- 使用 `List<int>` 保存 10 × 20 棋盘。
- 包含七种 Tetromino、碰撞检测、旋转、硬降、消行和下一个方块预览。
- 使用 ANSI 真彩色输出终端界面。
