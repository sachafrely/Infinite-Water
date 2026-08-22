using System;
using System.Collections.Generic;
using Godot;

internal sealed class WaterWheelManager
{
	public const int MaxWheelCount = 6;
	public const int WheelTileAtlasX = 7;
	public const int WheelTileAtlasY = 6;
	public const float WheelOuterRadius = 45.0f;
	public const float WheelInnerRadius = 12.5f;
	public const int WheelBladeCount = 8;
	public const float WheelBladeWidth = 7.5f;
	private const float CurrentGenerationThreshold = 0.002f;
	private const int PreferredInitialWheelSortedIndex = 2;
	private readonly PbfSolver solver;
	private readonly EnergySystem energySystem;
	private readonly Node2D owner;
	private readonly List<FluidWheelState> wheelStates = new List<FluidWheelState>();
	private readonly List<WaterWheelVisual> wheelVisuals = new List<WaterWheelVisual>();
	private readonly List<Vector2> wheelPositions = new List<Vector2>();
	private readonly bool[] wheelUnlocked = new bool[MaxWheelCount];
	private readonly WheelUpgradeState[] wheelUpgradeStates = new WheelUpgradeState[MaxWheelCount];
	private readonly List<int> activeWheelPositionIndices = new List<int>();
	private float[] previousWheelAngles = Array.Empty<float>();
	private double[] wheelEnergyGeneratedThisFrame = Array.Empty<double>();
	private double energyGeneratedThisFrame;

	public int WheelCount => wheelStates.Count;
	public int UnlockedWheelCount { get { int count = 0; for (int i = 0; i < wheelUnlocked.Length; i++) if (wheelUnlocked[i]) count++; return count; } }
	public double EnergyGeneratedThisFrame => energyGeneratedThisFrame;
	public IReadOnlyList<Vector2> WheelPositions => wheelPositions;

	public WaterWheelManager(PbfSolver solver, EnergySystem energySystem, Node2D owner)
	{
		this.solver = solver; this.energySystem = energySystem; this.owner = owner;
		for (int i = 0; i < MaxWheelCount; i++) wheelUpgradeStates[i] = new WheelUpgradeState();
	}

	public bool IsWheelUnlocked(int index) => index >= 0 && index < wheelPositions.Count && index < MaxWheelCount && wheelUnlocked[index];
	public bool CanUnlockNextWheel() { for (int i = 0; i < wheelPositions.Count && i < MaxWheelCount; i++) if (!wheelUnlocked[i]) return true; return false; }
	public int GetNextLockedWheelIndex() { for (int i = 0; i < wheelPositions.Count && i < MaxWheelCount; i++) if (!wheelUnlocked[i]) return i; return -1; }
	public Vector2 GetWheelPosition(int index) => index >= 0 && index < wheelPositions.Count ? wheelPositions[index] : Vector2.Zero;

	public bool HasAvailableUpgrades(int wheelIndex) => IsWheelUnlocked(wheelIndex) && wheelUpgradeStates[wheelIndex].HasAvailableUpgrade;
	public int GetUpgradeLevel(int wheelIndex, WheelUpgradeType type) => wheelIndex < 0 || wheelIndex >= MaxWheelCount ? 0 : wheelUpgradeStates[wheelIndex].GetLevel(type);
	public int GetUpgradePrice(int wheelIndex, WheelUpgradeType type) => wheelIndex < 0 || wheelIndex >= MaxWheelCount ? 0 : wheelUpgradeStates[wheelIndex].GetPrice(type);

	public bool CanPurchaseUpgrade(int wheelIndex, WheelUpgradeType type)
	{
		if (!IsWheelUnlocked(wheelIndex)) return false;
		int price = GetUpgradePrice(wheelIndex, type);
		return price > 0 && energySystem != null && energySystem.Dollars >= price;
	}

	public bool PurchaseUpgrade(int wheelIndex, WheelUpgradeType type)
	{
		if (!IsWheelUnlocked(wheelIndex)) return false;
		WheelUpgradeState state = wheelUpgradeStates[wheelIndex];
		int price = state.GetPrice(type);
		if (price <= 0 || energySystem == null || energySystem.Dollars < price) return false;
		int activeIndex = GetActiveWheelStateIndex(wheelIndex);
		if (activeIndex < 0 || activeIndex >= wheelStates.Count) return false;
		if (!energySystem.TrySpendDollars(price)) return false;
		if (!state.TryIncrease(type)) { GD.PushError("Wheel upgrade transaction failed after money validation for wheel slot " + wheelIndex + "."); return false; }
		ApplyUpgradeState(wheelIndex, activeIndex);
		return true;
	}

	private int GetActiveWheelStateIndex(int wheelPositionIndex) => activeWheelPositionIndices.IndexOf(wheelPositionIndex);

