namespace Game.Core.Models
{
    public class Combatant
    {
        public string Name { get; set; } = string.Empty;
        public int Attack { get; set; }
        public int MaxHealth { get; set; }
        public int CurrentHealth { get; set; }
        public bool IsEnemy { get; set; }

        public bool IsDead => CurrentHealth <= 0;

        public Combatant()
        {
        }

        public Combatant(string name, int attack, int maxHealth, bool isEnemy)
        {
            Name = name;
            Attack = attack;
            MaxHealth = maxHealth;
            CurrentHealth = maxHealth;
            IsEnemy = isEnemy;
        }

        public void TakeDamage(int amount)
        {
            if (amount < 0) amount = 0;
            CurrentHealth -= amount;
            if (CurrentHealth < 0) CurrentHealth = 0;
        }

        public void Heal(int amount)
        {
            if (amount < 0) amount = 0;
            CurrentHealth += amount;
            if (CurrentHealth > MaxHealth) CurrentHealth = MaxHealth;
        }
    }
}
