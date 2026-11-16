using UnityEngine;

namespace Astra
{
    /// <summary>Owns ASTRA's simplified, game-only resource values and time-step rules.</summary>
    public sealed class AstraResourceManager : MonoBehaviour
    {
        [Header("Game-only reserves (0–100)")]
        [SerializeField, Range(0f, 100f)] private float oxygen = 100f;
        [SerializeField, Range(0f, 100f)] private float water = 100f;
        [SerializeField, Range(0f, 100f)] private float food = 100f;
        [SerializeField, Range(0f, 100f)] private float power = 82f;
        [SerializeField, Range(0f, 100f)] private float habitatCondition = 100f;

        public float Oxygen => oxygen;
        public float Water => water;
        public float Food => food;
        public float Power => power;
        public float HabitatCondition => habitatCondition;

        public void SetMissionResources(float oxygenValue, float waterValue, float foodValue, float powerValue, float conditionValue)
        {
            oxygen = Mathf.Clamp(oxygenValue, 0f, 100f);
            water = Mathf.Clamp(waterValue, 0f, 100f);
            food = Mathf.Clamp(foodValue, 0f, 100f);
            power = Mathf.Clamp(powerValue, 0f, 100f);
            habitatCondition = Mathf.Clamp(conditionValue, 0f, 100f);
        }

        public string GetWarning()
        {
            if (oxygen <= 15f) return "CRITICAL OXYGEN  ·  Run the oxygen processor";
            if (water <= 15f) return "CRITICAL WATER  ·  Run the water recycler";
            if (power <= 10f) return "CRITICAL POWER  ·  Restore the solar array; powered equipment is offline";
            if (food <= 12f) return "CRITICAL FOOD  ·  Harvest at the greenhouse";
            if (habitatCondition <= 35f) return "HABITAT CONDITION LOW  ·  Inspect and stabilize the outpost";

            if (oxygen <= 35f) return "LOW OXYGEN  ·  Run the oxygen processor";
            if (water <= 35f) return "LOW WATER  ·  Run the water recycler";
            if (power <= 25f) return "LOW POWER  ·  Bring the solar array online";
            if (food <= 30f) return "LOW FOOD  ·  Harvest at the greenhouse";
            if (habitatCondition <= 60f) return "HABITAT NEEDS ATTENTION  ·  Inspect the habitat";
            return string.Empty;
        }

        public void AdvanceTimeStep(bool solarOnline, bool lifeSupportChecked)
        {
            // These rates intentionally represent game turns, not engineering data.
            float oxygenUse = lifeSupportChecked ? 0.8f : 1.4f;
            float waterUse = lifeSupportChecked ? 0.7f : 1.2f;
            if (power < 8f)
            {
                oxygenUse += 0.6f;
                waterUse += 0.5f;
            }

            oxygen = Mathf.Max(0f, oxygen - oxygenUse);
            water = Mathf.Max(0f, water - waterUse);
            food = Mathf.Max(0f, food - 0.8f);
            power = Mathf.Clamp(power + (solarOnline ? 1.4f : -2.2f), 0f, 100f);

            if (oxygen < 25f || water < 25f || food <= 0f || power < 18f)
                habitatCondition = Mathf.Max(0f, habitatCondition - (food <= 0f ? 3.5f : 2.5f));
        }

        public bool TrySpendPower(float amount)
        {
            if (amount < 0f || power < amount) return false;
            power = Mathf.Max(0f, power - amount);
            return true;
        }

        public void AddOxygen(float amount) => oxygen = Mathf.Clamp(oxygen + amount, 0f, 100f);
        public void AddWater(float amount) => water = Mathf.Clamp(water + amount, 0f, 100f);
        public void AddFood(float amount) => food = Mathf.Clamp(food + amount, 0f, 100f);
        public void AddPower(float amount) => power = Mathf.Clamp(power + amount, 0f, 100f);
        public bool TrySpendWater(float amount)
        {
            if (amount < 0f || water < amount) return false;
            water = Mathf.Max(0f, water - amount);
            return true;
        }
        public void DamageHabitat(float amount) => habitatCondition = Mathf.Max(0f, habitatCondition - amount);
        public void RepairHabitat(float amount) => habitatCondition = Mathf.Clamp(habitatCondition + amount, 0f, 100f);
    }
}
