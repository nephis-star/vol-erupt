using UnityEngine;

public enum GameState
{
    Starting,
    Exploration,
    Eruption,
    Earthquake,
    TVAvailable,
    TVWarning,
    Preparation,
    HomePrepared,
    EvacuationPrep,
    WaterSafety,
    EvacuationReady,
    Completed
}

public class GameStateManager : MonoBehaviour
{
    public static GameStateManager Instance { get; private set; }
    public static event System.Action<GameState> OnStateChanged;

    public GameState currentState = GameState.Starting;

    public GameState CurrentState
    {
        get { return currentState; }
    }

    void Awake()
    {
        Instance = this;
    }

    public void SetState(GameState newState)
    {
        if (currentState == newState) return;
        currentState = newState;
        if (OnStateChanged != null) OnStateChanged(newState);
    }
}