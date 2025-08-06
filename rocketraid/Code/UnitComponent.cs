using Sandbox;

public enum TeamType{
	[Icon("🎮")]
	[Description("Player team - human players")]
	Player,
	[Icon("🤢")]
	[Description("Enemy team - AI enemies")]
	Enemy,
	[Icon("🤖")]
	[Description("Neutral team - turrets, NPCs")]
	Neutral
}

public sealed class UnitComponent : Component
{

	[Property]
	[Category( "INFO" )]
	public string Name { get; set; }

	[Property]
	[Category( "INFO" )]
	public TeamType Team { get; set; }

	[Property]
	[Category( "HEALTH" )]
	[Range( 10f, 300f )]
	[Step( 10f )]
	public float MaxHealth { get; set; } = 100f;

	[Property]
	[Category( "HEALTH" )]
	[Range( 0f, 10f )]
	[Step( 1f )]
	public float HealthRegeneration { get; set; } = 0f;

	[Property]
	[Category( "COMPONENT" )]
	public SkinnedModelRenderer ModelRenderer { get; set; }

	public bool Alive = true;

	private float _health;

	public float Health
	{
		get
		{
			return _health;
		}

		set
		{
			UpdateHealth( value );
		}
	}

	private TimeUntil _nextRegen;
	private TimeUntil _fadeIn = 1f;
	private TimeUntil _fadeOut;

	protected override void OnUpdate()
	{
		if ( !Alive ) return;
		if ( !ModelRenderer.IsValid() ) return;

		if ( Team == TeamType.Enemy )
		{
			var remappedHealth = MathX.Remap( Health, 0f, MaxHealth, 0f, 100f );
			var currentHealth = ModelRenderer.GetFloat( "health" );
			var lerpedHealth = MathX.Lerp( currentHealth, remappedHealth, Time.Delta * 2f );
			ModelRenderer.Set( "health", lerpedHealth );
		}

		DebugOverlay.Text( WorldPosition + Vector3.Up * 80f, $"{Name} [{Health}/{MaxHealth}]" );
	}

	protected override void OnStart()
	{
		_health = MaxHealth;

		if ( ModelRenderer.IsValid() )
			ModelRenderer.Tint= ModelRenderer.Tint.WithAlpha( 0f );

		GameObject.BreakFromPrefab();
	}

	protected override void OnFixedUpdate()
	{
		if ( Alive )
		{
			if ( _nextRegen )
			{
				Damage( -HealthRegeneration ); // Heal
				_nextRegen = 1f; // Reset timer to 1 second
			}
		}


		if ( ModelRenderer.IsValid() )
		{
			if ( !_fadeIn )
				ModelRenderer.Tint = ModelRenderer.Tint.WithAlpha( _fadeIn.Fraction );

			if ( !Alive )
				ModelRenderer.Tint = ModelRenderer.Tint.WithAlpha( 1f- _fadeOut.Fraction );
		}
	}

	[Button( "Hurt 10", "🔪" )]
	[Category( "HEALTH" )]
	public void HurtDebug()
	{
		Damage( 10f );
	}

	[Button( "Heal 10", "💊" )]
	[Category( "HEALTH" )]
	public void HealDebug()
	{
		Damage( -10f );
	}

	[Button( "Hurt a lot", "⚔️" )]
	[Category( "HEALTH" )]
	public void HurtLotDebug()
	{
		Damage( 30f );
	}

	/// <summary>
	/// Positive = hurt, Negative = heal
	/// </summary>
	/// <param name="damage"></param>
	[Rpc.Broadcast]
	public void Damage( float damage )
	{
		if ( !Alive ) return;

		Health -= damage;

		if ( damage >= 0f )
			_nextRegen = 5f;
	}

	private void UpdateHealth( float newHealth )
	{
		var difference = newHealth - Health;
		_health = float.Clamp( newHealth, 0f, MaxHealth );

		if ( difference < 0f )
		{
			var remappedDamage = MathX.Remap( -difference, 0f, MaxHealth, 0f, 100f );
			DamageAnimation( remappedDamage );
		}

		if ( Health <= 0f )
			Kill();
	}

	private async void DamageAnimation( float damage )
	{
		ModelRenderer.LocalScale *= 1.1f;
		ModelRenderer.Tint = Color.Red;

		await Task.DelaySeconds( damage / 100f );

		ModelRenderer.LocalScale /= 1.1f;
		ModelRenderer.Tint = Color.White;
	}

	private void DeathAnimation()
	{
		ModelRenderer.Set( "dead", true );
		_fadeOut = 1f;
	}

	public async void Kill()
	{
		Alive = false;
		DeathAnimation();

		var playerComponent = GetComponent<SnotPlayerComponent>();

		if ( playerComponent.IsValid() )
			playerComponent.Ragdoll();

		await Task.DelaySeconds( 1f );

		if(playerComponent.IsValid())
			playerComponent.Respawn();
		else
			GameObject.Destroy();
	}
}
