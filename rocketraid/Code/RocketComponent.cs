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
	[Range(10f, 100f)]
	[Step(5f)]
	public float CollisionRadius { get; set; } = 20f;

	private GameObject _target;
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
		// Find all objects in the scene and look for one with "player" tag
		var allObjects = Scene.GetAllObjects(true);
		foreach (var obj in allObjects)
		{
			if (obj.Tags.Has("player"))
			{
				_target = obj;
				Log.Info($"Rocket found player target: {obj.Name}");
				break;
			}
		}
	}

	[Rpc.Broadcast]
	public void RedirectToOtherPlayer(GameObject currentTarget)
	{
		Log.Info($"RedirectToOtherPlayer called. Current target: {(_target.IsValid() ? _target.Name : "None")}, Punching player: {currentTarget.Name}");
		
		// Find all players with SnotPlayerComponent (more reliable than tags)
		var allPlayers = Scene.GetAllComponents<SnotPlayerComponent>().ToArray();
		GameObject newTarget = null;
		
		Log.Info($"Found {allPlayers.Length} players in scene");
		
		foreach (var playerComponent in allPlayers)
		{
			var playerObject = playerComponent.GameObject;
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
			Log.Info($"Rocket successfully redirected to: {newTarget.Name}");
			
			// Add some visual feedback - small boost in speed
			Speed *= 1.1f;
		}
		else
		{
			Log.Warning("No other player found to redirect rocket to!");
			// Debug: List all players found
			foreach (var playerComponent in allPlayers)
			{
				Log.Info($"Available player: {playerComponent.GameObject.Name}");
			}
		}
	}

	private void HomeTowardsTarget()
	{
		if (!_target.IsValid()) return;

		var targetPosition = _target.WorldPosition;
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

		var distanceToTarget = Vector3.DistanceBetween(WorldPosition, _target.WorldPosition);
		
		if (distanceToTarget <= CollisionRadius)
		{
			HitPlayer();
		}
	}

	[Rpc.Broadcast]
	private void HitPlayer()
	{
		if (_hasExploded) return;
		_hasExploded = true;

		Log.Info($"Rocket hit player! Dealing {Damage} damage");

		// Try to damage the player through UnitComponent
		var unitComponent = _target.Components.Get<UnitComponent>();
		if (unitComponent.IsValid())
		{
			unitComponent.Damage(Damage);
		}

		// Destroy the rocket
		GameObject.Destroy();
	}
}
