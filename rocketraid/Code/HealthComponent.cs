using Sandbox;
using System.Threading.Tasks;

/// <summary>
/// Optimized health system with events for scalability
/// </summary>
public sealed class HealthComponent : Component
{
	// Events for other systems to listen to
	public static event System.Action<HealthComponent, float, float> OnHealthChanged;
	public static event System.Action<HealthComponent> OnDeath;
	public static event System.Action<HealthComponent> OnRevive;

	[Property]
	[Category("Health")]
	[Range(10f, 500f)]
	[Step(10f)]
	public float MaxHealth { get; set; } = 100f;

	[Property]
	[Category("Health")]
	[Range(0f, 20f)]
	[Step(1f)]
	public float HealthRegeneration { get; set; } = 0f;

	[Property]
	[Category("Components")]
	public SkinnedModelRenderer ModelRenderer { get; set; }

	public bool Alive { get; set; } = true;

	private float _health;
	private TimeUntil _nextRegen;
	private TimeUntil _fadeIn = 1f;
	private TimeUntil _fadeOut;

	public float Health 
	{ 
		get => _health;
		private set
		{
			var oldHealth = _health;
			_health = float.Clamp(value, 0f, MaxHealth);
			
			// Only fire event if health actually changed
			if (oldHealth != _health)
			{
				OnHealthChanged?.Invoke(this, oldHealth, _health);
				
				// Check for death
				if (_health <= 0f && Alive)
				{
					Kill();
				}
			}
		}
	}

	protected override void OnStart()
	{
		_health = MaxHealth;
		
		// Log for debugging
		Log.Info($"HealthComponent initialized with {_health}/{MaxHealth} health");

		GameObject.BreakFromPrefab();
		
		// Ensure health is properly set after a frame
		_ = Task.DelayRealtimeSeconds(0.1f).ContinueWith(_ => 
		{
			if (IsValid)
			{
				Log.Info($"HealthComponent health confirmed: {Health}/{MaxHealth}");
			}
		});
	}

	protected override void OnFixedUpdate()
	{
		// Health regeneration
		if (Alive && HealthRegeneration > 0f && _nextRegen)
		{
			Heal(HealthRegeneration);
			_nextRegen = 1f; // Reset timer to 1 second
		}

		// Handle fade animations
		if (ModelRenderer.IsValid())
		{
			if (!_fadeIn)
				ModelRenderer.Tint = ModelRenderer.Tint.WithAlpha(_fadeIn.Fraction);

			if (!Alive)
				ModelRenderer.Tint = ModelRenderer.Tint.WithAlpha(1f - _fadeOut.Fraction);
		}
	}

	/// <summary>
	/// Damage this health component. Positive values cause damage, negative values heal.
	/// </summary>
	[Rpc.Broadcast]
	public void Damage(float damage)
	{
		if (!Alive) return;

		Log.Info($"Damage called: {damage} (current health: {Health})");
		Health -= damage;
		Log.Info($"Health after damage: {Health}");

		// Reset regen timer on damage
		if (damage > 0f)
			_nextRegen = 5f;

		// Trigger damage animation
		if (damage > 0f && ModelRenderer.IsValid())
		{
			PlayDamageAnimation(damage);
		}
	}
	
	/// <summary>
	/// Damage a specific health component (for targeting specific players)
	/// </summary>
	public static void DamagePlayer(HealthComponent target, float damage)
	{
		if (target?.IsValid() == true)
		{
			Log.Info($"DamagePlayer called: {damage} damage to {target.GameObject.Name}");
			target.Damage(damage);
		}
	}

	/// <summary>
	/// Heal this health component
	/// </summary>
	public void Heal(float amount)
	{
		if (!Alive) return;
		Log.Info($"Heal called: {amount} (current health: {Health})");
		Health += amount;
		Log.Info($"Health after heal: {Health}");
	}

	/// <summary>
	/// Set health to a specific value
	/// </summary>
	public void SetHealth(float newHealth)
	{
		Health = newHealth;
	}

