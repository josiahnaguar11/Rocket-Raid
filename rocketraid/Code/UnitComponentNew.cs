using Sandbox;

/// <summary>
/// Improved UnitComponent - acts as a bridge to the new component system
/// This maintains compatibility while using the new optimized architecture
/// </summary>
public sealed class UnitComponentNew : Component
{
	[Property]
	[Category("INFO")]
	public string Name { get; set; }

	[Property]
	[Category("Components")]
	public HealthComponent HealthComponent { get; set; }

	[Property]
	[Category("Components")]
	public TeamComponent TeamComponent { get; set; }

	[Property]
	[Category("Components")]
	public SkinnedModelRenderer ModelRenderer { get; set; }

	// Legacy properties for backwards compatibility
	public TeamType Team 
	{ 
		get => TeamComponent?.Team ?? TeamType.Player;
		set { if (TeamComponent.IsValid()) TeamComponent.Team = value; }
	}

	public float MaxHealth 
	{ 
		get => HealthComponent?.MaxHealth ?? 100f;
		set { if (HealthComponent.IsValid()) HealthComponent.MaxHealth = value; }
	}

	public float HealthRegeneration 
	{ 
		get => HealthComponent?.HealthRegeneration ?? 0f;
		set { if (HealthComponent.IsValid()) HealthComponent.HealthRegeneration = value; }
	}

	public bool Alive 
	{ 
		get => HealthComponent?.Alive ?? true;
		set { if (HealthComponent.IsValid()) HealthComponent.Alive = value; }
	}

	public float Health 
	{ 
		get => HealthComponent?.Health ?? 100f;
		set { if (HealthComponent.IsValid()) HealthComponent.SetHealth(value); }
	}

	protected override void OnStart()
	{
		// Auto-create components if they don't exist
		if (!HealthComponent.IsValid())
		{
			HealthComponent = AddComponent<HealthComponent>();
			HealthComponent.ModelRenderer = ModelRenderer;
		}

		if (!TeamComponent.IsValid())
		{
			TeamComponent = AddComponent<TeamComponent>();
			TeamComponent.ModelRenderer = ModelRenderer;
		}

		// Update Enemy-specific model animations when health changes
		if (TeamComponent.Team == TeamType.Enemy)
		{
			HealthComponent.OnHealthChanged += OnHealthChangedForEnemy;
		}

		GameObject.BreakFromPrefab();
	}

	protected override void OnDestroy()
	{
		// Clean up event subscriptions
		if (HealthComponent.IsValid())
		{
			HealthComponent.OnHealthChanged -= OnHealthChangedForEnemy;
		}
	}

	/// <summary>
	/// Legacy damage method for backwards compatibility
	/// </summary>
	[Rpc.Broadcast]
	public void Damage(float damage)
	{
		if (HealthComponent.IsValid())
		{
			HealthComponent.Damage(damage);
		}
	}

	/// <summary>
	/// Handle Enemy-specific health animations
	/// </summary>
	private void OnHealthChangedForEnemy(HealthComponent health, float oldHealth, float newHealth)
	{
		if (!ModelRenderer.IsValid() || TeamComponent.Team != TeamType.Enemy) return;

		var remappedHealth = MathX.Remap(newHealth, 0f, health.MaxHealth, 0f, 100f);
		ModelRenderer.Set("health", remappedHealth);
	}

	// Debug buttons for backwards compatibility
	[Button("Hurt 10", "🔪")]
	[Category("Debug")]
	public void HurtDebug()
	{
		Damage(10f);
	}

	[Button("Heal 10", "💊")]
	[Category("Debug")]
	public void HealDebug()
	{
		if (HealthComponent.IsValid())
		{
			HealthComponent.Heal(10f);
		}
	}

	[Button("Kill", "⚔️")]
	[Category("Debug")]
	public void KillDebug()
	{
		if (HealthComponent.IsValid())
		{
			HealthComponent.Kill();
		}
	}
}