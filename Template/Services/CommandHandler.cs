﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Discord;
using Discord.Addons.Hosting;
using Discord.Commands;
using Discord.WebSocket;
using Infrastructure;
using Microsoft.Extensions.Configuration;
using Template.Common;
using Template.Utilities;

namespace Template.Services
{
    public class CommandHandler : InitializedService
    {
        private readonly IServiceProvider _provider;
        private readonly DiscordSocketClient _client;
        private readonly CommandService _service;
        private readonly IConfiguration _config;
        private readonly Servers _servers;
        private readonly ServerHelper _serverHelper;
        private readonly Images _images;
        public static List<Mute> Mutes = new List<Mute>();

        public CommandHandler(IServiceProvider provider, DiscordSocketClient client, CommandService service, IConfiguration config, Servers servers, ServerHelper serverHelper, Images images)
        {
            _provider = provider;
            _client = client;
            _service = service;
            _config = config;
            _servers = servers;
            _serverHelper = serverHelper;
            _images = images;
        }

        public override async Task InitializeAsync(CancellationToken cancellationToken)
        {
            _client.MessageReceived += OnMessageReceived;

            var newTask = new Task(async () => await MuteHandler());
            newTask.Start();

            _service.CommandExecuted += OnCommandExecuted;
            await _service.AddModulesAsync(Assembly.GetEntryAssembly(), _provider);
        }

        private async Task MuteHandler()
        {
            List<Mute> Remove = new List<Mute>();

            foreach(var mute in Mutes)
            {
                if (DateTime.Now < mute.End)
                    continue;

                var guild = _client.GetGuild(mute.Guild.Id);

                if(guild.GetRole(mute.Role.Id) == null)
                {
                    Remove.Add(mute);
                    continue;
                }

                var role = guild.GetRole(mute.Role.Id);

                if(guild.GetUser(mute.User.Id) == null)
                {
                    Remove.Add(mute);
                    continue;
                }

                var user = guild.GetUser(mute.User.Id);

                if(role.Position > guild.CurrentUser.Hierarchy)
                {
                    Remove.Add(mute);
                    continue;
                }

                await user.RemoveRoleAsync(mute.Role);
                Remove.Add(mute);
            }

            Mutes = Mutes.Except(Remove).ToList();

            await Task.Delay(1 * 60 * 1000);
            await MuteHandler();
        }
        private async Task OnMessageReceived(SocketMessage arg)
        {
            if (!(arg is SocketUserMessage message)) return;
            if (message.Source != MessageSource.User) return;

            var argPos = 0;
            var prefix = await _servers.GetGuildPrefix((message.Channel as SocketGuildChannel).Guild.Id) ?? "!";
            if (!message.HasStringPrefix(prefix, ref argPos) && !message.HasMentionPrefix(_client.CurrentUser, ref argPos)) return;

            var context = new SocketCommandContext(_client, message);
            await _service.ExecuteAsync(context, argPos, _provider);
        }

        private async Task OnCommandExecuted(Optional<CommandInfo> command, ICommandContext context, IResult result)
        {
            if (command.IsSpecified && !result.IsSuccess) await (context.Channel as ISocketMessageChannel).SendErrorAsync("Error", result.ErrorReason);
        }
    }
}