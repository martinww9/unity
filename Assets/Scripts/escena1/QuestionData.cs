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
public class FeedbackRequest
{
    public int score;
    public int total;
    public int nivel = 3;
}

[Serializable]
public class FeedbackData
{
    public string mensaje_general;
    public string[] strengths;
    public string[] weaknesses;
    public string[] fortalezas;
    public string[] areas_mejora;
}
