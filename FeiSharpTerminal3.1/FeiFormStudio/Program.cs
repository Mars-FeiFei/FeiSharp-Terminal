using System.Drawing;
using System.Text;
using System.Text.Json;

namespace FeiFormStudio;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.Run(new StudioForm());
    }
}

/// <summary>A compact, FeiSharp-branded WinForms designer.  It deliberately
/// emits normal FeiSharp calls rather than inventing a second language.</summary>
internal sealed class StudioForm : Form
{
    private readonly ListBox _toolbox = new() { Dock = DockStyle.Fill };
    private readonly Panel _surface = new() { Dock = DockStyle.Fill, BackColor = Color.White, AllowDrop = true, BorderStyle = BorderStyle.FixedSingle };
    private readonly PropertyGrid _properties = new() { Dock = DockStyle.Fill };
    private Point _dragOffset;
    private Control? _selected;
    private string? _projectPath;
    private readonly List<Panel> _resizeHandles = [];
    private bool _resizing;
    private int _resizeHandleIndex;
    private Rectangle _resizeStartBounds;
    private Point _resizeStartPoint;
    private FormControl? _clipboardControl;

    public StudioForm()
    {
        Text = "FeiForm Studio — FeiSharp Visual Forms";
        AutoScaleMode = AutoScaleMode.None;
        Width = 1280; Height = 800; StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(760, 480);
        var menu = new MenuStrip();
        var fileMenu = new ToolStripMenuItem("文件");
        fileMenu.DropDownItems.Add("新建", null, NewProject);
        fileMenu.DropDownItems.Add("打开…", null, OpenProject);
        fileMenu.DropDownItems.Add("保存", null, SaveProject);
        fileMenu.DropDownItems.Add("导出 FeiSharp…", null, ExportFeiSharp);
        menu.Items.Add(fileMenu);
        menu.Items.Add("帮助", null, (_, _) => MessageBox.Show("拖动左侧控件到窗体画布；选中控件后可在右侧编辑属性。\n导出的代码只使用 FeiSharp 普通函数调用语法。", "FeiForm Studio"));
        Controls.Add(menu); MainMenuStrip = menu;

        _toolbox.Items.AddRange(["Pointer", "Button", "CheckBox", "CheckedListBox", "ComboBox", "DateTimePicker", "DomainUpDown", "Label", "LinkLabel", "ListBox", "ListView", "MaskedTextBox", "MonthCalendar", "NumericUpDown", "PictureBox", "ProgressBar", "RadioButton", "RichTextBox", "TextBox", "TrackBar", "TreeView", "WebBrowser", "HScrollBar", "VScrollBar", "Panel", "GroupBox", "TabControl", "TableLayoutPanel", "FlowLayoutPanel", "SplitContainer", "DataGridView", "MenuStrip", "StatusStrip", "ToolStrip", "Timer"]);
        _toolbox.MouseDown += (_, e) => { if (_toolbox.SelectedItem is string type) _toolbox.DoDragDrop(type, DragDropEffects.Copy); };
        _surface.DragEnter += (_, e) => e.Effect = e.Data?.GetDataPresent(typeof(string)) == true ? DragDropEffects.Copy : DragDropEffects.None;
        _surface.DragDrop += (_, e) => AddControl((string)e.Data!.GetData(typeof(string))!, _surface.PointToClient(new Point(e.X, e.Y)));
        _surface.MouseDown += (_, e) => { if (e.Button == MouseButtons.Left) Select(null); };
        _surface.KeyDown += SurfaceKeyDown;
        _surface.TabStop = true;
        _surface.ContextMenuStrip = CreateSurfaceMenu();

        // Drag either splitter like the Visual Studio designer to change the
        // right Properties pane or the left Toolbox width.
        // Size must precede minimum sizes and distance: a new SplitContainer
        // is only 150 pixels wide, too narrow even for the properties minimum.
        var centerAndProperties = new SplitContainer { Size = new Size(1000, 700), Dock = DockStyle.Fill, FixedPanel = FixedPanel.Panel2, SplitterWidth = 6, Panel1MinSize = 240, Panel2MinSize = 180 };
        centerAndProperties.Panel1.Controls.Add(Group("Form1 · 设计视图", _surface));
        centerAndProperties.Panel2.Controls.Add(Group("属性", _properties));
        var designerLayout = new SplitContainer { Size = ClientSize, Dock = DockStyle.Fill, FixedPanel = FixedPanel.Panel1, SplitterWidth = 6, Panel1MinSize = 140, Panel2MinSize = 426 };
        designerLayout.Panel1.Controls.Add(Group("工具箱", _toolbox));
        designerLayout.Panel2.Controls.Add(centerAndProperties);
        Controls.Add(designerLayout); designerLayout.BringToFront(); menu.BringToFront();
        // Choose the initial widths after docking, once the actual available
        // space is known. Do not reset them when the user later resizes panes.
        SetSplitterDistance(designerLayout, 210);
        SetSplitterDistance(centerAndProperties, centerAndProperties.ClientSize.Width - centerAndProperties.SplitterWidth - 285);
        LoadChronoFei();
    }

