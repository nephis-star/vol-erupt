using UnityEngine;

public class InterviewerNPC : Interactable
{
    void Start() { OnStart(); }

    public override void Interact(GameObject interactor)
    {
        if (!CanInteract) return;
        QuizUI quiz = QuizUI.Instance;
        if (quiz == null) quiz = QuizUI.EnsureInstance();
        if (quiz == null || quiz.IsInterviewActive()) return;
        quiz.StartInterview();
    }
}