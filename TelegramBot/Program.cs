using Microsoft.VisualBasic;
using System.Threading;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using ProglibIO;
using HeadhunterClientAPI;
using static HeadhunterClientAPI.HeadhunterClient;
using System.Collections.Concurrent;

var proglibClient = new ProglibIOClient();
var hhClient = new HeadhunterClient();
var botClient = new TelegramBotClient("5957164720:AAEd0R7qVOmfqYPEQRym1ReJubt_6YjuoGM");
var userId = "5854103005"; //To Get Your userId type "/start" to "@RawDataBot" on Telegram

using CancellationTokenSource cts = new();

// StartReceiving does not block the caller thread. Receiving is done on the ThreadPool.
ReceiverOptions receiverOptions = new()
{
    AllowedUpdates = Array.Empty<UpdateType>() // receive all update types
};

if(userId.Length == 0)
{
    Console.WriteLine("Please input a user id:");
    userId = Console.ReadLine();
}
//Send the message to introduce yourself
if (userId.Length > 9)
{
    var welcomeMsg = await botClient.SendTextMessageAsync(userId, "Hello! My name is Billy Bob Johnson! What is yours?");
}

//Start Receiving
botClient.StartReceiving(
    updateHandler: HandleUpdateAsync,
    pollingErrorHandler: HandlePollingErrorAsync,
    receiverOptions: receiverOptions,
    cancellationToken: cts.Token
);

var me = await botClient.GetMeAsync();

Console.WriteLine($"Start listening for @{me.Username} , {me.Id}");
Console.ReadLine();

// Send cancellation request to stop bot
cts.Cancel();

async static Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
{
    // Only process Message updates: https://core.telegram.org/bots/api#message
    if (update.Message is not { } message)
        return;
    // Only process text messages
    if (message.Text is not { } messageText)
        return;
    var chatId = message.Chat.Id;
    Console.WriteLine($"Received a '{messageText}' message in chat {chatId}.");

    if (messageText == "/help")
    {
        var commands = await botClient.GetMyCommandsAsync();
        var commandlist = string.Join("\n", commands.Select(x => $"/{x.Command} - {x.Description}"));
        Message helpMessage = await botClient.SendTextMessageAsync(
        chatId: chatId,
        text: commandlist,
        cancellationToken: cancellationToken);
    }
    else if (messageText=="/vacancies")
    {
        int totalPages = await ProglibIOClient.GetTotalPagesAsync();
        int count = 0;
        List<string> urls = new List<string>();

        await Task.Run(() => {
            for (int i = 1; i <= totalPages; i++)
            {
                urls.Add("https://proglib.io/vacancies/all?workType=all&workPlace=all&experience=&salaryFrom=&page=" + i.ToString());
            }
        });

        foreach(var currentUrl in urls) { 
            ConcurrentBag<Vacancy> vacancies = await ProglibIOClient.GetVacanciesByURLAsync(currentUrl);
            count += vacancies.Count;
            var msg = string.Join("\n\n\n", vacancies.Select(x => x.ToString()));
            Message loopMessage = await botClient.SendTextMessageAsync(
            chatId: chatId,
            text: msg,
            cancellationToken: cancellationToken);
            Thread.Sleep(750);
        }

        Message completionMessage = await botClient.SendTextMessageAsync(
        chatId: chatId,
        text: $"Command Completed!\nTotal Ammount Of Vacancies: {count}",
        cancellationToken: cancellationToken);

    }
    else if(messageText == "/search_employers")
    {
        BoolVariables.Searching = true;
        Message answerMessage = await botClient.SendTextMessageAsync(
        chatId: chatId,
        text: $"Input Search:",
        cancellationToken: cancellationToken);

    }
    else if(BoolVariables.Searching == true)
    {
        
        if (messageText.Length > 0)
        {
            var employers = await HeadhunterClient.SearchEmployers(messageText);

            if(employers.Items.Length == 0)
            {
                Message noEmployersMessage = await botClient.SendTextMessageAsync(
                chatId: chatId,
                text: "No Employers Found!",
                cancellationToken: cancellationToken);
            }

            if (employers.Items.Length > 0)
            {

                if (employers.Items.Length < 10)
                {
                    Message bigMessage = await botClient.SendTextMessageAsync(
                    chatId: chatId,
                    text: employers.ToString(),
                    cancellationToken: cancellationToken);

                }
                else
                {
                    int loopCount = 0;
                    if (employers.Items.Length % 10 == 0)
                    {
                        loopCount = employers.Items.Length / 10;
                    }
                    else
                    {
                        decimal num = employers.Items.Length / 10;
                        loopCount = (int)Math.Round(num) + 1;
                    }

                    for (int i = 1; i <= loopCount; i++)
                    {
                        List<string> list = new List<string>();
                        for (int j = i * 10 - 10; j < i * 10; j++)
                        {
                            if (j < employers.Items.Length)
                            {
                                list.Add(employers.Items[j].ToString());
                            }
                        }

                        var msg = string.Join("\n\n", list);
                        Message loopMessage = await botClient.SendTextMessageAsync(
                        chatId: chatId,
                        text: msg,
                        cancellationToken: cancellationToken);
                        Thread.Sleep(500);

                    }
                }

                Message completionMessage = await botClient.SendTextMessageAsync(
                chatId: chatId,
                text: $"Command Completed! Total Employer Ammount: {employers.Items.Length}",
                cancellationToken: cancellationToken);
            }

        }
        else if (messageText.Length <= 0)
        {
            Message loopMessage = await botClient.SendTextMessageAsync(
            chatId: chatId,
            text: "No Search Term Detected! Try Again Later!",
            cancellationToken: cancellationToken);
        }
        else
        {
            Message loopMessage = await botClient.SendTextMessageAsync(
            chatId: chatId,
            text: "Error while executing command!",
            cancellationToken: cancellationToken);
        }

        BoolVariables.Searching = false;
    }
    else if (BoolVariables.Answered == false)
    {
        BoolVariables.Answered = true;
        //Echo received Name
        Message answerMessage = await botClient.SendTextMessageAsync(
        chatId: chatId,
        text: "Nice to meet you " + messageText + " :)",
        cancellationToken: cancellationToken);
    }


}



Task HandlePollingErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
{
    var ErrorMessage = exception switch
    {
        ApiRequestException apiRequestException
            => $"Telegram API Error:\n[{apiRequestException.ErrorCode}]\n{apiRequestException.Message}",
        _ => exception.ToString()
    };

    Console.WriteLine(ErrorMessage);
    return Task.CompletedTask;
}

public class BoolVariables
{
    public static bool Answered = false;
    public static bool Searching = false;
}





