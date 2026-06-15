using System;
using UnityEngine;

public static class LevelProgressRules
{
    public const float PassPercentage = 0.6f;

    public static int GetRequiredCorrect(int totalQuestions)
    {
        if (totalQuestions <= 0)
            return 0;
        return Mathf.CeilToInt(totalQuestions * PassPercentage);
    }

    public static bool CanAdvance(int correctAnswers, int totalQuestions, bool lastQuestionReached)
    {
        if (totalQuestions <= 0)
            return lastQuestionReached;

        if (lastQuestionReached)
            return true;

        return correctAnswers >= GetRequiredCorrect(totalQuestions);
    }

    public static string FormatHudProgress(int level, int correct, int total)
    {
        if (total <= 0)
            return string.Empty;

        int stillNeeded = GetCorrectAnswersStillNeeded(correct, total);
        if (stillNeeded > 0)
            return $"N{level}: {correct}/{total} correctas — Necesitas {stillNeeded} más para avanzar (60%)";

        return $"N{level}: {correct}/{total} correctas — Umbral 60% alcanzado";
    }

    public static int GetRemainingQuestionOpportunities(int levelQuestionIndex, int totalQuestions, int lastAnsweredIndex = -1)
    {
        if (totalQuestions <= 0)
            return 0;

        int futureQuestions = Mathf.Max(0, totalQuestions - Mathf.Max(levelQuestionIndex + 1, 0));
        bool currentQuestionStillResolvable = levelQuestionIndex >= 0
            && levelQuestionIndex < totalQuestions
            && lastAnsweredIndex < levelQuestionIndex;

        return futureQuestions + (currentQuestionStillResolvable ? 1 : 0);
    }

    public static bool CanStillReachPassThreshold(int correctAnswers, int totalQuestions, int levelQuestionIndex, int lastAnsweredIndex = -1)
    {
        if (totalQuestions <= 0)
            return false;

        int required = GetRequiredCorrect(totalQuestions);
        if (correctAnswers >= required)
            return true;

        int remaining = GetRemainingQuestionOpportunities(levelQuestionIndex, totalQuestions, lastAnsweredIndex);
        return correctAnswers + remaining >= required;
    }

    public static int GetCorrectAnswersStillNeeded(int correctAnswers, int totalQuestions)
    {
        return Mathf.Max(0, GetRequiredCorrect(totalQuestions) - correctAnswers);
    }

    public static string FormatBlockedAtGoalMessage(int correctAnswers, int totalQuestions, int levelQuestionIndex, int lastAnsweredIndex = -1)
    {
        if (CanStillReachPassThreshold(correctAnswers, totalQuestions, levelQuestionIndex, lastAnsweredIndex))
        {
            int needed = GetCorrectAnswersStillNeeded(correctAnswers, totalQuestions);
            return $"Necesitas {needed} respuesta{(needed == 1 ? "" : "s")} correcta{(needed == 1 ? "" : "s")} más para avanzar (60%). Responde las preguntas pendientes del nivel.";
        }

        return "Ya no puedes alcanzar el 60%. Espera la última pregunta del nivel para continuar.";
    }
}
