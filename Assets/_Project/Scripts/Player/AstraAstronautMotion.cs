using System.Collections;
using UnityEngine;

namespace Astra
{
    /// <summary>Procedural, readable suit animation for the generated astronaut rig.</summary>
    public sealed class AstraAstronautMotion : MonoBehaviour
    {
        [SerializeField] private AstraPlayerController player;
        [SerializeField] private Transform modelRoot;
        [SerializeField] private Transform leftArmPivot;
        [SerializeField] private Transform rightArmPivot;
        [SerializeField] private Transform leftLegPivot;
        [SerializeField] private Transform rightLegPivot;

        private Quaternion leftArmRest;
        private Quaternion rightArmRest;
        private Quaternion leftLegRest;
        private Quaternion rightLegRest;
        private Quaternion modelRestRotation;
        private Vector3 modelRestPosition;
        private Vector3 importedModelRestPosition;
        private Quaternion importedModelRestRotation;
        private Transform importedModel;
        private float animationClock;
        private float blendedMove;
        private bool working;

        public void SetWorkingMotion(bool value) => working = value;

        private void Awake()
        {
            if (player == null) player = GetComponent<AstraPlayerController>();
            if (modelRoot != null)
            {
                modelRestPosition = modelRoot.localPosition;
                modelRestRotation = modelRoot.localRotation;
            }
            if (leftArmPivot != null) leftArmRest = leftArmPivot.localRotation;
            if (rightArmPivot != null) rightArmRest = rightArmPivot.localRotation;
            if (leftLegPivot != null) leftLegRest = leftLegPivot.localRotation;
            if (rightLegPivot != null) rightLegRest = rightLegPivot.localRotation;
        }

        private IEnumerator Start()
        {
            yield return null;
            AstraRuntimeModelLoader loader = AstraRuntimeModelLoader.Instance;
            if (loader == null) loader = FindFirstObjectByType<AstraRuntimeModelLoader>();
            if (loader == null)
            {
                Debug.LogWarning("ASTRA could not find the NASA runtime model loader. Keeping the fallback suit.");
                yield break;
            }
            // Use NASA's compact astronaut figure as the primary player model. The
            // Z-2 suit GLB is a single non-rigged mesh that has repeatedly failed
            // glTF import on lower-memory Unity installs, leaving the blockout suit
            // visible. The dedicated astronaut model is much smaller and still
            // receives the movement bob and lean animation below.
            loader.LoadInto("Astronaut", transform, 1.72f, instance =>
            {
                if (instance == null) return;
                instance.name = "NASA astronaut suit";
                foreach (Collider modelCollider in instance.GetComponentsInChildren<Collider>(true)) modelCollider.enabled = false;
                if (modelRoot != null) modelRoot.gameObject.SetActive(false);
                importedModel = instance.transform;
                importedModelRestPosition = importedModel.localPosition;
                importedModelRestRotation = importedModel.localRotation;
                if (player != null) player.RefreshAstronautRenderers();
            });
        }

        private void Update()
        {
            if (player == null) return;

            float dt = Time.deltaTime;
            blendedMove = Mathf.MoveTowards(blendedMove, working ? 0f : player.MoveAmount, dt * 5f);
            float cadence = player.IsSprinting ? 10f : 7.2f;
            animationClock += dt * Mathf.Lerp(1.8f, cadence, blendedMove);
            float breath = Mathf.Sin(Time.time * 1.65f);

            if (working)
            {
                float toolStroke = Mathf.Sin(Time.time * 9f);
                SetRotation(leftArmPivot, leftArmRest * Quaternion.Euler(-64f + toolStroke * 17f, 0f, -14f), dt, 16f);
                SetRotation(rightArmPivot, rightArmRest * Quaternion.Euler(-76f - toolStroke * 17f, 0f, 16f), dt, 16f);
                SetRotation(leftLegPivot, leftLegRest, dt, 12f);
                SetRotation(rightLegPivot, rightLegRest, dt, 12f);
                SetModelPose(new Vector3(0f, Mathf.Sin(Time.time * 9f) * 0.018f, 0f), new Vector3(7f, 0f, 0f), dt, 14f);
                AnimateImportedModel(0f, true);
                return;
            }

            float gait = Mathf.Sin(animationClock);
            bool airborne = !player.IsGrounded;
            float armSwing = gait * 38f * blendedMove;
            float legSwing = gait * 31f * blendedMove;
            float idleArm = breath * 4f * (1f - blendedMove);
            float airBlend = airborne ? 1f : 0f;

            SetRotation(leftArmPivot, leftArmRest * Quaternion.Euler(armSwing - idleArm - airBlend * 24f, 0f, -airBlend * 10f), dt, 15f);
            SetRotation(rightArmPivot, rightArmRest * Quaternion.Euler(-armSwing + idleArm - airBlend * 24f, 0f, airBlend * 10f), dt, 15f);
            SetRotation(leftLegPivot, leftLegRest * Quaternion.Euler(legSwing + airBlend * 28f, 0f, 0f), dt, 15f);
            SetRotation(rightLegPivot, rightLegRest * Quaternion.Euler(-legSwing + airBlend * 28f, 0f, 0f), dt, 15f);

            float stepBob = Mathf.Abs(Mathf.Sin(animationClock * 2f)) * blendedMove * 0.075f;
            float idleBreathing = (0.012f + breath * 0.012f) * (1f - blendedMove);
            float jumpFloat = airborne ? 0.045f : 0f;
            float lean = Mathf.Lerp(0f, player.IsSprinting ? 12f : 7f, blendedMove) + airBlend * -5f;
            float playfulSway = Mathf.Sin(animationClock) * blendedMove * 4f + breath * (1f - blendedMove) * 1.5f;
            SetModelPose(new Vector3(0f, stepBob + idleBreathing + jumpFloat, 0f),
                new Vector3(lean, playfulSway, -playfulSway * 0.45f), dt, 12f);
            AnimateImportedModel(gait, airborne);
        }

        private void SetRotation(Transform target, Quaternion rotation, float dt, float speed)
        {
            if (target != null)
                target.localRotation = Quaternion.Slerp(target.localRotation, rotation, 1f - Mathf.Exp(-speed * dt));
        }

        private void SetModelPose(Vector3 positionOffset, Vector3 rotationOffset, float dt, float speed)
        {
            if (modelRoot == null) return;
            float blend = 1f - Mathf.Exp(-speed * dt);
            modelRoot.localPosition = Vector3.Lerp(modelRoot.localPosition, modelRestPosition + positionOffset, blend);
            modelRoot.localRotation = Quaternion.Slerp(modelRoot.localRotation,
                modelRestRotation * Quaternion.Euler(rotationOffset), blend);
        }

        private void AnimateImportedModel(float gait, bool airborne)
        {
            if (importedModel == null) return;
            float move = player != null ? player.MoveAmount : 0f;
            float stepBob = player != null && player.IsGrounded ? Mathf.Abs(Mathf.Sin(animationClock * 2f)) * move * 0.028f : 0.018f;
            float breathing = Mathf.Sin(Time.time * 1.1f) * 0.006f;
            importedModel.localPosition = importedModelRestPosition + Vector3.up * (stepBob + breathing);
            float lean = airborne ? -7f : -gait * (player != null && player.IsSprinting ? 5f : 2.5f);
            float sway = player != null && player.IsGrounded ? gait * 2f : 0f;
            importedModel.localRotation = importedModelRestRotation * Quaternion.Euler(lean, 0f, sway);
        }
    }
}
