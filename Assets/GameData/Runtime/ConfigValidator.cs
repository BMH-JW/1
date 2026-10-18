using System.Collections.Generic;

namespace JU.GameData
{
    public static class ConfigValidator
    {
        public static List<string> Validate(GameConfigData c) { return V3Validation.Validate(c); }
        public static List<string> ValidateForPlay(GameConfigData c) { return V3Validation.ForPlay(c, false); }
        public static List<string> ValidateForRelease(GameConfigData c) { return V3Validation.ForPlay(c, true); }
    }
}
