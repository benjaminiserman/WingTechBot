﻿namespace ConnectFour;
using System;
using System.Collections.Generic;
using System.Linq;

#pragma warning disable
using Game = WingTechBot.Game; // the fact this can be simplified to "using Game = Game;" is terrifying
#pragma warning restore

public class ConnectFour : Game
{
	private static bool _auto = false;
	protected override bool Debug => false;

	public override void RunGame()
	{
		int columns, rows, connect, teamCount;
		bool noMiddleStart, load;
		string loadString = null;

		if (BoolQuery("Quickplay? (y/n)"))
		{
			columns = 7;
			rows = 6;
			connect = 4;
			teamCount = 2;
			noMiddleStart = false;
			load = false;
		}
		else
		{
			columns = IntQuery("How many columns should the board have?");
			while (columns is < 3 or > 16)
			{
				columns = IntQuery("Columns must be between 3 and 16.");
			}

			rows = IntQuery("How many rows should the board have?");
			while (rows is < 3 or > 16)
			{
				rows = IntQuery("Rows must be between 3 and 16.");
			}

			connect = IntQuery("How many dots should the player have to connect?");
			while (connect < 3 || (connect > columns && connect > rows))
			{
				connect = IntQuery("Connect must be greater than 2 and less than or equal to either columns or rows.");
			}

			teamCount = IntQuery("How many teams are playing?");
			while (teamCount is < 2 or > 8)
			{
				teamCount = IntQuery("Teams must be between 2 and 8.");
			}

			noMiddleStart = columns % 2 != 0 && BoolQuery("Should the first player be forbidden from starting in the center? (y/n)");

			load = BoolQuery("Load Game? (y/n)");
			if (load)
			{
				loadString = PromptString(GamemasterID, AllowedChannels, true, "Enter Load String. (Round History of desired game)").Trim();
			}
		}

		var @assembly = typeof(AI).Assembly;
		var AIs = @assembly.GetTypes().Where(t => t.IsSubclassOf(typeof(AI)));

		Dictionary<State, AI> teams = new();
		Dictionary<State, int> wins = new();
		var draws = 0;
		var totalGames = 0;

		var availableText = "Available AIs:";

		foreach (var ai in AIs)
		{
			availableText += $"\n{ai.Name}";
		}

		WriteLine(availableText);

		var input = "";
		var currentPlayerIndex = 0;

		for (var i = 0; i < teamCount; i++)
		{
			WriteLine($"Which AI should be Player {i + 1}?");
			Type foundAI = null;
			while (foundAI is null)
			{
				input = Prompt(GamemasterID, PromptMode.Any, true).Content.Trim().ToLower();

				foreach (var AI in AIs)
				{
					if (AI.Name.ToLower() == input)
					{
						foundAI = AI;
						break;
					}
				}
			}

			var createAI = Activator.CreateInstance(foundAI) as AI;

			createAI.Init(SaveWriteLine);
			if (createAI is IHuman humanAI)
			{
				humanAI.Init(PlayerIDs[currentPlayerIndex++], this);
			}

			createAI.Team = (State)(i + 1);

			teams.Add((State)(i + 1), createAI);
			wins.Add((State)(i + 1), 0);
		}

		var humanPresent = false;

		foreach (var ai in teams.Values)
		{
			if (ai is IHuman)
			{
				humanPresent = true;
				break;
			}
		}

		if (!humanPresent)
		{
			if (BoolQuery("Should the game repeat without player input? (y/n)"))
			{
				_auto = true; // $$$ RE-ENABLE AUTO
			}
			else
			{
				_auto = false;
			}
		}

		var currentTeam = State.Circle;

		while (true)
		{
			Board board = new(SaveWriteLine, DeleteSavedMessages, columns, rows, connect, teamCount, noMiddleStart, currentTeam);
			Match match = new(board, teams, board.Auth, WriteLine, load, loadString);

			if (load)
			{
				load = false;
			}

			match.RunGame();

			totalGames++;
			if (board.Victor != State.Empty)
			{
				wins[board.Victor]++;
			}
			else
			{
				draws++;
			}

			if (!_auto)
			{
				input = PromptAny(AllowedChannels, (string x) => x.Trim().ToLower() is "next" or "end", true, "Type \"next\" to continue or \"end\" to stop playing.").Item2.Trim().ToLower();

				if (input == "end")
				{
					break;
				}
			}
			else if (Console.KeyAvailable)
			{
				break;
			}

			currentTeam = Next(currentTeam, teamCount);
		}

		WriteLine();

		foreach (var ai in teams.Values)
		{
			WriteLine($"Team {ai.Team}, under {ai.Name}, had {wins[ai.Team]} wins, for a win rate of {wins[ai.Team] / (double)totalGames:P2}");

			try
			{
				ai.GameEnd();
			}
			catch { }
		}

		WriteLine($"{draws} games ended in a draw.");
	}

	private int IntQuery(string text) => Prompt<int>(GamemasterID, AllowedChannels, true, text);

	private bool BoolQuery(string text) => Prompt(GamemasterID, AllowedChannels, (string x) => x.Trim().ToLower() is "y" or "n", message: text).Trim().ToLower() == "y";

	public static State Next(State state, int teamCount)
	{
		if (state == State.Empty)
		{
			throw new("Invalid Argument: state cannot be State.Empty!");
		}

		var id = (int)state;
		id++;
		if (id > teamCount)
		{
			id = 1;
		}

		return (State)id;
	}

	public int? PromptMove(ulong id)
	{
		var numberInput = -1;
		var input = Prompt(id, AllowedChannels, (string s) => Library.TryDec(s, out numberInput) || s == "end", true, saveMessage: true);

		return 
			input == "end" 
			? null 
			: numberInput;
	}
}
