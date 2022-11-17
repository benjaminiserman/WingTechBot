namespace ConnectFour;
using System;

public class AssistAI : ProAI, IHuman
{
	public override string Name => "AssistAI";

	public ulong ID { get; private set; }
	private bool _init = false;

	private ConnectFour _game;

	public override void Init(Func<object, Discord.IMessage> saveWriteLine)
	{
		base.Init(saveWriteLine);
		Say("I'll help you out here.");
	}

	public void Init(ulong id, ConnectFour game)
	{
		if (!_init)
		{
			_init = true;
			ID = id;
			this._game = game;
		}
	}

	public override int Prompt(Board board, int round)
	{
		Say($"Input move, {board.CurrentTeam} team.");

		var numberInput = -1;
		var lastInput = -2;

		var confirmed = false;
		while (!confirmed)
		{
			var move = _game.PromptMove(ID);

			if (move is null)
			{
				board.Forfeit();
			}

			numberInput = move ?? 0;

			var values = GetValues(new(board));

			var max = Library.Max(values);

			if ((values[max] > 500 && max != numberInput) ||
				(values[numberInput] < 0 && values[max] > 0))
			{
				if (lastInput == numberInput)
				{
					confirmed = true;
				}
				else
				{
					Say($"Are you sure you want that? {max} may be a better option. Think carefully.");

					lastInput = numberInput;
				}
			}
			else
			{
				confirmed = true;
			}
		}

		return numberInput;
	}

	public override void MatchEnd(State victor, int round) // This is called every time a round ends.
	{
		if (victor == State.Empty)
		{
			Say("Good stuff, human.");
		}
		else if (victor == Team)
		{
			Say("Good stuff, human.");
		}
		else
		{
			Say("We'll get them next time.");
		}
	}

	public override void GameEnd() => Say("I hope I was helpful."); // This is called at the end of a series of games.
}
