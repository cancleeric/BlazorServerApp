using EnterpriseIDS.Infrastructure.Tests;

Console.WriteLine("Redis 分散式會話管理測試程式");
Console.WriteLine("按任意鍵開始測試...");
Console.ReadKey();
Console.WriteLine();

try
{
    await TestRedisSession.RunTestAsync();
}
catch (Exception ex)
{
    Console.WriteLine($"測試過程中發生錯誤: {ex}");
}

Console.WriteLine("\n按任意鍵結束程式...");
Console.ReadKey();