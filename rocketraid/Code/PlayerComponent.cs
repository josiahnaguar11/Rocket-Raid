using Sandbox;

/// <summary>
/// Optimized PlayerComponent - replaces SnotPlayerComponent with better architecture
/// </summary>
public sealed class PlayerComponent : Component
{
	[Property]
	[Category("Components")]
	public PlayerController Controller { get; set; }

	[Property]
	[Category("Components")]
	public SkinnedModelRenderer ModelRenderer { get; set; }

	[Property]
	[Category("Components")]
	public HealthComponent HealthComponent { get; set; }

	[Property]
	[Category("Components")]
	public TeamComponent TeamComponent { get; set; }

	[Property]
	[Category("Components")]
	public CurrencyComponent CurrencyComponent { get; set; }

	[Property]
	[Category("Combat")]
	[Range(50f, 200f)]
	[Step(10f)]
	public float PunchRange { get; set; } = 100f;
	
	[Property]
	[Category("Combat")]
	[Range(5f, 50f)]
	[Step(5f)]
	public float PunchDamage { get; set; } = 10f;
	
	[Property]
	[Category("Combat")]
	[Range(0f, 2f)]
	[Step(0.1f)]
	public float PunchCooldown { get; set; } = 0.5f;

	public TimeUntil NextPunch;
	private ModelPhysics _ragdoll;
	private Vector3 _spawnPosition;
	private TimeUntil _resetPose;

	// Legacy property for backwards compatibility
	public UnitComponent UnitComponent 
	{ 
		get 
		{
			// For backwards compatibility, try to find old UnitComponent
			return GetComponent<UnitComponent>();
		}
	}

	protected override void OnStart()
	{
		_spawnPosition = WorldPosition;

		// Auto-create components if they don't exist
		if (!HealthComponent.IsValid())
		{
			HealthComponent = AddComponent<HealthComponent>();
		}
		
		// Always link the ModelRenderer to ensure it's connected
		if (HealthComponent.IsValid() && ModelRenderer.IsValid())
		{
			HealthComponent.ModelRenderer = ModelRenderer;
		}

		if (!TeamComponent.IsValid())
		{
			TeamComponent = AddComponent<TeamComponent>();
			TeamComponent.Team = TeamType.Player;
		}
		
		// Always link the ModelRenderer to ensure it's connected
		if (TeamComponent.IsValid() && ModelRenderer.IsValid())
		{
			TeamComponent.ModelRenderer = ModelRenderer;
		}

		if (!CurrencyComponent.IsValid())
		{
			CurrencyComponent = AddComponent<CurrencyComponent>();
		}

		// Subscribe to health events
		HealthComponent.OnDeath += OnHealthDeath;
		
		// Debug logging - wait a frame to ensure components are fully initialized
		_ = Task.DelayRealtimeSeconds(0.1f).ContinueWith(_ => 
		{
			if (HealthComponent.IsValid())
			{
				Log.Info($"PlayerComponent fully initialized - Health: {HealthComponent.Health}/{HealthComponent.MaxHealth}, Team: {TeamComponent.Team}");
			}
		});
	}

	protected override void OnDestroy()
	{
		// Clean up event subscriptions
		if (HealthComponent.IsValid())
		{
			HealthComponent.OnDeath -= OnHealthDeath;
		}
	}

	protected override void OnFixedUpdate()
	{
		// Reset pose animation should run on all clients
		if (_resetPose)
			ModelRenderer.Set("holdtype", 0);

		// Only process input if this is our player
		if (!GameObject.Network.IsOwner)
			return;

		if (Input.Pressed("Attack1") && NextPunch)
		{
			Punch();
			NextPunch = PunchCooldown;
			Log.Info("Punch");
		}
	}

