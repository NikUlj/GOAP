using UnityEngine;

namespace CrashKonijn.Goap.Demos.Simple.Behaviours
{
    public class InventoryBehaviour : MonoBehaviour
    {
        public float HeldAppleNutrition { get; private set; }

        public void Put(float nutrition)
        {
            if (nutrition <= 0f)
                return;

            this.HeldAppleNutrition = nutrition;
        }

        public void SetHeldAppleNutrition(float nutrition)
        {
            this.HeldAppleNutrition = Mathf.Max(0f, nutrition);
        }

        public void Clear()
        {
            this.HeldAppleNutrition = 0f;
        }
    }
}
