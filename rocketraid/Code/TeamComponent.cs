using Sandbox;

/// <summary>
/// Manages team assignment and team-based logic
/// </summary>
public sealed class TeamComponent : Component
{
	// Events for team changes
	public static event System.Action<TeamComponent, TeamType, TeamType> OnTeamChanged;
	public static event System.Action<TeamComponent> OnTeamAssigned;

	[Property]
	[Category("Team")]
	public TeamType Team { get; set; } = TeamType.Player;

	[Property]
	[Category("Team")]
	public string TeamName { get; set; } = "Default Team";

	[Property]
	[Category("Team")]
	public Color TeamColor { get; set; } = Color.Blue;

	[Property]
	[Category("Components")]
	public SkinnedModelRenderer ModelRenderer { get; set; }

	private TeamType _previousTeam;

	protected override void OnStart()
	{
		_previousTeam = Team;
		OnTeamAssigned?.Invoke(this);
		ApplyTeamVisuals();
	}

	protected override void OnUpdate()
	{
		// Check for team changes
		if (_previousTeam != Team)
		{
			var oldTeam = _previousTeam;
			_previousTeam = Team;
			
			OnTeamChanged?.Invoke(this, oldTeam, Team);
			ApplyTeamVisuals();
			
			Log.Info($"{GameObject.Name} changed from team {oldTeam} to {Team}");
		}
	}

	/// <summary>
	/// Change this entity's team
	/// </summary>
	[Rpc.Broadcast]
	public void ChangeTeam(TeamType newTeam)
	{
		var oldTeam = Team;
		Team = newTeam;
		
		OnTeamChanged?.Invoke(this, oldTeam, newTeam);
		ApplyTeamVisuals();
		
		Log.Info($"{GameObject.Name} switched from {oldTeam} to {newTeam}");
	}

	/// <summary>
	/// Check if this entity is on the same team as another
	/// </summary>
	public bool IsSameTeam(TeamComponent other)
	{
		return other != null && other.Team == Team;
	}

	/// <summary>
	/// Check if this entity is on the same team as a specific team type
	/// </summary>
	public bool IsOnTeam(TeamType teamType)
	{
		return Team == teamType;
	}

	/// <summary>
	/// Check if this entity is an enemy of another
	/// </summary>
	public bool IsEnemyOf(TeamComponent other)
	{
		if (other == null) return false;
		
		// Neutral team is not enemy of anyone
		if (Team == TeamType.Neutral || other.Team == TeamType.Neutral)
			return false;
			
		return Team != other.Team;
	}

	/// <summary>
	/// Get all entities on the same team
	/// </summary>
	public IEnumerable<TeamComponent> GetTeammates()
	{
		return Scene.GetAllComponents<TeamComponent>()
			.Where(tc => tc != this && tc.Team == Team);
	}

	/// <summary>
	/// Get all entities on enemy teams
	/// </summary>
	public IEnumerable<TeamComponent> GetEnemies()
	{
		return Scene.GetAllComponents<TeamComponent>()
			.Where(tc => tc != this && IsEnemyOf(tc));
	}

	/// <summary>
	/// Apply team-specific visual changes
	/// </summary>
	private void ApplyTeamVisuals()
	{
		if (!ModelRenderer.IsValid()) return;

		// Apply team color tint
		var currentTint = ModelRenderer.Tint;
		ModelRenderer.Tint = Color.Lerp(currentTint, TeamColor, 0.3f);

		// You could add team-specific materials, decals, etc. here
		switch (Team)
		{
			case TeamType.Player:
				// Player team visuals
				break;
			case TeamType.Enemy:
				// Enemy team visuals
				break;
			case TeamType.Neutral:
				// Neutral team visuals
				break;
		}
	}

	// Debug buttons
	[Button("Switch to Player Team", "🎮")]
	[Category("Debug")]
	public void DebugSwitchToPlayer()
	{
		ChangeTeam(TeamType.Player);
	}

	[Button("Switch to Enemy Team", "🤢")]
	[Category("Debug")]
	public void DebugSwitchToEnemy()
	{
		ChangeTeam(TeamType.Enemy);
	}

	[Button("Switch to Neutral Team", "🤖")]
	[Category("Debug")]
	public void DebugSwitchToNeutral()
	{
		ChangeTeam(TeamType.Neutral);
	}
}