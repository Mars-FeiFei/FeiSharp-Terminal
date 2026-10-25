# FeiSharp 程序员指南

> 面向有 C#、JavaScript、Python 或类似语言经验的开发者。内容对应当前仓库的 FeiSharp 11.0 实现。

## 1. 语言画像

FeiSharp 是运行于 .NET 8 Windows 宿主中的动态类型脚本语言，语法整体接近 C 系：分号结句、花括号块、`if/for/while`、一等的命名函数和类。当前解释器还直接暴露一部分 .NET 字符串与集合成员，并提供文件、HTTP、JSON/JavaScript 和浏览器自动化标准 API。

需要最先建立的心智模型：

- 名称查找不区分大小写，文档约定使用 lower camel case；
- 变量动态类型，数字表达式通常按 `double` 运算；
- 单个 `=` 同时承担赋值语句和表达式中的相等比较，靠语境区分；
- `$` 是乘方，`^` 是整数按位 XOR；
- 脚本可在顶层执行；精确形式 `function main(` 会被 CLI 默认自动调用；
- 用户函数优先于同名标准函数，允许有意覆盖内置项；
- 类的实例化写作 `typeName(args)`，不是必须写 `new`；
- 文件加载与命名空间导入是两件不同的事。

## 2. 运行、项目与构建

从源码运行脚本：

```powershell
dotnet run --project FeiSharp.csproj -- app.fsc arg1 arg2
```

安装/发布命令后：

```powershell
feisharp app.fsc arg1 arg2
feisharp App.feiproj
```

脚本参数通过全局数组 `args` 获取：

```feisharp
function main() {
    printNl("argument count = " + len(args));
    if (len(args) > 0) {
        printNl(args[0]);
    }
}
```

构建单文件或项目：

```powershell
feisharp --build app.fsc
feisharp --build App.feiproj
feisharp --build App.feiproj custom.exe
```

当前实现的 `.feiproj` 结构：

```json
{
  "project_name": "MyApp",
  "project_main_file": "main.fsc",
  "target_platform": "win-x64",
  "trimmed_by_dotnet": false,
  "clean_build_directory": true,
  "main_function_run": true,
  "only_copy_executable": false,
  "dependencies": []
}
```

可用脚手架类型为 `Console`、`API`、`Data`、`fUnitTest`：

```powershell
feisharp create Console MyApp
```

注意：命令帮助文本部分位置写作 `build`，但当前参数分支实际识别的是 `--build`，仓库示例也使用 `--build`。

## 3. 最小程序与入口行为

```feisharp
function main() {
    printNl("Hello, FeiSharp");
}
```

CLI 预处理阶段会搜索区分大小写的文本 `function main(`，命中且 `main_function_run` 为 `true` 时在源码末尾追加 `main();`。因此：

- 入口建议严格写成 `function main() { ... }`；
- 不要同时在顶层手动调用，否则会运行两次；
- 导入文件以 `runMain = false` 预处理，其 `main` 不会自动调用；
- 需要库式顶层控制时，将项目的 `main_function_run` 设为 `false`。

`//` 与 `/* ... */` 注释由预处理器在保护字符串/字符文本的情况下移除。

## 4. 变量、值与转换

```feisharp
var mutable = 1;
mutable = "now a string";

const answer = 42;
var missing = null;
var enabled = true;
var letter = 'F';
```

`const` 必须初始化，重新赋值产生 `FS3009`。类字段还支持 `readonly`，`const var x` 与 `var const x` 也被解析器接受，但新代码推荐统一使用 `const x = value;`。

运行时类型工具：

```feisharp
typeOf(null);      // "null"
typeOf(1);         // "number"
typeOf("x");      // "string"
typeOf([1]);       // "array"
isNull(null);      // true
len("FeiSharp");  // 8
```

默认启动代码定义了转换辅助类：

```feisharp
var n = number.parse("42");
var s = string.parse(42);
var b = boolean.parse("true");
var n2 = "42" as int;
```

