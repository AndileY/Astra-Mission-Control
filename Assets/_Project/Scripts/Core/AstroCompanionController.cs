using UnityEngine;

namespace Astra
{
    /// <summary>Procedural friendly motion and pointing for ASTRO. An Animator can be added later.</summary>
    public sealed class AstroCompanionController : MonoBehaviour
    {
        public Transform body;
        public Transform head;
        public Transform pointingArm;
        public Renderer eyeGlow;

        private Vector3 bodyStart;
        private Quaternion armStart;
        private Transform pointTarget;
        private float moodPulse;

        private void Start()
        {
            if (body != null) bodyStart = body.localPosition;
            if (pointingArm != null) armStart = pointingArm.localRotation;
        }

        private void Update()
        {
            float t = Time.unscaledTime;
            if (body != null) body.localPosition = bodyStart + Vector3.up * (Mathf.Sin(t * 2.4f) * 0.08f);
            if (head != null) head.localRotation = Quaternion.Euler(0f, Mathf.Sin(t * 1.3f) * 9f, Mathf.Sin(t * 1.8f) * 4f);
            if (pointingArm != null)
            {
                if (pointTarget != null)
                {
                    Vector3 localDirection = transform.InverseTransformDirection((pointTarget.position - pointingArm.position).normalized);
                    pointingArm.localRotation = Quaternion.Slerp(pointingArm.localRotation,
                        Quaternion.LookRotation(localDirection, Vector3.up) * Quaternion.Euler(0f, 90f, 0f), 4f * Time.unscaledDeltaTime);
                }
                else pointingArm.localRotation = Quaternion.Slerp(pointingArm.localRotation, armStart, 3f * Time.unscaledDeltaTime);
            }
            if (eyeGlow != null && moodPulse > 0f)
            {
                moodPulse = Mathf.Max(0f, moodPulse - Time.unscaledDeltaTime);
                eyeGlow.material.SetColor("_EmissionColor", Color.Lerp(new Color(0.05f, 0.8f, 1f), Color.white, Mathf.PingPong(t * 4f, 0.55f)));
            }
        }

        public void PointAt(Transform target) => pointTarget = target;
        public void Cheer() { moodPulse = 1.2f; pointTarget = null; }
        public void Say(string key, string fallback, string clipName = null, bool repeat = false)
        {
            VoiceGuideManager.Instance?.Say(key, fallback, clipName, repeat);
            moodPulse = 0.8f;
        }
    }
}
