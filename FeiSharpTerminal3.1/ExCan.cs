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
            Console.CancelKeyPress += (sender, e) =>
            {
                if (_isExecuting)
                {
                    e.Cancel = true;
                    CancelExecution();
                    AnsiConsole.MarkupLine("[yellow]⚠ Execution cancelled by user (Ctrl+C)[/]");
                }
                else
                {
                    e.Cancel = true;
                }
            };
        }
        public static void SetExecuting(bool executing)
        {
            _isExecuting = executing;
            if (!executing)
            {
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