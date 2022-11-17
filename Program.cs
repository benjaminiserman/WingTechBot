﻿namespace WingTechBot;
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using WingTechBot.Handlers;

public static class Program
{
	public static DiscordSocketClient Client { get; private set; }

	public static SocketTextChannel BotChannel { get; private set; }

	public const string DELETE_PATH = @"save\deleted_messages.txt";
	public const string AUDIT_PATH = @"save\audit_log.txt";
	public const string CONFIG_PATH = @"save\config.json";

	public static bool BotOnly { get; set; }

	public static Random Random { get; } = new();

	public static CommandHandler CommandHandler { get; private set; } = new();
	public static GameHandler GameHandler { get; private set; } = new();
	public static KarmaHandler KarmaHandler { get; private set; } = new();
	public static AlarmHandler AlarmHandler { get; private set; }

	public static void Main()
	{
		try
		{
			MainAsync().GetAwaiter().GetResult();
		}
		catch (Exception ex)
		{
			Console.WriteLine(ex.Message);
			Console.ReadLine();
			throw;
		}
	}

	private static void OnProcessExit(object sender, EventArgs e) => KarmaHandler.Save();

	public static ulong BotID { get; private set; }

	public static Config Config { get; private set; }

	private static async Task MainAsync()
	{
		if (!Directory.Exists("save"))
		{
			Directory.CreateDirectory("save");
		}

		if (!File.Exists(CONFIG_PATH))
		{
			File.WriteAllText(CONFIG_PATH, JsonSerializer.Serialize(new Config()));
		}

		Config = JsonSerializer.Deserialize<Config>(File.ReadAllText(CONFIG_PATH));

		KarmaHandler.Load();

		AlarmHandler = File.Exists(AlarmHandler.ALARM_PATH)
			? Newtonsoft.Json.JsonConvert.DeserializeObject<AlarmHandler>(File.ReadAllText(AlarmHandler.ALARM_PATH))
			: new();

		var config = new DiscordSocketConfig() { MessageCacheSize = 100, AlwaysDownloadUsers = true };
		Client = new(config);

		Client.MessageReceived += CommandHandler.CommandTask;
		Client.MessageReceived += GameHandler.GameTask;
		Client.Log += Log;

		Client.ReactionAdded += KarmaHandler.ReactionAdded;
		Client.ReactionRemoved += KarmaHandler.ReactionRemoved;

		Client.Ready += Start;

		AppDomain.CurrentDomain.ProcessExit += OnProcessExit;

		await Client.LoginAsync(TokenType.Bot, Config.LoginToken); // Secrets is only accessible on my local copy... Sorry, no stealing my log-in!
		await Client.SetGameAsync("cringe | ~help");
		await Client.StartAsync();

		var hooks = AppDomain.CurrentDomain.GetAssemblies().SelectMany(x => x.GetTypes()).Where(x => typeof(IHookable).IsAssignableFrom(x) && !x.IsInterface && !x.IsAbstract);

		foreach (var hook in hooks)
		{
			Console.WriteLine($"Hooking {hook.Name}...");
			((IHookable)Activator.CreateInstance(hook)).Hook();
		}

		AlarmHandler.HookAlarms(Client);

		await AutoSave();
		await KarmaHandler.CheckRunningKarma();

		// Block this task until the Program is closed.
		await Task.Delay(-1);
	}

	private static Task Start()
	{
		BotChannel = Client.GetChannel(Config.BotChannelID ?? 0) as SocketTextChannel;

		BotID = Client.CurrentUser.Id;

		return Task.CompletedTask;
	}

	private static async Task AutoSave()
	{
		while (true)
		{
			await Task.Delay(3_600_000);
			Console.WriteLine("Autosaving");
			await KarmaHandler.Save();
		}
	}

	private static Task Log(LogMessage msg)
	{
		Console.WriteLine(msg.ToString());
		return Task.CompletedTask;
	}

	public static SocketUser GetUserFromMention(SocketMessage message, string[] arguments, int index = 1)
	{
		var requested = message.Author;
		var parsed = true;

		if (arguments.Length > 1)
		{
			parsed = MentionUtils.TryParseUser(arguments[index], out var id);
			requested = Client.GetUser(id);
		}

		if (requested is null || !parsed)
		{
			message.Channel.SendMessageAsync("User not found.");
			Console.WriteLine("User not found.");
			Console.WriteLine(arguments[index]);
			if (parsed)
			{
				Console.WriteLine(MentionUtils.ParseUser(arguments[index]));
			}
		}

		return requested;
	}

	public static bool TryGetUser(string mention, out IUser user)
	{
		user = null;
		var found = MentionUtils.TryParseUser(mention, out var id);

		if (found)
		{
			try
			{
				user = Client.GetUser(id);
			}
			catch { }
		}

		return found;
	}

	public static void AddToAuditLog(IUser mod, string action)
	{
		using var file = File.AppendText(AUDIT_PATH);

		file.WriteLine($"{mod.Username} {action} at {DateTime.Now}");
	}

	public static IUser GetUser(ulong id) => Client.GetUser(id);

	public static IChannel GetChannel(ulong id) => Client.GetChannel(id);
}
