internal class Program
{
    private static async Task Main(string[] args)
    {
        string token = "...";
        var bot = new BotClass(token);

        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
            bot.Stop();
        };

        await bot.StartAsync(cts.Token);
        await Task.Delay(-1, cts.Token);
    }
}