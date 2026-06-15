using System;

public static class ScoringRules
{
    private static readonly int[][] BonusByLevelAndPosition = new int[][]
    {
        new int[] { 40, 25, 15, 10 },
        new int[] { 49, 34, 20, 10 },
        new int[] { 59, 39, 25, 15 }
    };

    public static int GetLevelCompletionBonusByPosition(int level, int position)
    {
        if (level < 1 || level > BonusByLevelAndPosition.Length)
            return 0;

        int[] bonuses = BonusByLevelAndPosition[level - 1];
        if (bonuses == null || bonuses.Length == 0)
            return 0;

        int index = Math.Max(0, position - 1);
        if (index >= bonuses.Length)
            index = bonuses.Length - 1;

        return bonuses[index];
    }

    public static int GetMaxLevelCompletionBonus()
    {
        int total = 0;
        for (int level = 1; level <= BonusByLevelAndPosition.Length; level++)
            total += GetLevelCompletionBonusByPosition(level, 1);
        return total;
    }

    public static string FormatScoreHud(int score, int maxScore)
    {
        return $"Puntaje: {score}/{maxScore}";
    }
}
