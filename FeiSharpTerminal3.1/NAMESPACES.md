Cross-File Namespaces
When a `.fsc` file is run or built, sibling `.fsc` files in the same directory that declare a named namespace are automatically loaded into the same compilation unit. The statement `import "file.fsc";` still loads a source file explicitly, while `import Demo.Math;` (or `using Demo.Math;`) enables functions in that namespace to be invoked via short names.
For example, two files are placed in the same directory:
```text
// math.fsc
namespace Demo.Math;
function twice(value) { return value * 2; }
```
```text
// main.fsc
import Demo.Math;
print(twice(21));
print(Demo.Math.twice(3));
```
- Files without a named namespace are not loaded automatically, so unrelated sibling scripts do not execute as a side effect.
- Automatic namespace sharing only examines the entry file's directory and is non-recursive. Files elsewhere still require an explicit source import.
- Ordinary relative paths and the `~/` prefix are resolved relative to the file where the import statement is written. Absolute paths are supported, and the `$` prefix still points to the public import directory.
- Multiple files may declare the same namespace, and functions share their fully qualified names, such as `Demo.Math.twice`.
- A previously declared class may be imported under a local alias with `import Demo.Models.Counter as Tally;`. Classes without constructor parameters are immediately available as module-like instances, so `Tally.method()` calls their public instance methods. Classes requiring constructor arguments retain the class alias and can be instantiated with `Tally(...)`.
- Each file retains its own namespace and references, and the file-level `namespace Demo.Math;` declaration will not propagate to other files. Function execution uses the namespace references from the location where the function is defined.
- In a single preprocessing session, deduplication is performed based on full paths, so repeated imports and circular imports will not be expanded multiple times. Dependent source code is still executed prior to the current file; circular dependencies do not guarantee forward declaration of functions that have not yet been executed.
- Missing source files will trigger a report of the file path and halt preprocessing. The `main` function in imported files will not be automatically invoked.
Preprocessing preserves file boundaries through anonymous `namespace { ... }` blocks; regular named blocks such as `namespace Demo.Math { ... }` remain available. Symbol qualification for the current namespace follows the existing function mechanism, and variables and classes do not acquire new namespace qualification rules as a result.
