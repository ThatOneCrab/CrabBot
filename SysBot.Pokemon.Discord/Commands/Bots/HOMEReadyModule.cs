using Discord;
using Discord.Commands;
using Discord.WebSocket;
using PKHeX.Core;
using SysBot.Pokemon.Discord;
using SysBot.Pokemon.Discord.Helpers;
using SysBot.Pokemon.Helpers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;


namespace SysBot.Pokemon.Discord.Modules
{
    public class HOMEReadyModule<T> : ModuleBase<SocketCommandContext> where T : PKM, new()
    {
        private static T? GetRequest(Download<PKM> dl)
        {
            if (!dl.Success)
                return null;
            return dl.Data switch
            {
                null => null,
                T pk => pk,
                _ => EntityConverter.ConvertToType(dl.Data, typeof(T), out _) as T,
            };
        }

        private string HOMEFolder => SysCord<T>.Runner.Config.Folder.HOMEReadyPKMFolder;

        private string Prefix => SysCord<T>.Runner.Config.Discord.CommandPrefix;

        // ============================================================================
        //  INSTRUCTIONS
        // ============================================================================
        [Command("homeready")]
        [Alias("hr")]
        [Summary("Displays instructions on how to use the HOME-Ready module.")]
        private async Task HomeReadyInstructionsAsync()
        {
            if (string.IsNullOrWhiteSpace(HOMEFolder))
            {
                await ReplyAsync("This bot does not have the HOME-Ready module configured.").ConfigureAwait(false);
                return;
            }

            // Using your modern embed style
            async Task<IUserMessage> SendBreak(string title, string description)
            {
                var embed = new EmbedBuilder()
                    .WithTitle(title)
                    .WithDescription(description)
                    .WithColor(Color.Blue)
                    .WithImageUrl("https://raw.githubusercontent.com/Havokx89/Bot-Sprite-Images/refs/heads/main/homereadybreak.png");

                return await ReplyAsync(embed: embed.Build());
            }

            var m0 = await SendBreak(
                "------- HOME-READY MODULE INSTRUCTIONS -------",
                "Everything you need to know for the HOME-Ready commands."
            );

            var mention = Context.Client.CurrentUser.Mention;


            var m1 = await SendBreak(
                $"GET LIST — `{mention} hrl <Pokemon>`",
                $"- Searches the entire HOME-Ready module.\n**Example:** `{mention} hrl Mewtwo`"
            );

            var m2 = await SendBreak(
                $"CHANGE PAGES — `{mention} hrl <page>`",
                $"- Switch between pages, with or without filters.\n**Example:** `{mention} hrl 5 Charmander`"
            );

            var m3 = await SendBreak(
                $"TRADE A FILE — `{mention} hrr <number>`",
                $"- Trades the Pokémon by its number in the list.\n**Example:** `{mention} hrr 682`"
            );

            _ = Task.Run(async () =>
            {
                await Task.Delay(60_000);
                try
                {
                    await m0.DeleteAsync();
                    await m1.DeleteAsync();
                    await m2.DeleteAsync();
                    await m3.DeleteAsync();
                }
                catch { }
            });
        }

