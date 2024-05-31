using System;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

public class BotClass
{
    private readonly TelegramBotClient _botClient;
    private CancellationTokenSource _cancellationTokenSource;

    public BotClass(string token)
    {
        _botClient = new TelegramBotClient(token);
        _cancellationTokenSource = new CancellationTokenSource();
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        var receiverOptions = new ReceiverOptions
        {
            AllowedUpdates = Array.Empty<UpdateType>()
        };

        _botClient.StartReceiving(
            async (botClient, update, cancellationToken) =>
            {
                if (update.CallbackQuery != null)
                {
                    await HandleCallbackQueryAsync(botClient, update.CallbackQuery, cancellationToken);
                }
                else
                {
                    await HandleUpdateAsync(botClient, update, cancellationToken);
                }
            },
            HandleErrorAsync,
            receiverOptions,
            cancellationToken
        );

        var me = await _botClient.GetMeAsync();
        Console.WriteLine($"Start listening for @{me.Username}");
    }

    public void Stop()
    {
        _cancellationTokenSource.Cancel();
        Console.WriteLine("Bot is stopping...");
    }

    private async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
    {
        if (update.Message is not { } message)
            return;

        if (message.Text is not { } messageText)
            return;

        var chatId = message.Chat.Id;

        switch (messageText.Split(' ')[0])
        {
            case "/start":
                await botClient.SendTextMessageAsync(
                    chatId,
                    "Розрахунки відбуваються тут ('C')",
                    replyMarkup: GetCalculatorKeyboard(),
                    cancellationToken: cancellationToken);
                break;

            case "/stop":
                await botClient.SendTextMessageAsync(
                    chatId,
                    "Зупинка роботи бота !",
                    cancellationToken: cancellationToken);
                Stop();
                break;

            case "/help":
                await botClient.SendTextMessageAsync(
                    chatId,
                    "Бот-калькулятор, для обчислень скористуйтесь кнопками нижче (для початку необхідно очисти 'C'), існуючі команди /start /stop /help /gif",
                    cancellationToken: cancellationToken);
                break;

            case "/gif":
                await botClient.SendTextMessageAsync(
                    chatId,
                    "https://cdn.pixabay.com/animation/2022/12/05/15/28/15-28-43-29_512.gif",
                    cancellationToken: cancellationToken);
                break;

            default:
                await botClient.SendTextMessageAsync(chatId, $"Невідома команда: {messageText} , існуючі команди /start /stop /help", cancellationToken: cancellationToken);
                break;
        }
    }

