using Discord;
using Discord.Commands;
using Discord.Net;
using Discord.WebSocket;
using PKHeX.Core;
using PKHeX.Core.AutoMod;
using PKHeX.Drawing.PokeSprite;
using SysBot.Pokemon.Discord.Commands.Bots;
using SysBot.Pokemon.Helpers;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Color = System.Drawing.Color;
using DiscordColor = Discord.Color;

namespace SysBot.Pokemon.Discord;

public static class QueueHelper<T> where T : PKM, new()
{
    private const uint MaxTradeCode = 9999_9999;

    // A dictionary to hold batch trade file paths and their deletion status
    private static readonly Dictionary<int, List<string>> batchTradeFiles = [];

    private static readonly Dictionary<ulong, int> userBatchTradeMaxDetailId = [];

    private static readonly ConcurrentDictionary<ulong, int> ActiveBatchIds = new();

    private static readonly Dictionary<int, string> MilestoneImages = new()
    {
       
    };

    private static int GetOrCreateBatchId(ulong userId, int batchTradeNumber)
    {
        if (batchTradeNumber == 1)
        {
            var newId = GenerateUniqueTradeID();
            ActiveBatchIds[userId] = newId;
            return newId;
        }

        return ActiveBatchIds.TryGetValue(userId, out var existingId) ? existingId : GenerateUniqueTradeID();
    }

    public static async Task AddToQueueAsync(SocketCommandContext context, int code, string trainer, RequestSignificance sig, T trade, PokeRoutineType routine, PokeTradeType type, SocketUser trader, bool isBatchTrade = false, int batchTradeNumber = 1, int totalBatchTrades = 1, bool isHiddenTrade = false, bool isMysteryMon = false, bool isMysteryEgg = false, List<Pictocodes>? lgcode = null, bool ignoreAutoOT = false, bool setEdited = false, bool isNonNative = false)

    {
        var queueCheck = new TradeQueueResult(true);
        if (!queueCheck.Success)
        {
            return;
        }
        if ((uint)code > MaxTradeCode)
        {
            await context.Channel.SendMessageAsync("Trade code should be 00000000-99999999!").ConfigureAwait(false);
            return;
        }

        try
        {
            if (!isBatchTrade || batchTradeNumber == 1)
            {
                if (trade is PB7 && lgcode != null)
                {
                    var (thefile, lgcodeembed) = CreateLGLinkCodeSpriteEmbed(lgcode);
                    await trader.SendFileAsync(thefile, "Your trade code will be.", embed: lgcodeembed).ConfigureAwait(false);
                }
                else
                {
                    await EmbedHelper.SendTradeCodeEmbedAsync(trader, code).ConfigureAwait(false);
                }
            }

            var result = await AddToTradeQueue(context, trade, code, trainer, sig, routine, isBatchTrade ? PokeTradeType.Batch : type, trader, isBatchTrade, batchTradeNumber, totalBatchTrades, isHiddenTrade, isMysteryMon, isMysteryEgg, lgcode, ignoreAutoOT, setEdited, isNonNative).ConfigureAwait(false);
        }
        catch (HttpException ex)
        {
            await HandleDiscordExceptionAsync(context, trader, ex).ConfigureAwait(false);
        }
    }

    public static Task AddToQueueAsync(SocketCommandContext context, int code, string trainer, RequestSignificance sig, T trade, PokeRoutineType routine, PokeTradeType type, bool ignoreAutoOT = false)
    {
        return AddToQueueAsync(context, code, trainer, sig, trade, routine, type, context.User, ignoreAutoOT: ignoreAutoOT);
    }

