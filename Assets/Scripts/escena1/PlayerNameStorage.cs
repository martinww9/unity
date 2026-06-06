public static class PlayerNameStorage
{
    private const int MaxLength = 32;
    private const string DefaultName = "Jugador";

    private static string _sessionName;

    public static string Get()
    {
        string sanitized = Sanitize(_sessionName);
        return string.IsNullOrWhiteSpace(sanitized) ? DefaultName : sanitized;
    }

    public static void Set(string name)
    {
        _sessionName = Sanitize(name);
    }

    public static void Clear()
    {
        _sessionName = null;
    }

    public static string Sanitize(string name)
    {
        if (string.IsNullOrEmpty(name))
            return string.Empty;

        name = name.Replace("\n", " ").Replace("\r", " ").Trim();
        if (name.Length > MaxLength)
            name = name.Substring(0, MaxLength);

        return name;
    }
}
