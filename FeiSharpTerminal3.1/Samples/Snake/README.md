# FeiSharp Snake / 贪吃蛇

一个完全运行在终端里的 FeiSharp 小游戏，不使用 HTML、JavaScript 或浏览器。

## 运行

```powershell
dotnet run --project FeiSharp.csproj -- Samples/Snake/main.fsc
```

已经安装 `feisharp` 时，也可以运行：

```powershell
feisharp Samples/Snake/main.fsc
```

使用 `W`、`A`、`S`、`D` 控制方向；每按一次移动一步，按 `Q` 退出。

## 面向对象结构

- `gameEntity`：所有棋盘实体的基类，封装坐标与移动行为。
- `snakeEntity : gameEntity`：继承实体基类，管理蛇身、移动与碰撞。
- `foodEntity : gameEntity`：继承实体基类，提供食物符号与分值。
- `gameStats`：封装分数和移动次数。

项目展示了类、构造函数、继承、访问修饰符、`this`、`base`、对象方法、列表、循环和 ANSI 色彩输出。
