using System;

namespace SysBot.Pokemon.Discord
{
    public static class BatchEditing
    {
        // Try to get a description/type for a batch-editing property.
        // This is a minimal implementation; extend with real property metadata.
        public static bool TryGetPropertyType(string propertyName, out string? result)
        {
            result = null;
            if (string.IsNullOrWhiteSpace(propertyName))
                return false;

            switch (propertyName.Trim().ToLowerInvariant())
            {
                case "level":
                    result = "int (0-100)";
                    return true;
                case "nickname":
                    result = "string";
                    return true;
                default:
                    result = null;
                    return false;
            }
        }
    }
}