    private static void SetSplitterDistance(SplitContainer split, int desired)
    {
        int maximum = split.ClientSize.Width - split.SplitterWidth - split.Panel2MinSize;
        if (maximum >= split.Panel1MinSize)
            split.SplitterDistance = Math.Clamp(desired, split.Panel1MinSize, maximum);
    }

    private static GroupBox Group(string title, Control child) { var group = new GroupBox { Text = title, Dock = DockStyle.Fill, Padding = new Padding(8) }; group.Controls.Add(child); return group; }

    private void AddControl(string type, Point location)
    {
        if (type == "Pointer") return;
        Control control = type switch
        {
            "Label" => new Label { Text = "FeiSharp Label", AutoSize = true },
            "Button" => new Button { Text = "Button", Size = new Size(110, 34) },
            "TextBox" => new TextBox { Text = "", Size = new Size(180, 28) },
            "CheckBox" => new CheckBox { Text = "CheckBox", AutoSize = true },
            "ProgressBar" => new ProgressBar { Size = new Size(180, 26), Value = 55 },
            "Panel" => new Panel { Size = new Size(220, 120), BackColor = Color.AliceBlue, BorderStyle = BorderStyle.FixedSingle },
            "Timer" => new Label { Text = "Timer (non-visual component)", AutoSize = true, ForeColor = Color.SlateGray, Tag = "Timer" },
            _ => CreateStandardControl(type)
        };
        control.Name = UniqueName(type.ToLowerInvariant()); control.Location = location;
        control.MouseDown += SelectAndStartMove; control.MouseMove += MoveSelected; control.Click += (_, _) => Select(control);
        control.ContextMenuStrip = CreateControlMenu(control);
        _surface.Controls.Add(control); Select(control);
    }

    private static Control CreateStandardControl(string type)
    {
        var controlType = typeof(Control).Assembly.GetType($"System.Windows.Forms.{type}");
        if (controlType == null || !typeof(Control).IsAssignableFrom(controlType)) return new Label { Text = type, AutoSize = true };
        var control = (Control)Activator.CreateInstance(controlType)!;
        control.Size = type is "MonthCalendar" ? new Size(230, 165) : new Size(180, 32);
        if (control is TabControl tabs) { tabs.TabPages.Add("Tab 1"); tabs.TabPages.Add("Tab 2"); control.Size = new Size(260, 160); }
        if (control is GroupBox group) { group.Text = "GroupBox"; control.Size = new Size(220, 120); }
        if (control is ListView listView) { listView.View = View.Details; listView.Columns.Add("Column", 160); }
        if (control is DataGridView grid) { grid.ColumnCount = 2; grid.Columns[0].Name = "Column 1"; grid.Columns[1].Name = "Column 2"; control.Size = new Size(280, 160); }
        if (control is MenuStrip menuStrip) menuStrip.Items.Add("Menu");
        if (control is StatusStrip statusStrip) statusStrip.Items.Add("Status");
        if (control is ToolStrip toolStrip) toolStrip.Items.Add("Tool");
        if (control is TextBoxBase || control is ButtonBase || control is Label || control is LinkLabel || control is GroupBox) control.Text = type;
        return control;
    }

    private string UniqueName(string prefix) => prefix + (_surface.Controls.Cast<Control>().Count(c => c.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) + 1);
    private void Select(Control? control)
    {
        _selected = control;
        _properties.SelectedObject = control;
        foreach (var handle in _resizeHandles) _surface.Controls.Remove(handle);
        _resizeHandles.Clear();
        if (control == null) return;
        for (var i = 0; i < 8; i++)
        {
            var handle = new Panel { Size = new Size(7, 7), BackColor = Color.DodgerBlue, Cursor = ResizeCursor(i), Tag = i };
            handle.MouseDown += ResizeStart; handle.MouseMove += ResizeMove; handle.MouseUp += ResizeEndHandle;
            _resizeHandles.Add(handle); _surface.Controls.Add(handle); handle.BringToFront();
        }
        PositionHandles();
    }

    private void SelectAndStartMove(object? sender, MouseEventArgs e)
    {
        if (sender is not Control control) return;
        if (e.Button == MouseButtons.Right) { Select(control); return; }
        if (e.Button != MouseButtons.Left) return;
        Select(control); _dragOffset = e.Location; _surface.Focus();
    }

