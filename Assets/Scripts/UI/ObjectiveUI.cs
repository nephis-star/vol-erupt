using System.Text;
using UnityEngine;
using UnityEngine.UI;

public class ObjectiveUI : MonoBehaviour
{
    public static ObjectiveUI Instance { get; private set; }

    Text objectiveText;
    Text listText;
    Image panel;

    void Awake()
    {
        Instance = this;
        Build();
        GameStateManager.OnStateChanged += OnStateChanged;
        ObjectiveManager.TasksChanged += Rebuild;
    }

    void OnDestroy()
    {
        GameStateManager.OnStateChanged -= OnStateChanged;
        ObjectiveManager.TasksChanged -= Rebuild;
    }

    void OnStateChanged(GameState state)
    {
        Rebuild();
    }

    void Start()
    {
        Rebuild();
    }

    void Build()
    {
        RectTransform canvasRt = (RectTransform)UICanvasBootstrap.EnsureCanvas().transform;

        panel = UICanvasBootstrap.MakeImage(canvasRt, "Objective Panel",
            new Vector2(-600f, 360f), new Vector2(560f, 440f), new Color(0f, 0f, 0f, 0.55f));

        objectiveText = UICanvasBootstrap.MakeText(canvasRt, "Objective Text",
            new Vector2(-600f, 500f), new Vector2(540f, 64f), 30, TextAnchor.MiddleCenter, new Color(1f, 0.9f, 0.4f));

        listText = UICanvasBootstrap.MakeText(canvasRt, "Objective List",
            new Vector2(-600f, 250f), new Vector2(540f, 440f), 26, TextAnchor.MiddleLeft, Color.white);
    }

    public string Heading
    {
        get { return objectiveText != null ? objectiveText.text : ""; }
    }

    public string ListText
    {
        get { return listText != null ? listText.text : ""; }
    }

    public void Rebuild()
    {
        if (objectiveText == null || listText == null) return;

        objectiveText.text = ObjectiveManager.CurrentObjectiveDisplay;

        StringBuilder sb = new StringBuilder();
        if (ObjectiveManager.Instance != null)
        {
            ObjectiveManager om = ObjectiveManager.Instance;

            if (om.homePrepared && !om.equipmentEnabled)
            {
                sb.Append("<color=#7cfc00>✓ HOME PREPARED</color>\n");
                sb.Append("Your home has been secured\nagainst volcanic ashfall.");
            }
            else if (om.equipmentEnabled)
            {
                sb.Append("<color=#ffcf40>EVACUATION PREPARATION</color>\n");
                foreach (ObjectiveManager.TaskEntry t in om.equipment)
                {
                    sb.Append(t.done ? "[x] " : "[ ] ");
                    sb.Append(t.displayName);
                    sb.Append("\n");
                }
                foreach (ObjectiveManager.TaskEntry t in om.safetySteps)
                {
                    sb.Append(t.done ? "[x] " : "[ ] ");
                    sb.Append(t.displayName);
                    sb.Append("\n");
                }
            }
            else if (om.preparationStarted)
            {
                sb.Append("<color=#7cfc00>REQUIRED</color>\n");
                foreach (ObjectiveManager.TaskEntry t in om.activeTasks)
                {
                    if (t.optional) continue;
                    sb.Append(t.done ? "[x] " : "[ ] ");
                    sb.Append(t.label);
                    sb.Append("\n");
                }
                sb.Append("\n<color=#9adcff>OPTIONAL</color>\n");
                foreach (ObjectiveManager.TaskEntry t in om.activeTasks)
                {
                    if (!t.optional) continue;
                    sb.Append(t.done ? "[x] " : "○ ");
                    sb.Append(t.label);
                    sb.Append("\n");
                }
            }
        }
        listText.text = sb.ToString();
    }
}