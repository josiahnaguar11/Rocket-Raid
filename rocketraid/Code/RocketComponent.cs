using Sandbox;

public sealed class RocketComponent : Component
{
	[Property]
	[Category("Movement")]
	[Range(100f, 1000f)]
	[Step(50f)]
	public float Speed { get; set; } = 300f;

	[Property]
	[Category("Movement")]
	[Range(1f, 10f)]
	[Step(0.5f)]
	public float TurnSpeed { get; set; } = 3f;

	[Property]
	[Category("Combat")]
	[Range(1f, 50f)]
	[Step(1f)]
	public float Damage { get; set; } = 10f;

	[Property]
	[Category("Combat")]
	public Collider RocketCollider { get; set; }



	private GameObject _target;
	private GameObject _currentHitCenter;
	private bool _hasExploded = false;

	protected override void OnStart()
	{
		// Find the player target by tag
		FindPlayerTarget();
	}

	protected override void OnUpdate()
	{
		if (_hasExploded) return;
		
		// Home towards the target if we have one
		if (_target.IsValid())
		{
			HomeTowardsTarget();
		}
		else
		{
			// If target is lost, try to find it again
			FindPlayerTarget();
		}

		// Check for collision with player
		CheckCollision();
	}

	private void FindPlayerTarget()
	{
		// Find all alive players
		var players = Scene.GetAllComponents<PlayerComponent>()
			.Where(player => player.HealthComponent.IsValid() && player.HealthComponent.Alive)
			.Select(player => player.GameObject)
			.ToArray();

		if (players.Length == 0)
		{
			Log.Warning("No players found to target!");
			return;
		}

		// Select a random player
		var randomIndex = Game.Random.Int(0, players.Length - 1);
		var selectedPlayer = players[randomIndex];
		
		_target = selectedPlayer;
		
		// Try to find the hit center child object
		_currentHitCenter = FindHitCenter(selectedPlayer);
		
		if (_currentHitCenter.IsValid())
		{
			Log.Info($"Rocket found player target: {selectedPlayer.Name} with hit center: {_currentHitCenter.Name}");
		}
		else
		{
			Log.Info($"Rocket found player target: {selectedPlayer.Name} (no hit center found, using player root)");
		}
	}

	private GameObject FindHitCenter(GameObject player)
	{
		// Search for a child object named "hitcentre" or "hitcenter" (case insensitive)
		var hitCenter = player.Children.FirstOrDefault(child => 
		{
			var childName = child.Name.ToLower();
			return childName.Contains("hitcentre") || childName.Contains("hitcenter");
		});
			
		return hitCenter;
	}

	[Rpc.Broadcast]
	public void RedirectToOtherPlayer(GameObject currentTarget)
	{
		Log.Info($"RedirectToOtherPlayer called. Current target: {(_target.IsValid() ? _target.Name : "None")}, Punching player: {currentTarget.Name}");
		
		// Find all alive players
		var players = Scene.GetAllComponents<PlayerComponent>()
			.Where(player => player.HealthComponent.IsValid() && player.HealthComponent.Alive)
			.Select(player => player.GameObject)
			.ToArray();
		
		GameObject newTarget = null;
		
		Log.Info($"Found {players.Length} players in scene");
		
		foreach (var playerObject in players)
		{
			Log.Info($"Checking player: {playerObject.Name}, Is current target: {playerObject == _target}, Is punching player: {playerObject == currentTarget}");
			
			// Find a player that is not the current target
			if (playerObject != _target)
			{
				newTarget = playerObject;
				Log.Info($"Selected new target: {newTarget.Name}");
				break;
			}
		}
		
		if (newTarget.IsValid())
		{
			_target = newTarget;
			
			// Find the hit center for the new target
			_currentHitCenter = FindHitCenter(newTarget);
			
			if (_currentHitCenter.IsValid())
			{
				Log.Info($"Rocket successfully redirected to: {newTarget.Name} with hit center: {_currentHitCenter.Name}");
			}
			else
			{
				Log.Info($"Rocket successfully redirected to: {newTarget.Name} (no hit center found, using player root)");
			}
			
			// Add some visual feedback - small boost in speed
			Speed *= 1.1f;
		}
		else
		{
			Log.Warning("No other player found to redirect rocket to!");
			// Debug: List all players found
			foreach (var playerObject in players)
			{
				Log.Info($"Available player: {playerObject.Name}");
			}
		}
	}

	private void HomeTowardsTarget()
	{
		if (!_target.IsValid()) return;

		// Use hit center if available, otherwise use player root position
		var targetPosition = _currentHitCenter.IsValid() ? _currentHitCenter.WorldPosition : _target.WorldPosition;
		var currentPosition = WorldPosition;
		var direction = (targetPosition - currentPosition).Normal;

		// Smoothly rotate towards target
		var targetRotation = Rotation.LookAt(direction);
		WorldRotation = Rotation.Slerp(WorldRotation, targetRotation, Time.Delta * TurnSpeed);

		// Move forward in the direction we're facing
		var forwardDirection = WorldRotation.Forward;
		WorldPosition += forwardDirection * Speed * Time.Delta;
	}

	private void CheckCollision()
	{
		if (!_target.IsValid()) return;

		// Use hit center if available, otherwise use player root position
		var targetPosition = _currentHitCenter.IsValid() ? _currentHitCenter.WorldPosition : _target.WorldPosition;
		
		// Get collision radius from the rocket's collider bounds
		float collisionRadius = GetCollisionRadius();
		
		// Check distance-based collision
		var distanceToTarget = Vector3.DistanceBetween(WorldPosition, targetPosition);
		if (distanceToTarget <= collisionRadius)
		{
			HitPlayer();
		}
	}

	private float GetCollisionRadius()
	{
		// If we have a collider, try to get its approximate size
		if (RocketCollider.IsValid())
		{
			// Different collider types have different ways to get their size
			if (RocketCollider is SphereCollider sphereCollider)
			{
				return sphereCollider.Radius;
			}
			else if (RocketCollider is BoxCollider boxCollider)
			{
				var scale = boxCollider.Scale;
				// Get the maximum dimension and divide by 2 for radius
				float maxDimension = scale.x;
				if (scale.y > maxDimension) maxDimension = scale.y;
				if (scale.z > maxDimension) maxDimension = scale.z;
				return maxDimension * 0.5f;
			}
			else if (RocketCollider is CapsuleCollider capsuleCollider)
			{
				// Calculate height from Start and End points
				var height = Vector3.DistanceBetween(capsuleCollider.Start, capsuleCollider.End);
				var halfHeight = height * 0.5f;
				// Return the larger of radius or half-height
				return capsuleCollider.Radius > halfHeight ? capsuleCollider.Radius : halfHeight;
			}
			else
			{
				// For other collider types, use a reasonable default
				return 15f;
			}
		}
		
		// Fallback to a default radius if no collider is set
		return 20f;
	}

	private void HitPlayer()
	{
		if (_hasExploded) return;
		_hasExploded = true;

		Log.Info($"Rocket hit player! Dealing {Damage} damage");

		// Only apply damage on the host to prevent double-damage
		if (GameObject.Network.IsOwner)
		{
			// Damage the specific target player only
			var healthComponent = _target.Components.Get<HealthComponent>();
			if (healthComponent.IsValid())
			{
				healthComponent.Damage(Damage);
			}
			else
			{
				Log.Warning($"No HealthComponent found on target {_target.Name}!");
			}
		}

		// Destroy the rocket
		GameObject.Destroy();
	}
}
