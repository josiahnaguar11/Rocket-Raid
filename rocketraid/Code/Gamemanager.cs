using Sandbox;
using System.Linq;

public sealed class Gamemanager : Component, Component.INetworkListener
{
	[Property]
	[Category("Round System")]
	public GameObject RocketPrefab { get; set; }

	[Property]
	[Category("Round System")]
	[Range(1f, 40f)]
	public float RoundStartDelay { get; set; } = 3f;

	[Property]
	[Category("Round System")]
	[Range(1f, 5f)]
	public float RocketRespawnDelay { get; set; } = 2f;

	public int CurrentRound { get; private set; } = 1;
	
	private bool _roundActive = false;
	private GameObject _currentRocket;
	private TimeUntil _roundStartTimer;
	private TimeUntil _rocketRespawnTimer;
	private bool _waitingToRespawnRocket = false;
	private bool _isHost = false;

	protected override void OnStart()
	{
		Log.Info("=== GAME MANAGER INITIALIZED ===");
		
		// Subscribe to player death events
		SubscribeToPlayerDeaths();
		
		// Check if we're running in single player or if networking is not active
		if (!Networking.IsActive)
		{
			_isHost = true;
			Log.Info("Running in single player mode - starting rounds");
			_roundStartTimer = RoundStartDelay;
			Log.Info($"First round will start in {RoundStartDelay} seconds...");
		}
		else
		{
			Log.Info("Running in multiplayer mode - waiting for host determination");
		}
	}

	protected override void OnUpdate()
	{
		// Only the host/server should manage rounds to prevent desync
		if (!_isHost)
			return;

		// Check if it's time to start a round
		if (!_roundActive && _roundStartTimer)
		{
			StartRound();
		}

		// Check if it's time to respawn the rocket
		if (_waitingToRespawnRocket && _rocketRespawnTimer)
		{
			SpawnRocket();
			_waitingToRespawnRocket = false;
		}

		// Monitor rocket status
		MonitorRocket();
	}

	private void SubscribeToPlayerDeaths()
	{
		// Find all players and subscribe to their death events
		var playerComponents = Scene.GetAllComponents<PlayerComponent>().ToArray();

		Log.Info($"Found {playerComponents.Length} players to monitor");

		// Subscribe to HealthComponent death events
		foreach (var playerComponent in playerComponents)
		{
			if (playerComponent.HealthComponent.IsValid())
			{
				HealthComponent.OnDeath += OnPlayerDeath;
				Log.Info($"Subscribed to death events for player: {playerComponent.GameObject.Name}");
			}
		}
	}

	private void OnPlayerDeath(HealthComponent healthComponent)
	{
		var playerComponent = healthComponent.GetComponent<PlayerComponent>();
		if (playerComponent.IsValid())
		{
			Log.Info($"Player {playerComponent.GameObject.Name} has died!");
			EndRound(playerComponent.GameObject);
		}
	}

	[Rpc.Broadcast]
	private void StartRound()
	{
		_roundActive = true;
		Log.Info($"=== ROUND {CurrentRound} STARTED ===");
		
		// Only the host spawns the rocket, but broadcast the round start
		if (_isHost)
		{
			SpawnRocket();
		}
	}

	private void SpawnRocket()
	{
		if (!RocketPrefab.IsValid())
		{
			Log.Error("Rocket prefab is not set! Cannot spawn rocket.");
			return;
		}

		// Get random player to target
		var targetPlayer = GetRandomPlayer();
		if (!targetPlayer.IsValid())
		{
			Log.Error("No valid players found to target!");
			return;
		}

		// Spawn rocket at a position above the scene
		var spawnPosition = targetPlayer.WorldPosition + Vector3.Up * 200f + Vector3.Random * 100f;
		_currentRocket = RocketPrefab.Clone(spawnPosition);
		
		// Ensure the rocket is networked properly
		if (_currentRocket.IsValid())
		{
			_currentRocket.NetworkSpawn();
			Log.Info($"Rocket spawned and networked at {spawnPosition} targeting player: {targetPlayer.Name}");
		}
		else
		{
			Log.Error("Failed to spawn rocket!");
		}
	}

