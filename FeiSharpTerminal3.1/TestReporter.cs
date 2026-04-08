using Spectre.Console;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using static FeiSharpTerminal3._1.Tests.FeiSharpTests;

namespace FeiSharpTerminal3._1.Tests
{
    public static class TestReportExporter
    {
        public static readonly string ReportsDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "FeiSharp", "TestReports");

        public class TestReportData
        {
            public DateTime Timestamp { get; set; }
            public string Version { get; set; } = "8.0";
            public int TotalTests { get; set; }
            public int Passed { get; set; }
            public int Failed { get; set; }
            public double PassRate => TotalTests > 0 ? Math.Round((double)Passed / TotalTests * 100, 2) : 0;
            public double FailRate => TotalTests > 0 ? Math.Round((double)Failed / TotalTests * 100, 2) : 0;
            public TimeSpan Duration { get; set; }
            public List<TestResult> Results { get; set; } = new();
            public string SystemInfo { get; set; } = GetSystemInfo();
            public string ComputerName { get; set; } = Environment.MachineName;
            public string UserName { get; set; } = Environment.UserName;

            private static string GetSystemInfo()
            {
                return $"OS: {Environment.OSVersion}\n" +
                       $".NET Version: {Environment.Version}\n" +
                       $"64-bit OS: {Environment.Is64BitOperatingSystem}\n" +
                       $"Processor Count: {Environment.ProcessorCount}\n" +
                       $"Working Directory: {Environment.CurrentDirectory}";
            }
        }
        public static void ExportTestReport(
            List<TestResult> results,
            int passed,
            int failed,
            TimeSpan duration,
            bool openAfterExport = true)
        {
            try
            {
                // 创建报告目录
                Directory.CreateDirectory(ReportsDirectory);

                var reportData = new TestReportData
                {
                    Timestamp = DateTime.Now,
                    TotalTests = results.Count,
                    Passed = passed,
                    Failed = failed,
                    Duration = duration,
                    Results = results
                };

                // 生成文件名
                var timestamp = reportData.Timestamp.ToString("yyyyMMdd_HHmmss");
                var htmlFile = Path.Combine(ReportsDirectory, $"FeiSharp_Test_Report_{timestamp}.html");
                var jsonFile = Path.Combine(ReportsDirectory, $"FeiSharp_Test_Report_{timestamp}.json");

                // 生成报告
                GenerateHtmlReport(reportData, htmlFile);
                GenerateJsonReport(reportData, jsonFile);

                // 显示成功信息
                ShowExportSuccess(htmlFile, jsonFile, openAfterExport);
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[red]Export report failed: {ex.Message}[/]");
            }
        }

