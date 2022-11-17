﻿namespace WingTechBot;
using System;
using System.IO;
using System.Linq;
using Discord;
using Discord.WebSocket;
using WingTechBot.Handlers;

internal class DeleteCommand : Command
{
	public override void Execute()
	{
		try
		{
			message.Channel.SendMessageAsync($"Deleting message from {replied.Author.Mention}.");

			using (var file = File.AppendText(Program.DELETE_PATH))
			{
				file.WriteLine($"Message from: {replied.Author}");
				file.WriteLine($"Deleted by: {message.Author}");
				file.WriteLine($"Deleted on: {DateTime.Now}");
				file.WriteLine($"Content: {replied.Content}");

				if (replied.Attachments.Count > 0)
				{
					file.WriteLine($"Attachments:");
					foreach (var attachment in replied.Attachments)
					{
						file.WriteLine($" - {attachment.Url}");
					}
				}

				if (replied.Embeds.Count > 0)
				{
					file.WriteLine($"Embeds:");
					foreach (var embed in replied.Embeds.Cast<Embed>())
					{
						file.WriteLine($" - {embed.Url}");
					}
				}

				file.WriteLine("");
			}

			message.Channel.DeleteMessageAsync(replied.Id);
		}
		catch
		{
			throw new($"Failed to delete message.");
		}
	}

	public override string LogString => $"deleted a message from {replied.Author.Username} in {replied.Channel.Name}";
	public override bool Audit => true;
	public override ulong[] RequiredRoles => new[] { Program.Config.ModRoleID ?? 0 };
	public override string[] Aliases => new[] { "delete", "d", "remove", "x", "erase" };
	public override bool GetReply => true;
}

internal class PinCommand : Command
{
	private string _pin;

	public override void Execute()
	{
		try
		{
			replied = message.Channel.GetMessageAsync(message.Reference.MessageId.Value).Result;

			if (replied.IsPinned)
			{
				((SocketUserMessage)replied).UnpinAsync();
			}
			else
			{
				((SocketUserMessage)replied).PinAsync();
			}

			_pin = 
				replied.IsPinned 
				? "Pin" 
				: "Unpin";

			message.Channel.SendMessageAsync($"{_pin}ning message from {replied.Author.Mention}.");
		}
		catch
		{
			throw new($"Failed to {_pin} message.");
		}
	}

	public override string LogString => $"{_pin}ned a message in {replied.Channel.Name}";
	public override bool Audit => true;
	public override ulong[] RequiredRoles => new[] { Program.Config.ModRoleID ?? 0 };
	public override string[] Aliases => new[] { "pin", "unpin", "p", "up" };
	public override bool GetReply => true;
}

internal class ClearCommand : Command
{
	public override void Execute()
	{
		try
		{
			replied = message.Channel.GetMessageAsync(message.Reference.MessageId.Value).Result;
			message.Channel.SendMessageAsync($"Clearing message reactions on message from {replied.Author.Mention}.");

			foreach (var v in replied.Reactions)
			{
				if (KarmaHandler.trackableEmotes.Contains(v.Key.Name))
				{
					var index = Array.IndexOf(KarmaHandler.trackableEmotes, v.Key.Name);
					Program.KarmaHandler.KarmaDictionary[replied.Author.Id][index] -= v.Value.ReactionCount;
					Console.WriteLine($"{DateTime.Now}: revoked {v.Value.ReactionCount} {v.Key.Name}(s) from {replied.Author.Mention}.");
				}
			}

			replied.RemoveAllReactionsAsync();
		}
		catch
		{
			throw new($"Failed to clear message reactions.");
		}
	}

	public override string LogString => $"cleared reactions on a message from {replied.Author.Username}";
	public override bool Audit => true;
	public override ulong[] RequiredRoles => new[] { Program.Config.ModRoleID ?? 0 };
	public override bool GetReply => true;
}

internal class ToggleBotCommand : Command
{
	public override void Execute()
	{
		Program.BotOnly = !Program.BotOnly;
		message.Channel.SendMessageAsync($"Bot channel only toggle is: {Program.BotOnly}");
	}

	public override string LogString => $"botOnly set to {Program.BotOnly}";
	public override ulong[] RequiredRoles => new[] { Program.Config.ModRoleID ?? 0 };
	public override string[] Aliases => new[] { "togglebot", "tbot" };
}
