using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Extensions.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace ExDriveBot
{
    class Program
    {
        private static string _token = "***REMOVED***";
        static TelegramBotClient _client = new TelegramBotClient(_token);
        static void Main(string[] args)
        {
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
                    string name = update.Message.Document.FileName;
                    Console.WriteLine(id);
                    Telegram.Bot.Types.File _download = await _client.GetFileAsync(id);
                    
                    string path = _download.FilePath;
                    Stream destination = new MemoryStream();
                    await _client.DownloadFileAsync(path, destination);
                    Console.WriteLine("Finished downloading...");

                    string designation = $@"D:\Bot\{name}";
                    var fileStream = System.IO.File.Create(designation);
                    destination.Seek(0, SeekOrigin.Begin);
                    destination.CopyTo(fileStream);
                    fileStream.Close();
                }
            }
        }
    }
}
