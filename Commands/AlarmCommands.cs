﻿namespace WingTechBot.Commands;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Discord;
using Newtonsoft.Json;
using WingTechBot.Alarm;

internal class AlarmCommand : Command
{
	public static Dictionary<string, Func<UserAlarm, IMessage, string[], string>> SubCommands => new()
	{
		["log"] = (a, message, _) => AlarmSubCommands.Log(a, message),
		["skip"] = (a, message, _) => AlarmSubCommands.Skip(a, message),
		["preset"] = AlarmSubCommands.Preset,
		["pause"] = (a, message, _) => AlarmSubCommands.Pause(a, message),
		["resume"] = (a, message, _) => AlarmSubCommands.Resume(a, message),
		["clear"] = AlarmSubCommands.Clear,
		//["set"] = AlarmSubCommands.Set,
		["template"] = (_, message, _) => AlarmSubCommands.Template(message),
		["help"] = (a, message, _) => AlarmSubCommands.Help(a, message),
		["add"] = AlarmSubCommands.Add,
		["remove"] = AlarmSubCommands.Remove,
	};

	private static readonly string[] _allowNull = new[] { "add", "set", "template", "help" };

	private string _logString;
	private UserAlarm _alarm;

	public override void Execute()
	{
		_alarm = Program.AlarmHandler.GetAlarm(message.Author.Id);
		var command = arguments[1].ToLower();

		if (SubCommands.ContainsKey(command))
		{
			if (_alarm is not null || _allowNull.Contains(command))
			{
				_logString = SubCommands[command].Invoke(_alarm, message, arguments[2..]);
			}
			else
			{
				throw new($"You do not have any alarms saved.");
			}
		}
		else
		{
			throw new($"Alarm subcommand {arguments[1]} does not exist.");
		}
	}

	public override string LogString => _logString;
}

internal class LogAlarmsCommand : Command
{
	public override void Execute()
	{
		File.WriteAllText("alarm_dump.json", JsonConvert.SerializeObject(Program.AlarmHandler, Formatting.Indented));
		message.Channel.SendMessageAsync("dumped all alarms to alarm_dump.json");
	}

	public override bool OwnerOnly => true;

	public override string LogString => "logging all alarms";
}

internal static class AlarmSubCommands
{
	public static string Log(UserAlarm alarm, IMessage message)
	{
		message.Channel.SendFileAsync(new MemoryStream(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(alarm, Formatting.Indented))), "alarm.json");

