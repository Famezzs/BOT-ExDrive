using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Extensions.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.InputFiles;

public class CustomException : Exception
{
    public CustomException(String message) : base(message)
    { }
}

namespace ExDriveBot
{
    struct BotUpdate
    {
        public long chat_id;
        public string? username;
        public string file_id;
        public string file_name;
        public int? file_size;
        public string file_status;
    }

    class Program
    {
        static System.Net.Http.HttpClient http = new System.Net.Http.HttpClient();
        private static string _token = "***REMOVED***";
        static TelegramBotClient _client = new TelegramBotClient(_token, http, "http://localhost:8081/bot"); // запуск локально
        // static TelegramBotClient _client = new TelegramBotClient(_token);                                 // запуск на серверах Telegram
        static string auditName = "audit.json";
        static List<BotUpdate> botUpdates = new List<BotUpdate>();
        static void Main(string[] args)
        {
            try
            {
                var botUpdatesString = System.IO.File.ReadAllText(auditName);

                botUpdates = JsonConvert.DeserializeObject<List<BotUpdate>>(botUpdatesString) ??
                    botUpdates;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error reading or deserializing {ex}");
            }

            var receiverOptions = new ReceiverOptions
            {
                AllowedUpdates = new UpdateType[]
                {
                    UpdateType.Message,
                }
            };

            _client.StartReceiving(UpdateHandler, ErrorHandler, receiverOptions);
            Console.ReadLine();
        }

        private static Task ErrorHandler(ITelegramBotClient arg1, Exception arg2, CancellationToken arg3)
        {
                throw new NotImplementedException();
        }

        private static async Task UpdateHandler(ITelegramBotClient _client, Update update, CancellationToken arg3)
        {
            if(update.Type == UpdateType.Message)
            {
                if (update.Message.Type == MessageType.Document)
                {
                    string id = update.Message.Document.FileId;
                    long chatid = update.Message.Chat.Id;
                    string name = update.Message.Document.FileName;
                    int? pre_size = update.Message.Document.FileSize;
                    if (pre_size <= 2000000000)
                    {
                        Telegram.Bot.Types.File _download = await _client.GetFileAsync(id);
                        int? size = _download.FileSize;

                        var _botUpdate = new BotUpdate
                        {
                            chat_id = chatid,
                            username = update.Message.Chat.Username,
                            file_id = id,
                            file_name = name,
                            file_size = size,
                            file_status = "Done",
                        };
                        //InputOnlineFile inputOnlineFile = new InputOnlineFile(id);
                        //await _client.SendDocumentAsync()
                        // Stream destination = new MemoryStream();
                        // await _client.DownloadFileAsync(path, destination);
                        Console.WriteLine($"\"{name}\" finished downloading ({size} bits)");

                        //if (destination.Length > 0)
                          //  _botUpdate.file_status = "Downloaded";
                        //else
                          //  _botUpdate.file_status = "Error during download";
                        //string designation = $@"D:\Bot\{name}";
                        //var fileStream = System.IO.File.Create(designation);
                        //destination.Seek(0, SeekOrigin.Begin);
                        //destination.CopyTo(fileStream);
                        //if (fileStream.Length > 0)
                          //  _botUpdate.file_status = "Written";
                        //else
                          //  _botUpdate.file_status = "Error during writing";
                        //fileStream.Close();

                        botUpdates.Add(_botUpdate);
                        var botUpdatesString = JsonConvert.SerializeObject(botUpdates);

                        System.IO.File.WriteAllText(auditName, botUpdatesString);
                        await _client.SendTextMessageAsync(chatid, $"Download link for \"{name}\": ");
                    }
                    else
                    {
                        await _client.SendTextMessageAsync(chatid, $"Download of \"{name}\" failed.");
                    }
                }
            }
        }
    }
}