        // ============================================================================
        //  REQUEST
        // ============================================================================
        [Command("homereadyrequest")]
        [Alias("hrr")]
        [Summary("Downloads a HOME-ready PKM and queues it for trade.")]
        [RequireQueueRole(nameof(DiscordManager.RolesTrade))]
        private async Task HOMEReadyRequestAsync(int index)
        {
            if (string.IsNullOrWhiteSpace(HOMEFolder))
            {
                await ReplyAsync("This bot does not have the HOME-Ready module configured.").ConfigureAwait(false);
                return;
            }

            var userID = Context.User.Id;
            if (SysCord<T>.Runner.Hub.Queues.Info.IsUserInQueue(userID))
            {
                await ReplyAsync("You're already in a trade queue. Finish that one first.").ConfigureAwait(false);
                return;
            }

            try
            {
                var files = Directory.GetFiles(HOMEFolder)
                    .OrderBy(f => f)
                    .ToList();

                if (files.Count == 0)
                {
                    await ReplyAsync("No HOME-ready PKM files found.").ConfigureAwait(false);
                    return;
                }

                if (index < 1 || index > files.Count)
                {
                    await ReplyAsync("Invalid entry number. Choose a valid file.").ConfigureAwait(false);
                    return;
                }

                var filePath = files[index - 1];
                var data = await File.ReadAllBytesAsync(filePath);
                var entity = EntityFormat.GetFromBytes(data);

                if (entity == null)
                {
                    await ReplyAsync("Could not convert the file to a valid PKM.").ConfigureAwait(false);
                    return;
                }

                var download = new Download<PKM>
                {
                    Data = entity,
                    Success = true
                };

                var pk = GetRequest(download);
                if (pk == null && entity is PKM rawPkm)
                {
                    pk = EntityConverter.ConvertToType(rawPkm, typeof(T), out _) as T;
                }

                if (pk == null)
                {
                    await ReplyAsync("Failed to convert the HOME-ready file to your trade format.").ConfigureAwait(false);
                    return;
                }

                var code = SysCord<T>.Runner.Hub.Queues.Info.GetRandomTradeCode(userID);
                var lgcode = SysCord<T>.Runner.Hub.Queues.Info.GetRandomLGTradeCode();
                var sig = Context.User.GetFavor();

                await ReplyAsync($"**HOME-Ready Pokémon added to the queue.**").ConfigureAwait(false);

                await Helpers<T>.AddTradeToQueueAsync(
                    context: Context,
                    code: code,
                    trainerName: Context.User.Username,
                    pk: pk,
                    sig: sig,
                    usr: Context.User,
                    isBatchTrade: false,
                    batchTradeNumber: 1,
                    totalBatchTrades: 1,
                    isHiddenTrade: false,
                    isMysteryEgg: false,
                    lgcode: lgcode ?? SysCord<T>.Runner.Hub.Queues.Info.GetRandomLGTradeCode(),
                    tradeType: PokeTradeType.Specific,
                    ignoreAutoOT: false,
                    setEdited: false,
                    isNonNative: false
                );
            }
            catch (Exception ex)
            {
                await ReplyAsync($"**Error:** {ex.Message}").ConfigureAwait(false);
            }
            finally
            {
                try
                {
                    if (Context.Message is IUserMessage msg)
                        await msg.DeleteAsync().ConfigureAwait(false);
                }
                catch { }
            }
        }


