using Microsoft.VisualBasic;
using System.Threading;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using ProglibIO;
using HeadhunterClientAPI;
using System.Collections.Concurrent;
using System.Diagnostics;
using OpenAI_API.Completions;
using OpenAI_API.Models;
using System.Diagnostics.Contracts;
using System.Text.RegularExpressions;
using AngleSharp.Text;
using Newtonsoft.Json.Linq;
using AngleSharp.Io;
using OpenAI_API.Embedding;
using Telegram.Bot.Requests.Abstractions;
using System.Linq;

//Keys
var openApiKey = Environment.GetEnvironmentVariable("api_openapi_key");
if (openApiKey == null) { throw new InvalidOperationException("openApiKey Is Null!"); }
var telegramBotKey = Environment.GetEnvironmentVariable("api_telegrambot_key");
if (telegramBotKey == null) { throw new InvalidOperationException("telegramBotKey Is Null!");  }
var proglibClient = new ProglibIOClient();
var hhClient = new HeadhunterClient();
var botClient = new TelegramBotClient(telegramBotKey);
var openApiClient = new OpenAI_API.OpenAIAPI(openApiKey);
ConcurrentDictionary<long, UserInfo> _clientStates = new ConcurrentDictionary<long, UserInfo>();
ConcurrentDictionary<long, UpdateMsgType> _updateTypes = new ConcurrentDictionary<long, UpdateMsgType>();
using CancellationTokenSource cts = new();

// StartReceiving does not block the caller thread. Receiving is done on the ThreadPool.
ReceiverOptions receiverOptions = new(){  AllowedUpdates = Array.Empty<UpdateType>() }; // receive all update types};

//Commands
int cmdsAmmount = 6;
var cmdsCheck = await botClient.GetMyCommandsAsync();
if (cmdsCheck.Length == cmdsAmmount) { Console.WriteLine("Commands Exist!"); }
else //Make the commands
{
    await botClient.DeleteMyCommandsAsync();

    BotCommand[] cmds = new BotCommand[cmdsAmmount];
    
    BotCommand cmd1 = new BotCommand() { Command = "vacancies", 
        Description = "Gives you a list of all available vacancies." };
    BotCommand cmd2 = new BotCommand() { Command = "search_vacancies", 
        Description = "Gives you a list of all available vacancies with the specified name." };
    BotCommand cmd3 = new BotCommand() { Command = "search_employers", 
        Description = "Gives you a list of all available employers." };
    BotCommand cmd4 = new BotCommand() { Command = "startchatgpt", 
        Description = "Starts a conversation with ChatGPT." };
    BotCommand cmd5 = new BotCommand() { Command = "stopchatgpt", 
        Description = "Stops the conversation with ChatGPT." };
    BotCommand cmd6 = new BotCommand() { Command = "help", 
        Description = "Gives you a list of all the commands." };

    cmds[0] = cmd1; cmds[1] = cmd2; cmds[2] = cmd3; cmds[3] = cmd4; cmds[4] = cmd5; cmds[5] = cmd6;

    await botClient.SetMyCommandsAsync(cmds);
    Console.WriteLine("Commands Created! Please Reopen The Chat!");
}

//Start Receiving
botClient.StartReceiving(
    updateHandler: HandleUpdateAsync,
    pollingErrorHandler: HandlePollingErrorAsync,
    receiverOptions: receiverOptions,
    cancellationToken: cts.Token
);

//Me
var me = await botClient.GetMeAsync();
Console.WriteLine($"Start listening for @{me.Username} , {me.Id}");
Console.ReadLine();

// Send cancellation request to stop bot
cts.Cancel();

