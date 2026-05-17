using System;

[Serializable]
public class Question
{
    public string id;
    public string question;
    public string[] options; // [A, B, C, D]
    public int correctAnswerIndex;
    public string dificultad;
    public int puntaje;
}

[Serializable]
public class QuestionPool
{
    public string status;
    public Question[] questions;
}

[Serializable]
public class FeedbackRequest
{
    public int score;
    public int total;
}

[Serializable]
public class FeedbackData
{
    public string mensaje_general;
    public string[] strengths; // Ajustado por si acaso tu schema usa fortalezas/strengths
    public string[] weaknesses; 
    public string[] fortalezas;  // Mapeo bilingüe seguro
    public string[] areas_mejora;
}