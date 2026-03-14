namespace Game.Core.Effects
{
    // Central registry of status/effect keys so string usage stays consistent across the project.
    public static class EffectIds
    {
        // Economy and scaling effects.
        public const string BANK_ACCOUNT = "BANK_ACCOUNT";
        public const string BUY_LOW = "BUY_LOW";
        public const string SELL_HIGH = "SELL_HIGH";
        public const string LOAN_SHARK_DEBT = "LOAN_SHARK_DEBT";
        public const string STOCKS_BONDS = "STOCKS_BONDS";
        public const string CRYPTO_NEXT = "CRYPTO_NEXT";

        // Delayed utility and damage-conversion effects.
        public const string RAAN_DRAW = "RAAN_DRAW";
        public const string ENCHANT_NEXT_TURN = "ENCHANT_NEXT_TURN";
        public const string WANE_WAX_NEXT = "WANE_WAX_NEXT";
        public const string WANE_WAX_DAMAGE_TRACK = "WANE_WAX_DAMAGE_TRACK";

        // Defensive and reactive effects.
        public const string HEDGING_SHIELD = "HEDGING_SHIELD";
        public const string HEDGING_INCOME_PENALTY = "HEDGING_INCOME_PENALTY";
        public const string FIREWALL_READY = "FIREWALL_READY";
        public const string DISCREDIT_READY = "DISCREDIT_READY";
        public const string SUTURE_IMMUNE = "SUTURE_IMMUNE";
        public const string TRAUMA_TEAM_READY = "TRAUMA_TEAM_READY";

        // Misc combat state and follow-up effects.
        public const string GUARD_DOWN = "GUARD_DOWN";
        public const string RELEASE_FILES = "RELEASE_FILES";
        public const string EMP_IMMUNE = "EMP_IMMUNE";
        public const string HIRED_GUN_RETURN = "HIRED_GUN_RETURN";
        public const string PERSUASION_BUFF = "PERSUASION_BUFF";
        public const string PERSUASION_DEBUFF = "PERSUASION_DEBUFF";
        public const string SOCIAL_PRESSURE = "SOCIAL_PRESSURE";
    }
}