//Message Handler
async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
{
    // Only process Message updates: https://core.telegram.org/bots/api#message
    if (update.Message is not { } message)
        return;
    // Only process text messages
    if (message.Text is not { } messageText)
        return;
    var chatId = message.Chat.Id;
    Console.WriteLine($"Received a '{messageText}' message in chat {chatId}.");
    
    //thanks for the great idea Ilya :)
    var clientState = _clientStates.ContainsKey(chatId) ? _clientStates[chatId] : null;
    if (clientState == null) { SetState(chatId, State.None); }

    //Sends a request
    if (SendRequest(chatId) ==  false)
    {
        DateTime minuteInTheFuture = DateTime.UtcNow.AddMinutes(-1);

        await SendMsg(chatId, 
            $"You have been sending too many requests! Please wait for " +
            $"{(_clientStates[chatId].BotRequests[0].RequestTime - minuteInTheFuture).Seconds} " +
            $"seconds!", 
        cancellationToken);

        //Console.WriteLine((DateTime.UtcNow - _clientStates[chatId].BotRequests[0].RequestTime).Minutes);
        await Task.Delay(2500);
        return;
    }

    //checks if the message is a command or just a normal message
    CreateUpdateType(chatId, messageText);

    //If the message is a command
    if (_updateTypes[chatId].MsgType == MsgType.Command)
    {
        messageText = messageText.ToLower(); //Make sure the syntax is right
        switch (messageText)
        {

            case "/start":
                {
                    if (_clientStates[chatId].State != State.None) { await WarnToFinish(chatId, cancellationToken); }
                    else
                    {
                        SetState(chatId, State.Start);
                        await SendMsg(chatId, "Hello! My Name is GptTopBot! What is yours?", cancellationToken);
                    }
                    break;
                }
            case "/help":
                {
                    if (_clientStates[chatId].State != State.None) { await WarnToFinish(chatId, cancellationToken); }
                    else
                    {
                        var commands = await botClient.GetMyCommandsAsync();
                        var commandlist = string.Join("\n", commands.Select(x => $"/{x.Command} - {x.Description}"));
                        await SendMsg(chatId, commandlist, cancellationToken);
                    }
                    break;
                }
            case "/vacancies":
                {
                    if (_clientStates[chatId].State != State.None) { await WarnToFinish(chatId, cancellationToken); }
                    else
                    {
                        await SendVacancies(chatId, cancellationToken);
                    }
                    break;
                }
            case "/search_vacancies":
                {
                    if (_clientStates[chatId].State != State.None) { await WarnToFinish(chatId, cancellationToken); }
                    else
                    {
                        SetState(chatId, State.SearchVacancies);
                        await SendMsg(chatId, "Input Search:", cancellationToken);
                    }
                    break;
                }
            case "/search_employers":
                {
                    if (_clientStates[chatId].State != State.None) { await WarnToFinish(chatId, cancellationToken); }
                    else
                    {
                        SetState(chatId, State.SearchEmployers);
                        await SendMsg(chatId, "Input Search:", cancellationToken);
                    }
                    break;

                }
            case "/startchatgpt":
                {
                    if (_clientStates[chatId].State != State.None) { await WarnToFinish(chatId, cancellationToken); }
                    else
                    {
                        await SendMsg(chatId, "ChatGPT: Hello! This is ChatGPT, Ask me anything! B)", cancellationToken);
                        SetState(chatId, State.ChatGPT);
                    }
                    break;
                }
            case "/stopchatgpt":
                {
                    if (_clientStates[chatId].State == State.ChatGPT)
                    {
                        await SendMsg(chatId, "ChatGPT: It was nice talking to you! ChatGPT Out! B)", cancellationToken);
                        SetState(chatId, State.None);
                    }
                    else
                    {
                        await SendMsg(chatId, "ChatGPT: I wasn't even running in the first place! B)", cancellationToken);
                    }
                    break;
                }
            default: return;
        }
    }
    else if (_updateTypes[chatId].MsgType == MsgType.Message)
    {
        switch (clientState?.State)
        {
            case State.Start:
            {
                await SendWelcome(chatId, messageText, cancellationToken);
                SetState(chatId, State.None);
                break;
            }
            case State.SearchVacancies:
            {
                await SearchVacancies(messageText, chatId, cancellationToken);
                SetState(chatId, State.None);
                break;    
            }
            case State.SearchEmployers:
            {
                 await SearchEmployers(messageText, chatId, cancellationToken);
                 SetState(chatId, State.None);
                 break;
            }
            case State.ChatGPT:
            {
                 await StartChatGPT(chatId, messageText, cancellationToken);
                 break;
            }
           
            case State.None: return;
            default: return;

        }
    }
}



//Api Error Handler
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