	private void ApplyUpgradeState(int wheelPositionIndex, int activeIndex)
	{
		if (activeIndex < 0 || activeIndex >= wheelStates.Count) return;
		FluidWheelState wheel = wheelStates[activeIndex];
		WheelUpgradeState state = wheelUpgradeStates[wheelPositionIndex];
		wheel.SetUpgradeLevels(state.BiggerPaddlesLevel, state.LessFrictionLevel, state.MoreEfficientLevel);
		if (activeIndex < wheelVisuals.Count && wheelVisuals[activeIndex] != null)
		{
			// Bigger Paddles: paddle length +15% per level, complete wheel radius +17% per level.
			// Paddle thickness remains exactly the base WheelBladeWidth.
			wheelVisuals[activeIndex].OuterRadius = WheelOuterRadius * wheel.WheelRadiusMultiplier;
			wheelVisuals[activeIndex].BladeWidth = WheelBladeWidth;
		}
	}

	public bool TryUnlockWheel(int wheelPositionIndex)
	{
		if (wheelPositionIndex < 0 || wheelPositionIndex >= wheelPositions.Count || wheelPositionIndex >= MaxWheelCount || wheelUnlocked[wheelPositionIndex]) return false;
		if (energySystem.Dollars < EnergySystem.WheelPurchaseCost) return false;
		if (!ActivateWheel(wheelPositionIndex)) return false;
		if (!energySystem.TrySpendDollars(EnergySystem.WheelPurchaseCost)) { GD.PushError("Wheel activation succeeded but purchase transaction failed."); return false; }
		GD.Print("Wheel purchased: slot " + wheelPositionIndex + ". Active wheels=" + WheelCount + ". Dollars=" + energySystem.Dollars.ToString("F0"));
		return true;
	}

	public bool TryUnlockNextWheel() { int index = GetNextLockedWheelIndex(); return index >= 0 && TryUnlockWheel(index); }

	public void CreateWaterWheelsFromEnvironment(TileMapLayer environment, Func<Vector2, Vector2> toSimulationSpace)
	{
		wheelPositions.Clear(); activeWheelPositionIndices.Clear(); Array.Clear(wheelUnlocked, 0, wheelUnlocked.Length);
		if (environment == null || toSimulationSpace == null) return;
		foreach (Vector2I cell in environment.GetUsedCells())
		{
			if (environment.GetCellSourceId(cell) < 0) continue;
			Vector2I atlasCoords = environment.GetCellAtlasCoords(cell);
			if (atlasCoords.X != WheelTileAtlasX || atlasCoords.Y != WheelTileAtlasY) continue;
			wheelPositions.Add(toSimulationSpace(environment.ToGlobal(environment.MapToLocal(cell))));
		}
		wheelPositions.Sort((a, b) => { int y = a.Y.CompareTo(b.Y); return y != 0 ? y : a.X.CompareTo(b.X); });
		if (wheelPositions.Count > MaxWheelCount) wheelPositions.RemoveRange(MaxWheelCount, wheelPositions.Count - MaxWheelCount);
		if (wheelPositions.Count == 0) return;
		ActivateWheel(Math.Min(PreferredInitialWheelSortedIndex, wheelPositions.Count - 1));
	}

	public bool ActivateWheel(int wheelPositionIndex)
	{
		if (wheelPositionIndex < 0 || wheelPositionIndex >= wheelPositions.Count || wheelPositionIndex >= MaxWheelCount || wheelUnlocked[wheelPositionIndex] || wheelStates.Count >= MaxWheelCount) return false;
		if (!CreateWaterWheel(wheelPositions[wheelPositionIndex])) return false;
		wheelUnlocked[wheelPositionIndex] = true; activeWheelPositionIndices.Add(wheelPositionIndex); ResizeWheelEnergyTrackingForActiveWheel(); ApplyUpgradeState(wheelPositionIndex, wheelStates.Count - 1); return true;
	}