		return $"logged alarms for {message.Author.Username}";
	}

	public static string Skip(UserAlarm alarm, IMessage message)
	{
		if (alarm?.RepeatingTimes is null)
		{
			throw new("You do not have any repeating alarms to skip.");
		}
		else
		{
			if (alarm.Ringing)
			{
				alarm.StopRinging();

				message.Channel.SendMessageAsync("Skipping your currently ringing alarm.");
				return $"skipped {message.Author.Username}'s currently ringing alarm.";
			}
			else
			{
				var found = alarm.NextTime();

				message.Channel.SendMessageAsync($"Incrementing alarm {found}.");

				found.Increment();

				Program.AlarmHandler.SaveAlarms();
				return $"incremented {message.Author.Username}'s next alarm to {found}";
			}
		}
	}

	public static string Preset(UserAlarm alarm, IMessage message, string[] arguments)
	{
		if (arguments.Length < 2)
		{
			throw new("Invalid number of arguments! Preset expects 2 arguments.");
		}

		var name = arguments[1].ToLower();
		var found = alarm.Presets.FirstOrDefault(x => x.Name == name);

		switch (arguments[0].ToLower())
		{
			case "load":
			{
				if (found is null)
				{
					throw new($"Preset {arguments[1]} does not exist.");
				}
				else
				{
					alarm.RepeatingTimes = found.RepeatingTimes;
				}

				foreach (var x in alarm.RepeatingTimes)
				{
					x.Reset();
				}

				message.Channel.SendMessageAsync($"Loaded preset {arguments[1]}.");
				Program.AlarmHandler.SaveAlarms();
				return $"loaded preset {name} for {message.Author.Username}";
			}
			case "save": // $$$ add override warning
			{
				if (found is not null)
				{
					alarm.Presets.Remove(found);
				}

				alarm.Presets.Add(new(name, alarm.RepeatingTimes, alarm.SingleTimes));

				message.Channel.SendMessageAsync($"Saved current times to preset {arguments[1]}.");
				Program.AlarmHandler.SaveAlarms();
				return $"saved current times to preset {name} for {message.Author.Username}";
			}
			case "delete":
			{
				if (found is null)
				{
					throw new($"Preset {arguments[1]} does not exist.");
				}
				else
				{
					alarm.Presets.Remove(found);
				}

				message.Channel.SendMessageAsync($"Deleted preset {arguments[1]}.");
				Program.AlarmHandler.SaveAlarms();
				return $"deleted preset {name} for {message.Author.Username}";
			}
			case "rename":
			{
				if (arguments.Length < 3)
				{
					throw new("You must specify a new name.");
				}
				else if (found is null)
				{
					throw new($"Preset {arguments[1]} does not exist.");
				}
				else
				{
					found.Name = arguments[2].ToLower();
				}

				message.Channel.SendMessageAsync($"Renamed preset {arguments[1]} to {arguments[2]}.");
				Program.AlarmHandler.SaveAlarms();
				return $"renamed preset {name} to {found.Name} for {message.Author.Username}";
			}
			default:
			{
				throw new ArgumentException($"Command preset {arguments[0]} not found.");
			}
		}
	}

	public static string Pause(UserAlarm alarm, IMessage message)
	{
		if (alarm.Paused)
		{
			throw new("Your alarms are already paused.");
		}
		else
		{
			alarm.Paused = true;
			alarm.StopRinging();
		}

		message.Channel.SendMessageAsync("Alarms paused.");
		Program.AlarmHandler.SaveAlarms();
		return $"paused alarms for {message.Author.Username}";
	}

	public static string Resume(UserAlarm alarm, IMessage message)
	{
		if (alarm.Paused)
		{
			alarm.Paused = false;
		}
		else
		{
			throw new("Your alarms are already resumed.");
		}

		message.Channel.SendMessageAsync("Alarms resumed.");
		Program.AlarmHandler.SaveAlarms();
		return $"resumed alarms for {message.Author.Username}";
	}

	public static string Clear(UserAlarm alarm, IMessage message, string[] arguments)
	{
		bool clearRepeating = true, clearSingle = true;

		if (arguments.Length != 0)
		{
			switch (arguments[0].ToLower())
			{
				case "r":
				case "repeating":
					clearSingle = false;
					break;
				case "s":
				case "single":
					clearRepeating = false;
					break;
				case "a":
				case "all":
					break;
				default:
					throw new($"Criteria {arguments[0]} is not recognized.");
			}
		}

		if (clearRepeating)
		{
			alarm.RepeatingTimes.Clear();
		}

		if (clearSingle)
		{
			alarm.SingleTimes.Clear();
		}

		message.Channel.SendMessageAsync("Alarms cleared.");
		Program.AlarmHandler.SaveAlarms();
		return $"cleared alarms for {message.Author.Username}";
	}

	public static string Set(UserAlarm alarm, IMessage message, string[] arguments)
	{
		StringBuilder sb = new();
		foreach (var s in arguments)
		{
			sb.Append($"{s} ");
		}

		//message.Attachments.First()

		if (alarm is null)
		{
			alarm = JsonConvert.DeserializeObject<UserAlarm>(sb.ToString());

			if (alarm.UserID != message.Author.Id)
			{
				throw new("UserID cannot be changed!");
			}

			Program.Client.MessageReceived += alarm.OnReceiveMessage;
			Program.AlarmHandler.Alarms.Add(alarm);
		}
		else
		{
			var carry = JsonConvert.SerializeObject(alarm);
			JsonConvert.PopulateObject(sb.ToString(), alarm);

			if (alarm.UserID != message.Author.Id)
			{
				JsonConvert.PopulateObject(carry, alarm);
				throw new("UserID cannot be changed!");
			}
		}

		message.Channel.SendMessageAsync("Alarm profile set.");
		Program.AlarmHandler.SaveAlarms();
		return $"set alarm profile for {message.Author.Username} to {sb}";
	}

	public static string Template(IMessage message)
	{
		var tempTime = DateTime.Now;
		tempTime = tempTime.AddMinutes(1).AddTicks(-(tempTime.Ticks % TimeSpan.TicksPerSecond));

		UserAlarm template = new(0, new() { new(tempTime, 1) }, new() { new(tempTime, false) })
		{
			UserID = message.Author.Id,
			Name = "My Alarm Template",
		};
		Log(template, message);

		return $"sent alarm template to {message.Author.Username}";
	}

	public static string Help(UserAlarm alarm, IMessage message)
	{
		StringBuilder text = new("```\nPossible Commands:");

		foreach (var key in AlarmCommand.SubCommands.Keys)
		{
			text.Append($"\n - {key}");
		}

		text.Append("\n\nPossible Intervals:");

		foreach (var interval in Enum.GetValues(typeof(IntervalType)))
		{
			text.Append($"\n[{(int)interval}] - {interval}");
		}

		text.Append("\n```");

		message.Channel.SendMessageAsync(text.ToString());
		return $"sent alarm subcommands help to {message.Author.Username}";
	}

	public static string Add(UserAlarm alarm, IMessage message, string[] arguments)
	{
		if (alarm is null)
		{
			alarm = new(message.Author.Id, new(), new());
			Program.Client.MessageReceived += alarm.OnReceiveMessage;
			Program.AlarmHandler.Alarms.Add(alarm);
			Program.AlarmHandler.AddAlarmToTimer(alarm);
		}

		StringBuilder sb = new();
		foreach (var s in arguments[1..])
		{
			sb.Append($"{s} ");
		}

		switch (arguments[0].ToLower())
		{
			case "r":
			case "repeat":
			case "repeating":
			{
				alarm.RepeatingTimes.Add(JsonConvert.DeserializeObject<RepeatingTime>(sb.ToString()));
				message.Channel.SendMessageAsync($"Added repeating alarm.");
				break;
			}
			case "s":
			case "once":
			case "single":
			{
				var date = DateTime.Parse(arguments[1]);
				var time = DateTime.Parse(arguments[2]);

				alarm.SingleTimes.Add(new(new(date.Year, date.Month, date.Day, time.Hour, time.Minute, time.Second, DateTimeKind.Local), false));

				message.Channel.SendMessageAsync($"Added single alarm");
				break;
			}
			case "w":
			case "weekly":
			{
				var day = Enum.Parse<DayOfWeek>($"{char.ToUpper(arguments[1][0])}{arguments[1][1..].ToLower()}");
				var time = DateTime.Parse(arguments[2]);

				alarm.RepeatingTimes.Add(new((int)day, time.Hour, time.Minute, 7));

				message.Channel.SendMessageAsync($"Added weekly alarm.");
				break;
			}
			case "override":
			{
				var date = DateTime.Parse(arguments[1]);
				var time = DateTime.Parse(arguments[2]);

				alarm.SingleTimes.Add(new(new(date.Year, date.Month, date.Day, time.Hour, time.Minute, time.Second, DateTimeKind.Local), true));

				message.Channel.SendMessageAsync($"Added alarm override.");
				break;
			}

			default:
			{
				throw new ArgumentException($"{arguments[0]} is not recognized a recognized alarm type. Try types repeating or single.");
			}
		}

		Program.AlarmHandler.SaveAlarms();

		return $"added alarm for {message.Author.Username}";
	}

	public static string Remove(UserAlarm alarm, IMessage message, string[] arguments)
	{
		if (arguments.Length != 0)
		{
			throw new ArgumentException("`~alarm remove` must be called with no arguments.");
		}

		alarm.Paused = true;
		alarm.StopRinging();
		Program.AlarmHandler.Alarms.Remove(alarm);
		Program.AlarmHandler.SaveAlarms();

		message.Channel.SendMessageAsync("Alarm profile deleted.");

		return $"removed alarm profile for {message.Author.Username}";
	}
}