//Small Methods
//checks all requests for the specified user and removes them if they timed out 1 minute
void CheckRequests(long chatId)
{
    if (_clientStates[chatId].BotRequests.Count == 0) { return; }

    for (int i = _clientStates[chatId].BotRequests.Count - 1; i >= 0; i--) //Backwards iteration for no errors
    {
        if ((DateTime.UtcNow - _clientStates[chatId].BotRequests[i].RequestTime).Minutes >= 1)
        {
            //Removes an element from the array if its been there for more than 60 seconds
            _clientStates[chatId].BotRequests.RemoveAt(i);
        }
    }

}
//adds a request to the specified user
bool SendRequest(long chatId)
{
    CheckRequests(chatId);
    if (_clientStates[chatId].BotRequests.Count >= 10)
    {
        return false;
    }
    else
    {
        BotRequest request = new BotRequest { RequestTime = DateTime.UtcNow };
        _clientStates[chatId].BotRequests.Add(request);
        return true;
    }
}

//tells the user to finish their command first
async Task WarnToFinish(ChatId chatId, CancellationToken cancellationToken)
{
    await SendMsg(chatId, "Please finish your other command first!", cancellationToken);
}

//sends a welcome msg and list of commands
async Task SendWelcome(ChatId chatId, string messageText, CancellationToken cancellationToken)
{
    await SendMsg(chatId, "Nice to meet you " + messageText + "! :D", cancellationToken);
    var commands = await botClient.GetMyCommandsAsync();
    var commandlist = string.Join("\n", commands.Select(x => $"/{x.Command} - {x.Description}"));
    await SendMsg(chatId, "Here's a list of commands to get you started:\n" + commandlist, cancellationToken);
}

//sends a message via telegram
async Task SendMsg(ChatId chatId, string Text, CancellationToken cts)
{
    await botClient.SendTextMessageAsync(
    chatId: chatId,
    text: Text,
    cancellationToken: cts);
}

//checks if a string is null or whitespace
static bool IsNullOrWhitespace(string s)
{
    Contract.Ensures(s != null || Contract.Result<bool>());
    Contract.Ensures((s != null && !Contract.ForAll<char>(s, c => char.IsWhiteSpace(c))) || Contract.Result<bool>());

    if (s == null) return true;

    for (var i = 0; i < s.Length; i++)
    {
        if (!char.IsWhiteSpace(s, i))
        {
            Contract.Assume(!Contract.ForAll<char>(s, c => char.IsWhiteSpace(c)));
            return false;
        }
    }
    return true;
}

//sets or creates the state for a client
void SetState(long key, State state)
{
    if (_clientStates.ContainsKey(key))
    {
        _clientStates[key].State = state;
    }
    else
    {
        _clientStates.TryAdd(key, new UserInfo { State = state });
    }
}

//sets or creates a message type for an update for a user
void SetUpdateType(long key, MsgType msgType)
{
    if (_updateTypes.ContainsKey(key))
    {
        _updateTypes[key].MsgType = msgType;
    }
    else
    {
        _updateTypes.TryAdd(key, new UpdateMsgType { MsgType = msgType });
    }
}

//asynchronously generats the message type
void CreateUpdateType(long chatId, string messageText)
{
    
    if (messageText.Contains('/'))
    {
       SetUpdateType(chatId, MsgType.Command);
    }
    else
    {
        SetUpdateType(chatId, MsgType.Message);
    }
    
}

//Long Methods
//starts chat gpt
async Task StartChatGPT(ChatId chatId, string messageText, CancellationToken cancellationToken)
{
    int count = 0;
    var starterText = "ChatGPT: ";
    string textContainer = "";
    var botmsg = await botClient.SendTextMessageAsync(chatId: chatId, text: starterText + "Writing Response...", cancellationToken: cancellationToken);
    var completionRequest = new CompletionRequest(messageText, Model.DavinciText, 200, 0.5, presencePenalty: 0.1, frequencyPenalty: 0.1);

    await foreach (var token in openApiClient.Completions.StreamCompletionEnumerableAsync(completionRequest))
    {
        
        if (!IsNullOrWhitespace(token.ToString()))
        {
            textContainer += token.ToString();
            count++;   
        }

        if(count%15==0 && textContainer!= "" && botmsg?.Text?.Equals((starterText + textContainer) + " ✍️") == false)
        {
            botmsg = await botClient.EditMessageTextAsync(chatId, botmsg.MessageId, (starterText + textContainer) + " ✍️");
            await Task.Delay(3000);
        }
        
    }

    botmsg = await botClient.EditMessageTextAsync(chatId, botmsg.MessageId, (starterText + textContainer));
    string? finalText = botmsg.Text;
    finalText += " 😎👍";
    await botClient.EditMessageTextAsync(chatId, botmsg.MessageId, finalText);
}