        // ============================================================================
        //  LIST
        // ============================================================================
        [Command("homereadylist")]
        [Alias("hrl")]
        [Summary("Lists available HOME-Ready files with filtering + pagination.")]
        private async Task HOMEListAsync([Remainder] string args = "")
        {
            if (string.IsNullOrWhiteSpace(HOMEFolder))
            {
                await ReplyAsync("This bot does not have the HOME-Ready module configured.");
                return;
            }

            const int itemsPerPage = 10;

            var files = Directory.GetFiles(HOMEFolder)
                .Select(Path.GetFileName)
                .Where(f => f != null)
                .Select(f => f!)
                .OrderBy(x => x)
                .ToList();

            if (files.Count == 0)
            {
                await ReplyAsync("No HOME-ready files found.");
                return;
            }

            // Parse filter + page
            var parts = args.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            string filter = "";
            int page = 1;

            if (parts.Length > 0)
            {
                if (int.TryParse(parts.Last(), out int parsedPage))
                {
                    page = parsedPage;
                    filter = string.Join(" ", parts.Take(parts.Length - 1));
                }
                else
                {
                    filter = string.Join(" ", parts);
                }
            }

            var filtered = files
                .Where(f => string.IsNullOrWhiteSpace(filter) ||
                            f.Contains(filter, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (filtered.Count == 0)
            {
                await ReplyAsync($"No HOME-ready files found for '{filter}'.");
                return;
            }

            var pageCount = (int)Math.Ceiling(filtered.Count / (double)itemsPerPage);
            page = Math.Clamp(page, 1, pageCount);

            var pageItems = filtered
                .Skip((page - 1) * itemsPerPage)
                .Take(itemsPerPage)
                .ToList();

            var embed = new EmbedBuilder()
                .WithTitle($"HOME-Ready Files — '{filter}'")
                .WithDescription($"Page **{page}** of **{pageCount}**")
                .WithColor(Color.Blue);

            // Map file extensions to game names
            var extensionToGame = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                 { ".pb7", "LGPE" },
                 { ".pk8", "SWSH" },
                 { ".pb8", "BDSP" },
                 { ".pa8", "PLA" },
                 { ".pa9", "PLZA" },
                 { ".pk9", "SV" }
            };

            var mention = Context.Client.CurrentUser.Mention;

            foreach (var item in pageItems)
            {
                var index = files.IndexOf(item) + 1;

                // Get the file extension, trim whitespace, and uppercase it
                var ext = Path.GetExtension(item)?.Trim().ToUpperInvariant() ?? "";

                // Lookup in dictionary
                string game = extensionToGame.TryGetValue(ext, out var g) ? g : "Unknown";

                // Add embed field with mention-based examples for guilds
                embed.AddField(
                    $"{index}. {item}",
                    $"Use `{mention} hrr {index}` to request this Pokémon.\n" +
                    $"Use `{mention} hrv {index}` to view Pokémon details.\n" +
                    $"Use `{mention} hrd {index}` to download this PKM file.\n" +
                    $"This file is for **{game}**."
                );
            }

            var embedMsg = await ReplyAsync(embed: embed.Build());

            await Task.Delay(20_000);

            try
            {
                await embedMsg.DeleteAsync();
            }
            catch { }
        }

        // ============================================================================
        //  VIEW
        // ============================================================================
        [Command("homereadyview")]
        [Alias("hrv")]
        [Summary("Views a HOME-ready PKM in Showdown format before downloading.")]
        private async Task HOMEReadyViewAsync(int index)
        {
            if (string.IsNullOrWhiteSpace(HOMEFolder))
            {
                await ReplyAsync("This bot does not have the HOME-Ready module configured.").ConfigureAwait(false);
                return;
            }

            try
            {
                var files = Directory.GetFiles(HOMEFolder)
                    .OrderBy(f => f)
                    .ToList();

                if (files.Count == 0)
                {
                    await ReplyAsync("No HOME-ready PKM files found.").ConfigureAwait(false);
                    return;
                }

                if (index < 1 || index > files.Count)
                {
                    await ReplyAsync("Invalid entry number. Choose a valid file.");
                    return;
                }

                var filePath = files[index - 1];
                var raw = await File.ReadAllBytesAsync(filePath);

                var entity = EntityFormat.GetFromBytes(raw);
                if (entity == null)
                {
                    await ReplyAsync("Could not read the PKM file.").ConfigureAwait(false);
                    return;
                }

                // Convert to correct PKM type for this bot
                PKM? typed = entity as T
                    ?? EntityConverter.ConvertToType(entity, typeof(T), out _) as T;

                if (typed == null)
                {
                    await ReplyAsync("File loaded, but could not convert to your game generation.").ConfigureAwait(false);
                    return;
                }

                // Generate showdown text
                string showdown = ShowdownParsing.GetShowdownText(typed);

                // ============================
                // METADATA
                // ============================

                string otName = typed.OriginalTrainerName;
                string tid = typed.TrainerTID7.ToString();

                string versionName = GameInfo.GetVersionName(typed.Version);

                string metDate = typed.MetDate?.ToString("yyyy-MM-dd") ?? "Unknown";

                string metLocStr = GameInfo.Strings.GetLocationName(
                    isEggLocation: false,
                    location: typed.MetLocation,
                    format: typed.Format,
                    generation: typed.Generation,
                    version: typed.Version
                );

                string game = versionName;
                ulong homeTracker = GetHomeTrackerSafe(typed);
                string homeTrackerStr = homeTracker == 0
                    ? $"N/A - Just trade with a **{game}** bot."
                    : $"{homeTracker:X16}";

                string details =
                $@"**Additional Details**
                • OT: {otName}
                • TID: {tid}
                • Game: {versionName}
                • Met Location: {typed.MetLocation} ({metLocStr})
                • Met Date: {metDate}
                • Tracker: {homeTrackerStr}";


                string finalText =
                $"```text\n{showdown}\n```\n{details}";

                var embed = new EmbedBuilder()
                    .WithTitle($"Viewing HOME-Ready Entry #{index}")
                    .WithDescription(finalText)
                    .WithColor(Color.Magenta)
                    .WithFooter($"Use {Prefix}hrr {index} to request this Pokémon for trade.\n" +
                    $"Use {Prefix}hrd {index} to download this PKM file.");

                await ReplyAsync(embed: embed.Build());
            }
            catch (Exception ex)
            {
                await ReplyAsync($"**Error:** {ex.Message}");
            }
            finally
            {
                try { if (Context.Message is IUserMessage m) await m.DeleteAsync(); } catch { }
            }
        }

        private static ulong GetHomeTrackerSafe(PKM pkm)
        {
            var type = pkm.GetType();
            var prop = type.GetProperty("Tracker");

            if (prop == null)
                return 0; // This format doesn’t support HOME tracking

            object? value = prop.GetValue(pkm);
            if (value is ulong tracker)
                return tracker;

            return 0;
        }


        // ============================================================================
        //  DOWNLOAD FILE
        // ============================================================================
        [Command("homereadydownload")]
        [Alias("hrd")]
        [Summary("Downloads a HOME-ready PKM file by its number from the list.")]
        private async Task HOMEReadyDownloadAsync(int index)
        {
            if (string.IsNullOrWhiteSpace(HOMEFolder))
            {
                await ReplyAsync("This bot does not have the HOME-Ready module configured.").ConfigureAwait(false);
                return;
            }

            try
            {
                var files = Directory.GetFiles(HOMEFolder)
                    .OrderBy(f => f)
                    .ToList();

                if (files.Count == 0)
                {
                    await ReplyAsync("No HOME-ready PKM files found.").ConfigureAwait(false);
                    return;
                }

                if (index < 1 || index > files.Count)
                {
                    await ReplyAsync("Invalid entry number. Choose a valid file.");
                    return;
                }

                var filePath = files[index - 1];
                var fileName = Path.GetFileName(filePath);

                // Send file directly
                await using (var fs = File.OpenRead(filePath))
                {
                    var msg = await Context.Channel.SendFileAsync(
                        stream: fs,
                        filename: fileName,
                        text: $"Here's your HOME-Ready Pokémon file: **{fileName}**"
                    );
                }
            }
            catch (Exception ex)
            {
                await ReplyAsync($"**Error:** {ex.Message}");
            }
            finally
            {
                try { if (Context.Message is IUserMessage m) await m.DeleteAsync(); } catch { }
            }
        }

        // ============================================================================
        //  UPLOAD FILE
        // ============================================================================
        [Command("homereadyupload")]
        [Alias("hru")]
        [Summary("Uploads one or more HOME-ready PKM files (3 max thnks to shitcord) to the configured HOME-Ready folder. Role controlled via config.")]
        private async Task HOMEReadyUploadAsync()
        {
            if (string.IsNullOrWhiteSpace(HOMEFolder))
            {
                await ReplyAsync("This bot does not have the HOME-Ready module configured.").ConfigureAwait(false);
                return;
            }

            // Permission check using config role setting
            try
            {
                var mgr = SysCordSettings.Manager;
                // Global sudo users allowed
                if (!(mgr.Config.AllowGlobalSudo && mgr.CanUseSudo(Context.User.Id)))
                {
                    if (Context.User is not SocketGuildUser gUser)
                    {
                        await ReplyAsync("This command must be run in a guild channel.").ConfigureAwait(false);
                        return;
                    }

                    // Check role-based sudo/roles first (sudo roles remain name-based)
                    if (mgr.CanUseSudo(gUser.Roles.Select(r => r.Name)))
                    {
                        // allowed via sudo role
                    }
                    else
                    {
                        var cfgList = SysCord<T>.Runner.Config.Discord.RoleCanUploadHOME;

                        // If config allows anyone when empty, allow; otherwise check membership by role ID
                        if (!(cfgList.AllowIfEmpty && cfgList.List.Count == 0))
                        {
                            var hasRoleById = gUser.Roles.Any(r => cfgList.Contains(r.Id));
                            if (!hasRoleById)
                            {
                                await ReplyAsync("You do not have the required role (configured by role ID in Discord settings) to upload HOME-ready files.").ConfigureAwait(false);
                                return;
                            }
                        }
                    }
                }
            }
            catch
            {
                // If permission check fails unexpectedly, disallow to be safe.
                await ReplyAsync("Permission check failed. Contact an administrator.").ConfigureAwait(false);
                return;
            }

            var attachments = Context.Message?.Attachments;
            if (attachments == null || attachments.Count == 0)
            {
                await ReplyAsync("Please attach one or more HOME-ready PKM files to upload (3 max).").ConfigureAwait(false);
                return;
            }

            // Allowed extensions
            var allowedExt = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                ".pb7", ".pk8", ".pb8", ".pa8", ".pa9", ".pk9"
            };

            Directory.CreateDirectory(HOMEFolder);

            var successes = new List<string>();
            var failures = new List<string>();

            try
            {
                using var http = new System.Net.Http.HttpClient();

                foreach (var att in attachments)
                {
                    var filename = Path.GetFileName(att.Filename) ?? att.Filename;
                    var ext = Path.GetExtension(filename) ?? "";

                    if (!allowedExt.Contains(ext))
                    {
                        failures.Add($"{filename} (invalid extension)");
                        continue;
                    }

                    try
                    {
                        var data = await http.GetByteArrayAsync(att.Url).ConfigureAwait(false);

                        // Validate file is a PKM
                        var entity = EntityFormat.GetFromBytes(data);
                        if (entity == null)
                        {
                            failures.Add($"{filename} (not a valid PKM)");
                            continue;
                        }

                        // Sanitize filename
                        var safeName = string.Concat(filename.Split(Path.GetInvalidFileNameChars()));
                        if (string.IsNullOrWhiteSpace(safeName))
                            safeName = $"homeready_{Guid.NewGuid():N}{ext}";

                        var dest = Path.Combine(HOMEFolder, safeName);
                        if (File.Exists(dest))
                        {
                            var baseName = Path.GetFileNameWithoutExtension(safeName);
                            var newName = $"{baseName}_{Guid.NewGuid():N}{ext}";
                            dest = Path.Combine(HOMEFolder, newName);
                            safeName = newName;
                        }

                        await File.WriteAllBytesAsync(dest, data).ConfigureAwait(false);
                        successes.Add(safeName);
                    }
                    catch (Exception ex)
                    {
                        failures.Add($"{filename} ({ex.Message})");
                    }
                }

                var parts = new List<string>();
                if (successes.Count > 0)
                    parts.Add($"Uploaded {successes.Count} file(s): {string.Join(", ", successes)}");
                if (failures.Count > 0)
                    parts.Add($"Failed to upload {failures.Count} file(s): {string.Join(", ", failures)}");

                await ReplyAsync(parts.Count > 0 ? string.Join("\n", parts) : "No files were uploaded.").ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                await ReplyAsync($"**Error while uploading files:** {ex.Message}").ConfigureAwait(false);
            }
            finally
            {
                try
                {
                    if (Context.Message is IUserMessage msg)
                        await msg.DeleteAsync().ConfigureAwait(false);
                }
                catch { }
            }
        }
    }
}

