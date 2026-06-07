using UnityEngine;

namespace CrashKonijn.Goap.Demos.Simple.Behaviours
{
    public class AppleBehaviour : MonoBehaviour
    {
        private static AppleCollection sharedAppleCollection;

        public float nutritionValue = 50f;
        public bool IsPickedUp { get; private set; }
        private AppleCollection appleCollection;
        
        private void Awake()
        {
            this.nutritionValue = Random.Range(80f, 150f);
            if (sharedAppleCollection == null)
                sharedAppleCollection = Compatibility.FindObjectOfType<AppleCollection>();

            this.appleCollection = sharedAppleCollection;
        }

        private void OnEnable()
        {
            this.appleCollection.Add(this);
        }
        
        private void OnDisable()
        {
            this.appleCollection.Remove(this);
        }

        public void PickUp()
        {
            this.IsPickedUp = true;
            this.GetComponentInChildren<SpriteRenderer>().enabled = false;
            this.appleCollection.Remove(this);
        }
        
        public void Drop()
        {
            this.IsPickedUp = false;
            this.appleCollection.Add(this);
        }
    }
}