`as` 在预处理阶段转换成 `cast(value, "number")` 等形式。别把它当成 C# 的安全引用转换：失败时不会返回 `null`，而会产生转换错误。

## 5. 运算符与求值

从低到高的当前优先级：

| 优先级 | 运算符 | 含义 |
| ---: | --- | --- |
| 1 | `||` | 逻辑或 |
| 2 | `&&` | 逻辑与 |
| 3 | `|` | 布尔或 / 整数按位或 |
| 4 | `&` | 布尔与 / 整数按位与 |
| 5 | `=` `!=` `>` `<` `>=` `<=` | 相等与比较 |
| 6 | `^` | 整数按位异或 |
| 7 | `+` `-` | 加减；`+` 也拼接字符串 |
| 8 | `*` `/` | 乘除 |
| 9 | `$` | 乘方 |
| 10 | `++` `--` | 自增、自减 |

一元运算支持 `!`、`+`、`-` 和前/后置 `++/--`。

```feisharp
var same = 2 = 2;
var power = 2 $ 10;
var xorValue = 5 ^ 3;
var text = "value=" + power;
```

与常见语言的显著差异：

- 没有 `==`；相等比较用 `=`；
- 数字除法按浮点语义执行；
- 不要把 `^` 当幂；
- `&&`/`||` 是逻辑运算，`&`/`|` 还可做整数位运算；
- 字符串 `-` 会移除匹配子串，字符串 `/` 返回分隔符出现次数，这属于实现提供的扩展行为，不建议在可移植业务代码中依赖。

条件表达式不是 `?:`：

```feisharp
var label = "adult" when age >= 18 otherwise "minor";
var maybe = value when condition; // 条件为 false 时为 null
```

预处理器会展开简单变量的 `+=`、`-=`、`*=`、`/=` 等复合赋值；由于它基于正则且只面向简单标识符，新代码在复杂上下文中推荐显式写 `x = x + y;`。

## 6. 控制流

```feisharp
if (condition) {
    work();
} else {
    fallback();
}

while (condition) {
    work();
}

for (var i = 0; i < 10; i = i + 1) {
    work(i);
}

foreach (var item in items) {
    work(item);
}

doWhile {
    work();
} while (condition);
```

`foreach` 支持字符串及实现 `IEnumerable` 的数组、列表、字典等运行时对象。循环变量在循环结束后恢复原值；若循环前不存在，则移除。

当前主流路径没有可推荐的 `break`/`continue` 语句。需要提前结束时，重构条件、抽取函数或使用状态变量。

## 7. 函数与作用域

```feisharp
function clampScore(value, minimum, maximum) {
    return math.clamp(value, minimum, maximum);
}

var score = clampScore(120, 0, 100);
```

函数没有参数或返回类型声明。`return` 可返回表达式；没有显式返回值时结果为 `null`。调用参数按位置传递。

函数声明按执行顺序进入符号表。尤其在循环依赖的导入中，不要假定尚未执行的文件已经完成前向声明。

名称解析不区分大小写，函数内部会保留定义位置的当前命名空间和 `using` 集合。用户声明先于标准库解析，因此可以覆盖 `len`、`math` 等内置名称；除非确有必要，不建议这样做。

## 8. 集合与运行时成员

```feisharp
var array = [1, 2, 3];
var typedArray = new int[] { 4, 5, 6 };
var list = new List<string> { "a", "b" };
var map = new Dictionary<string, int> { { "a", 1 }, { "b", 2 } };

list.Add("c");
list[0] = "A";
map.Add("c", 3);
map["b"] = 20;

printNl(array.Length);
printNl(list.Count);
printNl(map.Count);
```

实现通过反射访问公开的 .NET 属性、字段和方法，并尝试按参数类型转换，因此常见的 `Length`、`Count`、`Add`、字符串 `Substring`/`IndexOf` 可直接使用。它不是完整的 C# 互操作层：重载解析、泛型类型集合与可用类型均受解析器白名单/运行时实现限制。

`range` 返回整数列表，终点不包含：

