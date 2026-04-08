// ExecutionCancellation.cs
using Spectre.Console;
using System;
using System.Threading;

namespace FeiSharp8._5RuntimeSdk
{
    public static class ExecutionCancellation
    {
        private static CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
        public static bool _isExecuting = false;

        public static void Initialize()
        {
            // 设置 Ctrl+C 处理程序
            Console.CancelKeyPress += (sender, e) =>
            {
                if (_isExecuting)
                {
                    // 如果正在执行代码，取消执行但不退出程序
                    e.Cancel = true; // 阻止程序退出
                    CancelExecution();
                    AnsiConsole.MarkupLine("[yellow]⚠ Execution cancelled by user (Ctrl+C)[/]");
                }
                else
                {
                    // 如果不在执行，询问是否退出
                    e.Cancel = true;
                }
            };
        }

        public static void SetExecuting(bool executing)
        {
            _isExecuting = executing;
            if (!executing)
            {
                // 重置 cancellation token
                _cancellationTokenSource.Dispose();
                _cancellationTokenSource = new CancellationTokenSource();
            }
        }

        public static void CancelExecution()
        {
            _cancellationTokenSource.Cancel();
        }

        public static CancellationToken GetCancellationToken()
        {
            return _cancellationTokenSource.Token;
        }

        public static bool IsCancellationRequested => _cancellationTokenSource.IsCancellationRequested;
    }
}