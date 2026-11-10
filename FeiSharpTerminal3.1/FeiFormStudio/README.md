# FeiForm Studio

FeiForm Studio is FeiSharp's visual WinForms designer. Drag controls from the toolbox, move them on the design surface, edit their properties, save a `.feiform` project, then export ordinary `.fsc` source.

The included **ChronoFei** project is a stopwatch form layout. The generated source deliberately uses the existing FeiSharp model—`using`, `function`, `var`, strings and regular member calls—rather than adding a separate markup language.

Run it with:

```powershell
dotnet run --project FeiFormStudio/FeiFormStudio.csproj
```
