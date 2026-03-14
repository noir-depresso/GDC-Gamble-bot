namespace Game.Core.Models
{
    // Shared combat-side data for both the player and the current enemy.
    public class Combatant
    {
        public string Name { get; set; } = string.Empty;
        public int Attack { get; set; }
        public int MaxHealth { get; set; }
        public int CurrentHealth { get; set; }
        public bool IsEnemy { get; set; }

        public bool IsDead => CurrentHealth <= 0;

        // Parameterless constructor supports serialization.
        public Combatant()
        {
        }

        // Main constructor used by the engine when creating fresh combatants.
        public Combatant(string name, int attack, int maxHealth, bool isEnemy)
        {
            Name = name;
            Attack = attack;
            MaxHealth = maxHealth;
            CurrentHealth = maxHealth;
            IsEnemy = isEnemy;
        }

        // Damage is clamped so effects cannot accidentally heal via negative values.
        public void TakeDamage(int amount)
        {
            if (amount < 0) amount = 0;
            CurrentHealth -= amount;
            if (CurrentHealth < 0) CurrentHealth = 0;
        }

        // Healing is capped at max health for consistent combat math.
        public void Heal(int amount)
        {
            if (amount < 0) amount = 0;
            CurrentHealth += amount;
            if (CurrentHealth > MaxHealth) CurrentHealth = MaxHealth;
        }
    }
}