//sends a list of employers via HeadHunter API
async Task SearchEmployers(string messageText, ChatId chatId, CancellationToken cancellationToken)
{
    if (messageText.Length > 0)
    {
        Stopwatch sw = Stopwatch.StartNew();

        var employers = await HeadhunterClient.SearchEmployers(messageText);

        if (employers.Items.Length == 0)
        {
            await SendMsg(chatId, "No Employers Found!", cancellationToken);
        }

        if (employers.Items.Length > 0)
        {

            if (employers.Items.Length < 10)
            {
                await SendMsg(chatId, employers.ToString(), cancellationToken);
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
                    await SendMsg(chatId, msg, cancellationToken);
                    await Task.Delay(500);

                }
            }

            sw.Stop();
            TimeSpan ts = sw.Elapsed;
            await SendMsg(chatId, $"Command Completed! Total Employer Ammount: {employers.Items.Length}\nTime Elapsed: {ts}", cancellationToken);
        }

    }
    else if (messageText.Length <= 0)
    {
        await SendMsg(chatId, "No Search Term Detected! Try Again Later!", cancellationToken);
    }
    else
    {
        await SendMsg(chatId, "Error while executing command!", cancellationToken);
    }
}

//sends a list of vacancies via our ProblibIO Parser
async Task SendVacancies(ChatId chatId, CancellationToken cancellationToken)
{
    await SendMsg(chatId, "Collecting the vacancies! Please wait!\n(This might take a while)", cancellationToken);
    Stopwatch sw = Stopwatch.StartNew();
    int totalPages = await ProglibIOClient.GetTotalPagesAsync();
    int count = 0;
    List<string> urls = new List<string>();

    for (int i = 1; i <= totalPages; i++)
    {
        urls.Add("https://proglib.io/vacancies/all?workType=all&workPlace=all&experience=&salaryFrom=&page=" + i.ToString());
    }

    foreach (var currentUrl in urls)
    {
        IReadOnlyList<Vacancy> vacancies = await ProglibIOClient.GetVacanciesListByURLAsync(currentUrl);
        count += vacancies.Count;
        var msg = string.Join("\n\n\n", vacancies.Select(x => x.ToString()));
        await SendMsg(chatId, msg, cancellationToken);
        await Task.Delay(750);
    }
    sw.Stop();
    TimeSpan ts = sw.Elapsed;
    await SendMsg(chatId, $"Command Completed!\nTotal Ammount Of Vacancies: {count}\nTime Elapsed: {ts}", cancellationToken);
}

//searches for specified vacancies
async Task SearchVacancies(string searchName,ChatId chatId, CancellationToken cancellationToken)
{
    await SendMsg(chatId, "Collecting the vacancies! Please wait!\n(This might take a while)", cancellationToken);
    Stopwatch sw = Stopwatch.StartNew();
    int totalPages = await ProglibIOClient.GetTotalPagesAsync();
    int count = 0;
    List<string> urls = new List<string>();

    for (int i = 1; i <= totalPages; i++)
    {
        urls.Add("https://proglib.io/vacancies/all?workType=all&workPlace=all&experience=&salaryFrom=&page=" + i.ToString());
    }

    foreach (var currentUrl in urls)
    {
        IReadOnlyList<Vacancy> vacancies = await ProglibIOClient.GetVacanciesListByURLAndNameAsync(currentUrl, searchName);
        if(vacancies.Count > 0)
        {
            count += vacancies.Count;
            var msg = string.Join("\n\n", vacancies.Select(x => x.ToString()));
            await SendMsg(chatId, msg, cancellationToken);
            await Task.Delay(750);
        }
    }
    
    if(count == 0)
    {
        await SendMsg(chatId, $"Command Completed!\nNo Vacancies Found!", cancellationToken);
    }
    else
    {
        sw.Stop();
        TimeSpan ts = sw.Elapsed;
        await SendMsg(chatId, $"Command Completed!\nTotal Ammount Of Vacancies: {count}\nTime Elapsed: {ts}", cancellationToken);
    }
    
}


//Enums
internal enum State
{
    None,
    SearchEmployers,
    SearchVacancies,
    Start,
    ChatGPT
}

internal enum MsgType
{
    Command,
    Message
}


//Classes
internal class BotRequest
{
    public DateTime RequestTime { get; set; }
}

internal class UpdateMsgType
{
    public MsgType MsgType { get; set; }
}

internal class UserInfo
{
    public State State { get; set; }

    public List<BotRequest> BotRequests = new List<BotRequest>();
}






