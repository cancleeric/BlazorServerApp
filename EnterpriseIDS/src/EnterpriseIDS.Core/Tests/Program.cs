using EnterpriseIDS.Core.Tests;

/// <summary>
/// 測試程式進入點
/// </summary>
class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("🧪 執行 EnterpriseIDS Core 模組測試");
        Console.WriteLine("=" + new string('=', 50));

        try
        {
            var success = await TestRunner.RunAllCoreTests();
            Environment.Exit(success ? 0 : 1);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"💥 測試執行失敗: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
            Environment.Exit(1);
        }
    }
}