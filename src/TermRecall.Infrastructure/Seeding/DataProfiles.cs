namespace TermRecall.Infrastructure.Seeding;

public static class DataProfiles
{
    public const string UserName = "user";
    public const string DemoName = "demo";

    public static string Root
    {
        get
        {
            var root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (string.IsNullOrWhiteSpace(root))
            {
                root = Path.GetTempPath();
            }

            return Path.Combine(root, "TermRecall");
        }
    }

    public static string UserDatabasePath => Path.Combine(Root, "termrecall.db");
    public static string DemoDatabasePath => Path.Combine(Root, "termrecall-demo.db");
    public static string MarkerPath => Path.Combine(Root, "active-profile.txt");

    public static bool IsDemoActive() => string.Equals(Read(), DemoName, StringComparison.Ordinal);

    public static string Read()
    {
        try
        {
            if (!File.Exists(MarkerPath))
            {
                return string.Empty;
            }

            return File.ReadAllText(MarkerPath).Trim();
        }
        catch (IOException)
        {
            return string.Empty;
        }
    }

    public static void Set(string profile)
    {
        Directory.CreateDirectory(Root);
        var value = string.Equals(profile, DemoName, StringComparison.Ordinal) ? DemoName : UserName;
        File.WriteAllText(MarkerPath, value);
    }

    public static string ResolveStartupProfile()
    {
        var current = Read();
        if (current is UserName or DemoName)
        {
            return current;
        }

#if DEBUG
        return DemoName;
#else
        return UserName;
#endif
    }
}