    private async Task HandleCallbackQueryAsync(ITelegramBotClient botClient, CallbackQuery callbackQuery, CancellationToken cancellationToken)
    {
        string userExpression = callbackQuery.Data;

        switch (userExpression)
        {
            case "C":
                if (callbackQuery.Message.Text == "0")
                {
                    return;
                }
                await botClient.EditMessageTextAsync(
                    callbackQuery.Message.Chat.Id,
                    callbackQuery.Message.MessageId,
                    "0",
                    replyMarkup: GetCalculatorKeyboard(),
                    cancellationToken: cancellationToken);
                return;

            case "⌫":
                if (callbackQuery.Message.Text == "0")
                {
                    return;
                }

                string text = callbackQuery.Message.Text.Length > 1
                    ? callbackQuery.Message.Text.Substring(0, callbackQuery.Message.Text.Length - 1)
                    : "0";
                await botClient.EditMessageTextAsync(
                    callbackQuery.Message.Chat.Id,
                    callbackQuery.Message.MessageId,
                    text,
                    replyMarkup: GetCalculatorKeyboard(),
                    cancellationToken: cancellationToken);
                return;

            case "=":
                if (callbackQuery.Message.Text == "0")
                {
                    return;
                }
                string expression = callbackQuery.Message.Text;
                string result = Calculate(expression);
                await botClient.EditMessageTextAsync(
                    callbackQuery.Message.Chat.Id,
                    callbackQuery.Message.MessageId,
                    result,
                    replyMarkup: GetCalculatorKeyboard(),
                    cancellationToken: cancellationToken);
                return;

            case ".":
                if (IsOperator(callbackQuery.Message.Text[^1]))
                {
                    return; // Не дозволяти крапки після операторів
                }
                string[] parts = callbackQuery.Message.Text.Split(new[] { '+', '-', '*', '/' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length > 0 && parts[^1].Contains("."))
                {
                    return; // Не дозволяти більше однієї крапки в одному числі
                }
                break;

            default:
                if (callbackQuery.Message.Text == "0")
                {
                    if (userExpression == "0" || (IsOperator(userExpression[0]) && userExpression != "-"))
                    {
                        return; // Не дозволяти 0 і оператори окрім "-"
                    }
                }

                if (IsOperator(callbackQuery.Message.Text[^1]) && IsOperator(userExpression[0]))
                {
                    return; // Не дозволяти два оператори підряд
                }

                if (callbackQuery.Message.Text == "0" && IsOperator(userExpression[0]) && IsOperator(callbackQuery.Message.Text[^1]))
                {
                    return; // Не дозволяти два оператори підряд
                }

                break;
        }

        string newText = (callbackQuery.Message.Text == "0" && userExpression != ".") ? userExpression : callbackQuery.Message.Text + userExpression;

        // Перевірка, чи змінилася розмітка та вміст повідомлення перед оновленням
        if (callbackQuery.Message.Text != newText || callbackQuery.Message.ReplyMarkup != GetCalculatorKeyboard())
        {
            await botClient.EditMessageTextAsync(
                callbackQuery.Message.Chat.Id,
                callbackQuery.Message.MessageId,
                newText,
                replyMarkup: GetCalculatorKeyboard(),
                cancellationToken: cancellationToken);
        }
    }

    private bool IsOperator(char ch)
    {
        return ch == '+' || ch == '-' || ch == '*' || ch == '/';
    }

    private InlineKeyboardMarkup GetCalculatorKeyboard()
    {
        return new InlineKeyboardMarkup(new[]
        {
            new[]
            {
                InlineKeyboardButton.WithCallbackData("C", "C"),
                InlineKeyboardButton.WithCallbackData("⌫", "⌫")
            },
            new[]
            {
                InlineKeyboardButton.WithCallbackData("1", "1"),
                InlineKeyboardButton.WithCallbackData("2", "2"),
                InlineKeyboardButton.WithCallbackData("3", "3"),
                InlineKeyboardButton.WithCallbackData("/", "/")
            },
            new[]
            {
                InlineKeyboardButton.WithCallbackData("4", "4"),
                InlineKeyboardButton.WithCallbackData("5", "5"),
                InlineKeyboardButton.WithCallbackData("6", "6"),
                InlineKeyboardButton.WithCallbackData("*", "*")
            },
            new[]
            {
                InlineKeyboardButton.WithCallbackData("7", "7"),
                InlineKeyboardButton.WithCallbackData("8", "8"),
                InlineKeyboardButton.WithCallbackData("9", "9"),
                InlineKeyboardButton.WithCallbackData("-", "-")
            },
            new[]
            {
                InlineKeyboardButton.WithCallbackData("0", "0"),
                InlineKeyboardButton.WithCallbackData(".", "."),
                InlineKeyboardButton.WithCallbackData("+", "+")
            },
            new[]
            { 
                InlineKeyboardButton.WithCallbackData("=", "="), 
            }               
        });
    }

    private string Calculate(string expression)
    {
        try
        {
            var e = new NCalc.Expression(expression);
            var result = e.Evaluate();
            return result.ToString();
        }
        catch
        {
            return "Error";
        }
    }

    private Task HandleErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
    {
        var errorMessage = exception switch
        {
            ApiRequestException apiRequestException => $"Telegram API Error:\n[{apiRequestException.ErrorCode}]\n{apiRequestException.Message}",
            _ => exception.ToString()
        };

        Console.WriteLine(errorMessage);
        return Task.CompletedTask;
    }
}