```feisharp
range(5);          // 0..4
range(2, 8);       // 2..7
range(10, 0, -2);  // 10,8,6,4,2
```

步长为 0 会产生 `FS3005`。

## 9. 类、可见性、静态成员与继承

```feisharp
class counter {
    protected var value = 0;
    public static var created = 0;

    constructor(seed) {
        this.value = seed;
        counter.created = counter.created + 1;
    }

    public function add(delta) {
        this.value = this.value + delta;
        return this.value;
    }

    protected function rawValue() {
        return this.value;
    }
}

class limitedCounter : counter {
    private var limit = 0;

    constructor(maxValue) {
        this.limit = maxValue;
    }

    public function addSafely(delta) {
        var next = base.rawValue() + delta;
        if (next > this.limit) {
            throw "limit exceeded";
        }
        return base.add(delta);
    }
}

var c = limitedCounter(10);
printNl(c.addSafely(3));
printNl(counter.created);
```

语义摘要：

- `constructor` 是特殊构造函数；实例化通常写 `counter(10)`；
- 字段和方法支持 `public`、`protected`、`private`；
- 类成员默认方法可见性是 `private`，字段未显式修饰时也应视为内部实现；
- `static` 只允许在类内；静态成员通过类名访问，不能通过实例访问；
- `this.member` 访问当前对象；`base.member` 访问基类上下文；
- 外部访问私有/受保护成员或用实例访问静态成员产生 `FS3001`；
- 类支持单继承 `class child : parent`；
- 当前派生构造函数没有 C# 风格的 `: base(...)` 语法，基类字段默认值会合并进实例。

修饰符用于顶层声明会被拒绝。例如顶层 `public function`、`static function`、`static var` 都不是合法的推荐写法。

## 10. 异常

```feisharp
try {
    if (amount < 0) {
        throw "amount must be non-negative";
    }
} catch (ex) {
    printNl(ex.type);
    printNl(ex.msg);
    printNl(ex.index);
}
```

`throw` 字符串会产生用户异常，典型元数据为：

- `ex.type`：`UserException`；
- `ex.msg`：抛出的消息；
- `ex.index`：`FS4001`。

解析器和标准库错误同样可在 `try/catch` 中捕获。稳定错误码：

| 错误码 | 语义 |
| --- | --- |
| `FS2004` | 缺少分隔符 |
| `FS3004` | 未定义符号 |
| `FS3005` | 参数无效 |
| `FS3006` | 类型不匹配 |
| `FS3007` | 索引越界 |
| `FS3008` | 空引用 |
| `FS3009` | 只读值被修改 |
| `FS3010` | 标准 API 所需命名空间未导入 |
| `FS5001` | 文件操作失败 |
| `FS5002` | 网络请求失败 |
| `FS5003` | 网络超时 |

## 11. 文件导入和命名空间

`math_helpers.fsc`：

```feisharp
namespace Demo.Math;

function twice(value) {
    return value * 2;
}
```

`main.fsc`：

```feisharp
import "math_helpers.fsc";
using Demo.Math;

function main() {
    printNl(twice(21));
    printNl(Demo.Math.twice(3));
}
```

规则：

- `import "file.fsc";` 预处理并加载源文件；
- `using Demo.Math;` 或 `import Demo.Math;` 导入命名空间以便短名调用；
- 命名空间引用本身不会搜索磁盘；
- 普通相对路径和 `~/` 相对声明 import 的文件解析；
- `$name.fsc` 从公共导入目录解析；
- 同一预处理会话按规范化完整路径去重；
- 导入内容在当前文件之前执行，并置于匿名 `namespace { ... }` 边界；
- 文件级命名空间与 using 不传播到别的文件；
- 多文件可声明同一命名空间，共享全限定函数名；
- 当前命名空间资格化主要应用于函数，变量和类没有同等的跨文件资格化规则。

也支持块式命名空间：

```feisharp
namespace Demo {
    function hello() {
        return "hello";
    }
}
```

