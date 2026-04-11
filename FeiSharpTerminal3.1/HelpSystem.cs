// HelpSystem.cs
using Spectre.Console;
using System.Text;

namespace FeiSharp8._5RuntimeSdk
{
    public static class HelpSystem
    {
        private static Dictionary<string, CommandHelp> _commands = new Dictionary<string, CommandHelp>
        {
            ["help"] = new CommandHelp
            {
                Name = "help",
                Summary = "Display this interactive help system",
                Description = "The help command displays detailed information about all available FeiSharp commands. " +
                             "You can navigate using arrow keys, search with '/', and press 'q' to exit.",
                Usage = "help [command]",
                Examples = new[]
                {
                    "help           - Show interactive help browser",
                    "help build     - Show help for build command",
                    "help file      - Show help for file command"
                },
                Options = new[]
                {
                    "command - Optional: Display help for a specific command"
                },
                SeeAlso = new[] { "build", "file", "version", "license" }
            },

            ["version"] = new CommandHelp
            {
                Name = "version",
                Summary = "Display version information and manage FeiSharp versions",
                Description = "Shows current FeiSharp version and provides options to check for updates or switch between versions.",
                Usage = "version --update | --select",
                Examples = new[]
                {
                    "version           - Show current version",
                    "version --update   - Check for updates",
                    "version --select   - Select and switch to different version"
                },
                Options = new[]
                {
                    "--update  - Check for available updates",
                    "--select  - Interactive version selection"
                },
                SeeAlso = new[] { "help", "license" }
            },

            ["license"] = new CommandHelp
            {
                Name = "license",
                Summary = "Display license information",
                Description = "Shows the Apache License 2.0 under which FeiSharp is distributed.",
                Usage = "license --type | -v",
                Examples = new[]
                {
                    "license        - Show full license text",
                    "license --type  - Show license type",
                    "license -v      - Show license version"
                },
                Options = new[]
                {
                    "--type  - Display license type only",
                    "-v      - Display license version only"
                },
                SeeAlso = new[] { "copyright", "credits" }
            },

            ["copyright"] = new CommandHelp
            {
                Name = "copyright",
                Summary = "Display copyright information",
                Description = "Shows copyright information for FeiSharp and its contributors.",
                Usage = "copyright",
                Examples = new[]
                {
                    "copyright  - Show copyright information"
                },
                SeeAlso = new[] { "license", "credits" }
            },

            ["credits"] = new CommandHelp
            {
                Name = "credits",
                Summary = "Display credits and acknowledgments",
                Description = "Shows a list of individuals and organizations that have contributed to FeiSharp.",
                Usage = "credits",
                Examples = new[]
                {
                    "credits  - Show credits and acknowledgments"
                },
                SeeAlso = new[] { "copyright", "license" }
            },

            ["run"] = new CommandHelp
            {
                Name = "run",
                Summary = "Enter interactive code execution mode",
                Description = "Switches to interactive mode where you can enter and execute FeiSharp code line by line. " +
                             "Type 'exit' to return to the command prompt.",
                Usage = "run",
                Examples = new[]
                {
                    "run  - Enter code execution mode",
                    "Then type your code, press Enter to execute, type 'exit' to quit"
                },
                SeeAlso = new[] { "file", "help" }
            },

            ["file"] = new CommandHelp
            {
                Name = "file",
                Summary = "Load and execute a FeiSharp source file or project",
                Description = "Reads and executes FeiSharp code from a source file path. " +
                             "You can also pass a .feiproj file and FeiSharp will resolve its project_main_file automatically.",
                Usage = "file",
                Examples = new[]
                {
                    "file  - Enter file path mode",
                    "Then enter the path to your .fsc file",
                    "You can also enter the path to a .feiproj file"
                },
                SeeAlso = new[] { "run", "build", "help" }
            },

            ["build"] = new CommandHelp
            {
                Name = "build",
                Summary = "Compile a FeiSharp source file or project to a Windows exe",
                Description = "Builds a standalone Windows executable from a .fsc source file or a .feiproj project file. " +
                             "If you omit the output path, the exe is written to bin\\obj\\feisharp.sdk 8.0\\项目名.exe inside the project directory.",
                Usage = "build [source.fsc|project.feiproj]",
                Examples = new[]
                {
                    "build                       - Enter interactive build mode",
                    "build app.fsc               - Build bin\\obj\\feisharp.sdk 8.0\\app.exe",
                    "build demo.feiproj          - Build bin\\obj\\feisharp.sdk 8.0\\<project_name>.exe",
                    "CLI: feisharp --build app.fsc out\\app.exe"
                },
                Options = new[]
                {
                    "source.fsc|project.feiproj - Source file or project file to compile",
                    "output.exe                 - Optional output path when using --build on the CLI"
                },
                SeeAlso = new[] { "file", "run", "create", "help" }
            },

            ["cd"] = new CommandHelp
            {
                Name = "cd",
                Summary = "Change current directory",
                Description = "Changes the current working directory. Use 'cd..' to go to parent directory.",
                Usage = "cd [directory]",
                Examples = new[]
                {
                    "cd C:\\Projects    - Change to C:\\Projects",
                    "cd ..             - Go to parent directory",
                    "cd..              - Also works without space",
                    "cd Documents      - Change to Documents subfolder"
                },
                Options = new[]
                {
                    "directory - Path to change to"
                },
                SeeAlso = new[] { "pwd", "help" }
            },

            ["pwd"] = new CommandHelp
            {
                Name = "pwd",
                Summary = "Print working directory",
                Description = "Displays the full path of the current working directory.",
                Usage = "pwd",
                Examples = new[]
                {
                    "pwd  - Show current directory path"
                },
                SeeAlso = new[] { "cd", "help" }
            },

            ["cls"] = new CommandHelp
            {
                Name = "cls",
                Summary = "Clear the screen",
                Description = "Clears all text from the console window and resets the display.",
                Usage = "cls or clear",
                Examples = new[]
                {
                    "cls    - Clear screen",
                    "clear  - Also works"
                },
                Aliases = new[] { "clear" },
                SeeAlso = new[] { "resetColor", "help" }
            },

            ["clear"] = new CommandHelp
            {
                Name = "clear",
                Summary = "Clear the screen (alias for cls)",
                Description = "Clears all text from the console window and resets the display. Alias for 'cls'.",
                Usage = "clear",
                Examples = new[]
                {
                    "clear  - Clear screen"
                },
                SeeAlso = new[] { "cls", "help" }
            },

            ["feedback"] = new CommandHelp
            {
                Name = "feedback",
                Summary = "Send feedback to FeiSharp developers",
                Description = "Allows you to send feedback, bug reports, or suggestions to the FeiSharp team.",
                Usage = "feedback",
                Examples = new[]
                {
                    "feedback  - Enter feedback mode",
                    "Then type your feedback and press Enter to send"
                },
                SeeAlso = new[] { "help", "version" }
            },

            ["color"] = new CommandHelp
            {
                Name = "color",
                Summary = "Change console text color",
                Description = "Interactively select and change the console text color.",
                Usage = "color",
                Examples = new[]
                {
                    "color  - Launch interactive color selector"
                },
                SeeAlso = new[] { "resetColor", "help" }
            },

            ["resetColor"] = new CommandHelp
            {
                Name = "resetColor",
                Summary = "Reset console colors to defaults",
                Description = "Resets both foreground and background colors to their default values.",
                Usage = "resetColor",
                Examples = new[]
                {
                    "resetColor  - Reset to default colors"
                },
                SeeAlso = new[] { "color", "help" }
            },

            ["base"] = new CommandHelp
            {
                Name = "base",
                Summary = "Show FeiSharp technology stack",
                Description = "Displays information about the underlying technologies used by FeiSharp.",
                Usage = "base",
                Examples = new[]
                {
                    "base  - Show technology stack information"
                },
                SeeAlso = new[] { "version", "help" }
            },
            ["create"] = new CommandHelp
            {
                Name = "create",
                Summary = "Create FeiSharp project",
                Description = "Create some project by FeiSharp.",
                Usage = "create [ProjectType] [ProjectName]",
                Examples = new[]
                {
                    "create Console ConsoleApp1  - Create a console project named ConsoleApp1"
                },
                SeeAlso = new[] { "run", "file" }
            },

            ["ui"] = new CommandHelp
            {
                Name = "ui",
                Summary = "Launch FeiSharp UI application",
                Description = "Launches the graphical user interface version of FeiSharp with a specified file.",
                Usage = "ui",
                Examples = new[]
                {
                    "ui  - Launch UI mode",
                    "Then enter the path to the file you want to open"
                },
                SeeAlso = new[] { "run", "file", "help" }
            },

            ["exit"] = new CommandHelp
            {
                Name = "exit",
                Summary = "Exit FeiSharp",
                Description = "Terminates the FeiSharp terminal session and closes the application.",
                Usage = "exit",
                Examples = new[]
                {
                    "exit  - Close FeiSharp"
                },
                SeeAlso = new[] { "help" }
            }
        };