	public bool CreateWaterWheel(Vector2 center)
	{
		if (wheelStates.Count >= MaxWheelCount) return false;
		FluidWheelState wheelState = wheelStates.Count == 0 ? solver.CreateWheel(center) : new FluidWheelState(center);
		wheelStates.Add(wheelState);
		for (int i = 0; i < WheelBladeCount; i++)
		{
			float angle = Mathf.Tau * i / WheelBladeCount;
			Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
			Vector2 tangent = new Vector2(-direction.Y, direction.X);
			Vector2 innerCenter = direction * WheelInnerRadius;
			Vector2 outerCenter = direction * WheelOuterRadius;
			Vector2[] blade = { innerCenter + tangent * WheelBladeWidth, outerCenter + tangent * WheelBladeWidth, outerCenter - tangent * WheelBladeWidth, innerCenter - tangent * WheelBladeWidth };
			FluidPolygonCollider collider = new FluidPolygonCollider(blade);
			collider.ConfigureAsWheel(wheelState, true, WheelInnerRadius, WheelOuterRadius);
			solver.AddPolygonCollider(collider);
		}
		const int hubSegments = 16;
		Vector2[] hub = new Vector2[hubSegments];
		for (int i = 0; i < hubSegments; i++) { float angle = Mathf.Tau * i / hubSegments; hub[i] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * WheelInnerRadius; }
		FluidPolygonCollider hubCollider = new FluidPolygonCollider(hub); hubCollider.ConfigureAsWheel(wheelState); solver.AddPolygonCollider(hubCollider);
		WaterWheelVisual visual = new WaterWheelVisual { Position = center, OuterRadius = WheelOuterRadius, InnerRadius = WheelInnerRadius, BladeCount = WheelBladeCount, BladeWidth = WheelBladeWidth };
		owner.AddChild(visual); visual.SetWheelAngle(wheelState.Angle); wheelVisuals.Add(visual); return true;
	}

	private void ResizeWheelEnergyTrackingForActiveWheel()
	{
		int wheelCount = wheelStates.Count; float[] previousAngles = new float[wheelCount]; double[] frameEnergy = new double[wheelCount]; int previousCount = Math.Min(previousWheelAngles.Length, wheelCount - 1);
		for (int i = 0; i < previousCount; i++) { previousAngles[i] = previousWheelAngles[i]; if (i < wheelEnergyGeneratedThisFrame.Length) frameEnergy[i] = wheelEnergyGeneratedThisFrame[i]; }
		previousAngles[wheelCount - 1] = wheelStates[wheelCount - 1].Angle; previousWheelAngles = previousAngles; wheelEnergyGeneratedThisFrame = frameEnergy;
	}

	public void InitializeWheelEnergyTracking() { previousWheelAngles = new float[wheelStates.Count]; wheelEnergyGeneratedThisFrame = new double[wheelStates.Count]; for (int i = 0; i < wheelStates.Count; i++) previousWheelAngles[i] = wheelStates[i].Angle; }
	public void ResetFrameEnergy() { energyGeneratedThisFrame = 0.0; if (wheelEnergyGeneratedThisFrame.Length != wheelStates.Count) wheelEnergyGeneratedThisFrame = new double[wheelStates.Count]; Array.Clear(wheelEnergyGeneratedThisFrame, 0, wheelEnergyGeneratedThisFrame.Length); }

	public bool UpdateEnergyFromWheelRotation()
	{
		int wheelCount = wheelStates.Count; if (wheelCount <= 0) return false;
		if (previousWheelAngles.Length != wheelCount) { InitializeWheelEnergyTracking(); return false; }
		if (wheelEnergyGeneratedThisFrame.Length != wheelCount) wheelEnergyGeneratedThisFrame = new double[wheelCount];
		bool currentGenerated = false;
		for (int i = 0; i < wheelCount; i++)
		{
			float currentAngle = wheelStates[i].Angle; float angularMovement = Mathf.Abs(Mathf.AngleDifference(previousWheelAngles[i], currentAngle));
			if (angularMovement > 0.0f)
			{
				double frameEnergy = angularMovement * energySystem.EnergyPerRadian * wheelStates[i].EnergyGenerationMultiplier;
				energySystem.AddEnergy(frameEnergy); energyGeneratedThisFrame += frameEnergy; wheelEnergyGeneratedThisFrame[i] += frameEnergy;
			}
			if (angularMovement > CurrentGenerationThreshold) currentGenerated = true;
			previousWheelAngles[i] = currentAngle;
		}
		return currentGenerated;
	}

	public double GetWheelEnergyThisFrame(int wheelIndex) => wheelIndex >= 0 && wheelIndex < wheelEnergyGeneratedThisFrame.Length ? wheelEnergyGeneratedThisFrame[wheelIndex] : 0.0;
	public double GetWheelEnergyPerSecond(int wheelIndex, float delta) => delta <= 0.000001f ? 0.0 : GetWheelEnergyThisFrame(wheelIndex) / delta;
	public double[] CopyWheelEnergyGeneratedThisFrame() { double[] copy = new double[wheelEnergyGeneratedThisFrame.Length]; Array.Copy(wheelEnergyGeneratedThisFrame, copy, wheelEnergyGeneratedThisFrame.Length); return copy; }
	public void StepAdditionalWheels(float dt) { for (int i = 1; i < wheelStates.Count; i++) wheelStates[i].Step(dt); }
	public void StepPrimaryWheel(float dt) { if (wheelStates.Count > 0) wheelStates[0].Step(dt); }
	public void UpdateWheelVisuals() { int count = Math.Min(wheelStates.Count, wheelVisuals.Count); for (int i = 0; i < count; i++) wheelVisuals[i].SetWheelAngle(wheelStates[i].Angle); }
}