## 12. 标准库

### 12.1 全局函数

| API | 返回/行为 |
| --- | --- |
| `len(value)` | 字符串、数组、`ICollection` 的长度；`null` 返回 0 |
| `typeOf(value)` | `null/string/char/boolean/number/array/dictionary/list` 或 CLR 类型名 |
| `isNull(value)` | 是否为 `null` |
| `range(end)` | `[0, end)` |
| `range(start, end)` | `[start, end)`，自动选择正/负步长 |
| `range(start, end, step)` | 指定非零步长 |

### 12.2 math

无需 `using`：

```feisharp
math.abs(-2);
math.sqrt(81);
math.pow(2, 8);
math.min(2, 8);
math.max(2, 8);
math.clamp(12, 0, 10);
math.round(3.14159, 2);
math.floor(3.9);
math.ceiling(3.1);
math.pi;
math.e;
math.tau;
```

### 12.3 文件、目录和路径

必须先声明：

```feisharp
using FeiSharp.IO;
```

| 类 | 成员 |
| --- | --- |
| `file` | `exists(path)`、`readText(path)`、`writeText(path,text)`、`appendText(path,text)`、`delete(path)`、`copy(src,dst[,overwrite])`、`move(src,dst[,overwrite])` |
| `directory` | `exists(path)`、`create(path)`、`delete(path[,recursive])`、`getFiles(path[,pattern])`、`getDirectories(path[,pattern])`、`setCurrent(path)`、`current` |
| `path` | `combine(parts...)`、`getFileName(path)`、`getExtension(path)`、`getDirectoryName(path)`、`getFullPath(path)`、`changeExtension(path,ext)`、`separator`、`tempPath` |

这些 API 直接操作宿主文件系统；路径错误、权限和 I/O 异常包装为 `FS5001`。

### 12.4 HTTP

必须先声明：

```feisharp
using FeiSharp.Net;
```

```feisharp
http.get(url);
http.getStatus(url);
http.getJson(url); // 返回响应正文字符串，不自动 parse
http.post(url, body);
http.post(url, body, contentType);
http.postJson(url, jsonText);
http.request(url, method);
http.request(url, method, body, contentType);
```

每次调用创建一个 30 秒超时的客户端。请求失败、超时分别映射为 `FS5002`、`FS5003`。`post` 默认内容类型为 `application/json`。

### 12.5 JSON 与 JavaScript

无需额外 using：

```feisharp
var json = js.stringify([1, "ok"]);
var value = js.parse("[42, true]");
var answer = js.eval("1 + 2");
```

`js.parse` 将 JSON 对象转换为大小写不敏感的字典，数组转换为列表，JSON 数字转换为 FeiSharp 数字。`js.eval` 使用 Jint，执行超时为 1 秒；不要执行不可信代码。

### 12.6 浏览器自动化

`web` 基于 Playwright，并按当前实现启动 Microsoft Edge：

```feisharp
web.open("https://example.com", true); // 第二参数为 headless
web.navigate("https://example.com/page");
web.click("#submit");
web.fill("#name", "FeiSharp");
var text = web.text("h1");
var html = web.html("main");
var value = web.eval("document.title");
web.screenshot("page.png");
web.close();
```

运行环境需要可用的 Edge/Playwright 条件。自动化结束后应调用 `web.close()`。

## 13. 输入输出

```feisharp
print("no automatic newline");
printNl("with newline");

readLine("name");
readKey("key");
```

`readLine`/`readKey` 是语句式遗留 API，参数是目标变量名的字符串，而不是返回值函数：

```feisharp
readLine("text");
var numberValue = number.parse(text);
```

参数为 `"_"` 时读取但丢弃结果。

## 14. 从 C#、JavaScript、Python 迁移时的对照

