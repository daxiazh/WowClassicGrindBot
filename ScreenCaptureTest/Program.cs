using System;
using System.Diagnostics;
using System.Linq;
using Game;
using WinAPI;

namespace ScreenCaptureTest;

class Program
{
    static void Main()
    {
        Console.WriteLine("=== ScreenCaptureKit 动态库测试 ===\n");
        
        // 1. 查找 WoW 进程
        Console.WriteLine("步骤 1: 查找 WoW 进程...");
        Process? wow = WowProcess.Get();
        if (wow == null)
        {
            Console.WriteLine("❌ 未找到 WoW 进程");
            Console.WriteLine("   请确保 World of Warcraft Classic 正在运行");
            Console.WriteLine("\n按任意键退出...");
            Console.ReadKey();
            return;
        }
        Console.WriteLine($"✓ 找到 WoW 进程: PID={wow.Id}, Name={wow.ProcessName}\n");
        
        // 2. 获取窗口 ID
        Console.WriteLine("步骤 2: 获取窗口 ID...");
        MacOSProcessHelper helper = new();
        uint windowId = helper.GetWindowId(wow);
        if (windowId == 0)
        {
            Console.WriteLine("❌ 无法获取窗口 ID");
            Console.WriteLine("   可能原因:");
            Console.WriteLine("   - WoW 窗口未显示");
            Console.WriteLine("   - CoreGraphics API 调用失败");
            Console.WriteLine("\n按任意键退出...");
            Console.ReadKey();
            return;
        }
        Console.WriteLine($"✓ 窗口 ID: {windowId}\n");
        
        // 3. 创建 ScreenCaptureKit 流
        Console.WriteLine("步骤 3: 创建 ScreenCaptureKit 流...");
        int frameCount = 0;
        DateTime? firstFrameTime = null;
        DateTime? lastFrameTime = null;
        
        ScreenCaptureKitInterop.FrameCallback callback = (data, w, h, bpr) =>
        {
            frameCount++;
            lastFrameTime = DateTime.Now;
            if (firstFrameTime == null)
            {
                firstFrameTime = DateTime.Now;
                Console.WriteLine($"✓ 收到第一帧: {w}x{h}, {bpr} bytes/row");
            }
            else if (frameCount % 60 == 0)
            {
                Console.WriteLine($"  已接收 {frameCount} 帧...");
            }
        };
        
        IntPtr handle = ScreenCaptureKitInterop.sc_create_stream(windowId, callback);
        if (handle == IntPtr.Zero)
        {
            Console.WriteLine("❌ 创建流失败");
            Console.WriteLine("   可能原因:");
            Console.WriteLine("   - ScreenCaptureKit 框架不可用");
            Console.WriteLine("   - Swift 动态库加载失败");
            Console.WriteLine("   - 窗口 ID 无效");
            Console.WriteLine("\n按任意键退出...");
            Console.ReadKey();
            return;
        }
        Console.WriteLine($"✓ 创建流成功: handle=0x{handle:X}\n");
        
        // 4. 等待接收帧
        Console.WriteLine("步骤 4: 等待接收帧数据...");
        Console.WriteLine("(将等待 10 秒,按 Ctrl+C 可提前退出)\n");
        
        System.Threading.Thread.Sleep(10000);
        
        // 5. 停止流
        Console.WriteLine("\n步骤 5: 停止捕获流...");
        ScreenCaptureKitInterop.sc_stop_stream(handle);
        Console.WriteLine("✓ 流已停止\n");
        
        // 6. 显示统计信息
        Console.WriteLine("=== 测试结果 ===");
        Console.WriteLine($"总帧数: {frameCount}");
        
        if (firstFrameTime != null && lastFrameTime != null)
        {
            TimeSpan duration = lastFrameTime.Value - firstFrameTime.Value;
            if (duration.TotalSeconds > 0)
            {
                double fps = frameCount / duration.TotalSeconds;
                Console.WriteLine($"持续时间: {duration.TotalSeconds:F2} 秒");
                Console.WriteLine($"平均帧率: {fps:F1} FPS");
            }
        }
        
        if (frameCount > 0)
        {
            Console.WriteLine("\n✅ 测试成功! ScreenCaptureKit 工作正常");
        }
        else
        {
            Console.WriteLine("\n⚠️  测试部分成功: 流已创建但未收到帧");
            Console.WriteLine("   可能原因:");
            Console.WriteLine("   - 需要授予 Screen Recording 权限");
            Console.WriteLine("   - ScreenCaptureKit 异步初始化延迟");
            Console.WriteLine("   - Swift 回调未正确触发");
        }
        
        Console.WriteLine("\n按任意键退出...");
        Console.ReadKey();
    }
}
