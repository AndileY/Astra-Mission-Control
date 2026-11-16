using UnityEngine;

namespace Astra
{
    public class AstraInteractable : MonoBehaviour
    {
        public FacilityType facilityType;
        public string facilityName;
        [TextArea] public string description;
        public float interactionRange = 4f;
        public bool isOnline;

        public void Interact(AstraGameManager game)
        {
            if (game == null) return;
            game.InteractWith(this);
        }

        public void PlayCompletionFeedback()
        {
            AstraFacilityFeedback feedback = GetComponent<AstraFacilityFeedback>();
            if (feedback == null) feedback = gameObject.AddComponent<AstraFacilityFeedback>();
            feedback.Play(this);
        }

        public void ResetCompletionFeedback()
        {
            AstraFacilityFeedback feedback = GetComponent<AstraFacilityFeedback>();
            if (feedback != null) feedback.ResetVisuals();
        }
    }
}
