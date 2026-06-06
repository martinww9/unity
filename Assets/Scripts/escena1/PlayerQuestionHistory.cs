using System.Collections.Generic;

public struct SeenQuestion
{
    public int Level;
    public int Index;
    public Question Question;
}

public static class PlayerQuestionHistory
{
    private readonly struct SeenKey
    {
        public readonly int Level;
        public readonly int Index;

        public SeenKey(int level, int index)
        {
            Level = level;
            Index = index;
        }
    }

    private static readonly List<SeenKey> _order = new List<SeenKey>();
    private static readonly HashSet<(int level, int index)> _seen = new HashSet<(int, int)>();

    public static void Record(int level, int index)
    {
        if (_seen.Add((level, index)))
            _order.Add(new SeenKey(level, index));
    }

    public static SeenQuestion[] GetSeenQuestions(QuestionManager qm)
    {
        if (qm == null || _order.Count == 0)
            return System.Array.Empty<SeenQuestion>();

        var result = new List<SeenQuestion>(_order.Count);
        foreach (var key in _order)
        {
            Question q = qm.GetQuestion(key.Level, key.Index);
            if (q == null)
                continue;

            result.Add(new SeenQuestion
            {
                Level = key.Level,
                Index = key.Index,
                Question = q
            });
        }

        return result.ToArray();
    }

    public static void Clear()
    {
        _order.Clear();
        _seen.Clear();
    }
}
