using System;

[Serializable]
public class Question
{
    public string id;
    public string text;
    public string[] options; // [A, B, C, D]
    public int correctAnswerIndex;
}

[Serializable]
public class QuestionPool
{
    public string status;
    public Question[] questions;
}