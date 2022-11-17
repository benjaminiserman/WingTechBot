namespace ConnectFour;
using System;
using System.Collections.Generic;

public class Match
{
	public int Round { get; private set; }
	public static bool GameInProgress { get; private set; }
	public State PromptingTeam { get; private set; }

	public Board Board { get; private set; }
	private readonly Dictionary<State, AI> _teams = new();
	private readonly int _auth;
	private readonly bool _load;
	private readonly string _loadString;
	private int _loadCounter;

	private readonly Func<object, Discord.IMessage> _writeLine;

	public Match(Board board, Dictionary<State, AI> teams, int auth, Func<object, Discord.IMessage> writeLine, bool load = false, string loadString = null)
	{
		Board = board;
		_teams = teams;
		_auth = auth;
		_load = load;
		_loadString = loadString;
		_writeLine = writeLine;
	}

	public void RunGame()
	{
		if (GameInProgress)
		{
			throw new("A game is already in progress.");
		}

		GameInProgress = true;

		Round = 0;

		var startingTeam = Board.CurrentTeam;

		while (Board.GameInProgress)
		{
			PromptingTeam = Board.CurrentTeam;
			if (PromptingTeam == startingTeam)
			{
				Round++;
			}

			var input = -1;
			if (_load && _loadCounter < _loadString.Length)
			{
				if (_loadCounter == 0)
				{
					_writeLine("Replaying...");
				}

				try
				{
					if (Library.TryDec(_loadString[_loadCounter].ToString(), out input))
					{
						Board.InputMove(input, _auth, Round);
					}
					else
					{
						throw new();
					}
				}
				catch
				{
					_writeLine("Invalid Load String!");
					_loadCounter = _loadString.Length;
				}

				_loadCounter++;
			}
			else
			{
				if (_teams[PromptingTeam] is IHuman)
				{
					Board.Draw(Round);
				}

				do
				{
					try
					{
						input = _teams[Board.CurrentTeam].Prompt(Board, Round);
					}
					catch (Exception e)
					{
						_writeLine($"{PromptingTeam} errored and has ended the game.");
						_writeLine(e);
						Board.Forfeit();
					}
				}
				while (!Board.InputMove(Convert.ToInt32(input), _auth, Round));
			}
		}

		Board.DisableCreation(_auth);

		foreach (var ai in _teams.Values)
		{
			try
			{
				ai.MatchEnd(Board.Victor, Round);
			}
			catch
			{
				_writeLine($"{ai.Team} team, with AI {ai.Name} errored in it's MatchEnd() method.");
			}
		}

		Board.EnableCreation(_auth);

		GameInProgress = false;
	}
}
