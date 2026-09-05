namespace YemenGuide.Web.Maintenance
{
    public static class MaintenanceTypeNames
    {
        public const string Full = "Full";
        public const string ReadOnly = "ReadOnly";
        public const string Partial = "Partial";

        public static string Normalize(string? value)
        {
            return value switch
            {
                ReadOnly => ReadOnly,
                Partial => Partial,
                _ => Full
            };
        }
    }
}