	private GameObject GetRandomPlayer()
	{
		// Find all alive players
		var alivePlayers = Scene.GetAllComponents<PlayerComponent>()
			.Where(player => player.HealthComponent.IsValid() && player.HealthComponent.Alive)
			.Select(player => player.GameObject)
			.ToArray();

		if (alivePlayers.Length == 0)
		{
			Log.Warning("No alive players found!");
			return null;
		}

		var randomIndex = Game.Random.Int(0, alivePlayers.Length - 1);
		var selectedPlayer = alivePlayers[randomIndex];
		
		Log.Info($"Selected random player: {selectedPlayer.Name} (from {alivePlayers.Length} alive players)");
		return selectedPlayer;
	}

	private void MonitorRocket()
	{
		// Check if rocket was destroyed and we need to respawn it
		if (_roundActive && !_currentRocket.IsValid() && !_waitingToRespawnRocket)
		{
			Log.Info("Rocket was destroyed! Scheduling respawn...");
			_waitingToRespawnRocket = true;
			_rocketRespawnTimer = RocketRespawnDelay;
		}

		// Check for player deaths to end the round
		CheckForPlayerDeaths();
	}

	private void CheckForPlayerDeaths()
	{
		// Player deaths are now handled by events, so this method is no longer needed
		// but kept for potential future use
		if (!_roundActive) return;
	}

	[Rpc.Broadcast]
	private void EndRound(GameObject deadPlayer)
	{
		_roundActive = false;
		_waitingToRespawnRocket = false;
		
		Log.Info($"=== ROUND {CurrentRound} ENDED ===");
		Log.Info($"Round ended because {deadPlayer.Name} died");
		
		// Only the host destroys the rocket, but broadcast the round end
		if (_isHost && _currentRocket.IsValid())
		{
			Log.Info("Destroying rocket due to round end");
			_currentRocket.Destroy();
		}

		// Prepare for next round (only on host)
		if (_isHost)
		{
			CurrentRound++;
			_roundStartTimer = RoundStartDelay;
		}
		
		Log.Info($"Next round ({CurrentRound}) will start in {RoundStartDelay} seconds...");
	}

	[Button("Force Start Round")]
	[Category("Debug")]
	public void ForceStartRound()
	{
		if (!_isHost)
		{
			Log.Warning("Only the host can force start rounds!");
			return;
		}

		if (!_roundActive)
		{
			Log.Info("Forcing round start via button");
			StartRound();
		}
		else
		{
			Log.Warning("Round is already active!");
		}
	}

	[Button("Force End Round")]
	[Category("Debug")]
	public void ForceEndRound()
	{
		if (!_isHost)
		{
			Log.Warning("Only the host can force end rounds!");
			return;
		}

		if (_roundActive)
		{
			Log.Info("Forcing round end via button");
			var firstPlayer = Scene.GetAllComponents<PlayerComponent>()
				.FirstOrDefault(player => player.HealthComponent.IsValid());
			if (firstPlayer != null)
			{
				EndRound(firstPlayer.GameObject);
			}
		}
		else
		{
			Log.Warning("No active round to end!");
		}
	}

	// INetworkListener implementation
	/// <summary>
	/// Called when a client connects. Only called on the host.
	/// </summary>
	public void OnActive(Connection connection)
	{
		// If this method is called, we are the host
		if (!_isHost)
		{
			_isHost = true;
			Log.Info("Detected as HOST - starting round management");
			_roundStartTimer = RoundStartDelay;
			Log.Info($"First round will start in {RoundStartDelay} seconds...");
		}
		
		Log.Info($"Player '{connection.DisplayName}' has joined the game");
	}
}
