using System.Runtime.InteropServices;
using System.Speech.Synthesis;
namespace FeiSharpTerminal3;
public class MeDuFeiAnimation
{
    // 导入Windows API函数用于检测按键状态
    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);

    private bool _isRunning;
    private SpeechSynthesizer _synth;
    private Thread _keyCheckThread;

    public MeDuFeiAnimation()
    {
        _isRunning = true;
        _synth = new SpeechSynthesizer();
    }

    public void Run()
    {
        Console.Title = "美肚飞小男孩动画 - 按Alt键退出";

        // 设置语音属性
        _synth.SelectVoiceByHints(VoiceGender.Male, VoiceAge.Child);
        _synth.Volume = 100;  // 音量 (0-100)
        _synth.Rate = 1;      // 语速 (-10 到 10)

        // 清屏并设置控制台颜色
        Console.Clear();
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.BackgroundColor = ConsoleColor.Blue;

        // 启动按键检测线程
        _keyCheckThread = new Thread(CheckForExitKey);
        _keyCheckThread.Start();

        // 动画循环
        while (_isRunning)
        {
            // 显示小男孩的不同动画帧
            ShowBoyFrame1();
            _synth.SpeakAsync("美肚飞");
            WaitWithExitCheck(2000);
            if (!_isRunning) break;

            ShowBoyFrame2();
            _synth.SpeakAsync("美肚飞");
            WaitWithExitCheck(2000);
            if (!_isRunning) break;

            ShowBoyFrame3();
            _synth.SpeakAsync("美肚飞");
            WaitWithExitCheck(2000);
            if (!_isRunning) break;
        }

        CleanUp();
    }

    private void CheckForExitKey()
    {
        const int VK_MENU = 0x12; // Alt键的虚拟键码

        while (_isRunning)
        {
            // 检查Alt键是否被按下
            if ((GetAsyncKeyState(VK_MENU) & 0x8000) != 0)
            {
                _isRunning = false;
                break;
            }
            Thread.Sleep(100);
        }
    }

    private void WaitWithExitCheck(int milliseconds)
    {
        int interval = 100; // 每次检查间隔
        int elapsed = 0;

        while (elapsed < milliseconds && _isRunning)
        {
            Thread.Sleep(interval);
            elapsed += interval;
        }
    }

    private void ShowBoyFrame1()
    {
        if (!_isRunning) return;
        Console.Clear();
        Console.WriteLine(@"
          /\
         /  \
        /____\
         |  |
         |  |
        /    \
       /      \
      /        \
     /__________\
        |    |
        |    |
       /      \
      /        \
     /          \
    /            \
   /______________\
");
    }

    private void ShowBoyFrame2()
    {
        if (!_isRunning) return;
        Console.Clear();
        Console.WriteLine(@"
          /\
         /  \
        /____\
         |  |
         |  |
        /    \
       /      \
      /        \
     /__________\
        |    |
        |    |
       /      \
      /   __   \
     /   |  |   \
    /    |__|    \
   /______________\
");
    }

    private void ShowBoyFrame3()
    {
        if (!_isRunning) return;
        Console.Clear();
        Console.WriteLine(@"
          /\
         /  \
        /____\
         |  |
         |  |
        /    \
       /      \
      /        \
     /__________\
        |    |
        |    |
       /      \
      /        \
     /   ____   \
    /   |    |   \
   /____|____|____\
");
    }

    private void CleanUp()
    {
        // 结束按键检测线程
        if (_keyCheckThread != null && _keyCheckThread.IsAlive)
        {
            _keyCheckThread.Join();
        }

        // 释放语音合成器资源
        if (_synth != null)
        {
            _synth.Dispose();
        }

        Console.ResetColor();
        Console.Clear();
        Console.WriteLine("程序已退出");
    }
}