    private void MoveSelected(object? sender, MouseEventArgs e)
    {
        if (_selected == null || _resizing || e.Button != MouseButtons.Left) return;
        var point = _surface.PointToClient(Cursor.Position);
        _selected.Location = new Point(Math.Max(0, Math.Min(_surface.ClientSize.Width - _selected.Width, point.X - _dragOffset.X)), Math.Max(0, Math.Min(_surface.ClientSize.Height - _selected.Height, point.Y - _dragOffset.Y)));
        PositionHandles();
    }

    private void PositionHandles()
    {
        if (_selected == null) return;
        var r = _selected.Bounds; var x = new[] { r.Left - 4, r.Left + r.Width / 2 - 3, r.Right - 3 }; var y = new[] { r.Top - 4, r.Top + r.Height / 2 - 3, r.Bottom - 3 };
        var positions = new[] { new Point(x[0], y[0]), new Point(x[1], y[0]), new Point(x[2], y[0]), new Point(x[2], y[1]), new Point(x[2], y[2]), new Point(x[1], y[2]), new Point(x[0], y[2]), new Point(x[0], y[1]) };
        for (var i = 0; i < _resizeHandles.Count; i++) _resizeHandles[i].Location = positions[i];
    }

    private static Cursor ResizeCursor(int index) => index is 0 or 4 ? Cursors.SizeNWSE : index is 2 or 6 ? Cursors.SizeNESW : index is 1 or 5 ? Cursors.SizeNS : Cursors.SizeWE;
    private void ResizeStart(object? sender, MouseEventArgs e) { if (sender is Panel h && e.Button == MouseButtons.Left && _selected != null) { _resizing = true; _resizeHandleIndex = (int)h.Tag!; _resizeStartBounds = _selected.Bounds; _resizeStartPoint = Cursor.Position; h.Capture = true; } }
    private void ResizeMove(object? sender, MouseEventArgs e)
    {
        if (!_resizing || _selected == null) return;
        var d = Cursor.Position - new Size(_resizeStartPoint); var r = _resizeStartBounds; const int min = 24;
        if (_resizeHandleIndex is 0 or 7 or 6) { r.X += d.X; r.Width -= d.X; } if (_resizeHandleIndex is 0 or 1 or 2) { r.Y += d.Y; r.Height -= d.Y; }
        if (_resizeHandleIndex is 2 or 3 or 4) r.Width += d.X; if (_resizeHandleIndex is 4 or 5 or 6) r.Height += d.Y;
        if (r.Width < min) { if (_resizeHandleIndex is 0 or 7 or 6) r.X = _resizeStartBounds.Right - min; r.Width = min; }
        if (r.Height < min) { if (_resizeHandleIndex is 0 or 1 or 2) r.Y = _resizeStartBounds.Bottom - min; r.Height = min; }
        r.X = Math.Max(0, r.X); r.Y = Math.Max(0, r.Y); r.Width = Math.Min(r.Width, _surface.ClientSize.Width - r.X); r.Height = Math.Min(r.Height, _surface.ClientSize.Height - r.Y); _selected.Bounds = r; PositionHandles();
    }
    private void ResizeEndHandle(object? sender, MouseEventArgs e) { _resizing = false; if (sender is Control h) h.Capture = false; }

    private ContextMenuStrip CreateSurfaceMenu() { var menu = new ContextMenuStrip(); menu.Items.Add("粘贴", null, (_, _) => PasteControl()); return menu; }
    private ContextMenuStrip CreateControlMenu(Control control) { var menu = CreateSurfaceMenu(); menu.Items.Insert(0, new ToolStripMenuItem("复制", null, (_, _) => CopyControl())); menu.Items.Insert(0, new ToolStripMenuItem("删除", null, (_, _) => DeleteSelected())); return menu; }
    private void CopyControl() { if (_selected != null) _clipboardControl = ToFormControl(_selected); }
    private void PasteControl() { if (_clipboardControl == null) return; var c = _clipboardControl with { Name = UniqueName(_clipboardControl.Type.ToLowerInvariant()), X = Math.Min(_clipboardControl.X + 20, Math.Max(0, _surface.ClientSize.Width - _clipboardControl.Width)), Y = Math.Min(_clipboardControl.Y + 20, Math.Max(0, _surface.ClientSize.Height - _clipboardControl.Height)) }; AddControl(c.Type, new Point(c.X, c.Y)); _selected!.Name = c.Name; _selected.Text = c.Text; _selected.Size = new Size(c.Width, c.Height); PositionHandles(); }
    private void DeleteSelected() { if (_selected == null) return; var old = _selected; Select(null); _surface.Controls.Remove(old); old.Dispose(); }
    private void SurfaceKeyDown(object? sender, KeyEventArgs e) { if (e.Control && e.KeyCode == Keys.C) { CopyControl(); e.SuppressKeyPress = true; } else if (e.Control && e.KeyCode == Keys.V) { PasteControl(); e.SuppressKeyPress = true; } else if (e.KeyCode == Keys.Delete) { DeleteSelected(); e.SuppressKeyPress = true; } }
    private static FormControl ToFormControl(Control c) => new(c.Tag as string ?? c.GetType().Name, c.Name, c.Text, c.Left, c.Top, c.Width, c.Height);

