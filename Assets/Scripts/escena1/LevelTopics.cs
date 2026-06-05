public static class LevelTopics
{
    public const int MaxLevel = 3;

    private static readonly string[] Topics =
    {
        "Estructuras de datos lineales",
        "Complejidad y árboles",
        "Grafos y tablas hash"
    };

    public static string GetTopic(int level)
    {
        if (level < 1 || level > MaxLevel)
            return string.Empty;
        return Topics[level - 1];
    }

    public static string FormatIndicator(int level)
    {
        string topic = GetTopic(level);
        if (string.IsNullOrEmpty(topic))
            return $"Nivel {level}/{MaxLevel}";
        return $"Nivel {level}/{MaxLevel}\n{topic}";
    }
}
