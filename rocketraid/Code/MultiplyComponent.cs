using Sandbox;

public sealed class MultiplyComponent : Component
{
	[Property]
	public RangedFloat Cooldown { get; set; } = new RangedFloat( 2f, 3f );

	[Property]
	public PrefabScene PrefabToClone { get; set; }

	private TimeUntil _nextClone;

	protected override void OnStart()
	{
		ResetTimer();

		var foundObjects = Scene.FindInPhysics( new Sphere( WorldPosition, 100f ) );

		foreach ( var gameObject in foundObjects )
		{
			if ( gameObject.Components.TryGet<SnotPlayerComponent>( out var player ) )
			{
				player.UnitComponent.Damage( 50f );
				DestroyGameObject();
			}
		}
	}

	protected override void OnFixedUpdate()
	{
		if ( _nextClone )
		{
			Multiply();
			ResetTimer();
		}
	}

	public void Multiply()
	{
		if (!PrefabToClone.IsValid()) return;

		var randomDirection = (Vector3)Game.Random.VectorInCircle().Normal;
		var startPos = WorldPosition + Vector3.Up * 20f;
		var endPos = startPos + randomDirection * 100f;
		var traceCheck = Scene.Trace.Ray( startPos, endPos )
			.Radius( 10f )
			.WithoutTags( "player" )
			.IgnoreGameObjectHierarchy( GameObject )
			.Run();

		if ( !traceCheck.Hit )
		{
			var spawnPos = traceCheck.EndPosition + Vector3.Down * 20f;
			var spawned = PrefabToClone.Clone( spawnPos );
			
			// Make sure the spawned object is networked for all players
			if ( spawned.IsValid() )
			{
				spawned.NetworkSpawn();
			}
		}
	}

	private void ResetTimer()
	{
		_nextClone = Cooldown.GetValue();
	}
}