    private void NewProject(object? sender, EventArgs e) { _surface.Controls.Clear(); _projectPath = null; Text = "FeiForm Studio — Untitled"; }
    private void SaveProject(object? sender, EventArgs e)
    {
        if (_projectPath == null) { using var dialog = new SaveFileDialog { Filter = "FeiForm project|*.feiform" }; if (dialog.ShowDialog() != DialogResult.OK) return; _projectPath = dialog.FileName; }
        File.WriteAllText(_projectPath, JsonSerializer.Serialize(FormDocument.From(_surface), new JsonSerializerOptions { WriteIndented = true }));
        Text = $"FeiForm Studio — {Path.GetFileNameWithoutExtension(_projectPath)}";
    }
    private void OpenProject(object? sender, EventArgs e)
    {
        using var dialog = new OpenFileDialog { Filter = "FeiForm project|*.feiform" }; if (dialog.ShowDialog() != DialogResult.OK) return;
        LoadDocument(JsonSerializer.Deserialize<FormDocument>(File.ReadAllText(dialog.FileName)) ?? new FormDocument()); _projectPath = dialog.FileName;
    }
    private void ExportFeiSharp(object? sender, EventArgs e)
    {
        using var dialog = new SaveFileDialog { Filter = "FeiSharp source|*.fsc", FileName = "Form1.fsc" }; if (dialog.ShowDialog() == DialogResult.OK) File.WriteAllText(dialog.FileName, FeiSharpGenerator.Generate(FormDocument.From(_surface)));
    }
    private void LoadChronoFei()
    {
        LoadDocument(ChronoFei.Sample()); Text = "FeiForm Studio — ChronoFei";
    }
    private void LoadDocument(FormDocument document)
    {
        _surface.Controls.Clear(); foreach (var item in document.Controls) { AddControl(item.Type, new Point(item.X, item.Y)); var control = _selected!; control.Name = item.Name; control.Text = item.Text; control.Size = new Size(item.Width, item.Height); }
    }
}

internal sealed record FormDocument(string Title = "FeiSharp Form", int Width = 720, int Height = 460, List<FormControl> Controls = null!)
{
    public static FormDocument From(Panel surface) => new(Controls: surface.Controls.Cast<Control>().Where(control => control.Tag is not int).Select(control => new FormControl(control.Tag as string ?? control.GetType().Name, control.Name, control.Text, control.Left, control.Top, control.Width, control.Height)).ToList());
}
internal sealed record FormControl(string Type, string Name, string Text, int X, int Y, int Width, int Height);

internal static class ChronoFei
{
    public static FormDocument Sample() => new("ChronoFei — FeiSharp Stopwatch", 720, 460,
    [new("Label", "timeLabel", "00:00:00.00", 220, 85, 290, 65), new("Button", "startButton", "开始", 155, 215, 110, 38), new("Button", "pauseButton", "暂停", 305, 215, 110, 38), new("Button", "resetButton", "复位", 455, 215, 110, 38), new("Label", "titleLabel", "ChronoFei", 285, 35, 180, 30)]);
}

internal static class FeiSharpGenerator
{
    public static string Generate(FormDocument document)
    {
        var code = new StringBuilder("// Generated by FeiForm Studio. Uses ordinary FeiSharp calls only.\nusing FeiSharp.UI;\n\n");
        code.Append("function main() {\n    var form = ui.createForm(\"").Append(document.Title).Append("\", ").Append(document.Width).Append(", ").Append(document.Height).Append(");\n");
        foreach (var control in document.Controls) code.Append("    var ").Append(control.Name).Append(" = ui.create(form, \"").Append(control.Type).Append("\", \"").Append(control.Text.Replace("\"", "\\\"")).Append("\", ").Append(control.X).Append(", ").Append(control.Y).Append(", ").Append(control.Width).Append(", ").Append(control.Height).Append(");\n");
        code.Append("    ui.show(form);\n}\n"); return code.ToString();
    }
}
