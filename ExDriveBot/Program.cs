using System;
using System.IO;
using System.Net;

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
        private static string _token = String.Empty;

        private static readonly string _auditName = "audit.json";
        private static readonly string _updatesName = "updates.json";

        private static readonly HttpClientHandler _clientHandler = new();
        private static HttpClient _http;
        private static TelegramBotClient _client;
        
        private static string _postUrl = String.Empty;
        private static string _apiUrl = String.Empty;

        private static List<Updates> _updatesCount = new();
        private static List<BotUpdate> _botUpdates = new();
        private static readonly long _maxFileSize = 620000000;  // File size is represented in bits

        private static string _absPath;
        private static string _newName;

        private static long _chatid;

        static void Main(string[] args)
        {
            _token = Environment.GetEnvironmentVariable("BOT_TOKEN");

            if (args.Length == 2)
            {
                _postUrl = $"https://localhost:{args[0]}/Storage/UploadTempFileBot";

                _apiUrl = $"http://localhost:{args[1]}/";
            }
            else
            {
                _postUrl = "https://localhost:44370/Storage/UploadTempFileBot";

                _apiUrl = "http://localhost:8081/";
            }
            
            _client = new(_token, _http, _apiUrl);

            _clientHandler.ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => { return true; };
            _http = new(_clientHandler);

            try
            {
                var botUpdatesString = System.IO.File.ReadAllText(_auditName);

                _botUpdates = JsonConvert.DeserializeObject<List<BotUpdate>>(botUpdatesString) ??
                    _botUpdates;

                var updatesNumber = System.IO.File.ReadAllText(_updatesName);

                _updatesCount = JsonConvert.DeserializeObject<List<Updates>>(updatesNumber) ??
                    _updatesCount;
            }
            catch (Exception)
            {
                _updatesCount.Add(new Updates());
                _botUpdates.Add(new BotUpdate());

                var botUpdatesString = JsonConvert.SerializeObject(_botUpdates);
                var updatesNumberString = JsonConvert.SerializeObject(_updatesCount);

                System.IO.File.WriteAllText(_auditName, botUpdatesString);
                System.IO.File.WriteAllText(_updatesName, updatesNumberString);
;            }

            var receiverOptions = new ReceiverOptions
            {
                Offset = _updatesCount.ToArray()[0].UpdatesCount,

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
            throw new Exception($"An error occured: {arg2.Message}");
        }

        private static async void SendMessageAsync(string message, CancellationToken token)
        {
            _ = await _client.SendTextMessageAsync(_chatid, message, cancellationToken: token);
        }

        private static async Task UpdateHandler(ITelegramBotClient _client, Update update, CancellationToken arg3)
        {
            try
            {
                if (update.Type != UpdateType.Message)
                {
                    return;
                }

                _chatid = update.Message.Chat.Id;

                if (update.Message.Type == MessageType.Sticker)
                {
                    InputOnlineFile onlineFile = new("https://media.giphy.com/media/CHQYQUsiWFggJ49tJB/giphy.gif");

                    _ = await _client.SendAnimationAsync(update.Message.Chat.Id, onlineFile, cancellationToken: arg3);

                    return;
                }

                if (update.Message.Type == MessageType.Audio ||
                    update.Message.Type == MessageType.Photo ||
                    update.Message.Type == MessageType.Video ||
                    update.Message.Type == MessageType.Voice ||
                    update.Message.Type == MessageType.Unknown)
                {
                    SendMessageAsync($@"Please send me files as documents instead.", arg3);
                    
                    return;
                }

                if (update.Message.Type != MessageType.Document)
                {
                    return;
                }

                if (update.Message.Document.FileSize > _maxFileSize)
                {
                    SendMessageAsync($@"File size should be more than 650mb.", arg3);
                    
                    return;
                }

                string id = update.Message.Document.FileId;
                string name = update.Message.Document.FileName;

                SendMessageAsync($"Download for \"{name}\" started... You will be notified shortly once the link is ready!", arg3);
                
                Telegram.Bot.Types.File _download;
                _download = await _client.GetFileAsync(id, cancellationToken: arg3);

                var path = _download.FilePath;
                _absPath = path.Remove(path.LastIndexOf('\\'));

                _newName = Guid.NewGuid().ToString();

                Directory.CreateDirectory(Path.Combine(_absPath, _newName));
                System.IO.File.Move(path, Path.Combine(_absPath, _newName, name));

                string downloadlink = "";
                using (Stream stream = System.IO.File.OpenRead(path: Path.Combine(_absPath, _newName, name)))
                {
                    var requestContent = new MultipartFormDataContent();
                    var inputData = new StreamContent(stream);

                    inputData.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
                    inputData.Headers.Add("file-name", name.Unidecode());

                    requestContent.Headers.ContentType = new MediaTypeHeaderValue("application/json");

                    HttpResponseMessage response = _http.PostAsync(_postUrl, inputData, arg3).Result;
                    downloadlink = response.Content.ReadAsStringAsync(arg3).Result;

                    if (response.StatusCode != HttpStatusCode.OK)
                    {
                        SendMessageAsync($"Download of \"{name}\" failed. Please try again.",
                            arg3);
                        
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

                var botUpdatesString = JsonConvert.SerializeObject(_botUpdates);
                var updatesNumberString = JsonConvert.SerializeObject(_updatesCount);

                System.IO.File.WriteAllText(_auditName, botUpdatesString);
                System.IO.File.WriteAllText(_updatesName, updatesNumberString);

                if (!string.IsNullOrEmpty(downloadlink))
                {
                    SendMessageAsync($"Download link for \"{name}\": {downloadlink}", arg3);

                    Directory.Delete(Path.Combine(_absPath, _newName), true);
                }
                else
                {
                    throw new Exception("File link is not valid");
                }
            }
            catch (Exception)
            {
                SendMessageAsync($@"Oops... We are sorry, but it looks like something went wrong. Please try again.",
                        arg3);
                
                Directory.Delete(Path.Combine(_absPath, _newName), true);

                return;
            }
        }
    }
}
