using UnityEngine;

namespace CrashKonijn.Goap.Demos.Simple.Behaviours
{
    public class TreeBehaviour : MonoBehaviour
    {
        public GameObject applePrefab;
        public float minAppleNutrition = 80f;
        public float maxAppleNutrition = 150f;

        public float CreateAppleNutrition()
        {
            return Random.Range(this.minAppleNutrition, this.maxAppleNutrition);
        }
    }
}
