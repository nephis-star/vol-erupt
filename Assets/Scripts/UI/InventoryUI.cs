using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

/// <summary>
/// Tab-toggleable screen-space inventory. Built entirely at runtime (the project creates all UI
/// through UICanvasBootstrap), so it works in the currently canvas-less scene and in PlayMode.
/// While open the mouse cursor is available and the first-person controller is frozen; closing
/// restores both. Slots show icon, name, description, quantity, state and an action button
/// (DRINK / WEAR / EQUIP / PACK) that routes to InventoryManager.
/// </summary>
public class InventoryUI : MonoBehaviour
{
    public static InventoryUI Instance { get; private set; }

    [Tooltip("Key that toggles the inventory (project uses the new Input System).")]
    public Key inventoryKey = Key.Tab;

    public bool IsOpen { get; private set; }

    GameObject panel;
    RectTransform slotRoot;
    bool panelBuilt;
    bool wasCursorLocked;
    bool wasCursorVisible;
    GameObject frozenPlayer;
    Sprite iconSprite;

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        BuildPanel();
        InventoryManager inv = InventoryManager.Instance;
        if (inv != null) inv.Changed += OnInventoryChanged;
    }

    void OnDestroy()
    {
        InventoryManager inv = InventoryManager.Instance;
        if (inv != null) inv.Changed -= OnInventoryChanged;
        if (Instance == this) Instance = null;
    }

    void Update()
    {
        if (Keyboard.current != null && Keyboard.current[inventoryKey].wasPressedThisFrame)
            Toggle();
    }

    public void Toggle()
    {
        if (IsOpen) Close();
        else Open();
    }

    public void Open()
    {
        if (IsOpen) return;
        if (!panelBuilt) BuildPanel();

        wasCursorLocked = Cursor.lockState == CursorLockMode.Locked;
        wasCursorVisible = Cursor.visible;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        frozenPlayer = GameObject.Find("First Person Controller");
        if (frozenPlayer != null) frozenPlayer.SetActive(false);

        IsOpen = true;
        if (panel != null) panel.SetActive(true);
        RebuildSlots();
    }

    public void Close()
    {
        if (!IsOpen) return;
        IsOpen = false;
        if (panel != null) panel.SetActive(false);

        if (frozenPlayer != null)
        {
            frozenPlayer.SetActive(true);
            frozenPlayer = null;
        }
        Cursor.visible = wasCursorVisible;
        if (wasCursorLocked) Cursor.lockState = CursorLockMode.Locked;
    }

    void OnInventoryChanged()
    {
        if (IsOpen) RebuildSlots();
    }

    #region UI construction

    void BuildPanel()
    {
        EnsureEventSystem();
        RectTransform canvasRt = (RectTransform)UICanvasBootstrap.EnsureCanvas().transform;

        panel = new GameObject("Inventory Panel", typeof(RectTransform));
        RectTransform panelRt = (RectTransform)panel.transform;
        panelRt.SetParent(canvasRt, false);
        panelRt.anchorMin = Vector2.zero;
        panelRt.anchorMax = Vector2.one;
        panelRt.offsetMin = Vector2.zero;
        panelRt.offsetMax = Vector2.zero;

        Image backdrop = panel.AddComponent<Image>();
        backdrop.color = new Color(0f, 0f, 0f, 0.82f);

        // Title
        Text title = UICanvasBootstrap.MakeText(panelRt, "Inventory Title",
            new Vector2(0f, 460f), new Vector2(1200f, 60f), 46, TextAnchor.MiddleCenter, Color.white);
        title.text = "INVENTORY  —  press " + inventoryKey + " to close";

        // Background card for the list.
        Image card = UICanvasBootstrap.MakeImage(panelRt, "Inventory Card",
            new Vector2(0f, 0f), new Vector2(1000f, 820f), new Color(0.06f, 0.09f, 0.14f, 0.96f));

        RectTransform cardRt = (RectTransform)card.transform;
        slotRoot = new GameObject("Slots", typeof(RectTransform)).GetComponent<RectTransform>();
        slotRoot.SetParent(cardRt, false);
        slotRoot.anchorMin = new Vector2(0f, 0f);
        slotRoot.anchorMax = new Vector2(1f, 1f);
        slotRoot.offsetMin = new Vector2(20f, 20f);
        slotRoot.offsetMax = new Vector2(-20f, -20f);

        panel.SetActive(false);
        panelBuilt = true;
    }

    void RebuildSlots()
    {
        if (!panelBuilt || slotRoot == null) return;

        for (int i = slotRoot.childCount - 1; i >= 0; i--)
            Destroy(slotRoot.GetChild(i).gameObject);

        InventoryManager inv = InventoryManager.Instance;
        if (inv == null || inv.AllItems.Count == 0)
        {
            Text empty = UICanvasBootstrap.MakeText(slotRoot, "Empty",
                new Vector2(0f, 0f), new Vector2(900f, 60f), 32, TextAnchor.MiddleCenter,
                new Color(0.75f, 0.75f, 0.75f));
            empty.text = "Nothing collected yet.";
            return;
        }

        float y = 0f;
        foreach (InventoryEntry entry in inv.AllItems)
        {
            CreateSlot(entry, y);
            y -= 120f;
        }
    }

    void CreateSlot(InventoryEntry entry, float y)
    {
        if (entry == null || entry.data == null) return;
        InventoryItemData d = entry.data;

        Transform slotTransform = new GameObject("Slot", typeof(RectTransform)).transform;
        slotTransform.SetParent(slotRoot, false);
        RectTransform row = (RectTransform)slotTransform;
        row.anchorMin = new Vector2(0f, 1f);
        row.anchorMax = new Vector2(1f, 1f);
        row.pivot = new Vector2(0.5f, 0.5f);
        row.offsetMin = new Vector2(10f, y - 110f);
        row.offsetMax = new Vector2(-10f, y);

        // Icon swatch.
        Image icon = UICanvasBootstrap.MakeImage(row, "Icon", new Vector2(-440f, 0f),
            new Vector2(70f, 70f), d.iconColor);
        if (iconSprite == null)
            iconSprite = MakeWhiteSprite();
        icon.sprite = iconSprite;

        // Name + description.
        Text nameText = UICanvasBootstrap.MakeText(row, "Name",
            new Vector2(-330f, 22f), new Vector2(700f, 40f), 30, TextAnchor.LowerLeft, Color.white);
        nameText.text = d.itemName;
        nameText.text += "  (x" + entry.quantity + ")";

        Text descText = UICanvasBootstrap.MakeText(row, "Desc",
            new Vector2(-330f, -14f), new Vector2(700f, 34f), 22, TextAnchor.UpperLeft,
            new Color(0.8f, 0.8f, 0.8f));
        descText.text = d.description + "\nState: " + entry.state;

        // Action button.
        Button button = MakeButton(row);
        button.onClick.AddListener(delegate { OnActionButton(d.id); });

        Text btnLabel = button.GetComponentInChildren<Text>();
        if (btnLabel != null) btnLabel.text = UseLabel(d);
    }

    void OnActionButton(string id)
    {
        InventoryManager inv = InventoryManager.Instance;
        if (inv != null) inv.UseNamed(id);
        if (IsOpen) RebuildSlots();
    }

    static string UseLabel(InventoryItemData d)
    {
        if (d == null) return "USE";
        if (d.consumable) return "DRINK";
        if (d.wearable) return "WEAR";
        if (d.equippable) return "PACK";
        return d.useLabel;
    }

    Button MakeButton(Transform parent)
    {
        GameObject go = new GameObject("Action", typeof(RectTransform));
        RectTransform rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = new Vector2(1f, 0.5f);
        rt.anchorMax = new Vector2(1f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(-110f, 0f);
        rt.sizeDelta = new Vector2(170f, 56f);

        Image bg = go.AddComponent<Image>();
        bg.color = new Color(0.12f, 0.42f, 0.75f, 1f);

        Button button = go.AddComponent<Button>();
        button.targetGraphic = bg;
        button.transition = Selectable.Transition.ColorTint;
        ColorBlock colors = button.colors;
        colors.highlightedColor = new Color(0.2f, 0.55f, 0.9f);
        colors.pressedColor = new Color(0.07f, 0.25f, 0.45f);
        button.colors = colors;

        Text label = UICanvasBootstrap.MakeText(rt, "Label", new Vector2(0f, 0f),
            new Vector2(160f, 40f), 28, TextAnchor.MiddleCenter, Color.white);
        return button;
    }

    static void EnsureEventSystem()
    {
        if (FindObjectOfType<EventSystem>() != null) return;
        GameObject go = new GameObject("EventSystem");
        go.AddComponent<EventSystem>();
        go.AddComponent<InputSystemUIInputModule>();
    }

    static Sprite MakeWhiteSprite()
    {
        Texture2D tex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
        Color[] pixels = new Color[16];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.white;
        tex.SetPixels(pixels);
        tex.Apply();
        tex.hideFlags = HideFlags.HideAndDontSave;
        return Sprite.Create(tex, new Rect(0f, 0f, 4f, 4f), new Vector2(0.5f, 0.5f), 100f);
    }

    #endregion
}