        public static async Task ShowInteractiveHelp(string specificCommand = null)
        {
            Console.CursorVisible = false;

            if (!string.IsNullOrEmpty(specificCommand) && _commands.ContainsKey(specificCommand))
            {
                await ShowCommandHelp(specificCommand);
                return;
            }

            await ShowHelpBrowser();

            Console.CursorVisible = true;
        }

        private static async Task ShowHelpBrowser()
        {
            var selectedIndex = 0;
            var searchMode = false;
            var searchQuery = "";
            var filteredCommands = _commands.Values.OrderBy(c => c.Name).ToList();

            while (true)
            {
                Console.Clear();

                // 标题
                AnsiConsole.Write(
                    new FigletText("FeiSharp Help")
                        .Color(Color.Cyan1));

                // 搜索栏
                if (searchMode)
                {
                    AnsiConsole.Markup($"[yellow]Search: [/][green]{searchQuery}[/][yellow]_[/]");
                    AnsiConsole.WriteLine();
                }
                else
                {
                    AnsiConsole.Markup("[grey]Press [/][yellow]/[/][grey] to search, [/][yellow]↑/↓[/][grey] to navigate, [/][yellow]Enter[/][grey] to view, [/][yellow]q[/][grey] to quit[/]");
                    AnsiConsole.WriteLine();
                }

                AnsiConsole.WriteLine();

                // 创建命令列表
                var table = new Table()
                    .Border(TableBorder.Rounded)
                    .AddColumn(new TableColumn("[yellow]Command[/]").Centered())
                    .AddColumn(new TableColumn("[yellow]Description[/]"));

                for (int i = 0; i < filteredCommands.Count; i++)
                {
                    var cmd = filteredCommands[i];
                    var prefix = i == selectedIndex ? "→ " : "  ";
                    var commandName = i == selectedIndex
                        ? $"[bold green]{prefix}{cmd.Name}[/]"
                        : $"[white]{prefix}{cmd.Name}[/]";

                    table.AddRow(
                        commandName,
                        $"[grey]{cmd.Summary}[/]"
                    );
                }

                AnsiConsole.Write(table);

                // 底部提示
                AnsiConsole.WriteLine();
                AnsiConsole.MarkupLine($"[grey]Showing {filteredCommands.Count} commands[/]");

                // 处理键盘输入
                var key = Console.ReadKey(true);

                if (searchMode)
                {
                    if (key.Key == ConsoleKey.Enter)
                    {
                        searchMode = false;
                        if (!string.IsNullOrWhiteSpace(searchQuery))
                        {
                            filteredCommands = _commands.Values
                                .Where(c => c.Name.Contains(searchQuery, StringComparison.OrdinalIgnoreCase) ||
                                           c.Summary.Contains(searchQuery, StringComparison.OrdinalIgnoreCase) ||
                                           c.Description.Contains(searchQuery, StringComparison.OrdinalIgnoreCase))
                                .OrderBy(c => c.Name)
                                .ToList();
                            selectedIndex = 0;
                        }
                        searchQuery = "";
                    }
                    else if (key.Key == ConsoleKey.Escape)
                    {
                        searchMode = false;
                        searchQuery = "";
                        filteredCommands = _commands.Values.OrderBy(c => c.Name).ToList();
                        selectedIndex = 0;
                    }
                    else if (key.Key == ConsoleKey.Backspace && searchQuery.Length > 0)
                    {
                        searchQuery = searchQuery[0..^1];
                    }
                    else if (!char.IsControl(key.KeyChar))
                    {
                        searchQuery += key.KeyChar;
                    }
                }
                else
                {
                    switch (key.Key)
                    {
                        case ConsoleKey.UpArrow:
                            selectedIndex = Math.Max(0, selectedIndex - 1);
                            break;

                        case ConsoleKey.DownArrow:
                            selectedIndex = Math.Min(filteredCommands.Count - 1, selectedIndex + 1);
                            break;

                        case ConsoleKey.Enter:
                            if (filteredCommands.Count > 0)
                            {
                                await ShowCommandHelp(filteredCommands[selectedIndex].Name);
                            }
                            break;

                        case ConsoleKey.Oem2: // '/' key
                        case ConsoleKey.Divide:
                            searchMode = true;
                            searchQuery = "";
                            break;

                        case ConsoleKey.Q:
                            return;
                    }
                }
            }
        }

