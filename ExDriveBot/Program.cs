using System;
using System.IO;

using System.Collections.Generic;

using System.Net.Http;
using System.Net.Http.Headers;

using System.Threading;
using System.Threading.Tasks;

using Telegram.Bot;
using Telegram.Bot.Extensions.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.InputFiles;

using UnidecodeSharpFork;

using Newtonsoft.Json;

namespace ExDriveBot
{
    struct BotUpdate
    {
        public long ChatId;
        public string UserName;
        public string FileId;
        public string FileName;
        public int FileSize;
    }

    struct Updates
    {
        public int UpdatesCount;
    }

    class Program
    {
        private static readonly string _token = "***REMOVED***";

        private static readonly string _auditName = "audit.json";
        private static readonly string _updatesName = "updates.json";

        private static readonly System.Net.Http.HttpClient _http = new();
        private static readonly TelegramBotClient _client = new(_token, _http, "http://localhost:8081/");
        private static readonly string _postUrl = "https://localhost:44370/Storage/UploadTempFileBot";

        private static List<Updates> _updatesCount = new();
        private static List<BotUpdate> _botUpdates = new();
        private static readonly long _maxFileSize = 620000000;  // File size is represented in bits

        private static long _chatid;

        static void Main()
        {
            try
            {
                var botUpdatesString = System.IO.File.ReadAllText(_auditName);

                _botUpdates = JsonConvert.DeserializeObject<List<BotUpdate>>(botUpdatesString) ??
                    _botUpdates;

                var updatesNumber = System.IO.File.ReadAllText(_updatesName);

                _updatesCount = JsonConvert.DeserializeObject<List<Updates>>(updatesNumber) ??
                    _updatesCount;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error reading or deserializing {ex}");
            }

            var receiverOptions = new ReceiverOptions
            {
                Offset = _updatesCount.ToArray()[0].UpdatesCount,
                Limit = 100,
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
            throw new Exception("An error occured");
        }

        private static async Task UpdateHandler(ITelegramBotClient _client, Update update, CancellationToken arg3)
        {
            try
            {
                if (update.Type != UpdateType.Message)
                {
                    return;
                }

                if (update.Message.Type == MessageType.Sticker)
                {
                    InputOnlineFile onlineFile = new("https://media.giphy.com/media/CHQYQUsiWFggJ49tJB/giphy.gif");

                    _ = await _client.SendAnimationAsync(update.Message.Chat.Id, onlineFile, cancellationToken: arg3);

                    return;
                }

                if (update.Message.Type == MessageType.Voice)
                {
                    InputOnlineFile onlineFile = new("https://media.giphy.com/media/848sepFf5US1WT8L7N/giphy.gif");

                    _ = await _client.SendAnimationAsync(update.Message.Chat.Id, onlineFile, cancellationToken: arg3);

                    return;
                }

                if (update.Message.Type != MessageType.Document)
                {
                    return;
                }

                if (update.Message.Type == MessageType.Audio ||
                    update.Message.Type == MessageType.Photo ||
                    update.Message.Type == MessageType.Video ||
                    update.Message.Type == MessageType.Unknown)
                {
                    await _client.SendTextMessageAsync(update.Message.Chat.Id, 
                        $@"Please send me files as documents instead.", cancellationToken: arg3);

                    return;
                }

                if (update.Message.Document.FileSize > _maxFileSize)
                {
                    await _client.SendTextMessageAsync(update.Message.Chat.Id,
                        $@"File size should be more than 650mb.", cancellationToken: arg3);

                    return;
                }

                _chatid = update.Message.Chat.Id;

                string id = update.Message.Document.FileId;
                string name = update.Message.Document.FileName;

                Telegram.Bot.Types.File _download;
                _download = await _client.GetFileAsync(id, cancellationToken: arg3);

                var path = _download.FilePath;
                string abspath = path.Remove(path.LastIndexOf('\\'));

                string newname = Guid.NewGuid().ToString();

                System.IO.Directory.CreateDirectory(Path.Combine(abspath, newname));
                System.IO.File.Move(path, Path.Combine(abspath, newname, name));

                string downloadlink = "";
                using (Stream stream = System.IO.File.OpenRead(path: Path.Combine(abspath, newname, name)))
                {
                    var requestContent = new MultipartFormDataContent();
                    var inputData = new StreamContent(stream);

                    inputData.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
                    inputData.Headers.Add("file-name", name.Unidecode());

                    requestContent.Headers.ContentType = new MediaTypeHeaderValue("application/json");

                    HttpResponseMessage response = _http.PostAsync(_postUrl, inputData, arg3).Result;
                    downloadlink = response.Content.ReadAsStringAsync(arg3).Result;

                    if (((int)response.StatusCode) != 200)
                    {
                        _ = await _client.SendTextMessageAsync(_chatid, 
                            $"Download of \"{name}\" failed. Please try again.",
                            cancellationToken: arg3);

                        return;
                    }
                }

                int size = (int)_download.FileSize;

                BotUpdate botUpdate = new()
                {
                    ChatId = _chatid,
                    UserName = update.Message.Chat.Username,
                    FileId = id,
                    FileName = name,
                    FileSize = size,
                };

                Updates updates = new()
                {
                    UpdatesCount = _updatesCount.ToArray()[0].UpdatesCount + 1,
                };

                _updatesCount.Clear();
                _updatesCount.Add(updates);

                _botUpdates.Add(botUpdate);

                Console.WriteLine($"\"{name.Unidecode()}\" finished downloading ({size} bits)");

                var botUpdatesString = JsonConvert.SerializeObject(_botUpdates);
                var updatesNumberString = JsonConvert.SerializeObject(_updatesCount);

                System.IO.File.WriteAllText(_auditName, botUpdatesString);
                System.IO.File.WriteAllText(_updatesName, updatesNumberString);

                if (!string.IsNullOrEmpty(downloadlink))
                {
                    _ = await _client.SendTextMessageAsync(_chatid, $"Download link for \"{name}\": " + downloadlink,
                        cancellationToken: arg3);
                }
                else
                {
                    _ = await _client.SendTextMessageAsync(_chatid,
                        $@"Oops... We are sorry, but it looks like something went wrong. Please try again.",
                        cancellationToken: arg3);
                }

                Directory.Delete(Path.Combine(abspath, newname), true);
            }
            catch (Exception)
            {
                _ = await _client.SendTextMessageAsync(_chatid, 
                    $@"Oops... We are sorry, but it looks like something went wrong. Please try again.",
                    cancellationToken: arg3);

                return;
            }
        }
    }
}
