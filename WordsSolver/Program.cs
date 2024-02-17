//var words = await new Solver("abcd", 2, 2, new InMemoryWordsSource("abdc","abc", "aba", "abcd", "abdc"), true).GetIncludedWordsAsync();

//string gridString = "fitcehosbnaohormuatcmldoesnernomuniolecamiatlldentcbztoaslaielivrncexonlevoekamsa";
string gridString = "brpgejkke";
string wordsUrl = "https://raw.githubusercontent.com/mister-giga/words/main/ordered-alphabetically-ascending.txt";
//byte width = 9;
byte width = 3;
//byte height = 9;
byte height = 3;
bool diagonals = true;
IWordsSource wordsSource = new RawHttpPerLineWordsSource(wordsUrl);

try
{
	var start = DateTime.Now;
	var solver = new Solver(gridString, width, height, wordsSource, diagonals);
	var words = await solver.GetIncludedWordsAsync();
	var end = DateTime.Now;
	Console.WriteLine($"Found: {words.Length} words in {(end - start).TotalMilliseconds} ms");
}
catch(Exception ex)
{
	Console.WriteLine(ex.Message);
}

Console.WriteLine();
interface IWordsSource
{
	Task<string[]> GetWordsAsync();
}

class RawHttpPerLineWordsSource : IWordsSource
{
	readonly string _url;
	public RawHttpPerLineWordsSource(string url)
	{
		_url = url;
	}

	public async Task<string[]> GetWordsAsync()
	{
		using HttpClient httpClient = new();
		using Stream httpStream = await httpClient.GetStreamAsync(_url);
		using StreamReader streamReader = new (httpStream);
		HashSet<string> words = new();
		while (true)
		{
			var line = await streamReader.ReadLineAsync();

			if (line is null) break;

			words.Add(line.Trim().ToLower());
		}

		var wordsArray = words.ToArray();
		return wordsArray;
	}
}

class InMemoryWordsSource : IWordsSource
{
	private readonly string[] _words;

	public InMemoryWordsSource(params string[] words)
	{
		_words = words;
	}

	public Task<string[]> GetWordsAsync() => Task.FromResult(_words);
}

class Solver
{
	private readonly byte _width;
	private readonly byte _height;
	private readonly IWordsSource _wordsSource;
	private readonly bool _diagonals;
	private readonly char[,] _grid;
	private readonly Cell[,] _cells;

	public Solver(string gridString, byte width, byte height, IWordsSource wordsSource, bool diagonals)
	{
		ValidateGridString(gridString, width, height);
		_width = width;
		_height = height;
		_wordsSource = wordsSource;
		_diagonals = diagonals;
		(_grid, _cells) = GetGrid(gridString, width, height);
		PopulateBorderingCells(_cells, width, height, diagonals);
	}

	public async Task<string[]> GetIncludedWordsAsync()
	{
		var allWords = await _wordsSource.GetWordsAsync();
		List<string> includedWords = new();

		foreach (var word in allWords)
		{
			foreach (var cell in _cells)
			{
				if (cell.CanBuildWord(word, _diagonals, 0))
				{
					includedWords.Add(word);
					break;
				}
			}
		}

		return includedWords.ToArray();
	}

	private static void ValidateGridString(string gridString, byte width, byte height)
	{
		if (width * height != gridString.Length)
			throw new Exception($"Grid length shoud be {width}(width) * {height}(height) = {width * height} and not {gridString.Length}");
		ValidateGridCharacters(gridString);
	}
	private static void ValidateGridCharacters(string gridString)
	{
		var invalidCharacters = gridString.Where(c => !IsValidGridCharacter(c)).ToHashSet();
		if (invalidCharacters.Any())
			throw new Exception($"Grid string contains invalid characters: {string.Join(',', invalidCharacters)}");
	}

	private static bool IsValidGridCharacter(char c) => c >= 'a' && c <= 'z';

	private static (char[,], Cell[,]) GetGrid(string gridString, byte width, byte height)
	{
		char[,] grid = new char[width, height];
		Cell[,] cells = new Cell[width, height];

		for (byte y = 0; y < height; y++)
		{
			for (byte x = 0; x < width; x++)
			{
				int index = x + width * y;
				char gridChar = gridString[index];
				grid[x, y] = gridChar;
				cells[x, y] = new Cell(x, y, gridChar);
			}
		}

		return (grid, cells);
	}

	private static void PopulateBorderingCells(Cell[,] cells, byte width, byte height, bool diagonals)
	{
		foreach (var cell in cells)
		{
			cell.PopulateBorderingCells(cells, width, height, diagonals);
		}
	}
}

class Cell
{
	public byte X { get; }
	public byte Y { get; }
	public char C { get; }

	public Cell(byte x, byte y, char c)
	{
		X = x;
		Y = y;
		C = c;
	}

	public override string ToString() => $"{C}({X}:{Y})";

	public Cell? Top { get; private set; }
	public Cell? Left { get; private set; }
	public Cell? Right { get; private set; }
	public Cell? Bottom { get; private set; }

	public Cell? TopLeft { get; private set; }
	public Cell? TopRight { get; private set; }
	public Cell? BottomLeft { get; private set; }
	public Cell? BottomRight { get; private set; }

	public IEnumerable<Cell> BorderingCells(bool diagonals)
	{
		if (Top is not null) yield return Top;
		if (Left is not null) yield return Left;
		if (Right is not null) yield return Right;
		if (Bottom is not null) yield return Bottom;

		if (!diagonals) yield break;

		if (TopLeft is not null) yield return TopLeft;
		if (TopRight is not null) yield return TopRight;
		if (BottomLeft is not null) yield return BottomLeft;
		if (BottomRight is not null) yield return BottomRight;
	}


	public void PopulateBorderingCells(Cell[,] cells, byte width, byte height, bool diagonals)
	{
		var canTop = Y - 1 >= 0;
		var canLeft = X - 1 >= 0;
		var canRight = X + 1 < width;
		var canBottom = Y + 1 < height;

		if (canTop) Top = cells[X, Y - 1];
		if (canLeft) Left = cells[X - 1, Y];
		if (canRight) Right = cells[X + 1, Y];
		if (canBottom) Bottom = cells[X, Y + 1];

		if (diagonals)
		{
			if (canTop && canLeft) TopLeft = cells[X - 1, Y - 1];
			if (canTop && canRight) TopRight = cells[X + 1, Y - 1];
			if (canBottom && canLeft) BottomLeft = cells[X - 1, Y + 1];
			if (canBottom && canRight) BottomRight = cells[X + 1, Y + 1];
		}
	}

	public bool CanBuildWord(string word, bool diagonals, int index = 0, params Cell[] except)
	{
		var wordChar = word[index];
		if (wordChar != C)
			return false;


		if (index == word.Length - 1)
			return true;

		foreach (var borderingCell in BorderingCells(diagonals))
		{
			if(!except.Contains(borderingCell) && borderingCell.CanBuildWord(word, diagonals, index + 1, except.With(this)))
			{
				return true;
			}
		}

		return false;
	}

	
}

public static class Extensions
{
	public static T[] With<T>(this T[] items, T with)
	{
		var newArray = new T[items.Length + 1];
		Array.Copy(items, newArray, items.Length);
		newArray[items.Length] = with;
		return newArray;
	}
}