	/// <summary>
	/// Get health as a percentage (0-1)
	/// </summary>
	public float HealthPercentage => MaxHealth > 0 ? Health / MaxHealth : 0f;
	
	/// <summary>
	/// Get current health value
	/// </summary>
	public float CurrentHealth => Health;
	
	/// <summary>
	/// Get maximum health value
	/// </summary>
	public float CurrentMaxHealth => MaxHealth;

	/// <summary>
	/// Kill this unit
	/// </summary>
	[Rpc.Broadcast]
	public void Kill()
	{
		if (!Alive) return;

		Alive = false;
		Health = 0f;
		
		OnDeath?.Invoke(this);
		
		PlayDeathSequence();
	}

	/// <summary>
	/// Revive this unit with full health
	/// </summary>
	[Rpc.Broadcast]
	public void Revive()
	{
		if (Alive) return;

		Alive = true;
		Health = MaxHealth;
		
		if (ModelRenderer.IsValid())
		{
			ModelRenderer.Set("dead", false);
			ModelRenderer.Tint = ModelRenderer.Tint.WithAlpha(1f);
		}
		
		OnRevive?.Invoke(this);
	}

	private async void PlayDamageAnimation(float damage)
	{
		if (!ModelRenderer.IsValid()) return;

		var remappedDamage = MathX.Remap(damage, 0f, MaxHealth, 0f, 100f);
		
		ModelRenderer.LocalScale *= 1.1f;
		ModelRenderer.Tint = Color.Red;

		await Task.DelaySeconds(remappedDamage / 100f);

		if (ModelRenderer.IsValid())
		{
			ModelRenderer.LocalScale /= 1.1f;
			ModelRenderer.Tint = Color.White;
		}
	}

	private async void PlayDeathSequence()
	{
		if (ModelRenderer.IsValid())
		{
			ModelRenderer.Set("dead", true);
			_fadeOut = 1f;
		}

		// Handle player-specific death logic
		var playerComponent = GetComponent<PlayerComponent>();
		if (playerComponent.IsValid())
		{
			playerComponent.OnDeath();
		}

		await Task.DelaySeconds(1f);

		// Handle respawn or destruction
		if (playerComponent.IsValid())
		{
			playerComponent.Respawn();
		}
		else
		{
			// Non-player entities get destroyed
			GameObject.Destroy();
		}
	}

	// Debug buttons
	[Button("Damage 10", "🔪")]
	[Category("Debug")]
	public void DebugDamage()
	{
		Damage(10f);
		ForceRefreshPlayerHUD();
	}

	[Button("Heal 10", "💊")]
	[Category("Debug")]
	public void DebugHeal()
	{
		Heal(10f);
		ForceRefreshPlayerHUD();
	}

	[Button("Kill", "⚔️")]
	[Category("Debug")]
	public void DebugKill()
	{
		Kill();
		ForceRefreshPlayerHUD();
	}
	
	[Button("Debug Connect HUD", "🔗")]
	[Category("Debug")]
	public void DebugConnectHUD()
	{
		Log.Info("DebugConnectHUD called - attempting to connect PlayerHUD...");
		
		// Try to find and connect any PlayerHUD
		var playerHUDs = Scene.GetAllComponents<PlayerHud>();
		foreach (var hud in playerHUDs)
		{
			if (hud.IsValid())
			{
				Log.Info($"Found PlayerHUD: {hud.GetType().Name}");
				hud.DebugConnectToAnyPlayer();
			}
		}
		
		Log.Info($"DebugConnectHUD completed - found {playerHUDs.Count()} PlayerHUDs");
	}
	
	private void ForceRefreshPlayerHUD()
	{
		// Force refresh by triggering a health change event
		// This will cause the PlayerHUD to detect the change and update
		Log.Info($"ForceRefreshPlayerHUD called - current health: {Health}/{MaxHealth}");
		
		// Trigger the health changed event to notify the HUD
		OnHealthChanged?.Invoke(this, Health, Health);
	}
}