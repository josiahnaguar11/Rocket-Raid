using Sandbox;

/// <summary>
/// Manages player currency and experience for future features
/// </summary>
public sealed class CurrencyComponent : Component
{
	// Events for currency changes
	public static event System.Action<CurrencyComponent, int, int> OnCoinsChanged;
	public static event System.Action<CurrencyComponent, int, int> OnExperienceChanged;
	public static event System.Action<CurrencyComponent, int> OnLevelUp;

	[Property]
	[Category("Currency")]
	[Range(0, 999999)]
	public int Coins { get; private set; } = 0;

	[Property]
	[Category("Experience")]
	[Range(0, 999999)]
	public int Experience { get; private set; } = 0;

	[Property]
	[Category("Experience")]
	[Range(1, 100)]
	public int Level { get; private set; } = 1;

	[Property]
	[Category("Experience")]
	[Range(50, 500)]
	[Step(25)]
	public int BaseExperiencePerLevel { get; set; } = 100;

	[Property]
	[Category("Experience")]
	[Range(1.1f, 3.0f)]
	[Step(0.1f)]
	public float ExperienceMultiplier { get; set; } = 1.5f;

	/// <summary>
	/// Experience required for the next level
	/// </summary>
	public int ExperienceToNextLevel => CalculateExperienceForLevel(Level + 1) - Experience;

	/// <summary>
	/// Experience required for current level
	/// </summary>
	public int ExperienceForCurrentLevel => CalculateExperienceForLevel(Level);

	/// <summary>
	/// Progress toward next level (0-1)
	/// </summary>
	public float LevelProgress
	{
		get
		{
			var currentLevelExp = CalculateExperienceForLevel(Level);
			var nextLevelExp = CalculateExperienceForLevel(Level + 1);
			var totalExpNeeded = nextLevelExp - currentLevelExp;
			var expProgress = Experience - currentLevelExp;
			return totalExpNeeded > 0 ? (float)expProgress / totalExpNeeded : 0f;
		}
	}

	/// <summary>
	/// Add coins to the player
	/// </summary>
	public void AddCoins(int amount)
	{
		if (amount <= 0) return;

		var oldCoins = Coins;
		Coins += amount;
		
		OnCoinsChanged?.Invoke(this, oldCoins, Coins);
		Log.Info($"{GameObject.Name} earned {amount} coins (Total: {Coins})");
	}

	/// <summary>
	/// Spend coins if the player has enough
	/// </summary>
	public bool SpendCoins(int amount)
	{
		if (amount <= 0 || Coins < amount) return false;

		var oldCoins = Coins;
		Coins -= amount;
		
		OnCoinsChanged?.Invoke(this, oldCoins, Coins);
		Log.Info($"{GameObject.Name} spent {amount} coins (Remaining: {Coins})");
		return true;
	}

	/// <summary>
	/// Add experience and handle level ups
	/// </summary>
	public void AddExperience(int amount)
	{
		if (amount <= 0) return;

		var oldExperience = Experience;
		var oldLevel = Level;
		Experience += amount;
		
		// Check for level ups
		while (Experience >= CalculateExperienceForLevel(Level + 1))
		{
			Level++;
			OnLevelUp?.Invoke(this, Level);
			Log.Info($"{GameObject.Name} leveled up to level {Level}!");
		}
		
		OnExperienceChanged?.Invoke(this, oldExperience, Experience);
		
		if (Level > oldLevel)
		{
			Log.Info($"{GameObject.Name} gained {amount} experience and leveled up from {oldLevel} to {Level}!");
		}
		else
		{
			Log.Info($"{GameObject.Name} gained {amount} experience (Total: {Experience})");
		}
	}

	/// <summary>
	/// Set coins to a specific amount (for admin/debug use)
	/// </summary>
	public void SetCoins(int amount)
	{
		var oldCoins = Coins;
		Coins = amount > 0 ? amount : 0;
		OnCoinsChanged?.Invoke(this, oldCoins, Coins);
	}

	/// <summary>
	/// Set experience to a specific amount (for admin/debug use)
	/// </summary>
	public void SetExperience(int amount)
	{
		var oldExperience = Experience;
		var oldLevel = Level;
		Experience = amount > 0 ? amount : 0;
		
		// Recalculate level based on new experience
		Level = CalculateLevelFromExperience(Experience);
		
		if (Level != oldLevel)
		{
			OnLevelUp?.Invoke(this, Level);
		}
		
		OnExperienceChanged?.Invoke(this, oldExperience, Experience);
	}

	/// <summary>
	/// Calculate experience required for a specific level
	/// </summary>
	private int CalculateExperienceForLevel(int level)
	{
		if (level <= 1) return 0;
		
		int totalExp = 0;
		for (int i = 2; i <= level; i++)
		{
			totalExp += (int)(BaseExperiencePerLevel * System.Math.Pow(ExperienceMultiplier, i - 2));
		}
		return totalExp;
	}

	/// <summary>
	/// Calculate what level a player should be based on experience
	/// </summary>
	private int CalculateLevelFromExperience(int experience)
	{
		int level = 1;
		while (experience >= CalculateExperienceForLevel(level + 1))
		{
			level++;
		}
		return level;
	}

	// Debug buttons
	[Button("Add 10 Coins", "💰")]
	[Category("Debug")]
	public void DebugAddCoins()
	{
		AddCoins(10);
	}

	[Button("Add 50 Experience", "⭐")]
	[Category("Debug")]
	public void DebugAddExperience()
	{
		AddExperience(50);
	}

	[Button("Spend 5 Coins", "💸")]
	[Category("Debug")]
	public void DebugSpendCoins()
	{
		SpendCoins(5);
	}

	[Button("Level Up", "🚀")]
	[Category("Debug")]
	public void DebugLevelUp()
	{
		AddExperience(ExperienceToNextLevel);
	}
}