	[Rpc.Broadcast]
	public void Punch()
	{
		// Play animation on all clients
		PlayPunchAnimation();

		// Only the owner processes the actual punch logic
		if (!GameObject.Network.IsOwner)
			return;

		var punchDirection = Controller.EyeAngles.Forward;
		var punchStart = Controller.EyePosition;
		var punchEnd = punchStart + punchDirection * PunchRange;

		// Draw debug line for the punch ray
		DebugOverlay.Line(punchStart, punchEnd, Color.Yellow, 0.5f);

		var punchTrace = Scene.Trace.Ray(punchStart, punchEnd)
			.Radius(20f)
			.WithoutTags("player")
			.IgnoreGameObjectHierarchy(GameObject)
			.Run();

		// Draw debug sphere at the hit position if we hit something
		if (punchTrace.Hit)
		{
			DebugOverlay.Sphere(new Sphere(punchTrace.HitPosition, 20f), Color.Red, 0.5f);
		}

		if (!punchTrace.Hit) return;

		// Check if we hit a rocket and redirect it
		if (punchTrace.GameObject.Components.TryGet<RocketComponent>(out var rocket))
		{
			Log.Info("Punched and redirected a rocket!");
			rocket.RedirectToOtherPlayer(GameObject);
			
			// Award experience for rocket redirection
			if (CurrencyComponent.IsValid())
			{
				CurrencyComponent.AddExperience(10);
			}
			return;
		}

		// Check if we hit a unit and damage it
		var targetHealth = punchTrace.GameObject.Components.Get<HealthComponent>();
		var targetTeam = punchTrace.GameObject.Components.Get<TeamComponent>();
		
		if (targetHealth.IsValid() && targetTeam.IsValid())
		{
			// Don't damage teammates
			if (TeamComponent.IsValid() && TeamComponent.IsSameTeam(targetTeam))
				return;

			targetHealth.Damage(PunchDamage);
			
			// Award experience and coins for dealing damage
			if (CurrencyComponent.IsValid())
			{
				CurrencyComponent.AddExperience(5);
				
				// Award coins if we killed the target
				if (!targetHealth.Alive)
				{
					CurrencyComponent.AddCoins(10);
					CurrencyComponent.AddExperience(25); // Bonus XP for kills
				}
			}
		}
		else
		{
			// Legacy support - check for old UnitComponent
			if (punchTrace.GameObject.Components.TryGet<UnitComponent>(out var unit))
			{
				if (TeamComponent.IsValid() && unit.Team == TeamComponent.Team)
					return;

				unit.Damage(PunchDamage);
			}
		}
	}

	private void PlayPunchAnimation()
	{
		ModelRenderer.Set("holdtype", 5);
		ModelRenderer.Set("b_attack", true);
		_resetPose = 3f;
	}

	/// <summary>
	/// Called when the health component triggers death
	/// </summary>
	public void OnDeath()
	{
		Ragdoll();
	}

	[Button]
	public void Ragdoll()
	{
		if (!ModelRenderer.IsValid()) return;
		if (_ragdoll.IsValid()) return;

		_ragdoll = AddComponent<ModelPhysics>();
		_ragdoll.Renderer = ModelRenderer;
		_ragdoll.Model = ModelRenderer.Model;

		Controller.UseInputControls = false;
	}

	[Button]
	public void Unragdoll()
	{
		if (!ModelRenderer.IsValid()) return;
		if (!_ragdoll.IsValid()) return;

		_ragdoll.Destroy();
		Controller.UseInputControls = true;
	}

	public void Respawn()
	{
		Unragdoll();
		
		if (HealthComponent.IsValid())
		{
			HealthComponent.Revive();
		}
		
		WorldPosition = _spawnPosition;
		ModelRenderer.Tint = ModelRenderer.Tint.WithAlpha(1f);
		
		Log.Info($"{GameObject.Name} respawned");
	}

	/// <summary>
	/// Called by HealthComponent when this player dies
	/// </summary>
	private void OnHealthDeath(HealthComponent health)
	{
		Log.Info($"Player {GameObject.Name} died");
		// Additional death logic can go here
	}
}