using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class QuizUI : MonoBehaviour
{
    public static QuizUI Instance { get; private set; }

    public class QuestionData
    {
        public string question;
        public string[] answers;
        public int correctIndex;
    }

    static readonly QuestionData[] questions = new QuestionData[]
    {
        new QuestionData { question = "What should you do with doors and windows when volcanic ash begins falling?", answers = new[] { "Open them for ventilation", "Close them", "Leave them halfway open", "Remove them" }, correctIndex = 1 },
        new QuestionData { question = "What should you do with air conditioners, heaters, and fans during heavy ashfall?", answers = new[] { "Turn them off", "Turn them to maximum", "Open all vents", "Leave them running" }, correctIndex = 0 },
        new QuestionData { question = "What can be used to help seal gaps around doors and windows?", answers = new[] { "Dry paper", "Damp towels", "Leaves", "Sand" }, correctIndex = 1 },
        new QuestionData { question = "Why should you save clean water during a volcanic eruption?", answers = new[] { "Water supplies may become contaminated or interrupted", "To wash the volcano", "To cool lava", "To clean the roads" }, correctIndex = 0 },
        new QuestionData { question = "What should you do when heavy volcanic ash begins falling?", answers = new[] { "Go outside", "Seek shelter inside a sturdy building", "Go to a river valley", "Move toward the volcano" }, correctIndex = 1 },
        new QuestionData { question = "What should you wear to protect your breathing from volcanic ash?", answers = new[] { "Sunglasses", "N95 respirator mask", "Sandals", "Hat only" }, correctIndex = 1 },
        new QuestionData { question = "What should you use to protect your eyes from volcanic ash?", answers = new[] { "Contact lenses", "Goggles or eyeglasses", "Nothing", "Wet tissue over one eye" }, correctIndex = 1 },
        new QuestionData { question = "What clothing helps protect your skin from volcanic ash?", answers = new[] { "Short sleeves and shorts", "Long-sleeved shirt and long pants", "Swimsuit", "No clothing" }, correctIndex = 1 },
        new QuestionData { question = "Which areas should you avoid during volcanic hazards?", answers = new[] { "River valleys and low-lying areas", "High and designated evacuation areas", "Evacuation centers", "Shelters" }, correctIndex = 0 },
        new QuestionData { question = "Where should you get reliable updates during the eruption?", answers = new[] { "Random social media posts", "Official emergency updates", "Rumors from strangers", "Old information" }, correctIndex = 1 }
    };

    static readonly string[] closingLines =
    {
        "Thank you for answering the questions.",
        "Your experience can help us understand how people respond during a volcanic eruption.",
        "Remember to stay informed, follow official evacuation instructions, and protect yourself from volcanic ash.",
        "Take care, and stay safe."
    };

    Canvas canvas;
    GameObject introPanel;
    GameObject quizPanel;
    GameObject talkPanel;
    Text quizTitle;
    Text introText;
    Text questionCounter;
    Text questionText;
    Button continueBtn;
    Button closeBtn;
    Text scoreTitle;
    Text scoreValue;
    Text scoreReminder;
    Text talkHeader;
    Text talkText;

    int currentQuestion = 0;
    int score = 0;
    int selectedAnswer = -1;
    bool quizActive = false;
    bool interviewActive = false;
    bool quizComplete = false;
    bool postQuizStarted = false;
    Coroutine pendingScoreFlow;
    Coroutine conversation;

    readonly List<Button> answerButtons = new List<Button>();
    static readonly Color NormalColor = new Color(0.12f, 0.42f, 0.75f, 1f);
    static readonly Color SelectedColor = new Color(0.35f, 0.65f, 1f, 1f);
    static readonly Color PanelColor = new Color(0.075f, 0.08f, 0.11f, 0.93f);
    static readonly Color AccentYellow = new Color(1f, 0.85f, 0.3f, 1f);
    static readonly Color SoftText = new Color(0.82f, 0.82f, 0.88f, 1f);

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        gameObject.SetActive(false);
    }

    void Update()
    {
        if (!interviewActive) return;
        if (Keyboard.current == null) return;

        if (introPanel != null && introPanel.activeInHierarchy)
        {
            if (Keyboard.current[Key.Enter].wasPressedThisFrame ||
                Keyboard.current[Key.NumpadEnter].wasPressedThisFrame)
            {
                OnContinueClicked();
            }
            return;
        }

        if (quizPanel == null || !quizPanel.activeInHierarchy) return;

        int count = answerButtons.Count;

        if (Keyboard.current[Key.DownArrow].wasPressedThisFrame || Keyboard.current[Key.S].wasPressedThisFrame)
        {
            if (count > 0) { selectedAnswer = (selectedAnswer + 1) % count; ShowSelection(); }
        }
        else if (Keyboard.current[Key.UpArrow].wasPressedThisFrame || Keyboard.current[Key.W].wasPressedThisFrame)
        {
            if (count > 0) { selectedAnswer = (selectedAnswer + count - 1) % count; ShowSelection(); }
        }
        else if (Keyboard.current[Key.Digit1].wasPressedThisFrame && count > 0) OnAnswer(0);
        else if (Keyboard.current[Key.Digit2].wasPressedThisFrame && count > 1) OnAnswer(1);
        else if (Keyboard.current[Key.Digit3].wasPressedThisFrame && count > 2) OnAnswer(2);
        else if (Keyboard.current[Key.Digit4].wasPressedThisFrame && count > 3) OnAnswer(3);
        else if (Keyboard.current[Key.Enter].wasPressedThisFrame || Keyboard.current[Key.NumpadEnter].wasPressedThisFrame)
        {
            if (quizComplete) BeginPostQuiz();
            else if (selectedAnswer >= 0 && selectedAnswer < count) OnAnswer(selectedAnswer);
        }
    }

    public static QuizUI EnsureInstance()
    {
        if (Instance != null) return Instance;
        var go = new GameObject("QuizUI");
        Instance = go.AddComponent<QuizUI>();
        return Instance;
    }

    public void StartInterview()
    {
        if (interviewActive) return;
        interviewActive = true;
        quizActive = false;
        currentQuestion = 0;
        score = 0;
        selectedAnswer = -1;
        quizComplete = false;
        postQuizStarted = false;
        gameObject.SetActive(true);
        if (canvas == null) BuildCanvas();
        introPanel.SetActive(true);
        quizPanel.SetActive(false);
        talkPanel.SetActive(false);
    }

    public void ShowQuiz()
    {
        StartInterview();
        OnContinueClicked();
    }

    public bool IsActive() { return quizActive; }

    public bool IsInterviewActive() { return interviewActive; }

    void BuildCanvas()
    {
        canvas = UICanvasBootstrap.EnsureCanvas();
        if (canvas == null)
        {
            var canvasGo = new GameObject("InterviewCanvas");
            canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            canvasGo.AddComponent<GraphicRaycaster>();
        }

        BuildIntroPanel();
        BuildQuizPanel();
        BuildTalkPanel();
    }

    void BuildIntroPanel()
    {
        introPanel = MakePanel(canvas.transform, "IntroPanel", new Vector2(0f, -170f), new Vector2(980f, 500f), PanelColor);

        UICanvasBootstrap.MakeText(introPanel.transform, "IntroTitle",
            new Vector2(0f, 190f), new Vector2(900f, 56f), 36, TextAnchor.MiddleCenter, AccentYellow).text = "EVACUATION INTERVIEW";

        UICanvasBootstrap.MakeText(introPanel.transform, "InterviewerLabel",
            new Vector2(0f, 110f), new Vector2(900f, 40f), 26, TextAnchor.MiddleCenter, SoftText).text = "Interviewer:";

        introText = UICanvasBootstrap.MakeText(introPanel.transform, "IntroText",
            new Vector2(0f, -15f), new Vector2(900f, 150f), 30, TextAnchor.MiddleCenter, Color.white);
        introText.text = "Can you tell me about your experience during the volcanic eruption?";

        continueBtn = CreateButton(introPanel.transform, "ContinueBtn", new Vector2(0f, -185f), new Vector2(280f, 64f));
        var continueLabel = continueBtn.GetComponentInChildren<Text>();
        if (continueLabel != null) continueLabel.text = "Continue";
        continueBtn.onClick.AddListener(OnContinueClicked);
    }

    void BuildQuizPanel()
    {
        quizPanel = MakePanel(canvas.transform, "QuizPanel", new Vector2(0f, 0f), new Vector2(1040f, 740f), PanelColor);

        quizTitle = UICanvasBootstrap.MakeText(quizPanel.transform, "QuizTitle",
            new Vector2(0f, 312f), new Vector2(960f, 50f), 34, TextAnchor.MiddleCenter, AccentYellow);
        quizTitle.text = "VOLCANIC ERUPTION INTERVIEW";

        questionCounter = UICanvasBootstrap.MakeText(quizPanel.transform, "QuestionCounter",
            new Vector2(0f, 268f), new Vector2(960f, 34f), 24, TextAnchor.MiddleCenter, SoftText);

        questionText = UICanvasBootstrap.MakeText(quizPanel.transform, "QuestionText",
            new Vector2(0f, 150f), new Vector2(960f, 110f), 28, TextAnchor.MiddleLeft, Color.white);

        closeBtn = CreateButton(quizPanel.transform, "CloseBtn", new Vector2(0f, -318f), new Vector2(170f, 46f));
        var closeLabel = closeBtn.GetComponentInChildren<Text>();
        if (closeLabel != null) closeLabel.text = "Close";
        closeBtn.onClick.AddListener(CloseQuiz);

        scoreTitle = UICanvasBootstrap.MakeText(quizPanel.transform, "ScoreTitle",
            new Vector2(0f, 180f), new Vector2(920f, 60f), 40, TextAnchor.MiddleCenter, AccentYellow);
        scoreTitle.text = "INTERVIEW COMPLETE";

        scoreValue = UICanvasBootstrap.MakeText(quizPanel.transform, "ScoreValue",
            new Vector2(0f, 86f), new Vector2(920f, 70f), 36, TextAnchor.MiddleCenter, Color.white);

        scoreReminder = UICanvasBootstrap.MakeText(quizPanel.transform, "ScoreReminder",
            new Vector2(0f, -26f), new Vector2(920f, 70f), 26, TextAnchor.MiddleCenter, SoftText);
        scoreReminder.text = "Remember these safety actions during a volcanic eruption.";

        scoreTitle.gameObject.SetActive(false);
        scoreValue.gameObject.SetActive(false);
        scoreReminder.gameObject.SetActive(false);
    }

    void BuildTalkPanel()
    {
        talkPanel = MakePanel(canvas.transform, "TalkPanel", new Vector2(0f, -190f), new Vector2(900f, 210f), PanelColor);

        talkHeader = UICanvasBootstrap.MakeText(talkPanel.transform, "TalkHeader",
            new Vector2(0f, 72f), new Vector2(860f, 36f), 28, TextAnchor.MiddleCenter, AccentYellow);
        talkHeader.text = "INTERVIEWER";

        talkText = UICanvasBootstrap.MakeText(talkPanel.transform, "TalkText",
            new Vector2(0f, -18f), new Vector2(840f, 112f), 26, TextAnchor.MiddleCenter, Color.white);
    }

    static GameObject MakePanel(Transform parent, string name, Vector2 anchoredPosition, Vector2 size, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = new Vector2(0.5f, 0.5f); rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = anchoredPosition;
        rt.sizeDelta = size;
        var bg = go.AddComponent<Image>();
        bg.sprite = UICanvasBootstrap.MakeWhiteSprite();
        bg.color = color;

        UICanvasBootstrap.MakeStretchedImage(go.transform, "Border", new Color(0.6f, 0.7f, 1f, 0.35f));

        return go;
    }

    void OnContinueClicked()
    {
        if (!interviewActive) return;
        introPanel.SetActive(false);
        quizPanel.SetActive(true);
        quizActive = true;
        ShowQuestion();
    }

    void ShowQuestion()
    {
        quizComplete = false;
        quizTitle.gameObject.SetActive(true);
        questionCounter.gameObject.SetActive(true);
        questionText.gameObject.SetActive(true);

        scoreTitle.gameObject.SetActive(false);
        scoreValue.gameObject.SetActive(false);
        scoreReminder.gameObject.SetActive(false);

        var closeLabel = closeBtn.GetComponentInChildren<Text>();
        if (closeLabel != null) closeLabel.text = "Close";
        closeBtn.onClick.RemoveAllListeners();
        closeBtn.onClick.AddListener(CloseQuiz);
        closeBtn.gameObject.SetActive(true);

        if (currentQuestion >= questions.Length) { ShowFinalScore(); return; }

        var q = questions[currentQuestion];
        questionCounter.text = "Question " + (currentQuestion + 1) + " / " + questions.Length;
        questionText.text = q.question;

        foreach (var b in answerButtons) Destroy(b.gameObject);
        answerButtons.Clear();

        for (int i = 0; i < q.answers.Length; i++)
        {
            var btn = CreateButton(quizPanel.transform, "Answer" + i, new Vector2(0f, 34f - i * 72f), new Vector2(900f, 60f));
            var label = btn.GetComponentInChildren<Text>();
            if (label != null) label.text = (char)('A' + i) + ". " + q.answers[i];
            int index = i;
            btn.onClick.AddListener(() => OnAnswer(index));
            answerButtons.Add(btn);
        }

        selectedAnswer = 0;
        ShowSelection();
    }

    void ShowSelection()
    {
        for (int i = 0; i < answerButtons.Count; i++)
        {
            var img = answerButtons[i].targetGraphic as Image;
            if (img != null) img.color = (i == selectedAnswer) ? SelectedColor : NormalColor;
        }
    }

    void OnAnswer(int index)
    {
        var q = questions[currentQuestion];
        if (index == q.correctIndex) { score++; if (InteractionUI.Instance != null) InteractionUI.Instance.ShowToast("Correct!"); }
        else { if (InteractionUI.Instance != null) InteractionUI.Instance.ShowToast("Incorrect."); }
        currentQuestion++;
        ShowQuestion();
    }

    void ShowFinalScore()
    {
        quizComplete = true;
        selectedAnswer = -1;
        postQuizStarted = false;

        quizTitle.gameObject.SetActive(false);
        questionCounter.gameObject.SetActive(false);
        questionText.gameObject.SetActive(false);
        closeBtn.gameObject.SetActive(false);

        foreach (var b in answerButtons) Destroy(b.gameObject);
        answerButtons.Clear();

        scoreTitle.gameObject.SetActive(true);
        scoreValue.gameObject.SetActive(true);
        scoreReminder.gameObject.SetActive(true);
        scoreTitle.text = "INTERVIEW COMPLETE";
        scoreValue.text = "Your Score: " + score + " / " + questions.Length;
        scoreReminder.text = "Remember these safety actions during a volcanic eruption.";

        pendingScoreFlow = StartCoroutine(WaitThenPostQuiz(2f));
    }

    IEnumerator WaitThenPostQuiz(float delay)
    {
        yield return new WaitForSeconds(delay);
        BeginPostQuiz();
    }

    void BeginPostQuiz()
    {
        if (postQuizStarted) return;
        postQuizStarted = true;
        if (pendingScoreFlow != null) { StopCoroutine(pendingScoreFlow); pendingScoreFlow = null; }
        if (conversation != null) { StopCoroutine(conversation); }
        conversation = StartCoroutine(PostQuizConversation());
    }

    IEnumerator PostQuizConversation()
    {
        quizPanel.SetActive(false);
        talkPanel.SetActive(true);

        for (int i = 0; i < closingLines.Length; i++)
        {
            talkText.text = closingLines[i];
            yield return new WaitForSeconds(2.2f);
        }

        CloseQuiz();
    }

    void CloseQuiz()
    {
        interviewActive = false;
        quizActive = false;
        quizComplete = false;
        postQuizStarted = false;
        if (pendingScoreFlow != null) { StopCoroutine(pendingScoreFlow); pendingScoreFlow = null; }
        if (conversation != null) { StopCoroutine(conversation); conversation = null; }

        if (introPanel != null) introPanel.SetActive(false);
        if (quizPanel != null) quizPanel.SetActive(false);
        if (talkPanel != null) talkPanel.SetActive(false);

        gameObject.SetActive(false);
    }

    static Button CreateButton(Transform parent, string name, Vector2 anchoredPosition, Vector2 size)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = new Vector2(0.5f, 0.5f); rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = anchoredPosition;
        rt.sizeDelta = size;
        Image bg = go.AddComponent<Image>();
        bg.sprite = UICanvasBootstrap.MakeWhiteSprite();
        bg.color = NormalColor;
        Button btn = go.AddComponent<Button>();
        btn.targetGraphic = bg;
        UICanvasBootstrap.MakeText(go.transform, "Label",
            new Vector2(0f, 0f), new Vector2(size.x - 20f, size.y - 14f), 24, TextAnchor.MiddleCenter, Color.white);
        return btn;
    }
}