namespace YargArchipelagoCommon
{
    public static class Versions
    {
        public const string Yarg = "3.0.0";
        public const string CloneHero = "0.1.0";
        public const string APWorld = "3.0.0";
        public const string Archipelago = "0.6.1";

        public static string ForGame(string game) => game == "Clone Hero" ? CloneHero : Yarg;
    }
}
