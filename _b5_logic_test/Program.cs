using System;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Windows.Input;

// B5 逻辑自测 + U盘制作闭环修复自测（结果状态/卷标消毒/写锁/重试）
class Program
{
    static int pass = 0, fail = 0;
    static void OK(string m) { pass++; Console.WriteLine("  PASS: " + m); }
    static void NG(string m) { fail++; Console.WriteLine("  FAIL: " + m); }

    static int Main()
    {
        var asm = Assembly.LoadFrom(Path.Combine(AppContext.BaseDirectory, "Windows_Client.dll"));

        Console.WriteLine("--- 1. WritePlan.IncludeTools ---");
        var planType = asm.GetType("Windows_Client.Models.WritePlan");
        var planProp = planType?.GetProperty("IncludeTools");
        if (planProp != null && planProp.PropertyType == typeof(bool) && planProp.CanWrite)
            OK("WritePlan.IncludeTools (bool, set)");
        else NG("WritePlan.IncludeTools 缺失或类型不符");

        Console.WriteLine("--- 2. UDiskViewModel.IncludeTools ---");
        var vmType = asm.GetType("Windows_Client.ViewModels.UDiskViewModel");
        var vmProp = vmType?.GetProperty("IncludeTools");
        if (vmProp != null && vmProp.PropertyType == typeof(bool) && vmProp.CanWrite && vmProp.CanRead)
            OK("UDiskViewModel.IncludeTools (bool, get+set)");
        else NG("UDiskViewModel.IncludeTools 缺失");

        Console.WriteLine("--- 3. CopyToolsToDrive 逻辑 ---");
        var svcType = asm.GetType("Windows_Client.Services.UDiskService");
        var m = svcType?.GetMethod("CopyToolsToDrive", BindingFlags.NonPublic | BindingFlags.Static);
        if (m == null) { NG("CopyToolsToDrive 未找到"); }
        else
        {
            var root = Path.Combine(Path.GetTempPath(), "zs_b5_test_" + Guid.NewGuid().ToString("N"));
            var toolRoot = Path.Combine(root, "Tools");
            var toolCache = Path.Combine(root, "cache");
            var destRoot = Path.Combine(root, "dest", "ZS_Tools");
            Directory.CreateDirectory(Path.Combine(toolRoot, "disk"));
            File.WriteAllText(Path.Combine(toolRoot, "disk", "dg.exe"), "x");
            File.WriteAllText(Path.Combine(toolRoot, "readme.txt"), "x");
            Directory.CreateDirectory(toolCache);
            File.WriteAllText(Path.Combine(toolCache, "net.exe"), "y");

            int count = 0;
            try { count = (int)m.Invoke(null, new object[] { toolRoot, toolCache, destRoot, null, CancellationToken.None, null })!; }
            catch (Exception ex) { NG("CopyToolsToDrive 调用异常: " + ex.Message); }

            var dg = Path.Combine(destRoot, "disk", "dg.exe");
            var rd = Path.Combine(destRoot, "readme.txt");
            var net = Path.Combine(destRoot, "net.exe");
            if (count == 3 && File.Exists(dg) && File.Exists(rd) && File.Exists(net))
                OK("合并拷贝 内置+缓存 -> " + count + " 个文件，子目录结构保留");
            else NG("count=" + count + " dg=" + File.Exists(dg) + " rd=" + File.Exists(rd) + " net=" + File.Exists(net));

            int count2 = 0;
            try { count2 = (int)m.Invoke(null, new object[] { Path.Combine(root, "no1"), Path.Combine(root, "no2"), Path.Combine(root, "emptydest"), null, CancellationToken.None, null })!; }
            catch (Exception ex) { NG("空目录调用异常: " + ex.Message); }
            if (count2 == 0) OK("空工具目录返回 0（不中断制作）");
            else NG("空目录 count=" + count2);

            try { Directory.Delete(root, true); } catch { }
        }

        Console.WriteLine("--- 4. 结果状态闭环 (ResultKind/success/failed/canceled) ---");
        if (vmType == null) { NG("UDiskViewModel 类型未找到"); }
        else
        {
            var rkProp = vmType.GetProperty("ResultKind");
            var okProp = vmType.GetProperty("IsSuccess");
            var failProp = vmType.GetProperty("IsFailed");
            var cancelProp = vmType.GetProperty("IsCanceled");
            var showCompProp = vmType.GetProperty("ShowCompletion");
            var retryCmdProp = vmType.GetProperty("RetryCommand");
            var curStepProp = vmType.GetProperty("CurrentStep");
            var isFinProp = vmType.GetProperty("IsFinished");

            if (rkProp == null || okProp == null || failProp == null || cancelProp == null)
                NG("结果状态属性缺失");
            else
            {
                var api = CreateApi(asm);
                var svc = Activator.CreateInstance(svcType!)!;
                var vm = Activator.CreateInstance(vmType, api, svc, "http://127.0.0.1", AppContext.BaseDirectory)!;

                rkProp.SetValue(vm, "success");
                if ((bool)okProp.GetValue(vm)! && !(bool)failProp.GetValue(vm)! && !(bool)cancelProp.GetValue(vm)!)
                    OK("success -> IsSuccess=true, IsFailed/IsCanceled=false");
                else NG("success 状态判定错误");

                rkProp.SetValue(vm, "failed");
                if ((bool)failProp.GetValue(vm)! && !(bool)okProp.GetValue(vm)!)
                    OK("failed -> IsFailed=true, IsSuccess=false");
                else NG("failed 状态判定错误");

                rkProp.SetValue(vm, "canceled");
                if ((bool)cancelProp.GetValue(vm)! && !(bool)okProp.GetValue(vm)! && !(bool)failProp.GetValue(vm)!)
                    OK("canceled -> IsCanceled=true, 其余=false");
                else NG("canceled 状态判定错误");

                curStepProp!.SetValue(vm, 3);
                isFinProp!.SetValue(vm, true);
                if ((bool)showCompProp!.GetValue(vm)!) OK("ShowCompletion = 步骤3 且完成");
                else NG("ShowCompletion 判定错误");

                var retryCmd = retryCmdProp!.GetValue(vm) as ICommand;
                if (retryCmd == null) { NG("RetryCommand 缺失"); }
                else
                {
                    var failReasonProp = vmType.GetProperty("FailReason");
                    var statusProp = vmType.GetProperty("StatusText");
                    failReasonProp!.SetValue(vm, "x");
                    statusProp!.SetValue(vm, "y");
                    rkProp.SetValue(vm, "failed");
                    isFinProp.SetValue(vm, true);
                    retryCmd.Execute(null);
                    bool reset = (string)rkProp.GetValue(vm)! == "" && (string)failReasonProp.GetValue(vm)! == ""
                        && (string)statusProp.GetValue(vm)! == "" && !(bool)isFinProp.GetValue(vm)!;
                    if (reset) OK("重试复位 ResultKind/FailReason/StatusText/IsFinished");
                    else NG("重试复位不完整");
                }
            }
        }

        Console.WriteLine("--- 5. 卷标消毒 SanitizeLabel ---");
        var sanMethod = vmType?.GetMethod("SanitizeLabel", BindingFlags.NonPublic | BindingFlags.Static);
        if (sanMethod == null) { NG("SanitizeLabel 未找到"); }
        else
        {
            string S(string v) => (string)sanMethod.Invoke(null, new object[] { v })!;
            if (S("ZS_PE") == "ZS_PE") OK("正常卷标不变");
            else NG("正常卷标被改动: " + S("ZS_PE"));
            if (S("") == "ZS_PE" && S("   ") == "ZS_PE") OK("空/空白卷标回退 ZS_PE");
            else NG("空卷标未回退");
            var bad = S("a/b:c*?\"<>|");
            if (bad == "a_b_c______") OK("非法字符替换为 _: " + bad);
            else NG("非法字符未替换: " + bad);
            var longL = S("这是一个非常非常长的卷标文字");
            if (longL.Length == 11) OK("超过 11 字符截断: " + longL + " (len=" + longL.Length + ")");
            else NG("截断失败 len=" + longL.Length);
        }

        Console.WriteLine("--- 6. 管理员预检 + 多实例写锁 ---");
        var adminMethod = svcType?.GetMethod("IsAdministrator");
        if (adminMethod != null && adminMethod.IsStatic && adminMethod.ReturnType == typeof(bool))
            OK("UDiskService.IsAdministrator 存在（当前实际值: " + adminMethod.Invoke(null, null) + "）");
        else NG("IsAdministrator 缺失");
        using (var mk1 = new Mutex(true, @"Local\ZS_UDiskWrite", out var created1))
        using (var mk2 = new Mutex(true, @"Local\ZS_UDiskWrite", out var created2))
        {
            if (created1 && !created2) OK("写锁互斥：同名 Mutex 第二次获取失败");
            else NG("写锁互斥失效 created1=" + created1 + " created2=" + created2);
        }

        Console.WriteLine("=== 结果: PASS=" + pass + " FAIL=" + fail + " ===");
        return fail > 0 ? 1 : 0;
    }

    static object CreateApi(Assembly asm)
    {
        var t = asm.GetType("Windows_Client.Services.ApiService");
        return Activator.CreateInstance(t!)!;
    }
}