    private static async Task<TradeQueueResult> AddToTradeQueue(SocketCommandContext context, T pk, int code, string trainerName, RequestSignificance sig, PokeRoutineType type, PokeTradeType t, SocketUser trader, bool isBatchTrade, int batchTradeNumber, int totalBatchTrades, bool isHiddenTrade, bool isMysteryMon = false, bool isMysteryEgg = false, List<Pictocodes>? lgcode = null, bool ignoreAutoOT = false, bool setEdited = false, bool isNonNative = false)
    {
        var user = trader;
        var userID = user.Id;
        var name = user.Username;
        var trainer = new PokeTradeTrainerInfo(trainerName, userID);
        var notifier = new DiscordTradeNotifier<T>(pk, trainer, code, trader, batchTradeNumber, totalBatchTrades, isMysteryMon, isMysteryEgg, lgcode: lgcode);
        int uniqueTradeID = isBatchTrade
            ? GetOrCreateBatchId(userID, batchTradeNumber)
            : GenerateUniqueTradeID();
        var detail = new PokeTradeDetail<T>(pk, trainer, notifier, t, code, sig == RequestSignificance.Favored, lgcode, batchTradeNumber, totalBatchTrades, isMysteryMon, isMysteryEgg, uniqueTradeID, ignoreAutoOT, setEdited);
        var trade = new TradeEntry<T>(detail, userID, PokeRoutineType.LinkTrade, name, uniqueTradeID);
        var hub = SysCord<T>.Runner.Hub;
        var Info = hub.Queues.Info;
        var allowMultiple = isBatchTrade;
        var isSudo = sig == RequestSignificance.Owner;
        var added = Info.AddToTradeQueue(trade, userID, allowMultiple, isSudo);
        if (isBatchTrade && batchTradeNumber == totalBatchTrades)
        {
            ActiveBatchIds.TryRemove(userID, out _);
        }
        int totalTradeCount = 0;
        TradeCodeStorage.TradeCodeDetails? tradeDetails = null;
        if (SysCord<T>.Runner.Config.Trade.TradeConfiguration.StoreTradeCodes)
        {
            var tradeCodeStorage = new TradeCodeStorage();
            totalTradeCount = tradeCodeStorage.GetTradeCount(trader.Id);
            tradeDetails = tradeCodeStorage.GetTradeDetails(trader.Id);
        }
        var queueCheck = new TradeQueueResult(true);
        if (!queueCheck.Success)
        {
            return new TradeQueueResult(false);
        }
        if (added == QueueResultAdd.AlreadyInQueue)
        {
            return new TradeQueueResult(false);
        }

        var embedData = DetailsExtractor<T>.ExtractPokemonDetails(
            pk, trader, isMysteryMon, isMysteryEgg, type == PokeRoutineType.Clone, type == PokeRoutineType.Dump,
            type == PokeRoutineType.FixOT, type == PokeRoutineType.SeedCheck, isBatchTrade, batchTradeNumber, totalBatchTrades
        );

        try
        {
            (string embedImageUrl, DiscordColor embedColor) = await PrepareEmbedDetails(pk);

            embedData.EmbedImageUrl = isMysteryMon ? "https://media.discordapp.net/attachments/1234791557396172874/1389388345708122163/Mystery_Egg.png?ex=6864703b&is=68631ebb&hm=befeec4cff4e25c1c16ebd5ed26e5fc875e2b0a06f6b140138551419b6225978&=" :
                                      isMysteryEgg ? "https://media.discordapp.net/attachments/1234791557396172874/1389388345708122163/Mystery_Egg.png?ex=6864703b&is=68631ebb&hm=befeec4cff4e25c1c16ebd5ed26e5fc875e2b0a06f6b140138551419b6225978&=" :
                                       type == PokeRoutineType.Dump ? "https://images-ext-1.discordapp.net/external/UHH02wZLmQbmM-U6fU9-nslUQyRFlP_hC0ViqXl7XMA/https/i.imgur.com/iwCCCAY.gif?width=980&height=971" :
                                       type == PokeRoutineType.Clone ? "https://media.discordapp.net/attachments/1375740073814917242/1393818901112033290/7L5CfPt.png?ex=68748e81&is=68733d01&hm=1efdba0976c02751d971fee507710f296b4f8ef5d699b300d2e6a0971d2d3fa5&=&format=webp&quality=lossless&width=407&height=401" :
                                       type == PokeRoutineType.SeedCheck ? "https://media.discordapp.net/attachments/1234791557396172874/1387497702132289657/sHtnqOm.gif?ex=685d8f6e&is=685c3dee&hm=a1301a22e4ee2b2b50b296fa841965f7af88837f56214c621da9be95d2d70974&=.gif" :
                                       type == PokeRoutineType.FixOT ? "https://media.discordapp.net/attachments/1234791557396172874/1387498978630697080/emote_icon_pose_13s.png?ex=685d909e&is=685c3f1e&hm=6e63e8b471221de36e6326cf73d9b270d1081740bacd1c4d5b97ca51a6e91a27&=&format=webp&quality=lossless" :
                                       embedImageUrl;

            

            embedData.IsLocalFile = File.Exists(embedData.EmbedImageUrl);

            var position = Info.CheckPosition(userID, uniqueTradeID, type);
            var botct = Info.Hub.Bots.Count;
            var baseEta = position.Position > botct ? Info.Hub.Config.Queues.EstimateDelay(position.Position, botct) : 0;
            var etaMessage = $"Estimated: {baseEta:F1} min(s) for trade {batchTradeNumber}/{totalBatchTrades}.";
            string footerText = $"Current Position: {(position.Position == -1 ? 1 : position.Position)}";

            string userDetailsText = DetailsExtractor<T>.GetUserDetails(totalTradeCount, tradeDetails);
            if (!string.IsNullOrEmpty(userDetailsText))
            {
                footerText += $"\n{userDetailsText}";
            }
            footerText += $"\n{etaMessage}";

            var embedBuilder = new EmbedBuilder()
                .WithColor(embedColor)
                .WithThumbnailUrl(embedData.IsLocalFile ? $"attachment://{Path.GetFileName(embedData.EmbedImageUrl)}" : embedData.EmbedImageUrl)
                .WithFooter(footerText)
                .WithAuthor(new EmbedAuthorBuilder()
                    .WithName(embedData.AuthorName)
                    .WithIconUrl(trader.GetAvatarUrl() ?? trader.GetDefaultAvatarUrl())
                    .WithUrl("https://media.discordapp.net/attachments/1234791557396172874/1387472253905801226/Banner_main.png?ex=685d77bb&is=685c263b&hm=743cf2beb927fb2bc33363dc933135fdc4c3fbf6066b0202eb176ebaeceee30a&=&format=webp&quality=lossless"));

            DetailsExtractor<T>.AddAdditionalText(embedBuilder);

            if (!isMysteryMon && !isMysteryEgg && type != PokeRoutineType.Clone && type != PokeRoutineType.Dump && type != PokeRoutineType.FixOT && type != PokeRoutineType.SeedCheck)
            {
                DetailsExtractor<T>.AddNormalTradeFields(embedBuilder, embedData, trader.Mention, pk);
            }
            else
            {
                DetailsExtractor<T>.AddSpecialTradeFields(embedBuilder, isMysteryMon, isMysteryEgg, type == PokeRoutineType.SeedCheck, type == PokeRoutineType.Clone, type == PokeRoutineType.FixOT, trader.Mention);
            }

            if (setEdited && Info.Hub.Config.Trade.AutoCorrectConfig.AutoCorrectEmbedIndicator)
            {
                embedBuilder.Footer.IconUrl = "https://raw.githubusercontent.com/ThatOneCrab/sprites/main/setedited.png";
                embedBuilder.AddField("**__Notice__**: **Your Showdown Set was Invalid.**", "*Auto Corrected to make legal.*");
            }
            // Check if the Pokemon is Non-Native and/or has a Home Tracker
            if (pk is IHomeTrack homeTrack)
            {
                if (homeTrack.HasTracker && isNonNative)
                {
                    // Both Non-Native and has Home Tracker
                    embedBuilder.Footer.IconUrl = "";
                    embedBuilder.AddField("**__Notice__**: **This Pokemon is Non-Native & Has Home Tracker.**", "*AutoOT not applied.*");
                }
                else if (homeTrack.HasTracker)
                {
                    // Only has Home Tracker
                    embedBuilder.Footer.IconUrl = "";
                    embedBuilder.AddField("**__Notice__**: **Home Tracker Detected.**", "*Auto OT Disabled.*");
                }
            }
            else if (isNonNative)
            {
                // Fallback for Non-Native Pokemon that don't implement IHomeTrack
                embedBuilder.Footer.IconUrl = "";
                embedBuilder.AddField("**__Notice__**: **This Pokemon may not enter Pokémon Home.**", "*& AutoOT not applied.*");
            }
            DetailsExtractor<T>.AddThumbnails(embedBuilder, type == PokeRoutineType.Clone, type == PokeRoutineType.SeedCheck, embedData.HeldItemUrl);

            if (!isHiddenTrade && SysCord<T>.Runner.Config.Trade.TradeEmbedSettings.UseEmbeds)
            {
                var embed = embedBuilder.Build();
                if (embed == null)
                {
                    Console.WriteLine("Error: Embed is null.");
                    await context.Channel.SendMessageAsync("An error occurred while preparing the trade details.");
                    return new TradeQueueResult(false);
                }

                if (embedData.IsLocalFile)
                {
                    await context.Channel.SendFileAsync(embedData.EmbedImageUrl, embed: embed);
                    if (isBatchTrade)
                    {
                        userBatchTradeMaxDetailId[userID] = Math.Max(userBatchTradeMaxDetailId.GetValueOrDefault(userID), detail.ID);
                        await ScheduleFileDeletion(embedData.EmbedImageUrl, 0, detail.ID);
                        if (detail.ID == userBatchTradeMaxDetailId[userID] && batchTradeNumber == totalBatchTrades)
                        {
                            DeleteBatchTradeFiles(detail.ID);
                        }
                    }
                    else
                    {
                        await ScheduleFileDeletion(embedData.EmbedImageUrl, 0);
                    }
                }
                else
                {
                    await context.Channel.SendMessageAsync(embed: embed);
                }
            }
            else
            {
                var message = $"{trader.Mention} - Added to the LinkTrade queue. Current Position: {position.Position}. Receiving: {embedData.SpeciesName}.\n{etaMessage}";
                await context.Channel.SendMessageAsync(message);
            }
        }
        catch (HttpException ex)
        {
            await HandleDiscordExceptionAsync(context, trader, ex);
            return new TradeQueueResult(false);
        }

        if (SysCord<T>.Runner.Hub.Config.Trade.TradeConfiguration.StoreTradeCodes)
        {
            var tradeCodeStorage = new TradeCodeStorage();
            int tradeCount = tradeCodeStorage.GetTradeCount(trader.Id);
           
        }

        return new TradeQueueResult(true);
    }