| 概念 | FeiSharp | 迁移提示 |
| --- | --- | --- |
| 变量 | `var x = 1;` | 动态类型；不像 C# `var` 那样静态推断 |
| 常量 | `const x = 1;` | 运行时只读检查 |
| 相等 | `a = b` | 不是 `==`/`===` |
| 不等 | `a != b` | 与三种语言常见写法一致 |
| 乘方 | `a $ b` | Python `**`、JS `**`、C# `Math.Pow` |
| 条件值 | `x when c otherwise y` | 对应 C#/JS `c ? x : y`、Python `x if c else y` |
| 函数 | `function f(a) { return a; }` | 无类型标注、无 `def`/箭头函数语法 |
| 列表 | `new List<int> { 1 }` | 接近 C#；也可用 `[1]` 数组字面量 |
| 字典 | `new Dictionary<string,int> { { "a", 1 } }` | 当前写法最接近 C# 初始化器 |
| 构造对象 | `widget(1)` | 用户类通常不写 `new` |
| 继承 | `class child : parent` | 接近 C# |
| 当前对象 | `this` | Python 的显式 `self` 参数不需要写 |
| 模块 | 文件 `import` + 命名空间 `using` | 加载文件与导入短名分离 |
| 空值 | `null` | 对应 JS/C# `null`、Python `None` |

## 15. 当前实现边界与稳健写法

为了让代码在当前版本上更可靠：

- 每条普通语句都写分号；
- 所有控制流与函数使用 `{ ... }`，不要依赖遗留的 `fbegin/fend`、`cbegin/cend`；
- 入口严格写 `function main()`；
- 一律用 lower camel case，虽然名称解析不区分大小写；
- 相等比较记住用单个 `=`；
- 复杂表达式主动加括号；
- 不依赖复杂字符串转义或插值，使用 `+` 拼接；
- 不依赖未声明函数的前向解析，尤其是跨文件循环依赖；
- 文件、HTTP 操作用 `try/catch`；
- I/O 和网络类使用前声明相应 `using`；
- 不把 `js.eval` 用于不可信输入；
- 复杂复合赋值写成显式赋值；
- 把遗留命令式关键词（如旧数据 API、反射调用 API）视为兼容层，新代码优先使用本文中的表达式式标准库。

当前字符串词法读取直到下一个双引号，不完整支持主流语言的转义规则。需要引号、换行等复杂文本时，建议拆分输出、从文件读取，或先验证目标文本在当前版本的行为。

## 16. 可运行综合示例

```feisharp
using FeiSharp.IO;

function average(values) {
    if (len(values) = 0) {
        throw "cannot average an empty collection";
    }

    var total = 0;
    foreach (var value in values) {
        total = total + value;
    }
    return total / len(values);
}

class report {
    private var title = "";
    private var values = [];

    constructor(title, values) {
        this.title = title;
        this.values = values;
    }

    public function render() {
        return this.title + ": average=" + average(this.values);
    }
}

function main() {
    try {
        var scores = [92, 78, 85, 96];
        var result = report("scores", scores);
        var text = result.render();
        printNl(text);
        file.writeText("report.txt", text);
    } catch (ex) {
        printNl("[" + ex.index + "] " + ex.msg);
    }
}
```

## 17. 继续探索仓库

建议按以下顺序阅读和运行：

1. `Samples/LanguageTour/main.fsc`：基础语法、集合、流程、函数、标准库；
2. `Samples/LanguageTour/tour_helpers.fsc`：文件导入与命名空间；
3. `Samples/LanguageTour/advanced.fsc`：类、继承、静态成员和异常；
4. `FeiSharpTests.cs`：当前行为的可执行规格；
5. `StandardLibrary.cs`：标准 API 的参数、返回值与错误映射；
6. `Parser.cs`：语法和运行时语义的最终依据。

运行语言导览：

```powershell
dotnet run --project FeiSharp.csproj -- Samples/LanguageTour/main.fsc
dotnet run --project FeiSharp.csproj -- Samples/LanguageTour/advanced.fsc
```
掌握本指南后，你应能独立编写多文件 FeiSharp 程序、设计函数和类、操作集合与文件、调用 HTTP/JSON API，并能根据 `FS` 错误码定位大多数问题。
