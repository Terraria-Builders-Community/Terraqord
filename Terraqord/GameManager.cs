using Discord.Webhook;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Terraqord.Configuration;
using Terraqord.Entities;
using TerrariaApi.Server;
using TShockAPI.Hooks;
using TShockAPI;
using Terraqord.Extensions;
using Terraria;
using Auxiliary.Configuration;
using Auxiliary;

namespace Terraqord
{
    public class GameManager
    {
        private readonly DateTime?[] _joinedAt;

        private readonly DiscordSocketClient _client;

        private readonly DiscordWebhookClient _logHook;
        private readonly DiscordWebhookClient _messageHook;
        private readonly DiscordWebhookClient _staffHook;

        public GameManager(DiscordSocketClient client)
        {
            _joinedAt = new DateTime?[256];

            for (int i = 0; i < _joinedAt.Length; i++)
                _joinedAt[i] = null;

            _client = client;

            _messageHook = new(Configuration<TerraqordSettings>.Settings.ServerHook);
            _staffHook = new(Configuration<TerraqordSettings>.Settings.StaffHook);
            _logHook = new(Configuration<TerraqordSettings>.Settings.LoggingHook);

            Terraqord.ChatSent += ChatSent;
            Terraqord.CommandSent += CommandSent;

            Terraqord.Join += Join;
            Terraqord.Leave += Leave;

            Terraqord.ServerStarted += ServerStarted;
        }

        private async Task ServerStarted()
        {
            var eb = new EmbedBuilder()
                .WithTitle("Server started!")
                .AddField("Map:", Main.worldName)
                .AddField("Max players:", TShock.Config.Settings.MaxSlots.ToString())
                .AddField("Difficulty:", $"{Enum.GetName(typeof(GameMode), GameMode.All)}")
                .WithFooter($"Join on: {Configuration<TerraqordSettings>.Settings.JoinIp}")
                .WithColor(Color.Blue);

            await _messageHook.SendMessageAsync(
                embeds: new[] { eb.Build() });
        }

        public async Task StartAsync()
        {
            var eb = new EmbedBuilder()
                .WithTitle("Server starting!")
                .WithColor(Color.Blue);

            await _messageHook.SendMessageAsync(
                embeds: new[] { eb.Build() });
        }

        private async Task Leave(LeaveEventArgs arg)
        {
            var player = TShock.Players[arg.Who];

            if (player != null && player.Active && player.RealPlayer)
            {
                var span = DateTime.UtcNow - _joinedAt[arg.Who]!.Value;

                var eb = new EmbedBuilder()
                    .WithTitle($"{player.Name} has left!")
                    .AddField("Playtime:", span.ToReadable())
                    .AddField("Playercount:", $"{TShock.Utils.GetActivePlayerCount() - 1}/{TShock.Config.Settings.MaxSlots}")
                    .WithColor(Color.Red);

                await _messageHook.SendMessageAsync(
                    embeds: new[] { eb.Build() });

                var lb = new EmbedBuilder()
                    .WithTitle($"{player.Name} has left!")
                    .AddField("IP:", $"_{player.IP}_")
                    .WithColor(Color.Red);

                await _logHook.SendMessageAsync(
                    embeds: new[] { lb.Build() });
            }
        }

        private async Task Join(GreetPlayerEventArgs arg)
        {
            _joinedAt[arg.Who] = DateTime.UtcNow;

            var player = TShock.Players[arg.Who];

            if (player != null && player.Active && player.RealPlayer)
            {
                var eb = new EmbedBuilder()
                    .WithTitle($"{player.Name} has joined!")
                    .AddField("Playercount:", $"{TShock.Utils.GetActivePlayerCount()}/{TShock.Config.Settings.MaxSlots}")
                    .WithColor(Color.Green);

                await _messageHook.SendMessageAsync(
                    username: "Server",
                    embeds: new[] { eb.Build() });

                var lb = new EmbedBuilder()
                    .WithTitle($"{player.Name} has joined!")
                    .AddField("IP:", $"_{player.IP}_")
                    .WithColor(Color.Green);

                await _logHook.SendMessageAsync(
                    username: "Server",
                    embeds: new[] { lb.Build() });
            }
        }

        private async Task CommandSent(PlayerCommandEventArgs arg)
        {
            var player = arg.Player;

            if (arg.CommandName is "login" or "password" or "user" or "register")
                return;

            if (arg.CommandName is "staffchat" or "sc" && !arg.Handled)
            {
                var stringify = arg.CommandText[arg.CommandName.Length..].StripTags().Trim();

                if (!string.IsNullOrEmpty(stringify))
                {

                    string? avatarUrl = null;
                    if (player.Account != null)
                    {
                        var user = await IModel.GetAsync(GetRequest.Bson<TerraqordUser>(x => x.TShockId == player.Account.ID));

                        avatarUrl = user?.AuthorUrl ?? null;
                    }

                    await _staffHook.SendMessageAsync(
                        text: stringify,
                        username: $"{player.Group.Prefix}{player.Name}".StripTags(),
                        avatarUrl: avatarUrl);
                }
            }

            var lb = new EmbedBuilder()
                .WithTitle($"{player.Name} has executed a command!")
                .AddField("Command:", $"{TShock.Config.Settings.CommandSpecifier}{arg.CommandText}")
                .WithColor(Color.Blue);

            await _logHook.SendMessageAsync(
                username: "Server",
                embeds: new[] { lb.Build() });
        }

        private async Task ChatSent(PlayerChatEventArgs arg)
        {
            var player = arg.Player;

            if (!arg.Handled)
            {
                var stringify = arg.RawText.StripTags().Trim();

                if (string.IsNullOrEmpty(stringify))
                    return;

                string? avatarUrl = null;
                if (player.Account != null)
                {
                    var user = await IModel.GetAsync(GetRequest.Bson<TerraqordUser>(x => x.TShockId == player.Account.ID));

                    avatarUrl = user?.AuthorUrl ?? null;
                }

                await _messageHook.SendMessageAsync(
                    text: stringify, 
                    username: $"{player.Group.Prefix}{player.Name}".StripTags(),
                    avatarUrl: avatarUrl);
            }
        }
    }
}