        private static void GenerateHtmlReport(TestReportData data, string filepath)
        {
            var html = $@"<!DOCTYPE html>
<html lang='zh-CN'>
<head>
    <meta charset='UTF-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <title>FeiSharp Test Report - {data.Timestamp:yyyy-MM-dd HH:mm:ss}</title>
    <style>
        * {{
            margin: 0;
            padding: 0;
            box-sizing: border-box;
            font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
        }}
        
        body {{
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
            min-height: 100vh;
            padding: 40px 20px;
        }}
        
        .container {{
            max-width: 1200px;
            margin: 0 auto;
            background: white;
            border-radius: 20px;
            box-shadow: 0 20px 60px rgba(0,0,0,0.3);
            overflow: hidden;
            animation: slideIn 0.5s ease-out;
        }}
        
        @keyframes slideIn {{
            from {{
                opacity: 0;
                transform: translateY(-30px);
            }}
            to {{
                opacity: 1;
                transform: translateY(0);
            }}
        }}
        
        .header {{
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
            color: white;
            padding: 40px;
            text-align: center;
            position: relative;
            overflow: hidden;
        }}
        
        .header::before {{
            content: '';
            position: absolute;
            top: -50%;
            right: -50%;
            width: 200%;
            height: 200%;
            background: radial-gradient(circle, rgba(255,255,255,0.1) 0%, transparent 70%);
            animation: rotate 20s linear infinite;
        }}
        
        @keyframes rotate {{
            from {{ transform: rotate(0deg); }}
            to {{ transform: rotate(360deg); }}
        }}
        
        .header h1 {{
            font-size: 3em;
            margin-bottom: 10px;
            position: relative;
            text-shadow: 2px 2px 4px rgba(0,0,0,0.2);
        }}
        
        .header p {{
            font-size: 1.2em;
            opacity: 0.95;
            position: relative;
        }}
        
        .summary-cards {{
            display: grid;
            grid-template-columns: repeat(auto-fit, minmax(250px, 1fr));
            gap: 20px;
            padding: 40px;
            background: #f8f9fa;
        }}
        
        .card {{
            background: white;
            border-radius: 15px;
            padding: 25px;
            text-align: center;
            box-shadow: 0 5px 20px rgba(0,0,0,0.05);
            transition: all 0.3s ease;
            border: 1px solid rgba(0,0,0,0.05);
            position: relative;
            overflow: hidden;
        }}
        
        .card:hover {{
            transform: translateY(-5px);
            box-shadow: 0 10px 30px rgba(102, 126, 234, 0.2);
        }}
        
        .card.pass {{
            border-bottom: 4px solid #4CAF50;
        }}
        
        .card.fail {{
            border-bottom: 4px solid #f44336;
        }}
        
        .card.total {{
            border-bottom: 4px solid #667eea;
        }}
        
        .card .icon {{
            font-size: 3em;
            margin-bottom: 15px;
        }}
        
        .card .value {{
            font-size: 2.5em;
            font-weight: bold;
            margin-bottom: 5px;
        }}
        
        .card .label {{
            color: #666;
            font-size: 0.9em;
            text-transform: uppercase;
            letter-spacing: 1px;
        }}
        
        .card .percentage {{
            position: absolute;
            top: 10px;
            right: 10px;
            background: rgba(0,0,0,0.05);
            padding: 5px 10px;
            border-radius: 20px;
            font-size: 0.8em;
            font-weight: bold;
        }}
        
        .info-section {{
            padding: 30px 40px;
            background: white;
            border-bottom: 1px solid #eee;
        }}
        
        .info-grid {{
            display: grid;
            grid-template-columns: repeat(auto-fit, minmax(300px, 1fr));
            gap: 20px;
        }}
        
        .info-item {{
            display: flex;
            align-items: center;
            padding: 10px;
            background: #f8f9fa;
            border-radius: 10px;
        }}
        
        .info-item .label {{
            font-weight: bold;
            color: #667eea;
            width: 100px;
        }}
        
        .info-item .value {{
            color: #333;
            flex: 1;
        }}
        
        .progress-section {{
            padding: 30px 40px;
            background: #f8f9fa;
        }}
        
        .progress-container {{
            margin: 20px 0;
        }}
        
        .progress-label {{
            display: flex;
            justify-content: space-between;
            margin-bottom: 5px;
            color: #666;
        }}
        
        .progress-bar {{
            height: 30px;
            background: #e0e0e0;
            border-radius: 15px;
            overflow: hidden;
            display: flex;
        }}
        
        .progress-pass {{
            height: 100%;
            background: linear-gradient(90deg, #4CAF50, #8BC34A);
            transition: width 0.5s ease;
            display: flex;
            align-items: center;
            justify-content: center;
            color: white;
            font-size: 0.9em;
            font-weight: bold;
            text-shadow: 1px 1px 2px rgba(0,0,0,0.2);
        }}
        
        .progress-fail {{
            height: 100%;
            background: linear-gradient(90deg, #f44336, #FF7043);
            transition: width 0.5s ease;
            display: flex;
            align-items: center;
            justify-content: center;
            color: white;
            font-size: 0.9em;
            font-weight: bold;
            text-shadow: 1px 1px 2px rgba(0,0,0,0.2);
        }}
        
        .results-section {{
            padding: 30px 40px;
        }}
        
        .results-section h2 {{
            margin-bottom: 20px;
            color: #333;
            display: flex;
            align-items: center;
            gap: 10px;
        }}
        
        .filter-bar {{
            display: flex;
            gap: 10px;
            margin-bottom: 20px;
            flex-wrap: wrap;
        }}
        
        .filter-btn {{
            padding: 8px 16px;
            border: none;
            border-radius: 20px;
            cursor: pointer;
            font-size: 0.9em;
            transition: all 0.3s ease;
            background: #e0e0e0;
        }}
        
        .filter-btn:hover {{
            transform: translateY(-2px);
        }}
        
        .filter-btn.all {{
            background: #667eea;
            color: white;
        }}
        
        .filter-btn.pass {{
            background: #4CAF50;
            color: white;
        }}
        
        .filter-btn.fail {{
            background: #f44336;
            color: white;
        }}
        
        table {{
            width: 100%;
            border-collapse: collapse;
            margin-top: 20px;
            background: white;
            border-radius: 10px;
            overflow: hidden;
            box-shadow: 0 5px 20px rgba(0,0,0,0.05);
        }}
        
        th {{
            background: #667eea;
            color: white;
            padding: 15px;
            text-align: left;
            font-weight: 500;
        }}
        
        td {{
            padding: 15px;
            border-bottom: 1px solid #eee;
        }}
        
        tr:hover {{
            background: #f8f9fa;
        }}
        
        .status-badge {{
            display: inline-block;
            padding: 5px 10px;
            border-radius: 20px;
            font-size: 0.85em;
            font-weight: bold;
        }}
        
        .status-badge.pass {{
            background: rgba(76, 175, 80, 0.1);
            color: #4CAF50;
        }}
        
        .status-badge.fail {{
            background: rgba(244, 67, 54, 0.1);
            color: #f44336;
        }}
        
        .error-detail {{
            background: #ffebee;
            padding: 10px;
            border-radius: 5px;
            margin-top: 5px;
            font-size: 0.9em;
            color: #c62828;
            border-left: 3px solid #f44336;
        }}
        
        .duration-badge {{
            background: #e0e0e0;
            padding: 3px 8px;
            border-radius: 12px;
            font-size: 0.85em;
            color: #666;
        }}
        
        .footer {{
            background: #333;
            color: white;
            text-align: center;
            padding: 20px;
            font-size: 0.9em;
        }}
        
        .footer a {{
            color: #667eea;
            text-decoration: none;
        }}
        
        .footer a:hover {{
            text-decoration: underline;
        }}
        
        .chart-container {{
            display: flex;
            justify-content: center;
            align-items: center;
            padding: 20px;
        }}
        
        .pie-chart {{
            width: 200px;
            height: 200px;
            border-radius: 50%;
            background: conic-gradient(
                #4CAF50 0deg {data.PassRate * 3.6}deg,
                #f44336 {data.PassRate * 3.6}deg 360deg
            );
            margin: 20px auto;
            box-shadow: 0 5px 20px rgba(0,0,0,0.1);
            animation: spin 1s ease-out;
        }}
        
        @keyframes spin {{
            from {{ transform: rotate(0deg); }}
            to {{ transform: rotate(360deg); }}
        }}
        
        .legend {{
            display: flex;
            justify-content: center;
            gap: 30px;
            margin-top: 20px;
        }}
        
        .legend-item {{
            display: flex;
            align-items: center;
            gap: 8px;
        }}
        
        .legend-color {{
            width: 16px;
            height: 16px;
            border-radius: 4px;
        }}
        
        .legend-color.pass {{ background: #4CAF50; }}
        .legend-color.fail {{ background: #f44336; }}
        
        @media (max-width: 768px) {{
            .header h1 {{ font-size: 2em; }}
            .summary-cards {{ grid-template-columns: 1fr; }}
            .info-grid {{ grid-template-columns: 1fr; }}
            td {{ font-size: 0.9em; }}
        }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1>🚀 FeiSharp Test Report</h1>
            <p>Generated Time: {data.Timestamp:yyyy-MM-dd HH:mm:ss}</p>
            <p>Version {data.Version}</p>
        </div>
        
        <div class='summary-cards'>
            <div class='card total'>
                <div class='icon'>📊</div>
                <div class='value'>{data.TotalTests}</div>
                <div class='label'>Total Tests Count</div>
            </div>
            
            <div class='card pass'>
                <div class='icon'>✅</div>
                <div class='value'>{data.Passed}</div>
                <div class='label'>PASS</div>
                <div class='percentage'>{data.PassRate}%</div>
            </div>
            
            <div class='card fail'>
                <div class='icon'>❌</div>
                <div class='value'>{data.Failed}</div>
                <div class='label'>FAIL</div>
                <div class='percentage'>{data.FailRate}%</div>
            </div>
            
            <div class='card'>
                <div class='icon'>⏱️</div>
                <div class='value'>{data.Duration.TotalMilliseconds:F0}ms</div>
                <div class='label'>Total Elapsed Time</div>
            </div>
        </div>
        
        <div class='info-section'>
            <h2>📋 System Information</h2>
            <div class='info-grid'>
                <div class='info-item'>
                    <span class='label'>Computer Name</span>
                    <span class='value'>{data.ComputerName}</span>
                </div>
                <div class='info-item'>
                    <span class='label'>User Name</span>
                    <span class='value'>{data.UserName}</span>
                </div>
                <div class='info-item'>
                    <span class='label'>Operator System Version</span>
                    <span class='value'>{Environment.OSVersion}</span>
                </div>
                <div class='info-item'>
                    <span class='label'>.NET Version</span>
                    <span class='value'>{Environment.Version}</span>
                </div>
                <div class='info-item'>
                    <span class='label'>CPU</span>
                    <span class='value'>{Environment.ProcessorCount} Core</span>
                </div>
                <div class='info-item'>
                    <span class='label'>64-bit System</span>
                    <span class='value'>{(Environment.Is64BitOperatingSystem ? "Yes" : "No")}</span>
                </div>
            </div>
        </div>
        
        <div class='progress-section'>
            <h2>📈 Distribution Of Test Results</h2>
            
            <div class='chart-container'>
                <div class='pie-chart'></div>
            </div>
            
            <div class='legend'>
                <div class='legend-item'>
                    <div class='legend-color pass'></div>
                    <span>PASS ({data.Passed}) - {data.PassRate}%</span>
                </div>
                <div class='legend-item'>
                    <div class='legend-color fail'></div>
                    <span>FAIL ({data.Failed}) - {data.FailRate}%</span>
                </div>
            </div>
            
            <div class='progress-container'>
                <div class='progress-label'>
                    <span>Passed Rate</span>
                    <span>{data.PassRate}%</span>
                </div>
                <div class='progress-bar'>
                    <div class='progress-pass' style='width: {data.PassRate}%;'>
                        {data.Passed} PASS
                    </div>
                </div>
            </div>
            
            <div class='progress-container'>
                <div class='progress-label'>
                    <span>Failed Rate</span>
                    <span>{data.FailRate}%</span>
                </div>
                <div class='progress-bar'>
                    <div class='progress-fail' style='width: {data.FailRate}%;'>
                        {data.Failed} FAIL
                    </div>
                </div>
            </div>
        </div>
        
        <div class='results-section'>
            <h2>🔍 Test Results Details</h2>
            
            <div class='filter-bar'>
                <button class='filter-btn all' onclick='filterTests(""all"")'>All ({data.TotalTests})</button>
                <button class='filter-btn pass' onclick='filterTests(""pass"")'>Pass ({data.Passed})</button>
                <button class='filter-btn fail' onclick='filterTests(""fail"")'>Fail ({data.Failed})</button>
            </div>
            
            <table id='testTable'>
                <thead>
                    <tr>
                        <th>#</th>
                        <th>Test Name</th>
                        <th>Status</th>
                        <th>Elapsed Time</th>
                        <th>Error Information</th>
                    </tr>
                </thead>
                <tbody>
                    {GenerateTestRows(data.Results)}
                </tbody>
            </table>
        </div>
        
        <div class='footer'>
            <p>FeiSharp Test Report | Generated by FeiSharp Test Framework</p>
            <p>© 2024-2026 Mars Fei. All rights reserved.</p>
        </div>
    </div>
    
    <script>
        function filterTests(type) {{
            const rows = document.querySelectorAll('#testTable tbody tr');
            rows.forEach(row => {{
                const status = row.querySelector('.status-badge').textContent.trim();
                if (type === 'all' || 
                    (type === 'pass' && status === 'PASS') ||
                    (type === 'fail' && status === 'FAIL')) {{
                    row.style.display = '';
                }} else {{
                    row.style.display = 'none';
                }}
            }});
            
            // 更新按钮样式
            document.querySelectorAll('.filter-btn').forEach(btn => {{
                btn.style.opacity = '0.5';
            }});
            event.target.style.opacity = '1';
        }}
        
        // 添加动画效果
        document.addEventListener('DOMContentLoaded', function() {{
            const cards = document.querySelectorAll('.card');
            cards.forEach((card, index) => {{
                card.style.animation = `slideIn 0.5s ease-out ${{index * 0.1}}s both`;
            }});
        }});
    </script>
</body>
</html>";

            File.WriteAllText(filepath, html);
        }

        private static string GenerateTestRows(List<TestResult> results)
        {
            var sb = new StringBuilder();
            foreach (var result in results)
            {
                var statusClass = result.Passed ? "pass" : "fail";
                var status = result.Passed ? "PASS" : "FAIL";
                var errorMessage = !result.Passed && !string.IsNullOrEmpty(result.ErrorMessage)
                    ? EscapeHtml(result.ErrorMessage)
                    : "-";

                sb.AppendLine($@"
                <tr>
                    <td>{result.TestNumber}</td>
                    <td><strong>{EscapeHtml(result.TestName)}</strong></td>
                    <td><span class='status-badge {statusClass}'>{status}</span></td>
                    <td><span class='duration-badge'>{result.Duration.TotalMilliseconds:F0}ms</span></td>
                    <td>{errorMessage}</td>
                </tr>");
            }
            return sb.ToString();
        }

        private static string EscapeHtml(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            return System.Security.SecurityElement.Escape(text) ?? "";
        }

        private static void GenerateJsonReport(TestReportData data, string filepath)
        {
            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            var json = JsonSerializer.Serialize(data, options);
            File.WriteAllText(filepath, json);
        }

        private static void ShowExportSuccess(string htmlFile, string jsonFile, bool openAfterExport)
        {
            var panel = new Panel(
                $"[green]✅ Test report export successful！[/]\n\n" +
                $"[yellow]HTML Report:[/] [blue]{htmlFile}[/]\n" +
                $"[yellow]JSON Report:[/] [blue]{jsonFile}[/]\n\n" +
                $"[grey]The report has been saved in the FeiSharp/TestReports directory.[/]")
                .Border(BoxBorder.Rounded)
                .BorderStyle(Style.Parse("green"))
                .Header("[green]Export Successful[/]")
                .Padding(1, 1, 1, 1);

            AnsiConsole.Write(panel);

            if (openAfterExport)
            {
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = htmlFile,
                        UseShellExecute = true
                    });
                    AnsiConsole.MarkupLine("[grey]The HTML report has been automatically opened[/]");
                }
                catch
                {
                    AnsiConsole.MarkupLine("[yellow]Cannot automatically open the report file[/]");
                }
            }
        }

        public static void ShowReportHistory()
        {
            if (!Directory.Exists(ReportsDirectory))
            {
                AnsiConsole.MarkupLine("[yellow]No test report history available[/]");
                return;
            }

            var files = Directory.GetFiles(ReportsDirectory, "*.html")
                                 .OrderByDescending(f => File.GetCreationTime(f))
                                 .Take(10)
                                 .ToList();

            if (files.Count == 0)
            {
                AnsiConsole.MarkupLine("[yellow]No test report history available[/]");
                return;
            }

            var table = new Table()
                .Border(TableBorder.Rounded)
                .AddColumn("[yellow]#[/]")
                .AddColumn("[yellow]Report Files[/]")
                .AddColumn("[yellow]Created Time[/]")
                .AddColumn("[yellow]Size[/]");

            for (int i = 0; i < files.Count; i++)
            {
                var file = files[i];
                var info = new FileInfo(file);
                table.AddRow(
                    (i + 1).ToString(),
                    Path.GetFileName(file),
                    info.CreationTime.ToString("yyyy-MM-dd HH:mm:ss"),
                    $"{info.Length / 1024.0:F1} KB");
            }

            AnsiConsole.Write(table);

            if (AnsiConsole.Confirm("Open the latest report?"))
            {
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = files[0],
                        UseShellExecute = true
                    });
                }
                catch (Exception ex)
                {
                    AnsiConsole.MarkupLine($"[red]Unable to open the report: {ex.Message}[/]");
                }
            }
        }
    }
}