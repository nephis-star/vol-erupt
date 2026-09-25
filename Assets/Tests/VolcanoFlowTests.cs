using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public class VolcanoFlowTests
{
    const float eruptionBudget = 50f;
    const float prepBudget = 15f;
    const float completionBudget = 50f;

    IEnumerator LoadSceneAndWait()
    {
        SceneManager.LoadScene("SampleScene");
        yield return new WaitForSeconds(1f);
    }

    IEnumerator WaitForState(GameStateManager gs, GameState target, float budget)
    {
        float t = Time.time;
        while (Time.time - t < budget && gs.CurrentState != target)
            yield return null;
    }

    IEnumerator ReachTvAvailable(GameStateManager gs)
    {
        // New flow: home preparation starts directly after the eruption warning, no TV visit required.
        yield return WaitForState(gs, GameState.Preparation, eruptionBudget);
        Assert.AreEqual(GameState.Preparation, gs.CurrentState, "Eruption/earthquake never reached Preparation");
    }

    IEnumerator InteractTvAndReachPreparation(GameStateManager gs)
    {
        yield return WaitForState(gs, GameState.Preparation, prepBudget);
        Assert.AreEqual(GameState.Preparation, gs.CurrentState, "Preparation not reached");
    }

    PreparationTask[] AllTasks()
    {
        return Object.FindObjectsByType<PreparationTask>(FindObjectsSortMode.None);
    }

    TapeSealTask FindTape(string id)
    {
        TapeSealTask[] tapes = Object.FindObjectsByType<TapeSealTask>(FindObjectsSortMode.None);
        foreach (TapeSealTask t in tapes)
            if (t.taskId == id) return t;
        return null;
    }

    PreparationTask FindCloseTask(string id)
    {
        foreach (PreparationTask t in AllTasks())
            if (t.taskId == id) return t;
        return null;
    }

    IEnumerator WaitTaskDone(string id)
    {
        float t = Time.time;
        while (Time.time - t < prepBudget && !ObjectiveManager.Instance.IsPreparationDone(id))
            yield return null;
        Assert.IsTrue(ObjectiveManager.Instance.IsPreparationDone(id), "Task " + id + " did not complete");
    }

    IEnumerator CloseAndWait(PreparationTask task)
    {
        Assert.IsTrue(task.CanInteract, task.taskId + " should be interactable");
        task.Interact(null);
        float t = Time.time;
        while (Time.time - t < prepBudget && !task.IsClosed)
            yield return null;
        Assert.IsTrue(task.IsClosed, task.taskId + " should be closed");
    }

    IEnumerator EnsureSealingTape()
    {
        if (ObjectiveManager.Instance.hasSealingTape) yield break;
        SealingTape st = Object.FindAnyObjectByType<SealingTape>();
        Assert.NotNull(st, "SealingTape missing");
        float t = Time.time;
        while (Time.time - t < prepBudget && !st.CanInteract) yield return null;
        Assert.IsTrue(st.CanInteract, "SealingTape should be interactable after closing a window");
        st.Interact(null);
        Assert.IsTrue(ObjectiveManager.Instance.hasSealingTape, "hasSealingTape not set");
    }

    IEnumerator SealWindow(TapeSealTask task)
    {
        Assert.IsTrue(task.CanInteract, task.taskId + " should be interactable");
        Assert.IsTrue(ObjectiveManager.Instance.hasSealingTape, "Must have sealing tape before sealing");
        task.Interact(null);
        float t = Time.time;
        while (Time.time - t < prepBudget && !ObjectiveManager.Instance.IsPreparationDone(task.taskId))
            yield return null;
        Assert.IsTrue(ObjectiveManager.Instance.IsPreparationDone(task.taskId), task.taskId + " did not complete");
    }

    IEnumerator TapeAndWait(TapeSealTask task)
    {
        yield return EnsureSealingTape();
        float t = Time.time;
        while (Time.time - t < prepBudget && !task.CanInteract) yield return null;
        Assert.IsTrue(task.CanInteract, task.taskId + " should activate after close");
        task.Interact(null);
        t = Time.time;
        while (Time.time - t < prepBudget && !ObjectiveManager.Instance.IsPreparationDone(task.taskId))
            yield return null;
        Assert.IsTrue(ObjectiveManager.Instance.IsPreparationDone(task.taskId), task.taskId + " did not complete");
    }

    static bool IsActiveTasksContains(string id)
    {
        if (ObjectiveManager.Instance == null) return false;
        foreach (ObjectiveManager.TaskEntry e in ObjectiveManager.Instance.activeTasks)
            if (e.id == id) return true;
        return false;
    }

    IEnumerator DrinkWater(bool safe)
    {
        WaterChoice[] choices = Object.FindObjectsByType<WaterChoice>(FindObjectsSortMode.None);
        WaterChoice target = null;
        foreach (WaterChoice w in choices)
            if (w.isSafe == safe) { target = w; break; }
        Assert.NotNull(target, "WaterChoice (safe=" + safe + ") missing");
        float t = Time.time;
        while (Time.time - t < prepBudget && !target.CanInteract)
            yield return null;
        Assert.IsTrue(target.CanInteract, "Water choice should be interactable during the thirsty step");
        target.Interact(null);
        Assert.IsTrue(ObjectiveManager.Instance.waterChoiceMade, "Water choice must register");
        Assert.AreEqual(safe, ObjectiveManager.Instance.safeWaterChosen, "safeWaterChosen must match the chosen source");
    }

    IEnumerator PickupClothingIntoInventory()
    {
        ProtectiveClothing pc = Object.FindAnyObjectByType<ProtectiveClothing>();
        Assert.NotNull(pc, "ProtectiveClothing missing");
        float t = Time.time;
        while (Time.time - t < prepBudget && !pc.CanInteract)
            yield return null;
        Assert.IsTrue(pc.CanInteract, "ProtectiveClothing should be interactable during evacuation prep");
        pc.Interact(null);
        Assert.IsTrue(InventoryManager.Instance.HasItem("Protective Clothing"),
            "Clothing must be in inventory after pickup");
        Assert.IsFalse(ObjectiveManager.Instance.protectiveClothingDone,
            "Picking up clothing must NOT complete the objective by itself");
    }

    IEnumerator WearProtectiveClothing()
    {
        yield return PickupClothingIntoInventory();
        Assert.IsTrue(InventoryManager.Instance.WearNamed("Protective Clothing"),
            "Clothing must be wearable from the inventory");
        Assert.IsTrue(ObjectiveManager.Instance.protectiveClothingDone, "Protective clothing must be worn");
        Assert.AreEqual(InventoryItemState.Equipped,
            InventoryManager.Instance.StateOf("Protective Clothing"),
            "Clothing state must be Equipped after wearing");
    }

    IEnumerator CollectAllFour()
    {
        PickupItem[] items = Object.FindObjectsByType<PickupItem>(FindObjectsSortMode.None);
        foreach (PickupItem it in items)
            if (it.CanInteract) it.Interact(null);
        Assert.IsTrue(ObjectiveManager.Instance.AllEquipmentCollected(), "All four emergency items must be collected");
        yield break;
    }

    IEnumerator WaitEvacuationPrep(GameStateManager gs)
    {
        yield return WaitForState(gs, GameState.EvacuationPrep, prepBudget);
        Assert.IsTrue(ObjectiveManager.Instance.equipmentEnabled, "Equipment must be enabled after second announcement");
    }

    IEnumerator CompleteAllPreparations()
    {
        yield return CloseAndWait(FindCloseTask("window1"));
        yield return EnsureSealingTape();
        yield return SealWindow(FindTape("tape01"));
        yield return CloseAndWait(FindCloseTask("window2"));
        yield return SealWindow(FindTape("tape02"));
        yield return CloseAndWait(FindCloseTask("window3"));
        yield return SealWindow(FindTape("tape03"));
        yield return CloseAndWait(FindCloseTask("door"));

        Assert.IsTrue(ObjectiveManager.Instance.AllPreparationsDone(), "All required prep tasks done");
        float t = Time.time;
        while (Time.time - t < prepBudget && !ObjectiveManager.Instance.homePrepared)
            yield return null;
        Assert.IsTrue(ObjectiveManager.Instance.homePrepared, "Home should be prepared after all prep tasks");
    }

    IEnumerator WaitWaterStepActive()
    {
        float t = Time.time;
        while (Time.time - t < prepBudget && !ObjectiveManager.Instance.waterStepActive)
            yield return null;
        Assert.IsTrue(ObjectiveManager.Instance.waterStepActive, "Thirsty step did not activate");
    }

    IEnumerator WaitSecondAnnouncementReady()
    {
        float t = Time.time;
        while (Time.time - t < prepBudget && !ObjectiveManager.Instance.secondAnnouncementReady)
            yield return null;
        Assert.IsTrue(ObjectiveManager.Instance.secondAnnouncementReady,
            "Second announcement should be ready after the water choice");
    }

    IEnumerator CompletePrepAndHome()
    {
        yield return CompleteAllPreparations();
        // The evacuation advisory is delivered by the TV news video.
        if (!ObjectiveManager.Instance.newsVideoCompleted)
            ObjectiveManager.Instance.OnNewsVideoCompleted();
        yield return DrinkWater(true);
        yield return WaitSecondAnnouncementReady();
    }

    IEnumerator CollectPickup(string id)
    {
        PickupItem target = null;
        PickupItem[] items = Object.FindObjectsByType<PickupItem>(FindObjectsSortMode.None);
        foreach (PickupItem it in items)
            if (it.itemName == id) { target = it; break; }
        Assert.NotNull(target, "Pickup " + id + " missing");
        float t = Time.time;
        while (Time.time - t < prepBudget && !target.CanInteract)
            yield return null;
        Assert.IsTrue(target.CanInteract, id + " should be interactable");
        target.Interact(null);
        Assert.IsTrue(InventoryManager.Instance.HasItem(id), id + " must be in the inventory");
    }

    IEnumerator AdvanceToEvacuationPrep(GameStateManager gs)
    {
        yield return ReachTvAvailable(gs);
        yield return InteractTvAndReachPreparation(gs);
        yield return CompletePrepAndHome();
        yield return WaitEvacuationPrep(gs);
    }

    [UnityTest]
    public IEnumerator C01_GameStarts_PlayerSpawned_ObjectivesRegistered()
    {
        yield return LoadSceneAndWait();

        Assert.NotNull(Object.FindAnyObjectByType<GameStateManager>(), "GameStateManager missing");
        Assert.NotNull(Object.FindAnyObjectByType<VolcanoEventManager>(), "VolcanoEventManager missing");

        GameObject fpc = GameObject.Find("First Person Controller");
        Assert.NotNull(fpc, "Player missing");
        CapsuleCollider cc = fpc.GetComponent<CapsuleCollider>();
        float bottomY = fpc.transform.position.y + cc.center.y - cc.height * 0.5f;
        Assert.Greater(bottomY, 1.4f, "Player bottom is at floor or terrain level, not on the bed");
        GameObject home = GameObject.Find("House");
        Assert.NotNull(home, "House missing");
        float bedTop = -1e9f;
        Collider[] homeColliders = home.GetComponentsInChildren<Collider>();
        foreach (Collider c in homeColliders)
            if (c.gameObject.name.Contains("Mattress")) bedTop = c.bounds.max.y;
        Assert.Greater(bedTop, 1.4f, "House mattress top is too low");
        Assert.Less(Mathf.Abs(bottomY - bedTop), 0.3f, "Player is not resting on the bed mattress");

        Assert.NotNull(ObjectiveManager.Instance, "ObjectiveManager missing");
        Assert.AreEqual(4, ObjectiveManager.Instance.equipment.Count, "Expected 4 emergency items registered");
        Assert.AreEqual(7, ObjectiveManager.Instance.preparations.Count,
            "Expected 7 preparation tasks (3 close + 3 tape + 1 door)");
        Assert.AreEqual(4, ObjectiveManager.Instance.activeTasks.Count,
            "Expected only 4 active prep tasks before any window is closed (tape tasks hidden)");
        Assert.NotNull(Object.FindAnyObjectByType<SealingTape>(), "TEMP_SealingTape missing");
        Assert.NotNull(Object.FindAnyObjectByType<PickupItem>(), "TEMP_EyeProtection missing");
    }

    [UnityTest]
    public IEnumerator C02_FullSequence_Eruption_TV_Prep_HomeDone_SecondAnnounce_Collect_Completed()
    {
        yield return LoadSceneAndWait();

        GameStateManager gs = Object.FindAnyObjectByType<GameStateManager>();
        Assert.NotNull(gs);

        yield return ReachTvAvailable(gs);

        VolcanoEventManager vem = Object.FindAnyObjectByType<VolcanoEventManager>();
        if (vem != null)
        {
            Assert.IsFalse(vem.effectsActive, "The short-lived smoke burst should be done once TV becomes available");
            Assert.IsTrue(AshfallController.Active, "Ashfall must persist after the eruption and earthquake");
            Assert.NotNull(vem.ashFallEffect, "AshFallEffect missing on the manager");
            Assert.IsTrue(vem.ashFallEffect.isPlaying, "Ash must still be falling when the TV becomes available");
            Assert.IsTrue(vem.ashFallEffect.main.loop, "Ash must loop for the rest of the game");
            Assert.IsTrue(vem.ashFallEffect.collision.enabled, "Ash collision must be enabled so it dies on the roof");
            Assert.NotNull(AshfallController.Instance != null ? AshfallController.Instance.RoofBlocker : null,
                "Roof blocker must exist so ash never falls through the roof");
        }

        yield return InteractTvAndReachPreparation(gs);
        Debug.Log("[VOLCANOTEST] Preparation started; objective=" + ObjectiveManager.CurrentObjectiveDisplay);

        yield return CloseAndWait(FindCloseTask("window1"));
        yield return EnsureSealingTape();
        yield return SealWindow(FindTape("tape01"));
        yield return CloseAndWait(FindCloseTask("window2"));
        yield return SealWindow(FindTape("tape02"));
        yield return CloseAndWait(FindCloseTask("window3"));
        yield return SealWindow(FindTape("tape03"));
        yield return CloseAndWait(FindCloseTask("door"));
        Assert.IsTrue(ObjectiveManager.Instance.AllPreparationsDone(), "All required prep tasks done");

        Assert.IsTrue(ObjectiveManager.Instance.homePrepared, "Home should be prepared after all prep tasks");
        Assert.IsFalse(ObjectiveManager.Instance.equipmentEnabled,
            "Equipment must not be enabled before the (automatic) second announcement");

        // The thirst step offers a water choice; pick the safe sealed water so the story continues fast.
        yield return DrinkWater(true);

        // The 2nd news then plays on its own (TVController auto-trigger) once secondAnnouncementReady is set.
        yield return WaitEvacuationPrep(gs);

        yield return CollectAllFour();
        yield return WearProtectiveClothing();

        float start = Time.time;
        while (Time.time - start < completionBudget && gs.CurrentState != GameState.Completed)
            yield return null;
        Assert.AreEqual(GameState.Completed, gs.CurrentState, "Game never reached Completed");

        Debug.Log("[VOLCANOTEST] OBJECTIVE COMPLETE at t=" + Time.timeSinceLevelLoad.ToString("F1") +
                  " final=" + ObjectiveManager.CurrentObjectiveDisplay);
    }

    [UnityTest]
    public IEnumerator C03_TapeTaskLocked_UntilWindowClosed()
    {
        yield return LoadSceneAndWait();

        GameStateManager gs = Object.FindAnyObjectByType<GameStateManager>();
        yield return ReachTvAvailable(gs);
        yield return InteractTvAndReachPreparation(gs);

        TapeSealTask tape = FindTape("tape01");
        Assert.NotNull(tape, "tape01 task missing");
        Assert.IsTrue(IsActiveTasksContains("tape01"), "tape01 should be visible at prep start");
        Assert.IsFalse(tape.CanInteract, "tape01 must NOT be interactable before Window 1 is closed");

        PreparationTask w1 = FindCloseTask("window1");
        yield return CloseAndWait(w1);
        Assert.IsTrue(tape.CanInteract, "tape01 should become interactable after Window 1 closes");
        Assert.IsTrue(IsActiveTasksContains("tape01"),
            "tape01 should appear in the active task list after its window closes");
    }

    [UnityTest]
    public IEnumerator C04_CloseThenTape_RevealsBarricadeOnlyAfterTape()
    {
        yield return LoadSceneAndWait();

        GameStateManager gs = Object.FindAnyObjectByType<GameStateManager>();
        yield return ReachTvAvailable(gs);
        yield return InteractTvAndReachPreparation(gs);

        SealPrepVisual win1Seal = null;
        SealPrepVisual[] seals = Object.FindObjectsByType<SealPrepVisual>(FindObjectsSortMode.None);
        foreach (SealPrepVisual s in seals)
            if (s.taskId == "window1") win1Seal = s;
        Assert.NotNull(win1Seal, "Window 1 barricade visual missing");
        Assert.AreEqual("tape01", win1Seal.tapeTaskId, "Window 1 must require a tape step");

        PreparationTask w1 = FindCloseTask("window1");
        yield return CloseAndWait(w1);
        yield return new WaitForSeconds(0.5f);
        Assert.IsFalse(win1Seal.IsRevealed, "Barricade must not reveal until the tape step is done");

        yield return TapeAndWait(FindTape("tape01"));
        float t = Time.time;
        while (Time.time - t < prepBudget && !win1Seal.IsRevealed)
            yield return null;
        Assert.IsTrue(win1Seal.IsRevealed, "Window 1 barricade should reveal once the gap is taped");
    }

    [UnityTest]
    public IEnumerator C05_TapeOptional_RequiredCloseTasksCompleteHome()
    {
        yield return LoadSceneAndWait();

        GameStateManager gs = Object.FindAnyObjectByType<GameStateManager>();
        yield return ReachTvAvailable(gs);
        yield return InteractTvAndReachPreparation(gs);

        // Required tasks only: close all three windows and the main door. No tape touched.
        yield return CloseAndWait(FindCloseTask("window1"));
        yield return CloseAndWait(FindCloseTask("window2"));
        yield return CloseAndWait(FindCloseTask("window3"));
        yield return CloseAndWait(FindCloseTask("door"));

        Assert.IsTrue(ObjectiveManager.Instance.AllPreparationsDone(),
            "Preparation must be complete once the REQUIRED close tasks are done (tape is optional)");
        Assert.IsFalse(ObjectiveManager.Instance.IsPreparationDone("tape01"),
            "tape01 must remain undone without use");
        Assert.IsFalse(ObjectiveManager.Instance.IsPreparationDone("tape02"),
            "tape02 must remain undone without use");
        Assert.IsFalse(ObjectiveManager.Instance.IsPreparationDone("tape03"),
            "tape03 must remain undone without use");

        float t = Time.time;
        while (Time.time - t < prepBudget && !ObjectiveManager.Instance.homePrepared)
            yield return null;
        Assert.IsTrue(ObjectiveManager.Instance.homePrepared,
            "HOME PREPARED must be reached without any tape step");
    }

    [UnityTest]
    public IEnumerator C06_ActiveTaskList_ShowsAppropriateTasksAtEachStage()
    {
        yield return LoadSceneAndWait();

        GameStateManager gs = Object.FindAnyObjectByType<GameStateManager>();
        yield return ReachTvAvailable(gs);
        yield return InteractTvAndReachPreparation(gs);

        Assert.AreEqual(7, ObjectiveManager.Instance.activeTasks.Count,
            "At prep start, active list contains the three close tasks, the door, and all three tape tasks");
        Assert.IsTrue(IsActiveTasksContains("window1"), "window1 should be active initially");
        Assert.IsTrue(IsActiveTasksContains("window2"), "window2 should be active initially");
        Assert.IsTrue(IsActiveTasksContains("window3"), "window3 should be active initially");
        Assert.IsTrue(IsActiveTasksContains("door"), "door should be active initially");
        Assert.IsTrue(IsActiveTasksContains("tape01"), "tape01 should be visible at prep start");
        Assert.IsTrue(IsActiveTasksContains("tape02"), "tape02 should be visible at prep start");
        Assert.IsTrue(IsActiveTasksContains("tape03"), "tape03 should be visible at prep start");
        Assert.IsFalse(IsActiveTasksContains("findtape"), "findtape should not appear yet");

        yield return CloseAndWait(FindCloseTask("window1"));
        Assert.IsTrue(IsActiveTasksContains("findtape"), "findtape should appear after window1 closes");

        yield return TapeAndWait(FindTape("tape01"));

        yield return CloseAndWait(FindCloseTask("window2"));
        Assert.IsTrue(IsActiveTasksContains("tape02"), "tape02 visible after window2 closes");
        yield return TapeAndWait(FindTape("tape02"));

        yield return CloseAndWait(FindCloseTask("window3"));
        Assert.IsTrue(IsActiveTasksContains("tape03"), "tape03 visible after window3 closes");
        yield return TapeAndWait(FindTape("tape03"));

        yield return CloseAndWait(FindCloseTask("door"));

        Assert.IsTrue(ObjectiveManager.Instance.AllPreparationsDone(), "All required prep tasks done");
    }

    [UnityTest]
    public IEnumerator C07_TapeNotRequired_DoorBarricade_RevealsOnCloseOnly()
    {
        yield return LoadSceneAndWait();

        GameStateManager gs = Object.FindAnyObjectByType<GameStateManager>();
        yield return ReachTvAvailable(gs);
        yield return InteractTvAndReachPreparation(gs);

        SealPrepVisual doorSeal = null;
        SealPrepVisual[] seals = Object.FindObjectsByType<SealPrepVisual>(FindObjectsSortMode.None);
        foreach (SealPrepVisual s in seals)
            if (s.taskId == "door") doorSeal = s;
        Assert.NotNull(doorSeal, "Door barricade visual missing");
        Assert.AreEqual("", doorSeal.tapeTaskId, "Door must not require a tape step");

        yield return CloseAndWait(FindCloseTask("door"));

        float t = Time.time;
        while (Time.time - t < prepBudget && !doorSeal.IsRevealed)
            yield return null;
        Assert.IsTrue(doorSeal.IsRevealed, "Door barricade should reveal as soon as the door is secured");
    }

    [UnityTest]
    public IEnumerator C08_TapeThenMarksTask_NoEarlyDoubleInteract()
    {
        yield return LoadSceneAndWait();

        GameStateManager gs = Object.FindAnyObjectByType<GameStateManager>();
        yield return ReachTvAvailable(gs);
        yield return InteractTvAndReachPreparation(gs);

        yield return CloseAndWait(FindCloseTask("window1"));
        yield return EnsureSealingTape();
        TapeSealTask tape = FindTape("tape01");
        yield return TapeAndWait(tape);

        Assert.IsTrue(ObjectiveManager.Instance.IsPreparationDone("tape01"), "tape01 done");
        Assert.IsFalse(tape.CanInteract, "tape01 must not be interactable after completion");
        Assert.IsTrue(tape.IsClosed, "tape01 IsClosed after completion");
    }

    [UnityTest]
    public IEnumerator C09_EmergencyItemsNotInObjectiveAtBoot()
    {
        yield return LoadSceneAndWait();

        ObjectiveUI oi = ObjectiveUI.Instance;
        Assert.NotNull(oi, "ObjectiveUI missing");
        Assert.AreEqual("Explore your home.", oi.Heading, "Boot headline should be Explore your home.");
        Assert.IsFalse(oi.ListText.Contains("Flashlight"), "Flashlight must not appear in the list at boot");
        Assert.IsFalse(oi.ListText.Contains("Face Mask"), "Face Mask must not appear at boot");
        Assert.IsFalse(oi.ListText.Contains("Eye Protection"), "Eye Protection must not appear at boot");
        Assert.IsFalse(oi.ListText.Contains("Bottled Water"), "Bottled Water must not appear at boot");
        Assert.IsFalse(ObjectiveManager.Instance.equipmentEnabled, "Equipment must not be enabled at boot");
        Assert.IsFalse(ObjectiveManager.Instance.preparationStarted, "Preparation must not have started at boot");
    }

    [UnityTest]
    public IEnumerator C10_EquipmentLockedDuringPreparation()
    {
        yield return LoadSceneAndWait();

        GameStateManager gs = Object.FindAnyObjectByType<GameStateManager>();
        yield return ReachTvAvailable(gs);
        yield return InteractTvAndReachPreparation(gs);

        PickupItem[] items = Object.FindObjectsByType<PickupItem>(FindObjectsSortMode.None);
        foreach (PickupItem it in items)
            if (it.itemName == "Bottled Water")
                Assert.IsTrue(it.CanInteract, "Bottled Water must be collectible early so it can be drunk from the inventory");
            else
                Assert.IsFalse(it.CanInteract, it.itemName + " must be locked during home preparation");

        SealingTape st = Object.FindAnyObjectByType<SealingTape>();
        Assert.IsFalse(st.CanInteract, "SealingTape must be locked before a window is closed");
    }

    [UnityTest]
    public IEnumerator C11_TapeCannotSealWithoutFindingTape()
    {
        yield return LoadSceneAndWait();

        GameStateManager gs = Object.FindAnyObjectByType<GameStateManager>();
        yield return ReachTvAvailable(gs);
        yield return InteractTvAndReachPreparation(gs);

        yield return CloseAndWait(FindCloseTask("window1"));

        TapeSealTask tape = FindTape("tape01");
        Assert.IsTrue(tape.CanInteract, "tape01 should be interactable after window1 closes");
        InteractionUI.Instance.ShowToast("");
        tape.Interact(null);
        Assert.IsFalse(ObjectiveManager.Instance.IsPreparationDone("tape01"),
            "Tape must not complete without finding the tape");
        Assert.IsFalse(ObjectiveManager.Instance.hasSealingTape, "hasSealingTape must still be false");
    }

    [UnityTest]
    public IEnumerator C12_TapePickupAfterWindowClosed()
    {
        yield return LoadSceneAndWait();

        GameStateManager gs = Object.FindAnyObjectByType<GameStateManager>();
        yield return ReachTvAvailable(gs);
        yield return InteractTvAndReachPreparation(gs);

        yield return CloseAndWait(FindCloseTask("window1"));

        SealingTape st = Object.FindAnyObjectByType<SealingTape>();
        Assert.IsTrue(st.CanInteract, "SealingTape should be interactable after a window closes");
        st.Interact(null);
        Assert.IsTrue(ObjectiveManager.Instance.hasSealingTape, "hasSealingTape must be true after pickup");
        Assert.IsTrue(IsActiveTasksContains("findtape"), "findtape should be in active tasks");
        Assert.IsFalse(st.gameObject.activeSelf, "Tape object should be hidden after pickup");
    }

    [UnityTest]
    public IEnumerator C13_Window1Sequence_Close_Find_Pickup_Seal()
    {
        yield return LoadSceneAndWait();

        GameStateManager gs = Object.FindAnyObjectByType<GameStateManager>();
        yield return ReachTvAvailable(gs);
        yield return InteractTvAndReachPreparation(gs);

        yield return CloseAndWait(FindCloseTask("window1"));
        Assert.IsTrue(IsActiveTasksContains("findtape"), "Find Sealing Tape objective must appear after closing Window 1");

        yield return EnsureSealingTape();
        yield return SealWindow(FindTape("tape01"));

        Assert.IsTrue(ObjectiveManager.Instance.IsPreparationDone("tape01"), "tape01 must be done");
        Assert.IsTrue(ObjectiveManager.Instance.hasSealingTape, "hasSealingTape must be true");
        Assert.AreEqual("Seal Window 1 Gaps", ObjectiveManager.Instance.GetPreparationLabel("tape01"),
            "Tape task label must be 'Seal Window 1 Gaps'");

        SealPrepVisual[] seals = Object.FindObjectsByType<SealPrepVisual>(FindObjectsSortMode.None);
        foreach (SealPrepVisual s in seals)
            if (s.taskId == "window1")
            {
                float t = Time.time;
                while (Time.time - t < prepBudget && !s.IsRevealed) yield return null;
                Assert.IsTrue(s.IsRevealed, "Window 1 barricade must reveal after taping");
        }
    }

    [UnityTest]
    public IEnumerator C41_EvacuationVideoTeleportsPlayer_AfterRealEquipFlow()
    {
        // ---- 1. Load and advance to evacuation prep (equipmentEnabled = true). ----
        yield return LoadSceneAndWait();
        GameStateManager gs = Object.FindAnyObjectByType<GameStateManager>();
        Assert.NotNull(gs);
        yield return AdvanceToEvacuationPrep(gs);
        Assert.IsTrue(ObjectiveManager.Instance.equipmentEnabled, "Equipment must be enabled after the second announcement");

        // ---- 2. Collect the four protective items via a real E-key press ----
        // (PlayerInteraction.Update checks Keyboard.current[Key.E].wasPressedThisFrame
        //  and fires Current.Interact, exactly like a physical player).
        PickupItem[] pickups = Object.FindObjectsByType<PickupItem>(FindObjectsSortMode.None);
        string[] equipIds = { "Mask", "Shades", "T-shirt", "Pants" };
        InventoryManager inv = InventoryManager.Instance;
        Assert.NotNull(inv, "InventoryManager missing");

        foreach (string id in equipIds)
        {
            PickupItem item = null;
            foreach (PickupItem p in pickups)
                if (p.itemName == id) { item = p; break; }
            Assert.NotNull(item, "Pickup " + id + " missing");
            yield return PressEToPickup(item);
            Assert.IsTrue(inv.HasItem(id), id + " must be in the inventory after the E-pickup");
        }

        // ---- 3. Open the inventory. This freezes the player (SetActive(false)) ----
        // while the 4th item is equipped from the open inventory — the real-game
        // path that previously made GameObject.Find return null.
        InventoryUI ui = InventoryUI.Instance;
        Assert.NotNull(ui, "InventoryUI missing");
        ui.Toggle();
        Assert.IsTrue(ui.IsOpen, "Inventory must open");
        GameObject fpc = GameObject.Find("First Person Controller");
        Assert.NotNull(fpc, "First Person Controller must exist");
        Assert.IsFalse(fpc.activeSelf, "Player must be frozen while the inventory is open");

        // ---- 4. Wear each item from the open inventory. The last wear fires ----
        // Changed while the player is still inactive, starting the evacuation
        // sequence while GameObject.Find would previously have returned null.
        foreach (string id in equipIds)
        {
            Assert.IsTrue(inv.UseNamed(id), id + " must be wearable from the inventory");
            Assert.AreEqual(InventoryItemState.Equipped, inv.StateOf(id), id + " must be Equipped");
        }

        // ---- 5. Wait for the evacuation video to finish and the player to teleport ----
        float videoBudget = 22f;
        float start = Time.time;
        while (Time.time - start < videoBudget)
        {
            // Break once the player has been teleported to the evacuation zone.
            if (Mathf.Abs(fpc.transform.position.x - (-21.5f)) < 0.3f &&
                Mathf.Abs(fpc.transform.position.z - 78.5f) < 0.3f)
                break;
            yield return null;
        }
        yield return new WaitForSeconds(0.6f); // let physics settle after the teleport

        // ---- 6. Verify the player is on the evacuation floor, not floating or sunk ----
        GameObject floorObj = GameObject.Find("EvacuationCampFloor");
        Assert.NotNull(floorObj, "EvacuationCampFloor must exist");
        float floorY = floorObj.transform.position.y;
        Vector3 finalPos = fpc.transform.position;
        Assert.GreaterOrEqual(finalPos.y, floorY - 0.15f, "Player must not be below the floor after settling");
        Assert.LessOrEqual(finalPos.y, floorY + 0.4f, "Player must not be floating well above the floor");

        // ---- 7. Verify the starting SpawnPoint is unchanged. ----
        GameObject sp = GameObject.Find("SpawnPoint");
        Assert.NotNull(sp, "SpawnPoint must exist");
        Assert.Less(Mathf.Abs(sp.transform.position.x - (-37.98f)), 0.05f, "SpawnPoint X must be unchanged");
        Assert.Less(Mathf.Abs(sp.transform.position.z - 100.46f), 0.05f, "SpawnPoint Z must be unchanged");

        // ---- 8. Close the inventory, restore the player, verify movement + FOV. ----
        ui.Toggle();
        Assert.IsFalse(ui.IsOpen, "Inventory must close");
        Assert.IsTrue(fpc.activeSelf, "Player must be active after closing the inventory");
        Camera fpsCam = fpc.GetComponentInChildren<Camera>();
        Assert.NotNull(fpsCam, "First-person camera must exist");
        Assert.AreEqual(60f, fpsCam.fieldOfView, 0.1f, "FOV must remain 60");

        // ---- 9. Duplicate guard: unequip then re-equip must not restart the video. ----
        foreach (string id in equipIds)
            inv.UseNamed(id); // unequip
        foreach (string id in equipIds)
            inv.UseNamed(id); // re-equip — evacuationSequenceStarted is true, ignored
        Assert.Less(Mathf.Abs(fpc.transform.position.x - (-21.5f)), 0.3f, "Player must still be at the camp after re-equip");
        Assert.IsNull(GameObject.Find("EvacuationVideoOverlay"), "Overlay must be gone");

        Debug.Log("[VOLCANOTEST] C41 evacuation teleport OK: final=" + finalPos.ToString("F2") + " floorY=" + floorY.ToString("F2"));
    }

    IEnumerator PressEToPickup(PickupItem item)
    {
        // Position the player so the camera ray hits the item's collider:
        // the camera sits 1.49 units above the player root, so place the root
        // at the item's centre Y minus 1.49 and 1.5 units behind the item.
        Collider col = item.GetComponentInChildren<Collider>();
        Vector3 centre = col != null ? col.bounds.center : item.transform.position;
        float rootY = centre.y - 1.49f;
        Rigidbody rb = item.GetComponentInParent<Rigidbody>();
        Vector3 targetPos = new Vector3(item.transform.position.x, rootY, item.transform.position.z - 1.5f);
        if (rb != null) rb.position = targetPos;
        else item.transform.position = targetPos;
        item.transform.rotation = Quaternion.identity;

        // Simulate a genuine E-key press through the Input System.
        var keys = new Key[] { Key.E };
        var state = new KeyboardState(keys);
        InputSystem.QueueStateEvent<KeyboardState>(Keyboard.current, state, -1);
        yield return null; // engine Update processes the queue -> wasPressedThisFrame fires
        keys = new Key[0];
        state = new KeyboardState(keys);
        InputSystem.QueueStateEvent<KeyboardState>(Keyboard.current, state, -1);
        yield return null;

        // Wait for the pickup to be collected into the inventory.
        float t = Time.time;
        while (Time.time - t < 5f && !InventoryManager.Instance.HasItem(item.itemName))
            yield return null;
        Assert.IsTrue(InventoryManager.Instance.HasItem(item.itemName), item.itemName + " must be collected");
    }

    [UnityTest]
    public IEnumerator C14_Window2ReusesSameTapeRoll()
    {
        yield return LoadSceneAndWait();

        GameStateManager gs = Object.FindAnyObjectByType<GameStateManager>();
        yield return ReachTvAvailable(gs);
        yield return InteractTvAndReachPreparation(gs);

        SealingTape st = Object.FindAnyObjectByType<SealingTape>();
        Assert.NotNull(st, "SealingTape missing");

        yield return CloseAndWait(FindCloseTask("window1"));
        yield return EnsureSealingTape();
        yield return SealWindow(FindTape("tape01"));

        Assert.IsTrue(ObjectiveManager.Instance.hasSealingTape, "hasSealingTape must remain true after Window 1 seal");

        yield return CloseAndWait(FindCloseTask("window2"));
        yield return SealWindow(FindTape("tape02"));

        Assert.IsTrue(ObjectiveManager.Instance.IsPreparationDone("tape02"), "tape02 must be done");
        Assert.IsTrue(ObjectiveManager.Instance.hasSealingTape, "Single tape roll must be reused for both windows");
        Assert.IsFalse(st.gameObject.activeSelf, "Tape object must be consumed visually");
    }

    [UnityTest]
    public IEnumerator C15_SecondAnnouncementNotBeforeHomePrep()
    {
        yield return LoadSceneAndWait();

        GameStateManager gs = Object.FindAnyObjectByType<GameStateManager>();
        yield return ReachTvAvailable(gs);
        yield return InteractTvAndReachPreparation(gs);

        Assert.AreEqual(GameState.Preparation, gs.CurrentState, "Must still be in Preparation before home prep");
        Assert.IsFalse(ObjectiveManager.Instance.equipmentEnabled, "Equipment must not be enabled");
        Assert.IsFalse(ObjectiveManager.Instance.secondAnnouncementReady, "Second announcement must not be ready");

        TVController tv = Object.FindAnyObjectByType<TVController>();
        Assert.NotNull(tv, "TV missing");
        Assert.IsFalse(tv.CanInteract, "TV must not be interactable during Preparation (only at TVAvailable or HomePrepared)");
    }

    [UnityTest]
    public IEnumerator C16_SecondAnnouncementOnlyAfterHomePrep()
    {
        yield return LoadSceneAndWait();

        GameStateManager gs = Object.FindAnyObjectByType<GameStateManager>();
        yield return ReachTvAvailable(gs);
        yield return InteractTvAndReachPreparation(gs);

        yield return CompletePrepAndHome();
        Assert.IsTrue(ObjectiveManager.Instance.AllPreparationsDone(), "All prep tasks must be done");
        Assert.IsTrue(ObjectiveManager.Instance.homePrepared, "Home must be prepared");
        Assert.IsTrue(ObjectiveManager.Instance.secondAnnouncementReady,
            "Second announcement must be ready after the water choice");

        // The second announcement now plays automatically on its own; no manual TV interaction required.
        yield return WaitEvacuationPrep(gs);
    }

    [UnityTest]
    public IEnumerator C17_ItemsActivateOnlyAfterSecondAnnouncement()
    {
        yield return LoadSceneAndWait();

        GameStateManager gs = Object.FindAnyObjectByType<GameStateManager>();
        yield return ReachTvAvailable(gs);
        yield return InteractTvAndReachPreparation(gs);

        PickupItem[] preItems = Object.FindObjectsByType<PickupItem>(FindObjectsSortMode.None);
        foreach (PickupItem it in preItems)
            if (it.itemName == "Bottled Water")
                Assert.IsTrue(it.CanInteract, "Bottled Water is collectible as soon as the game starts");
            else
                Assert.IsFalse(it.CanInteract, "Items must be locked before second announcement");

        yield return CompletePrepAndHome();
        Assert.IsTrue(ObjectiveManager.Instance.AllPreparationsDone());

        yield return WaitEvacuationPrep(gs);

        PickupItem[] postItems = Object.FindObjectsByType<PickupItem>(FindObjectsSortMode.None);
        foreach (PickupItem it in postItems)
            Assert.IsTrue(it.CanInteract, it.itemName + " must be interactable after second announcement");
    }

    [UnityTest]
    public IEnumerator C18_PromptsHiddenBeforeActivation()
    {
        yield return LoadSceneAndWait();

        GameStateManager gs = Object.FindAnyObjectByType<GameStateManager>();
        yield return ReachTvAvailable(gs);
        yield return InteractTvAndReachPreparation(gs);

        SealingTape st = Object.FindAnyObjectByType<SealingTape>();
        Assert.IsFalse(st.CanInteract, "SealingTape prompt must be hidden before a window is closed");

        PickupItem[] items = Object.FindObjectsByType<PickupItem>(FindObjectsSortMode.None);
        foreach (PickupItem it in items)
            if (it.itemName != "Bottled Water")
                Assert.IsFalse(it.CanInteract, it.itemName + " prompt must be hidden before second announcement");

        yield return CompletePrepAndHome();

        yield return WaitEvacuationPrep(gs);

        items = Object.FindObjectsByType<PickupItem>(FindObjectsSortMode.None);
        foreach (PickupItem it in items)
            Assert.IsTrue(it.CanInteract, it.itemName + " prompt must appear after second announcement");
    }

    [UnityTest]
    public IEnumerator C19_CollectAllFourAfterActivation()
    {
        yield return LoadSceneAndWait();

        GameStateManager gs = Object.FindAnyObjectByType<GameStateManager>();
        yield return ReachTvAvailable(gs);
        yield return InteractTvAndReachPreparation(gs);

        yield return CompletePrepAndHome();

        yield return WaitEvacuationPrep(gs);

        PickupItem[] items = Object.FindObjectsByType<PickupItem>(FindObjectsSortMode.None);
        foreach (PickupItem it in items)
            if (it.CanInteract) it.Interact(null);

        Assert.IsTrue(ObjectiveManager.Instance.AllEquipmentCollected(), "All four emergency items must be collected");
        Assert.IsFalse(ObjectiveManager.Instance.AllSafetyStepsDone(),
            "Safety steps (protective clothing) must still be pending before the clothing is worn");
    }

    [UnityTest]
    public IEnumerator C20_FinalEvacuationAfterAllFourCollected()
    {
        yield return LoadSceneAndWait();

        GameStateManager gs = Object.FindAnyObjectByType<GameStateManager>();
        yield return ReachTvAvailable(gs);
        yield return InteractTvAndReachPreparation(gs);

        yield return CompletePrepAndHome();

        yield return WaitEvacuationPrep(gs);

        PickupItem[] items = Object.FindObjectsByType<PickupItem>(FindObjectsSortMode.None);
        foreach (PickupItem it in items)
            if (it.CanInteract) it.Interact(null);
        Assert.IsTrue(ObjectiveManager.Instance.AllEquipmentCollected(), "All four emergency items must be collected");

        yield return WearProtectiveClothing();
        Assert.IsTrue(ObjectiveManager.Instance.evacuationPrepared,
            "Evacuation should be prepared once items are collected and clothing is worn");

        float start = Time.time;
        while (Time.time - start < completionBudget && gs.CurrentState != GameState.Completed)
            yield return null;
        Assert.AreEqual(GameState.Completed, gs.CurrentState,
            "Game must reach Completed after all four items collected and clothing worn");
        Assert.AreEqual("FINAL EVACUATION", ObjectiveManager.CurrentObjectiveDisplay);
    }

    [UnityTest]
    public IEnumerator C21_InventoryStartsEmpty()
    {
        yield return LoadSceneAndWait();

        InventoryManager inv = InventoryManager.Instance;
        Assert.NotNull(inv, "InventoryManager missing on the GameManager");
        Assert.AreEqual(0, inv.AllItems.Count, "Inventory must start empty");
        Assert.AreEqual(0, inv.Count("Bottled Water"), "No bottle at start");
    }

    [UnityTest]
    public IEnumerator C22_BottlePickup_EntersInventory()
    {
        yield return LoadSceneAndWait();

        yield return CollectPickup("Bottled Water");

        InventoryManager inv = InventoryManager.Instance;
        Assert.AreEqual(1, inv.Count("Bottled Water"), "Bottled water must be in the inventory");
        Assert.AreEqual(InventoryItemState.Collected, inv.StateOf("Bottled Water"),
            "Fresh pickup should be in the Collected state");
    }

    [UnityTest]
    public IEnumerator C23_CollectAllFour_PopulatesInventoryAndEquipment()
    {
        yield return LoadSceneAndWait();
        GameStateManager gs = Object.FindAnyObjectByType<GameStateManager>();
        yield return AdvanceToEvacuationPrep(gs);

        yield return CollectPickup("Bottled Water");
        yield return CollectPickup("Flashlight");
        yield return CollectPickup("Face Mask");
        yield return CollectPickup("Eye Protection");

        InventoryManager inv = InventoryManager.Instance;
        Assert.IsTrue(ObjectiveManager.Instance.AllEquipmentCollected(), "Equipment checklist must be done");
        Assert.IsTrue(inv.HasCollected("Bottled Water"), "Bottle in inventory");
        Assert.IsTrue(inv.HasCollected("Flashlight"), "Flashlight in inventory");
        Assert.IsTrue(inv.HasCollected("Face Mask"), "Face Mask in inventory");
        Assert.IsTrue(inv.HasCollected("Eye Protection"), "Eye Protection in inventory");
        Assert.AreEqual(4, ObjectiveManager.Instance.equipment.Count,
            "Exactly four emergency items are required for evacuation");
    }

    [UnityTest]
    public IEnumerator C24_InventoryTab_TogglesOpen_AndFreezesControls()
    {
        yield return LoadSceneAndWait();

        InventoryUI ui = InventoryUI.Instance;
        Assert.NotNull(ui, "InventoryUI missing");
        Assert.IsFalse(ui.IsOpen, "Inventory must start closed");

        GameObject fpc = GameObject.Find("First Person Controller");
        Assert.NotNull(fpc, "Player missing");

        ui.Toggle();
        Assert.IsTrue(ui.IsOpen, "Inventory must open after the toggle");
        Assert.IsFalse(fpc.activeSelf, "Player controls must be frozen while the inventory is open");

        ui.Toggle();
        Assert.IsFalse(ui.IsOpen, "Inventory must close after the second toggle");
        Assert.IsTrue(fpc.activeSelf, "Player controls must be restored after closing the inventory");
    }

    [UnityTest]
    public IEnumerator C25_DrinkWithoutBottle_Fails()
    {
        yield return LoadSceneAndWait();

        InventoryManager inv = InventoryManager.Instance;
        Assert.IsFalse(inv.DrinkNamed("Bottled Water"), "Drinking without a bottle must fail");
        Assert.IsFalse(ObjectiveManager.Instance.waterChoiceMade, "No water choice can be made without a bottle");
    }

    [UnityTest]
    public IEnumerator C26_DrinkBottleFromInventory_AdvancesWaterStep()
    {
        yield return LoadSceneAndWait();
        GameStateManager gs = Object.FindAnyObjectByType<GameStateManager>();

        yield return ReachTvAvailable(gs);
        yield return InteractTvAndReachPreparation(gs);
        yield return CollectPickup("Bottled Water");
        yield return CompleteAllPreparations();
        yield return WaitWaterStepActive();

        InventoryManager inv = InventoryManager.Instance;
        Assert.IsTrue(inv.DrinkNamed("Bottled Water"), "Bottle must be drinkable from the inventory");
        Assert.IsTrue(ObjectiveManager.Instance.waterChoiceMade, "Drinking from inventory must register the choice");
        Assert.IsTrue(ObjectiveManager.Instance.safeWaterChosen, "Inventory bottle must count as safe water");

        yield return WaitSecondAnnouncementReady();
        Assert.IsTrue(ObjectiveManager.Instance.secondAnnouncementReady,
            "Drinking the inventory bottle must lead to the second news");
    }

    [UnityTest]
    public IEnumerator C27_BottleConsumed_AfterDrinking()
    {
        yield return LoadSceneAndWait();

        yield return CollectPickup("Bottled Water");
        InventoryManager inv = InventoryManager.Instance;
        Assert.AreEqual(1, inv.Count("Bottled Water"));

        Assert.IsTrue(inv.DrinkNamed("Bottled Water"), "Bottle drinkable");
        Assert.AreEqual(0, inv.Count("Bottled Water"), "No bottles left after drinking");
        Assert.AreEqual(InventoryItemState.Consumed, inv.StateOf("Bottled Water"),
            "Drinking must mark the item Consumed");
        Assert.IsFalse(inv.HasItem("Bottled Water"), "Consumed bottle is no longer usable");
    }

    [UnityTest]
    public IEnumerator C28_EmergencyBag_PacksIntoInventory()
    {
        yield return LoadSceneAndWait();
        GameStateManager gs = Object.FindAnyObjectByType<GameStateManager>();
        yield return AdvanceToEvacuationPrep(gs);

        EmergencyBagPickup bag = Object.FindAnyObjectByType<EmergencyBagPickup>();
        Assert.NotNull(bag, "Emergency bag pickup missing");
        float t = Time.time;
        while (Time.time - t < prepBudget && !bag.CanInteract)
            yield return null;
        Assert.IsTrue(bag.CanInteract, "Emergency bag should be collectible during evacuation prep");
        bag.Interact(null);

        InventoryManager inv = InventoryManager.Instance;
        Assert.IsTrue(inv.HasItem("Emergency Bag"), "Emergency bag must be in the inventory");
        Assert.AreEqual(4, ObjectiveManager.Instance.equipment.Count,
            "Emergency bag must NOT add an equipment checklist task");
    }

    [UnityTest]
    public IEnumerator C29_ClothingPickup_AddsToInventory_NotWornYet()
    {
        yield return LoadSceneAndWait();
        GameStateManager gs = Object.FindAnyObjectByType<GameStateManager>();
        yield return AdvanceToEvacuationPrep(gs);

        ProtectiveClothing pc = Object.FindAnyObjectByType<ProtectiveClothing>();
        Assert.NotNull(pc, "ProtectiveClothing missing");
        yield return PickupClothingIntoInventory();

        Assert.IsFalse(pc.IsWorn, "Clothing must not be worn on pickup alone");
        Assert.IsFalse(ObjectiveManager.Instance.protectiveClothingDone,
            "Objective must stay pending until the clothing is worn from the inventory");
    }

    [UnityTest]
    public IEnumerator C30_Flashlight_EquipToggle()
    {
        yield return LoadSceneAndWait();
        GameStateManager gs = Object.FindAnyObjectByType<GameStateManager>();
        yield return AdvanceToEvacuationPrep(gs);

        yield return CollectPickup("Flashlight");
        InventoryManager inv = InventoryManager.Instance;

        Assert.IsTrue(inv.EquipToggleNamed("Flashlight"), "Flashlight equip should succeed");
        Assert.AreEqual(InventoryItemState.Equipped, inv.StateOf("Flashlight"),
            "Flashlight should be Equipped after equipping");

        Assert.IsTrue(inv.EquipToggleNamed("Flashlight"), "Flashlight unequip should succeed");
        Assert.AreEqual(InventoryItemState.Available, inv.StateOf("Flashlight"),
            "Flashlight should go back to Available when unequipped");
    }

    [UnityTest]
    public IEnumerator C31_InventoryCleared_OnSessionReset()
    {
        yield return LoadSceneAndWait();

        InventoryManager inv = InventoryManager.Instance;
        yield return CollectPickup("Bottled Water");
        Assert.AreEqual(1, inv.AllItems.Count);

        inv.Clear();
        Assert.AreEqual(0, inv.AllItems.Count, "Clearing must empty the inventory for a fresh session");
        Assert.AreEqual(0, inv.Count("Bottled Water"));
    }

    [UnityTest]
    public IEnumerator C32_EvacuationBlocked_WithoutAllFourCollected()
    {
        yield return LoadSceneAndWait();
        GameStateManager gs = Object.FindAnyObjectByType<GameStateManager>();
        yield return AdvanceToEvacuationPrep(gs);

        yield return CollectPickup("Flashlight");
        yield return CollectPickup("Face Mask");
        yield return CollectPickup("Eye Protection");
        yield return WearProtectiveClothing();

        Assert.IsFalse(ObjectiveManager.Instance.evacuationPrepared,
            "Evacuation must not be ready while the bottled water is still missing from the inventory");
    }

    [UnityTest]
    public IEnumerator C33_EvacuationBlocked_WhenClothingNotWorn()
    {
        yield return LoadSceneAndWait();
        GameStateManager gs = Object.FindAnyObjectByType<GameStateManager>();
        yield return AdvanceToEvacuationPrep(gs);

        yield return CollectAllFour();
        yield return PickupClothingIntoInventory();

        Assert.IsFalse(ObjectiveManager.Instance.protectiveClothingDone,
            "Clothing must not be marked worn on pickup");
        Assert.IsFalse(ObjectiveManager.Instance.evacuationPrepared,
            "Evacuation must not be ready before the clothing is worn from the inventory");
    }

    [UnityTest]
    public IEnumerator C34_FullInventoryDrivenCompletion()
    {
        yield return LoadSceneAndWait();

        GameStateManager gs = Object.FindAnyObjectByType<GameStateManager>();
        yield return ReachTvAvailable(gs);
        yield return InteractTvAndReachPreparation(gs);

        yield return CollectPickup("Bottled Water");
        yield return CompleteAllPreparations();
        yield return WaitWaterStepActive();

        InventoryManager inv = InventoryManager.Instance;
        Assert.IsTrue(inv.DrinkNamed("Bottled Water"), "Drink the inventory bottle when thirsty");

        yield return WaitSecondAnnouncementReady();
        yield return WaitEvacuationPrep(gs);

        yield return CollectPickup("Flashlight");
        yield return CollectPickup("Face Mask");
        yield return CollectPickup("Eye Protection");
        yield return WearProtectiveClothing();

        Assert.IsTrue(ObjectiveManager.Instance.evacuationPrepared,
            "Evacuation prepared once the real inventory + worn clothing are verified");

        float start = Time.time;
        while (Time.time - start < completionBudget && gs.CurrentState != GameState.Completed)
            yield return null;
        Assert.AreEqual(GameState.Completed, gs.CurrentState,
            "Full inventory-driven path must reach Completed");
    }

    [UnityTest]
    public IEnumerator C35_WorldWaterChoice_StillWorks()
    {
        yield return LoadSceneAndWait();

        GameStateManager gs = Object.FindAnyObjectByType<GameStateManager>();
        yield return ReachTvAvailable(gs);
        yield return InteractTvAndReachPreparation(gs);
        yield return CompletePrepAndHome();

        Assert.IsTrue(ObjectiveManager.Instance.waterChoiceMade, "World water choice must still register");
        Assert.IsTrue(ObjectiveManager.Instance.safeWaterChosen, "Safe world water must count as safe");
        Assert.IsTrue(ObjectiveManager.Instance.secondAnnouncementReady,
            "World water choice must still lead to the second news");
    }

    [UnityTest]
    public IEnumerator C36_FinalEvacuation_VerifiesActualInventory()
    {
        yield return LoadSceneAndWait();

        GameStateManager gs = Object.FindAnyObjectByType<GameStateManager>();
        yield return AdvanceToEvacuationPrep(gs);

        yield return CollectPickup("Bottled Water");
        yield return CollectPickup("Flashlight");
        yield return CollectPickup("Face Mask");
        yield return CollectPickup("Eye Protection");
        yield return WearProtectiveClothing();

        InventoryManager inv = InventoryManager.Instance;
        Assert.IsTrue(inv.HasCollected("Bottled Water"), "Verified: bottle collected");
        Assert.IsTrue(inv.HasCollected("Flashlight"), "Verified: flashlight collected");
        Assert.IsTrue(inv.HasCollected("Face Mask"), "Verified: mask collected");
        Assert.IsTrue(inv.HasCollected("Eye Protection"), "Verified: goggles collected");
        Assert.AreEqual(InventoryItemState.Equipped, inv.StateOf("Protective Clothing"),
            "Verified: clothing worn (state Equipped)");

        Assert.IsTrue(ObjectiveManager.Instance.evacuationPrepared,
            "Evacuation must be prepared after the inventory state is verified");

        float start = Time.time;
        while (Time.time - start < completionBudget && gs.CurrentState != GameState.Completed)
            yield return null;
        Assert.AreEqual(GameState.Completed, gs.CurrentState,
            "Verified inventory path must reach Completed");
    }

    [UnityTest]
    public IEnumerator C37_AshfallStartsAfterEruption_AndPersists()
    {
        yield return LoadSceneAndWait();

        GameStateManager gs = Object.FindAnyObjectByType<GameStateManager>();
        yield return WaitForState(gs, GameState.Eruption, eruptionBudget);
        Assert.AreEqual(GameState.Eruption, gs.CurrentState, "Eruption never started");

        float t = Time.time;
        while (Time.time - t < prepBudget && !AshfallController.Active)
            yield return null;
        Assert.IsTrue(AshfallController.Active, "Ashfall must activate when the eruption starts");

        VolcanoEventManager vem = Object.FindAnyObjectByType<VolcanoEventManager>();
        Assert.NotNull(vem, "VolcanoEventManager missing");
        Assert.NotNull(vem.ashFallEffect, "AshFallEffect missing on the manager");
        Assert.IsTrue(vem.ashFallEffect.isPlaying, "Ash particle system must be playing during the eruption");
        Assert.IsTrue(vem.ashFallEffect.main.loop, "Ash must loop and keep falling");
        Assert.IsTrue(vem.ashFallEffect.main.simulationSpace == ParticleSystemSimulationSpace.World,
            "Ash must simulate in world space around the house");
        Assert.NotNull(AshfallController.Instance != null ? AshfallController.Instance.RoofBlocker : null,
            "Roof blocker must exist so ash stays outside the house");

        yield return ReachTvAvailable(gs);
        Assert.IsTrue(vem.ashFallEffect.isPlaying,
            "Ash must keep falling after the earthquake while the TV news becomes available");
        Assert.IsTrue(AshfallController.Active, "Ashfall must remain active later in the game");
        Assert.IsFalse(vem.effectsActive, "The smoke burst is over, but the ashfall continues separately");
    }

    [UnityTest]
    public IEnumerator C38_AshfallBlockedAtRoof_StaysOutside()
    {
        yield return LoadSceneAndWait();

        GameStateManager gs = Object.FindAnyObjectByType<GameStateManager>();
        yield return WaitForState(gs, GameState.Eruption, eruptionBudget);
        Assert.AreEqual(GameState.Eruption, gs.CurrentState, "Eruption never started");

        float t = Time.time;
        while (Time.time - t < prepBudget && !AshfallController.Active)
            yield return null;
        Assert.IsTrue(AshfallController.Active, "Ashfall must be active after the eruption");

        GameObject house = GameObject.Find("House");
        Assert.NotNull(house, "House missing");
        Bounds houseBounds = new Bounds();
        bool any = false;
        Collider[] colliders = house.GetComponentsInChildren<Collider>();
        foreach (Collider c in colliders)
        {
            if (!any) { houseBounds = c.bounds; any = true; }
            else houseBounds.Encapsulate(c.bounds);
        }
        Assert.IsTrue(any, "House must have colliders to bound the roof");

        VolcanoEventManager vem = Object.FindAnyObjectByType<VolcanoEventManager>();
        ParticleSystem ash = vem != null ? vem.ashFallEffect : null;
        Assert.NotNull(ash, "AshFallEffect missing");
        Assert.IsTrue(ash.collision.enabled, "Ash collision must be enabled so ash dies on the roof");
        Assert.GreaterOrEqual(ash.collision.lifetimeLoss.constant, 0.9f,
            "Ash must die on the roof collision instead of passing through it");

        BoxCollider blocker = AshfallController.Instance != null ? AshfallController.Instance.RoofBlocker : null;
        Assert.NotNull(blocker, "Roof blocker collider must exist");

        float blockerTop = blocker.transform.position.y + blocker.transform.lossyScale.y * blocker.size.y * 0.5f;
        Assert.Less(Mathf.Abs(blockerTop - houseBounds.max.y), 2f,
            "Roof blocker must sit on the house roof so ash cannot fall through it");
        Assert.GreaterOrEqual(blocker.size.x, houseBounds.size.x, "Blocker must cover the entire roof width");
        Assert.GreaterOrEqual(blocker.size.z, houseBounds.size.z, "Blocker must cover the entire roof depth");

        float emitterBottom = ash.transform.position.y - ash.shape.scale.y * 0.5f;
        Assert.GreaterOrEqual(emitterBottom, houseBounds.max.y - 0.1f,
            "Ash emission box must start at/above the roof so particles never spawn inside the house");
    }

    [UnityTest]
    public IEnumerator C39_AshfallLayers_ConfiguredForEruptionScene()
    {
        yield return LoadSceneAndWait();

        GameStateManager gs = Object.FindAnyObjectByType<GameStateManager>();
        yield return WaitForState(gs, GameState.Eruption, eruptionBudget);
        Assert.AreEqual(GameState.Eruption, gs.CurrentState, "Eruption never started");

        float t = Time.time;
        while (Time.time - t < prepBudget && !AshfallController.Active)
            yield return null;
        Assert.IsTrue(AshfallController.Active, "Ashfall must be active after the eruption");

        AshfallController c = AshfallController.Instance;
        Assert.NotNull(c, "AshfallController missing");
        Assert.NotNull(c.AshSystem, "Medium ash system missing");
        Assert.NotNull(c.NearSpecksSystem, "Near ash specks system missing");
        Assert.NotNull(c.HazeSystem, "Near haze system missing");
        Assert.NotNull(c.FarHazeSystem, "Distant haze system missing");
        Assert.NotNull(c.PlumeSystem, "Eruption plume system missing");
        Assert.NotNull(c.RoofBlocker, "Roof blocker missing");

        Assert.IsTrue(c.AshSystem.isPlaying, "Medium ash must be playing");
        Assert.IsTrue(c.AshSystem.main.loop, "Medium ash must loop");
        Assert.IsTrue(c.PlumeSystem.main.loop && c.PlumeSystem.main.simulationSpace == ParticleSystemSimulationSpace.World,
            "Plume must loop in world space near the volcano");

        Assert.IsTrue(c.PlumeSystem.textureSheetAnimation.enabled,
            "Plume must use the smoke sprite sheet flipbook for billowing clouds");
        Assert.AreEqual(2, c.PlumeSystem.textureSheetAnimation.numTilesX,
            "Smoke sheet is a 2x2 flipbook");
        Assert.AreEqual(2, c.PlumeSystem.textureSheetAnimation.numTilesY,
            "Smoke sheet is a 2x2 flipbook");

        Assert.IsTrue(c.NearSpecksSystem.collision.enabled,
            "Near specks must collide so they die on the roof/walls");
        Assert.GreaterOrEqual(c.NearSpecksSystem.collision.lifetimeLoss.constant, 0.9f,
            "Near specks must die on collision instead of passing into the house");

        GameObject house = GameObject.Find("House");
        Assert.NotNull(house, "House missing");
        Bounds houseBounds = new Bounds();
        bool any = false;
        Collider[] colliders = house.GetComponentsInChildren<Collider>();
        foreach (Collider col in colliders)
        {
            if (!any) { houseBounds = col.bounds; any = true; }
            else houseBounds.Encapsulate(col.bounds);
        }
        Assert.IsTrue(any, "House must have colliders to bound the roof");

        float specksBottom = c.NearSpecksSystem.transform.position.y - c.NearSpecksSystem.shape.scale.y * 0.5f;
        Assert.GreaterOrEqual(specksBottom, houseBounds.max.y - 0.1f,
            "Near specks emission box must start at/above the roof so ash never spawns inside");
    }

    [UnityTest]
    public IEnumerator C40_AshAtmosphere_AppliedOnEruption_RestoredOnCompletion()
    {
        yield return LoadSceneAndWait();

        GameStateManager gs = Object.FindAnyObjectByType<GameStateManager>();
        yield return WaitForState(gs, GameState.Eruption, eruptionBudget);
        Assert.AreEqual(GameState.Eruption, gs.CurrentState, "Eruption never started");

        float t = Time.time;
        while (Time.time - t < prepBudget && !AshfallController.Active)
            yield return null;
        Assert.IsTrue(AshfallController.Active, "Ashfall must be active after the eruption");

        AshfallController c = AshfallController.Instance;
        Assert.NotNull(c, "AshfallController missing");
        Assert.IsTrue(c.AtmosphereActive, "Volcanic atmosphere must be applied once the eruption starts");
        Assert.IsTrue(RenderSettings.fog, "Distance fog must be enabled for the hazy atmosphere");
        Assert.AreEqual(FogMode.ExponentialSquared, RenderSettings.fogMode,
            "Fog mode must be exponential squared for soft distant haze");
        Assert.Greater(RenderSettings.fogDensity, 0f, "Fog density must be positive");

        Light sun = null;
        foreach (Light l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
            if (l.type == LightType.Directional) { sun = l; break; }
        Assert.NotNull(sun, "Directional light missing");
        Assert.Less(sun.intensity, 0.9f, "Sunlight must be dimmed during the ashfall");

        gs.SetState(GameState.Completed);
        float t2 = Time.time;
        while (Time.time - t2 < prepBudget && AshfallController.Instance.AtmosphereActive)
            yield return null;

        Assert.IsFalse(c.AtmosphereActive, "Atmosphere must be restored once the game completes");
        Assert.IsFalse(RenderSettings.fog, "Fog must be restored when the ashfall stops");
    }
}
