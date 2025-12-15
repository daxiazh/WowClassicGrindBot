namespace VizAura.Models;

/// <summary>
/// 验证检查结果
/// </summary>
/// <param name="Success">是否成功</param>
/// <param name="Data">传递给下一步的 WoW 进程信息</param>
public record CheckResult(bool Success, WowProcessInfo? Data);