    private static int GenerateUniqueTradeID()
    {
        long timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        int randomValue = Random.Shared.Next(1000);
        return (int)((timestamp % int.MaxValue) * 1000 + randomValue);
    }

    private static string GetImageFolderPath()
    {
        string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
        string imagesFolder = Path.Combine(baseDirectory, "Images");

        if (!Directory.Exists(imagesFolder))
        {
            Directory.CreateDirectory(imagesFolder);
        }

        return imagesFolder;
    }

    private static string SaveImageLocally(System.Drawing.Image image)
    {
        // Get the path to the images folder
        string imagesFolderPath = GetImageFolderPath();

        // Create a unique filename for the image
        string filePath = Path.Combine(imagesFolderPath, $"image_{Guid.NewGuid()}.png");

        // Save the image to the specified path
#pragma warning disable CA1416 // Validate platform compatibility
        image.Save(filePath, System.Drawing.Imaging.ImageFormat.Png);
#pragma warning restore CA1416 // Validate platform compatibility

        return filePath;
    }

    private static async Task<(string, DiscordColor)> PrepareEmbedDetails(T pk)
    {
        string embedImageUrl;
        string speciesImageUrl;

        if (pk.IsEgg)
        {
            const string eggImageUrl = "https://raw.githubusercontent.com/ThatOneCrab/sprites/main/egg.png";
            speciesImageUrl = TradeExtensions<T>.PokeImg(pk, false, true, null);
            System.Drawing.Image combinedImage = await OverlaySpeciesOnEgg(eggImageUrl, speciesImageUrl);
            embedImageUrl = SaveImageLocally(combinedImage);
        }
        else
        {
            bool canGmax = pk is PK8 pk8 && pk8.CanGigantamax;
            speciesImageUrl = TradeExtensions<T>.PokeImg(pk, canGmax, false, SysCord<T>.Runner.Config.Trade.TradeEmbedSettings.PreferredImageSize);
            embedImageUrl = speciesImageUrl;
        }

        var strings = GameInfo.GetStrings("en");
        string ballName = strings.balllist[pk.Ball];
        if (ballName.Contains("(LA)"))
        {
            ballName = "la" + ballName.Replace(" ", "").Replace("(LA)", "").ToLower();
        }
        else
        {
            ballName = ballName.Replace(" ", "").ToLower();
        }

        string ballImgUrl = $"https://raw.githubusercontent.com/ThatOneCrab/sprites/main/AltBallImg/20x20/{ballName}.png";

        // Check if embedImageUrl is a local file or a web URL
        if (Uri.TryCreate(embedImageUrl, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeFile)
        {
            // Load local image directly
#pragma warning disable CA1416 // Validate platform compatibility
            using var localImage = System.Drawing.Image.FromFile(uri.LocalPath);
#pragma warning restore CA1416 // Validate platform compatibility
            using var ballImage = await LoadImageFromUrl(ballImgUrl);
            if (ballImage != null)
            {
#pragma warning disable CA1416 // Validate platform compatibility
                using (var graphics = Graphics.FromImage(localImage))
                {
#pragma warning disable CA1416 // Validate platform compatibility
                    var ballPosition = new Point(localImage.Width - ballImage.Width, localImage.Height - ballImage.Height);
#pragma warning restore CA1416 // Validate platform compatibility
#pragma warning disable CA1416 // Validate platform compatibility
                    graphics.DrawImage(ballImage, ballPosition);
#pragma warning restore CA1416 // Validate platform compatibility
                }
#pragma warning restore CA1416 // Validate platform compatibility
                embedImageUrl = SaveImageLocally(localImage);
            }
        }
        else
        {
            (System.Drawing.Image finalCombinedImage, bool ballImageLoaded) = await OverlayBallOnSpecies(speciesImageUrl, ballImgUrl);
            embedImageUrl = SaveImageLocally(finalCombinedImage);

            if (!ballImageLoaded)
            {
                Console.WriteLine($"Ball image could not be loaded: {ballImgUrl}");

                // await context.Channel.SendMessageAsync($"Ball image could not be loaded: {ballImgUrl}"); // for debugging purposes
            }
        }

        (int R, int G, int B) = await GetDominantColorAsync(embedImageUrl);
        return (embedImageUrl, new DiscordColor(R, G, B));
    }

    private static async Task<(System.Drawing.Image, bool)> OverlayBallOnSpecies(string speciesImageUrl, string ballImageUrl)
    {
        using (var speciesImage = await LoadImageFromUrl(speciesImageUrl))
        {
            if (speciesImage == null)
            {
                Console.WriteLine("Species image could not be loaded.");
#pragma warning disable CS8619 // Nullability of reference types in value doesn't match target type.
                return (null, false);
#pragma warning restore CS8619 // Nullability of reference types in value doesn't match target type.
            }

            var ballImage = await LoadImageFromUrl(ballImageUrl);
            if (ballImage == null)
            {
                Console.WriteLine($"Ball image could not be loaded: {ballImageUrl}");
#pragma warning disable CA1416 // Validate platform compatibility
                return ((System.Drawing.Image)speciesImage.Clone(), false);
#pragma warning restore CA1416 // Validate platform compatibility
            }

            using (ballImage)
            {
#pragma warning disable CA1416 // Validate platform compatibility
                using (var graphics = Graphics.FromImage(speciesImage))
                {
#pragma warning disable CA1416 // Validate platform compatibility
                    var ballPosition = new Point(speciesImage.Width - ballImage.Width, speciesImage.Height - ballImage.Height);
#pragma warning restore CA1416 // Validate platform compatibility
#pragma warning disable CA1416 // Validate platform compatibility
                    graphics.DrawImage(ballImage, ballPosition);
#pragma warning restore CA1416 // Validate platform compatibility
                }
#pragma warning restore CA1416 // Validate platform compatibility

#pragma warning disable CA1416 // Validate platform compatibility
                return ((System.Drawing.Image)speciesImage.Clone(), true);
#pragma warning restore CA1416 // Validate platform compatibility
            }
        }
    }

    private static async Task<System.Drawing.Image> OverlaySpeciesOnEgg(string eggImageUrl, string speciesImageUrl)
    {
#pragma warning disable CS8600 // Converting null literal or possible null value to non-nullable type.
        System.Drawing.Image eggImage = await LoadImageFromUrl(eggImageUrl);
#pragma warning restore CS8600 // Converting null literal or possible null value to non-nullable type.
#pragma warning disable CS8600 // Converting null literal or possible null value to non-nullable type.
        System.Drawing.Image speciesImage = await LoadImageFromUrl(speciesImageUrl);
#pragma warning restore CS8600 // Converting null literal or possible null value to non-nullable type.
#pragma warning disable CS8602 // Dereference of a possibly null reference.
#pragma warning disable CA1416 // Validate platform compatibility
        double scaleRatio = Math.Min((double)eggImage.Width / speciesImage.Width, (double)eggImage.Height / speciesImage.Height);
#pragma warning restore CA1416 // Validate platform compatibility
#pragma warning restore CS8602 // Dereference of a possibly null reference.
#pragma warning disable CA1416 // Validate platform compatibility
        Size newSize = new Size((int)(speciesImage.Width * scaleRatio), (int)(speciesImage.Height * scaleRatio));
#pragma warning restore CA1416 // Validate platform compatibility
#pragma warning disable CA1416 // Validate platform compatibility
        System.Drawing.Image resizedSpeciesImage = new Bitmap(speciesImage, newSize);
#pragma warning restore CA1416 // Validate platform compatibility
#pragma warning disable CA1416 // Validate platform compatibility
        using (Graphics g = Graphics.FromImage(eggImage))
        {
            // Calculate the position to center the species image on the egg image
#pragma warning disable CA1416 // Validate platform compatibility
            int speciesX = (eggImage.Width - resizedSpeciesImage.Width) / 2;
#pragma warning restore CA1416 // Validate platform compatibility
#pragma warning disable CA1416 // Validate platform compatibility
            int speciesY = (eggImage.Height - resizedSpeciesImage.Height) / 2;
#pragma warning restore CA1416 // Validate platform compatibility

            // Draw the resized and centered species image over the egg image
#pragma warning disable CA1416 // Validate platform compatibility
            g.DrawImage(resizedSpeciesImage, speciesX, speciesY, resizedSpeciesImage.Width, resizedSpeciesImage.Height);
#pragma warning restore CA1416 // Validate platform compatibility
        }
#pragma warning restore CA1416 // Validate platform compatibility

        // Dispose of the species image and the resized species image if they're no longer needed
#pragma warning disable CA1416 // Validate platform compatibility
        speciesImage.Dispose();
#pragma warning restore CA1416 // Validate platform compatibility
#pragma warning disable CA1416 // Validate platform compatibility
        resizedSpeciesImage.Dispose();
#pragma warning restore CA1416 // Validate platform compatibility

        // Calculate scale factor for resizing while maintaining aspect ratio
#pragma warning disable CA1416 // Validate platform compatibility
        double scale = Math.Min(128.0 / eggImage.Width, 128.0 / eggImage.Height);
#pragma warning restore CA1416 // Validate platform compatibility

        // Calculate new dimensions
#pragma warning disable CA1416 // Validate platform compatibility
        int newWidth = (int)(eggImage.Width * scale);
#pragma warning restore CA1416 // Validate platform compatibility
#pragma warning disable CA1416 // Validate platform compatibility
        int newHeight = (int)(eggImage.Height * scale);
#pragma warning restore CA1416 // Validate platform compatibility

#pragma warning disable CA1416 // Validate platform compatibility
        Bitmap finalImage = new Bitmap(128, 128);
#pragma warning restore CA1416 // Validate platform compatibility

        // Draw the resized egg image onto the new bitmap, centered
#pragma warning disable CA1416 // Validate platform compatibility
        using (Graphics g = Graphics.FromImage(finalImage))
        {
            // Calculate centering position
            int x = (128 - newWidth) / 2;
            int y = (128 - newHeight) / 2;

            // Draw the image
#pragma warning disable CA1416 // Validate platform compatibility
            g.DrawImage(eggImage, x, y, newWidth, newHeight);
#pragma warning restore CA1416 // Validate platform compatibility
        }
#pragma warning restore CA1416 // Validate platform compatibility
#pragma warning disable CA1416 // Validate platform compatibility
        eggImage.Dispose();
#pragma warning restore CA1416 // Validate platform compatibility
        return finalImage;
    }

    private static async Task<System.Drawing.Image?> LoadImageFromUrl(string url)
    {
        using HttpClient client = new();
        HttpResponseMessage response = await client.GetAsync(url);
        if (!response.IsSuccessStatusCode)
        {
            Console.WriteLine($"Failed to load image from {url}. Status code: {response.StatusCode}");
            return null;
        }

        Stream stream = await response.Content.ReadAsStreamAsync();
        if (stream == null || stream.Length == 0)
        {
            Console.WriteLine($"No data or empty stream received from {url}");
            return null;
        }

        try
        {
#pragma warning disable CA1416 // Validate platform compatibility
            return System.Drawing.Image.FromStream(stream);
#pragma warning restore CA1416 // Validate platform compatibility
        }
        catch (ArgumentException ex)
        {
            Console.WriteLine($"Failed to create image from stream. URL: {url}, Exception: {ex}");
            return null;
        }
    }

    private static async Task ScheduleFileDeletion(string filePath, int delayInMilliseconds, int batchTradeId = -1)
    {
        if (batchTradeId != -1)
        {
            // If this is part of a batch trade, add the file path to the dictionary
            if (!batchTradeFiles.TryGetValue(batchTradeId, out List<string>? value))
            {
                value = ([]);
                batchTradeFiles[batchTradeId] = value;
            }

            value.Add(filePath);
        }
        else
        {
            // If this is not part of a batch trade, delete the file after the delay
            await Task.Delay(delayInMilliseconds);
            DeleteFile(filePath);
        }
    }

    private static void DeleteFile(string filePath)
    {
        if (File.Exists(filePath))
        {
            try
            {
                File.Delete(filePath);
            }
            catch (IOException ex)
            {
                Console.WriteLine($"Error deleting file: {ex.Message}");
            }
        }
    }

    // Call this method after the last trade in a batch is completed
    private static void DeleteBatchTradeFiles(int batchTradeId)
    {
        if (batchTradeFiles.TryGetValue(batchTradeId, out var files))
        {
            foreach (var filePath in files)
            {
                DeleteFile(filePath);
            }
            batchTradeFiles.Remove(batchTradeId);
        }
    }

    

    public static async Task<(int R, int G, int B)> GetDominantColorAsync(string imagePath)
    {
        try
        {
            Bitmap image = await LoadImageAsync(imagePath);

            var colorCount = new Dictionary<Color, int>();
#pragma warning disable CA1416 // Validate platform compatibility
            for (int y = 0; y < image.Height; y++)
            {
#pragma warning disable CA1416 // Validate platform compatibility
                for (int x = 0; x < image.Width; x++)
                {
#pragma warning disable CA1416 // Validate platform compatibility
                    var pixelColor = image.GetPixel(x, y);
#pragma warning restore CA1416 // Validate platform compatibility

                    if (pixelColor.A < 128 || pixelColor.GetBrightness() > 0.9) continue;

                    var brightnessFactor = (int)(pixelColor.GetBrightness() * 100);
                    var saturationFactor = (int)(pixelColor.GetSaturation() * 100);
                    var combinedFactor = brightnessFactor + saturationFactor;

                    var quantizedColor = Color.FromArgb(
                        pixelColor.R / 10 * 10,
                        pixelColor.G / 10 * 10,
                        pixelColor.B / 10 * 10
                    );

                    if (colorCount.ContainsKey(quantizedColor))
                    {
                        colorCount[quantizedColor] += combinedFactor;
                    }
                    else
                    {
                        colorCount[quantizedColor] = combinedFactor;
                    }
                }
#pragma warning restore CA1416 // Validate platform compatibility
            }
#pragma warning restore CA1416 // Validate platform compatibility

#pragma warning disable CA1416 // Validate platform compatibility
            image.Dispose();
#pragma warning restore CA1416 // Validate platform compatibility

            if (colorCount.Count == 0)
                return (255, 255, 255);

            var dominantColor = colorCount.Aggregate((a, b) => a.Value > b.Value ? a : b).Key;
            return (dominantColor.R, dominantColor.G, dominantColor.B);
        }
        catch (Exception ex)
        {
            // Log or handle exceptions as needed
            Console.WriteLine($"Error processing image from {imagePath}. Error: {ex.Message}");
            return (255, 255, 255);  // Default to white if an exception occurs
        }
    }

    private static async Task<Bitmap> LoadImageAsync(string imagePath)
    {
        if (imagePath.StartsWith("http", StringComparison.OrdinalIgnoreCase))
        {
            using var httpClient = new HttpClient();
            using var response = await httpClient.GetAsync(imagePath);
            await using var stream = await response.Content.ReadAsStreamAsync();
#pragma warning disable CA1416 // Validate platform compatibility
            return new Bitmap(stream);
#pragma warning restore CA1416 // Validate platform compatibility
        }
        else
        {
#pragma warning disable CA1416 // Validate platform compatibility
            return new Bitmap(imagePath);
#pragma warning restore CA1416 // Validate platform compatibility
        }
    }

    private static async Task HandleDiscordExceptionAsync(SocketCommandContext context, SocketUser trader, HttpException ex)
    {
        string message = string.Empty;
        switch (ex.DiscordCode)
        {
            case DiscordErrorCode.InsufficientPermissions or DiscordErrorCode.MissingPermissions:
                {
                    // Check if the exception was raised due to missing "Send Messages" or "Manage Messages" permissions. Nag the bot owner if so.
                    var permissions = context.Guild.CurrentUser.GetPermissions(context.Channel as IGuildChannel);
                    if (!permissions.SendMessages)
                    {
                        // Nag the owner in logs.
                        message = "You must grant me \"Send Messages\" permissions!";
                        Base.LogUtil.LogError(message, "QueueHelper");
                        return;
                    }
                    if (!permissions.ManageMessages)
                    {
                        var app = await context.Client.GetApplicationInfoAsync().ConfigureAwait(false);
                        var owner = app.Owner.Id;
                        message = $"<@{owner}> You must grant me \"Manage Messages\" permissions!";
                    }
                }
                break;

            case DiscordErrorCode.CannotSendMessageToUser:
                {
                    // The user either has DMs turned off, or Discord thinks they do.
                    message = context.User == trader ? "You must enable private messages in order to be queued!" : "The mentioned user must enable private messages in order for them to be queued!";
                }
                break;

            default:
                {
                    // Send a generic error message.
                    message = ex.DiscordCode != null ? $"Discord error {(int)ex.DiscordCode}: {ex.Reason}" : $"Http error {(int)ex.HttpCode}: {ex.Message}";
                }
                break;
        }
        await context.Channel.SendMessageAsync(message).ConfigureAwait(false);
    }

    public static (string, Embed) CreateLGLinkCodeSpriteEmbed(List<Pictocodes> lgcode)
    {
        int codecount = 0;
        List<System.Drawing.Image> spritearray = [];
        foreach (Pictocodes cd in lgcode)
        {
            var showdown = new ShowdownSet(cd.ToString());
            var sav = SaveUtil.GetBlankSAV(EntityContext.Gen7b, "pip");
            PKM pk = sav.GetLegalFromSet(showdown).Created;
#pragma warning disable CA1416 // Validate platform compatibility
            System.Drawing.Image png = pk.Sprite();
#pragma warning restore CA1416 // Validate platform compatibility
            var destRect = new Rectangle(-40, -65, 137, 130);
#pragma warning disable CA1416 // Validate platform compatibility
            var destImage = new Bitmap(137, 130);
#pragma warning restore CA1416 // Validate platform compatibility

#pragma warning disable CA1416 // Validate platform compatibility
            destImage.SetResolution(png.HorizontalResolution, png.VerticalResolution);
#pragma warning restore CA1416 // Validate platform compatibility

#pragma warning disable CA1416 // Validate platform compatibility
            using (var graphics = Graphics.FromImage(destImage))
            {
#pragma warning disable CA1416 // Validate platform compatibility
                graphics.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceCopy;
#pragma warning restore CA1416 // Validate platform compatibility
#pragma warning disable CA1416 // Validate platform compatibility
                graphics.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;
#pragma warning restore CA1416 // Validate platform compatibility
#pragma warning disable CA1416 // Validate platform compatibility
                graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
#pragma warning restore CA1416 // Validate platform compatibility
#pragma warning disable CA1416 // Validate platform compatibility
                graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
#pragma warning restore CA1416 // Validate platform compatibility
#pragma warning disable CA1416 // Validate platform compatibility
                graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
#pragma warning restore CA1416 // Validate platform compatibility
#pragma warning disable CA1416 // Validate platform compatibility
                graphics.DrawImage(png, destRect, 0, 0, png.Width, png.Height, GraphicsUnit.Pixel);
#pragma warning restore CA1416 // Validate platform compatibility
            }
#pragma warning restore CA1416 // Validate platform compatibility
            png = destImage;
#pragma warning disable CA1416 // Validate platform compatibility
            spritearray.Add(png);
#pragma warning restore CA1416 // Validate platform compatibility
            codecount++;
        }
#pragma warning disable CA1416 // Validate platform compatibility
        int outputImageWidth = spritearray[0].Width + 20;
#pragma warning restore CA1416 // Validate platform compatibility

#pragma warning disable CA1416 // Validate platform compatibility
        int outputImageHeight = spritearray[0].Height - 65;
#pragma warning restore CA1416 // Validate platform compatibility

#pragma warning disable CA1416 // Validate platform compatibility
        Bitmap outputImage = new Bitmap(outputImageWidth, outputImageHeight, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
#pragma warning restore CA1416 // Validate platform compatibility

#pragma warning disable CA1416 // Validate platform compatibility
        using (Graphics graphics = Graphics.FromImage(outputImage))
        {
#pragma warning disable CA1416 // Validate platform compatibility
            graphics.DrawImage(spritearray[0], new Rectangle(0, 0, spritearray[0].Width, spritearray[0].Height),
                new Rectangle(new Point(), spritearray[0].Size), GraphicsUnit.Pixel);
#pragma warning restore CA1416 // Validate platform compatibility
#pragma warning disable CA1416 // Validate platform compatibility
            graphics.DrawImage(spritearray[1], new Rectangle(50, 0, spritearray[1].Width, spritearray[1].Height),
                new Rectangle(new Point(), spritearray[1].Size), GraphicsUnit.Pixel);
#pragma warning restore CA1416 // Validate platform compatibility
#pragma warning disable CA1416 // Validate platform compatibility
            graphics.DrawImage(spritearray[2], new Rectangle(100, 0, spritearray[2].Width, spritearray[2].Height),
                new Rectangle(new Point(), spritearray[2].Size), GraphicsUnit.Pixel);
#pragma warning restore CA1416 // Validate platform compatibility
        }
#pragma warning restore CA1416 // Validate platform compatibility
        System.Drawing.Image finalembedpic = outputImage;
        var filename = $"{Directory.GetCurrentDirectory()}//finalcode.png";
#pragma warning disable CA1416 // Validate platform compatibility
        finalembedpic.Save(filename);
#pragma warning restore CA1416 // Validate platform compatibility
        filename = Path.GetFileName($"{Directory.GetCurrentDirectory()}//finalcode.png");
        Embed returnembed = new EmbedBuilder().WithTitle($"{lgcode[0]}, {lgcode[1]}, {lgcode[2]}").WithImageUrl($"attachment://{filename}").Build();
        return (filename, returnembed);
    }
}
