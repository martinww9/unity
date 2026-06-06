using System;

[Serializable]
public class Question
{
    public string id;
    public string question;
    public string[] options;
    public int correctAnswerIndex;
    public string dificultad;
    public int puntaje;
    public string explanation;
}

[Serializable]
public class LevelQuestionPool
{
    public int nivel;
    public string status;
    public Question[] questions;
}

[Serializable]
public class QuestionsFile
{
    public int schemaVersion;
    public string status;
    public LevelQuestionPool[] levels;
}

[Serializable]
public class GenerateResponse
{
    public string status;
    public string message;
}

[Serializable]
public class QuestionsStatusResponse
{
    public string status;
}

[Serializable]
public class FeedbackQuestionPayload
{
    public string id;
    public string question;
    public string[] options;
    public int correctAnswerIndex;
    public int nivel;
}

[Serializable]
public class FeedbackRequest
{
    public int score;
    public int total;
    public FeedbackQuestionPayload[] questions;
}

[Serializable]
public class FeedbackItem
{
    public string id;
    public string question;
    public string correct_option;
    public string explanation;
    public int nivel;
}

[Serializable]
public class FeedbackData
{
    public FeedbackItem[] items;
}
