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
using AngleSharp.Io;

//Clients and settings
var proglibClient = new ProglibIOClient();
var hhClient = new HeadhunterClient();
var botClient = new TelegramBotClient(""); //Input your bot token
var userId = ""; //To Get Your userId type "/start" to "@RawDataBot" on Telegram
using CancellationTokenSource cts = new();

//Check if the commands are present
var cmdsCheck = await botClient.GetMyCommandsAsync();
if(cmdsCheck.Length> 0)
{
    Console.WriteLine("Commands Exist!");
}
else //Make the commands
{
    BotCommand[] cmds = new BotCommand[3];
    BotCommand cmd1 = new BotCommand();
    BotCommand cmd2 = new BotCommand();
    BotCommand cmd3 = new BotCommand();

    cmd1.Command = "vacancies";
    cmd2.Command = "search_employers";
    cmd3.Command = "help";

    cmd1.Description = "Gives you a list of all available vacancies.";
    cmd2.Description = "Gives you a list of all available employers.";
    cmd3.Description = "Gives you a list of all the commands.";

    cmds[0] = cmd1;
    cmds[1] = cmd2;
    cmds[2] = cmd3; 

    await botClient.SetMyCommandsAsync(cmds);
    Console.WriteLine("Commands Created! Please Reopen The Chat!");
}

//StartReceiving does not block the caller thread. Receiving is done on the ThreadPool.
ReceiverOptions receiverOptions = new()
{
    AllowedUpdates = Array.Empty<UpdateType>() // receive all update types
};

//Checks
if(userId.Length == 0)
{
    Console.Write("Please input a user id:");
    userId = Console.ReadLine();
}
//Send the message to introduce yourself (If no userid is found this step is skipped)
if (userId.Length > 9)
{
    var welcomeMsg = await botClient.SendTextMessageAsync(userId, "Hello! My name is Billy Bob Johnson! What is yours?");
}
else if(userId.Length < 0) //Skip if no userID
{
    Console.WriteLine("Introduction Skipped Because No Valid UserId Was Inserted!");
}

//Start Receiving
botClient.StartReceiving(
    updateHandler: HandleUpdateAsync,
    pollingErrorHandler: HandlePollingErrorAsync,
    receiverOptions: receiverOptions,
    cancellationToken: cts.Token
);
//Start Listening
var me = await botClient.GetMeAsync();
Console.WriteLine($"Start listening for @{me.Username} , {me.Id}");
Console.ReadLine();
// Send cancellation request to stop bot
cts.Cancel();

//Responding To Messages Task
async static Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
{
    // Only process Message updates: https://core.telegram.org/bots/api#message
    if (update.Message is not { } message)
        return;
    // Only process text messages
    if (message.Text is not { } messageText)
        return;
    var chatId = message.Chat.Id;
    Console.WriteLine($"Received a '{messageText}' message in chat {chatId}."); //Notification In Console

    if (messageText == "/help") //Help Command
    {
        var commands = await botClient.GetMyCommandsAsync();
        var commandlist = string.Join("\n", commands.Select(x => $"/{x.Command} - {x.Description}"));
        Message helpMessage = await botClient.SendTextMessageAsync(
        chatId: chatId,
        text: commandlist,
        cancellationToken: cancellationToken);
    }
    else if (messageText=="/vacancies") //Vacancies Command
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
    else if(messageText == "/search_employers") //1st Step for search_employers command
    {
        BoolVariables.Searching = true;
        Message answerMessage = await botClient.SendTextMessageAsync(
        chatId: chatId,
        text: $"Input Search:",
        cancellationToken: cancellationToken);

    }
    else if(BoolVariables.Searching == true) //2nd Step for search_employers command
    {
        
        if (messageText.Length > 0) //checks
        {
            var employers = await HeadhunterClient.SearchEmployers(messageText);

            if(employers.Items.Length == 0) //No Results
            {
                Message noEmployersMessage = await botClient.SendTextMessageAsync(
                chatId: chatId,
                text: "No Employers Found!",
                cancellationToken: cancellationToken);
            }

            if (employers.Items.Length > 0) //checks
            {

                if (employers.Items.Length < 10) //If Small result
                {
                    Message bigMessage = await botClient.SendTextMessageAsync(
                    chatId: chatId,
                    text: employers.ToString(),
                    cancellationToken: cancellationToken);

                }
                else //If Big Result
                {
                    int loopCount = 0; //Ammount of times the loop is ran
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
        else if (messageText.Length <= 0) //No Search Text Check
        {
            Message loopMessage = await botClient.SendTextMessageAsync(
            chatId: chatId,
            text: "No Search Term Detected! Try Again Later!",
            cancellationToken: cancellationToken);
        }
        else //Any other error
        {
            Message loopMessage = await botClient.SendTextMessageAsync(
            chatId: chatId,
            text: "Error while executing command!",
            cancellationToken: cancellationToken);
        }

        BoolVariables.Searching = false;
    }
    else if (BoolVariables.Answered == false) //Name Repeat
    {
        BoolVariables.Answered = true;
        //Echo received Name
        Message answerMessage = await botClient.SendTextMessageAsync(
        chatId: chatId,
        text: "Nice to meet you " + messageText + " :)",
        cancellationToken: cancellationToken);
    }


}


//Exception console writer
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


public class BoolVariables //Class containing some parameters
{
    public static bool Answered = false;
    public static bool Searching = false;
}





