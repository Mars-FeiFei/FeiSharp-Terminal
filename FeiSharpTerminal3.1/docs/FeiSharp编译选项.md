# FeiSharp 编译选项

FeiSharp 同时接受 `build` 和旧的 `--build` 写法：

```powershell
feisharp build app.fsc --mode aot -o out/app.exe
feisharp build demo.feiproj -c Debug --no-clean
feisharp --build app.fsc out/app.exe
```

## 命令行选项

| 选项 | 说明 |
| --- | --- |
| `-o, --output <path>` | 输出文件或目录；仍兼容旧的位置输出参数 |
| `-c, --configuration Debug\|Release` | 构建配置 |
| `-r, --runtime <RID>` | 目标运行时，例如 `win-x64`、`win-arm64` |
| `--mode aot\|self-contained\|framework-dependent` | 发布模式 |
| `--aot`、`--self-contained`、`--framework-dependent` | 发布模式的快捷写法 |
| `--single-file` / `--no-single-file` | 是否合并为单文件 |
| `--trim` / `--no-trim` | 是否裁剪未使用的程序集代码 |
| `--debug-symbols` / `--no-debug-symbols` | 是否生成调试符号 |
| `--full-globalization` / `--invariant-globalization` | 完整或固定区域性数据 |
| `--clean` / `--no-clean` | 构建前是否清理输出目录 |
| `--artifacts` / `--executable-only` | 复制全部发布文件或只复制主程序 |
| `--run` | CLI 构建成功后运行程序 |

命令行选项优先于 `.feiproj`。运行 `feisharp build --help` 可以随时查看简要帮助。

## `.feiproj` 配置

项目文件支持下面的新字段，同时保留原有字段：

```json
{
  "project_name": "demo",
  "project_main_file": "main.fsc",
  "target_platform": "win-x64",
  "build_configuration": "Release",
  "publish_mode": "self-contained",
  "single_file": true,
  "trimmed": false,
  "debug_symbols": false,
  "invariant_globalization": true,
  "output_path": "out/demo.exe",
  "clean_build_directory": true,
  "only_copy_executable": true
}
```

`publish_mode` 可取 `aot`、`self-contained` 或 `framework-dependent`。框架依赖且非单文件的发布结果需要配套 DLL，因此不能使用 `--executable-only`。
