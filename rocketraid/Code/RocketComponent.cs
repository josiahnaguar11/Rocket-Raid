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
