using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ObjectiveManager : MonoBehaviour
{
    public static ObjectiveManager Instance { get; private set; }
    public static event System.Action TasksChanged;

    [Tooltip("Delay (seconds) between the HOME PREPARED message and the thirst prompt.")]
    public float homeCompletionDelay = 3f;

    [Tooltip("How long the 'He's thirsty. Drink some water.' prompt stays on screen before the water choice must be made.")]
    public float dangerWarningDuration = 6f;

    [Tooltip("If no water is drunk within this many seconds, the water step is skipped so the story never blocks.")]
    public float waterChoiceTimeout = 15f;

    [Tooltip("Fallback: if the player still has not walked to the exit, complete evacuation after this many seconds.")]
    public float evacuationFallbackTime = 12f;

    [Tooltip("Fallback: if the player has not watched the TV news within this many seconds after HOME PREPARED, the story continues anyway.")]
    public float newsWatchTimeout = 45f;

    [System.Serializable]
    public class TaskEntry
    {
        public string id;
        public string displayName;
        public string label;
        public bool done;

        [Tooltip("Optional steps (e.g. sealing tape) do not block HOME PREPARED or the flow.")]
        public bool optional;
        public TaskEntry(string id, string displayName)
        {
            this.id = id;
            this.displayName = displayName;
            this.label = displayName;
        }
        public TaskEntry(string id, string displayName, string label)
        {
            this.id = id;
            this.displayName = displayName;
            this.label = label;
        }
    }

    public readonly List<TaskEntry> equipment = new List<TaskEntry>();
    public readonly List<TaskEntry> preparations = new List<TaskEntry>();
    public readonly List<TaskEntry> activeTasks = new List<TaskEntry>();
    public readonly List<TaskEntry> safetySteps = new List<TaskEntry>();
    string evacuationBagId;

    public bool preparationStarted { get; private set; }
    public bool homePrepared { get; private set; }
    public bool secondAnnouncementReady { get; private set; }
    public bool equipmentEnabled { get; private set; }
    public bool hasSealingTape { get; private set; }
    public bool evacuationPrepared { get; private set; }

    // New flow state (req #8 water choice + req #12 protective clothing).
    public bool waterStepActive { get; private set; }
    public bool waterChoiceMade { get; private set; }
    public bool safeWaterChosen { get; private set; }
    public bool protectiveClothingDone { get; private set; }
    public bool exitReached { get; private set; }
    public bool gameCompleted { get; private set; }

    // Evacuation-news flow: player is told to watch the news, then the TV video delivers the advisory.
    public bool newsWatchInstructed { get; private set; }
    public bool newsVideoCompleted { get; private set; }

    void Awake()
    {
        Instance = this;
    }

    public static string CurrentObjectiveDisplay
    {
        get
        {
            if (Instance == null) return "Explore your home.";
            return Instance.ComputeObjective();
        }
    }

    string ComputeObjective()
    {
        if (evacuationPrepared) return "FINAL EVACUATION";
        if (equipmentEnabled) return "EVACUATION PREPARATION";
        if (homePrepared) return "WATCH THE NEWS";
        if (preparationStarted) return "SECURE YOUR HOME! Close the windows and the main door.";
        return "Explore your home.";
    }

    public void RegisterEquipment(string id, string displayName)
    {
        if (Find(equipment, id) == null)
            equipment.Add(new TaskEntry(id, displayName));
    }

    public void RegisterPreparation(string id, string displayName)
    {
        RegisterPreparation(id, displayName, displayName);
    }

    public void RegisterPreparation(string id, string displayName, string label)
    {
        if (Find(preparations, id) != null) return;
        TaskEntry entry = new TaskEntry(id, displayName, label);
        preparations.Add(entry);
        activeTasks.Add(entry);
        FireChanged();
    }

    public void RegisterTapePreparation(string id, string displayName)
    {
        RegisterTapePreparation(id, displayName, displayName);
    }

    public void RegisterTapePreparation(string id, string displayName, string label)
    {
        if (Find(preparations, id) != null) return;
        TaskEntry entry = new TaskEntry(id, displayName, label);
        entry.optional = true;
        preparations.Add(entry);
    }

    public void MarkTaskActive(string id)
    {
        if (Find(activeTasks, id) != null) return;
        TaskEntry e = Find(preparations, id);
        if (e == null) return;
        activeTasks.Add(e);
        FireChanged();
    }

    public void RegisterSafetyStep(string id, string displayName)
    {
        if (Find(safetySteps, id) == null)
            safetySteps.Add(new TaskEntry(id, displayName));
    }

    public void CompleteSafetyStep(string id)
    {
        TaskEntry e = Find(safetySteps, id);
        if (e != null && !e.done)
        {
            e.done = true;
            FireChanged();
        }
    }

    public bool AllSafetyStepsDone()
    {
        foreach (TaskEntry t in safetySteps)
            if (!t.done) return false;
        return true;
    }

    public bool IsPreparationDone(string id)
    {
        TaskEntry e = Find(preparations, id);
        return e != null && e.done;
    }

    public void SetEvacuationBagId(string id)
    {
        evacuationBagId = id;
    }

    public bool EvacuationBagReady
    {
        get
        {
            if (string.IsNullOrEmpty(evacuationBagId)) return true;
            TaskEntry e = Find(equipment, evacuationBagId);
            return e != null && e.done;
        }
    }

    public void CollectItem(string id)
    {
        TaskEntry e = Find(equipment, id);
        if (e != null && !e.done)
        {
            e.done = true;
            FireChanged();
        }
        CheckEvacuationCompletion();
    }

    public void CompleteTask(string id)
    {
        TaskEntry e = Find(preparations, id);
        if (e != null && !e.done)
        {
            e.done = true;
            FireChanged();
        }
        if (id == "window1" || id == "window2" || id == "window3")
            RegisterFindTapeStep();
        CheckPreparationCompletion();
    }

    public void RegisterFindTapeStep()
    {
        if (Find(activeTasks, "findtape") != null) return;
        TaskEntry t = new TaskEntry("findtape", "Find Sealing Tape", "Find Sealing Tape");
        t.optional = true;
        activeTasks.Add(t);
        FireChanged();
    }

    public void SetHasSealingTape()
    {
        if (hasSealingTape) return;
        hasSealingTape = true;
        TaskEntry f = Find(activeTasks, "findtape");
        if (f != null && !f.done)
        {
            f.done = true;
            FireChanged();
        }
    }

    public void StartPreparation()
    {
        if (preparationStarted) return;
        preparationStarted = true;
        MarkTaskActive("tape01");
        MarkTaskActive("tape02");
        MarkTaskActive("tape03");
        FireChanged();
    }

    public void StartEvacuationPreparation()
    {
        if (equipmentEnabled) return;
        equipmentEnabled = true;
        RegisterSafetyStep("clothing", "Wear protective clothing");
        InventoryManager inv = InventoryManager.Instance;
        if (inv != null)
        {
            inv.Changed -= OnInventoryChangedForProtectiveClothing;
            inv.Changed += OnInventoryChangedForProtectiveClothing;
        }
        RefreshProtectiveClothingFromInventory();
        FireChanged();
        GameStateManager gs = GameStateManager.Instance;
        if (gs != null) gs.SetState(GameState.EvacuationPrep);
    }

    /// <summary>
    /// The 'Wear protective clothing' step is driven by the actual inventory: it is checked only
    /// while the mask, shades, t-shirt and pants are ALL equipped, and becomes unchecked again the
    /// moment any of them is unequipped. This mirrors the wearable equipment refresh, so the
    /// objective always reflects the real equipped state rather than a one-time pickup.
    /// </summary>
    void OnInventoryChangedForProtectiveClothing()
    {
        if (!equipmentEnabled) return;
        RefreshProtectiveClothingFromInventory();
    }

    void RefreshProtectiveClothingFromInventory()
    {
        InventoryManager inv = InventoryManager.Instance;
        bool allEquipped = inv != null
            && inv.IsEquipped("Mask")
            && inv.IsEquipped("Shades")
            && inv.IsEquipped("T-shirt")
            && inv.IsEquipped("Pants");
        if (protectiveClothingDone != allEquipped)
        {
            protectiveClothingDone = allEquipped;
            TaskEntry c = Find(safetySteps, "clothing");
            if (c != null && c.done != allEquipped)
            {
                c.done = allEquipped;
                FireChanged();
            }
            if (allEquipped) CheckEvacuationCompletion();
        }
    }

    void OnDestroy()
    {
        InventoryManager inv = InventoryManager.Instance;
        if (inv != null) inv.Changed -= OnInventoryChangedForProtectiveClothing;
    }

    public void SetNewsWatchInstructed()
    {
        if (newsWatchInstructed) return;
        newsWatchInstructed = true;
        FireChanged();
    }

    public void OnNewsVideoCompleted()
    {
        if (newsVideoCompleted) return;
        newsVideoCompleted = true;
        FireChanged();
        StartEvacuationPreparation();
    }

    public void RegisterWaterChoice(bool safe)
    {
        if (waterChoiceMade) return;
        waterChoiceMade = true;
        safeWaterChosen = safe;
        FireChanged();
        WaterChoiceNotified();
    }

    void WaterChoiceNotified()
    {
        WarningUI wu = WarningUI.Instance;
        if (safeWaterChosen)
        {
            if (wu != null)
                wu.ShowWarning("WATER CHOICE",
                    "Sealed bottled water is clean and safe to drink.",
                    3.5f);
            if (SubtitleUI.Instance != null)
                SubtitleUI.Instance.Show("Sealed bottled water is safe to drink.", 3.5f);
        }
        else
        {
            if (wu != null)
                wu.ShowWarning("⚠ UNSAFE WATER",
                    "Ash-contaminated water can be harmful to drink.\nUse sealed bottled water instead.",
                    3.5f);
            if (SubtitleUI.Instance != null)
                SubtitleUI.Instance.Show("That water may be contaminated. Use sealed bottled water.", 3.5f);
        }
    }

    public void CompleteProtectiveClothing()
    {
        if (protectiveClothingDone) return;
        protectiveClothingDone = true;
        CompleteSafetyStep("clothing");
        FireChanged();
        CheckEvacuationCompletion();
    }

    public void MarkExitReached()
    {
        if (exitReached) return;
        exitReached = true;
        FireChanged();
    }

    public string GetPreparationLabel(string id)
    {
        TaskEntry e = Find(preparations, id);
        return e != null ? e.label : null;
    }

    void CheckPreparationCompletion()
    {
        if (homePrepared) return;
        if (!preparationStarted) return;
        if (!AllPreparationsDone()) return;
        homePrepared = true;
        SetNewsWatchInstructed();
        FireChanged();
        GameStateManager gs = GameStateManager.Instance;
        if (gs != null) gs.SetState(GameState.HomePrepared);
        StartCoroutine(HomeCompleteRoutine());
    }

    IEnumerator HomeCompleteRoutine()
    {
        WarningUI wu = WarningUI.Instance;
        if (wu != null)
            wu.ShowWarning("HOME PREPARED",
                "Your home has been secured against volcanic ashfall.",
                homeCompletionDelay);
        if (SubtitleUI.Instance != null)
            SubtitleUI.Instance.Show(
                "Your home is prepared against ashfall.\nI should watch the news for an update.",
                homeCompletionDelay + 1.5f);

        yield return new WaitForSeconds(homeCompletionDelay + 1f);

        // Thirst step: screen-space prompt, then the water choice decides whether to continue.
        waterStepActive = true;
        if (wu != null)
            wu.ShowWarning("HE'S THIRSTY",
                "He's thirsty. Drink some water.",
                dangerWarningDuration);
        if (SubtitleUI.Instance != null)
            SubtitleUI.Instance.Show("He's thirsty. Drink some water.", dangerWarningDuration + 1f);

        // Wait for the water choice (or a timeout so the story never hard-blocks).
        float t = Time.time;
        while (Time.time - t < waterChoiceTimeout && !waterChoiceMade)
            yield return null;

        waterStepActive = false;
        yield return new WaitForSeconds(1f);

        // The evacuation advisory is delivered by the news video on the TV. If the player never
        // watches it, fall back after a timeout so the story still moves on.
        if (!newsVideoCompleted)
        {
            t = Time.time;
            while (Time.time - t < newsWatchTimeout && !newsVideoCompleted)
                yield return null;
            if (!newsVideoCompleted)
                StartEvacuationPreparation();
        }

        secondAnnouncementReady = true;
        FireChanged();
    }

    void CheckEvacuationCompletion()
    {
        if (evacuationPrepared) return;
        if (!equipmentEnabled) return;
        if (!AllEquipmentCollected()) return;
        if (!protectiveClothingDone) return;
        if (!InventoryVerified()) return;
        evacuationPrepared = true;
        FireChanged();
        StartCoroutine(EvacuationRoutine());
    }

    /// <summary>
    /// The final evacuation must reflect the actual inventory: the four emergency items must have
    /// been collected (bottle may already be consumed) and the protective clothing must be worn,
    /// not merely present. World pickups drive the equipment checklist and feed the same inventory,
    /// so this never conflicts with the existing objective flow.
    /// </summary>
    bool InventoryVerified()
    {
        InventoryManager inv = InventoryManager.Instance;
        if (inv == null) return true;
        if (inv.StateOf("Protective Clothing") != InventoryItemState.Equipped) return false;
        if (!inv.HasCollected("Bottled Water")) return false;
        if (!inv.HasCollected("Flashlight")) return false;
        if (!inv.HasCollected("Face Mask")) return false;
        if (!inv.HasCollected("Eye Protection")) return false;
        return true;
    }

    IEnumerator EvacuationRoutine()
    {
        GameStateManager gs = GameStateManager.Instance;
        if (gs != null) gs.SetState(GameState.EvacuationReady);

        WarningUI wu = WarningUI.Instance;
        if (wu != null)
        {
            wu.ShowWarning("FINAL EVACUATION WARNING",
                "Evacuate to a safe area.\nKeep your emergency supplies with you.\n\nWalk outside the open door.",
                5f);
        }
        if (SubtitleUI.Instance != null)
            SubtitleUI.Instance.Show("All emergency items collected. Walk outside to complete the evacuation.", 4f);

        // Reopen the main door so the player can physically walk out.
        PreparationTask door = PreparationTask.FindDoor();
        if (door != null) door.Reopen();

        // Complete when the player reaches the exit trigger (or a fallback so nobody soft-locks).
        float t = Time.time;
        while (Time.time - t < evacuationFallbackTime && !exitReached)
            yield return null;

        if (gs != null) gs.SetState(GameState.Completed);
        gameCompleted = true;
        if (wu != null)
            wu.ShowWarning("OBJECTIVE COMPLETE",
                "Your home is prepared.\nYour emergency kit is ready.\n\nWell done staying safe.",
                5f);
    }

    public bool AllEquipmentCollected()
    {
        foreach (TaskEntry t in equipment)
            if (!t.done) return false;
        return true;
    }

    public bool AllPreparationsDone()
    {
        foreach (TaskEntry t in preparations)
            if (!t.optional && !t.done) return false;
        return true;
    }

    /// <summary>
    /// True when every preparation task, including optional steps, has been completed. Used to verify
    /// the optional tape path, never to gate HOME PREPARED.
    /// </summary>
    public bool AllTasksIncludingOptionalDone()
    {
        foreach (TaskEntry t in preparations)
            if (!t.done) return false;
        return true;
    }

    void FireChanged()
    {
        if (TasksChanged != null) TasksChanged();
    }

    static TaskEntry Find(List<TaskEntry> list, string id)
    {
        foreach (TaskEntry t in list)
            if (t.id == id) return t;
        return null;
    }
}