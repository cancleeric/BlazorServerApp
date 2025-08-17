using RedisTestConsole;

Console.WriteLine("Redis 分散式會話管理測試程式");
Console.WriteLine("開始執行測試...\n");

try
{
    await TestRedisSession.RunTestAsync();
    Console.WriteLine("\n所有測試執行完成！");
}
catch (Exception ex)
{
    Console.WriteLine($"測試過程中發生錯誤: {ex}");
    Environment.Exit(1);
}