﻿using System;
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

using UnidecodeSharpFork;

using Newtonsoft.Json;

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

    struct Updates
    {
        public long updates;   
    }

    class Program
    {
        private static readonly string _token = "***REMOVED***";
        private static readonly string auditName = "audit.json";
        private static readonly string updatesName = "updates.json";

        private static readonly System.Net.Http.HttpClient http = new();
        private static readonly TelegramBotClient _client = new(_token, http, "http://localhost:8081/");

        private static List<BotUpdate> botUpdates = new();
        private static List<Updates> updatesNum = new();

        private static long chatid;
        static void Main()
        {
            try
            {
                var botUpdatesString = System.IO.File.ReadAllText(auditName);

                botUpdates = JsonConvert.DeserializeObject<List<BotUpdate>>(botUpdatesString) ??
                    botUpdates;

                var updatesNumber = System.IO.File.ReadAllText(updatesName);

                updatesNum = JsonConvert.DeserializeObject<List<Updates>>(updatesNumber) ??
                    updatesNum;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error reading or deserializing {ex}");
            }

            var receiverOptions = new ReceiverOptions
            {
                Offset = (int?)updatesNum.ToArray()[0].updates,
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

                if (update.Message.Type != MessageType.Document)
                {
                    await _client.SendTextMessageAsync(update.Message.Chat.Id, $"Please send me files as documents instead.", cancellationToken: arg3);
                    return;
                }

                if (update.Message.Document.FileSize > 620000000)
                {
                    await _client.SendTextMessageAsync(update.Message.Chat.Id, $"File size should be less than 650mb.", cancellationToken: arg3);
                    return;
                }

                string id = update.Message.Document.FileId;
                chatid = update.Message.Chat.Id;
                string name = update.Message.Document.FileName;
                int? pre_size = update.Message.Document.FileSize;

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
                    
                    HttpResponseMessage response = http.PostAsync("https://localhost:443/Storage/UploadTempFileBot", inputData, arg3).Result;
                    if (((int)response.StatusCode) != 200)
                    {
                        _ = await _client.SendTextMessageAsync(chatid, $"Download link for \"{name}\": " + downloadlink,
                                                            cancellationToken: arg3);
                        return;
                    }

                    downloadlink = response.Content.ReadAsStringAsync(arg3).Result;
                }

                int? size = _download.FileSize;

                BotUpdate _botUpdate = new()
                {
                    chat_id = chatid,
                    username = update.Message.Chat.Username,
                    file_id = id,
                    file_name = name,
                    file_size = size,
                    file_status = "Done",
                };

                Updates _updates = new()
                {
                    updates = updatesNum.ToArray()[0].updates + 1,
                };

                updatesNum.Clear();

                updatesNum.Add(_updates);
                botUpdates.Add(_botUpdate);

                Console.WriteLine($"\"{name.Unidecode()}\" finished downloading ({size} bits)");

                var botUpdatesString = JsonConvert.SerializeObject(botUpdates);
                var updatesNumberString = JsonConvert.SerializeObject(updatesNum);

                System.IO.File.WriteAllText(auditName, botUpdatesString);
                System.IO.File.WriteAllText(updatesName, updatesNumberString);

                if (!string.IsNullOrEmpty(downloadlink))
                {
                    _ = await _client.SendTextMessageAsync(chatid, $"Download link for \"{name}\": " + downloadlink, cancellationToken: arg3);
                }

                else
                {
                    _ = await _client.SendTextMessageAsync(chatid, $"Oops... It looks like something went worng. Please try again.", cancellationToken: arg3);
                }
            }
            catch (Exception)
            {
                _ = await _client.SendTextMessageAsync(chatid, $"Oops... It looks like something went worng. Please try again.", cancellationToken: arg3);

                return;
            }
        }       
    }
}
