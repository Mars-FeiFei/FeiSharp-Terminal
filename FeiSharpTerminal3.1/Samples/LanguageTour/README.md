# FeiSharp Language Tour

这是一个面向 FeiSharp 新用户的可运行示例项目。建议先运行完整导览，再按注释阅读源码。

## 运行

在 FeiSharp 源码目录执行：

```powershell
dotnet run --project FeiSharp.csproj -- Samples/LanguageTour/main.fsc
```

如果已经安装或发布了 `feisharp` 命令，也可以执行：

```powershell
feisharp Samples/LanguageTour/main.fsc
```

运行独立的类与异常示例：

```powershell
feisharp Samples/LanguageTour/advanced.fsc
```

构建项目：

```powershell
feisharp --build Samples/LanguageTour/LanguageTour.feiproj
```

## 文件说明

- `main.fsc`：从基础语法到集合、流程控制、函数和标准库的完整导览。
- `tour_helpers.fsc`：演示源文件导入、命名空间和自定义函数。
- `advanced.fsc`：演示类、构造函数、继承、`this`、`base`、静态成员和异常处理。
- `LanguageTour.feiproj`：可由 FeiSharp 构建命令读取的项目文件；`main_function_run` 用于控制是否自动运行 `main` 函数。

## 语法速查

| 用途 | 写法 |
| --- | --- |
| 变量 / 常量 | `var name = "FeiSharp";` / `const pi = 3.14;` |
| 条件值 | `"adult" when age >= 18 otherwise "minor"` |
| 数组 / 索引 | `var values = [10, 20]; values[0];` |
| 条件分支 | `if (condition) { ... } else { ... }` |
| 循环 | `for (...) { ... }`、`while (...) { ... }`、`forEach (var x in xs) { ... }` |
| 函数 | `function add(a, b) { return a + b; }` |
| 类 | `class user { constructor(name) { this.name = name; } }` |
| 异常 | `try { throw "message"; } catch (ex) { printNl(ex.msg); }` |
| 文件导入 | `import "tour_helpers.fsc";` |
| 命名空间 | `namespace Demo;` 与 `using Demo;` |

FeiSharp 的名称查找不区分大小写，但示例统一使用 lower camel case，便于形成一致的代码风格。