        private static async Task ShowCommandHelp(string commandName)
        {
            if (!_commands.ContainsKey(commandName))
                return;

            var cmd = _commands[commandName];
            var currentPage = 0;

            while (true)
            {
                Console.Clear();

                // 标题
                AnsiConsole.Write(
                    new FigletText(cmd.Name)
                        .Color(Color.Green));

                // 命令摘要
                AnsiConsole.MarkupLine($"[yellow]NAME[/]");
                AnsiConsole.MarkupLine($"    [green]{cmd.Name}[/] - {cmd.Summary}");
                AnsiConsole.WriteLine();

                // 用法
                AnsiConsole.MarkupLine($"[yellow]SYNOPSIS[/]");
                AnsiConsole.MarkupLine($"    [cyan]{cmd.Usage.EscapeMarkup()}[/]");
                AnsiConsole.WriteLine();

                // 描述
                AnsiConsole.MarkupLine($"[yellow]DESCRIPTION[/]");
                AnsiConsole.MarkupLine($"    {cmd.Description}");
                AnsiConsole.WriteLine();

                // 选项（如果有）
                if (cmd.Options?.Any() == true)
                {
                    AnsiConsole.MarkupLine($"[yellow]OPTIONS[/]");
                    foreach (var option in cmd.Options)
                    {
                        AnsiConsole.MarkupLine($"    [cyan]{option}[/]");
                    }
                    AnsiConsole.WriteLine();
                }

                // 示例
                if (cmd.Examples?.Any() == true)
                {
                    AnsiConsole.MarkupLine($"[yellow]EXAMPLES[/]");
                    foreach (var example in cmd.Examples)
                    {
                        AnsiConsole.MarkupLine($"    [grey]{example}[/]");
                    }
                    AnsiConsole.WriteLine();
                }

                // 别名（如果有）
                if (cmd.Aliases?.Any() == true)
                {
                    AnsiConsole.MarkupLine($"[yellow]ALIASES[/]");
                    AnsiConsole.MarkupLine($"    [green]{string.Join(", ", cmd.Aliases)}[/]");
                    AnsiConsole.WriteLine();
                }

                // 相关命令
                if (cmd.SeeAlso?.Any() == true)
                {
                    AnsiConsole.MarkupLine($"[yellow]SEE ALSO[/]");
                    var seeAlsoLinks = string.Join(", ", cmd.SeeAlso.Select(s => $"[blue]{s}[/]"));
                    AnsiConsole.MarkupLine($"    {seeAlsoLinks}");
                    AnsiConsole.WriteLine();
                }

                // 底部导航
                AnsiConsole.MarkupLine("[grey]Press [/][yellow]q[/][grey] to return to help browser, [/][yellow]Enter[/][grey] to view related commands[/]");

                var key = Console.ReadKey(true);

                if (key.Key == ConsoleKey.Q)
                {
                    break;
                }
                else if (key.Key == ConsoleKey.Enter && cmd.SeeAlso?.Any() == true)
                {
                    // 显示相关命令选择菜单
                    var relatedCommand = AnsiConsole.Prompt(
                        new SelectionPrompt<string>()
                            .Title("[cyan]Select a related command:[/]")
                            .PageSize(10)
                            .AddChoices(cmd.SeeAlso));

                    if (_commands.ContainsKey(relatedCommand))
                    {
                        cmd = _commands[relatedCommand];
                    }
                }
            }
        }
    }

    public class CommandHelp
    {
        public string Name { get; set; }
        public string Summary { get; set; }
        public string Description { get; set; }
        public string Usage { get; set; }
        public string[] Examples { get; set; }
        public string[] Options { get; set; }
        public string[] Aliases { get; set; }
        public string[] SeeAlso { get; set; }
    }
}
