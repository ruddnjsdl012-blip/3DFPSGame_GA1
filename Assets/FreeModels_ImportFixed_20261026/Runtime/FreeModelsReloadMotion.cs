using UnityEngine;

namespace FreeModelsImportFixed20261026
{
    // The firing component supplies normalized progress from its reload timer. This
    // component changes only model transforms and the removable magazine's visibility.
    [DisallowMultipleComponent]
    public sealed class FreeModelsReloadMotion : MonoBehaviour
    {
        [SerializeField] private Transform _gun;
        [SerializeField] private Transform _leftArm;
        [SerializeField] private Transform _magazine;
        [SerializeField] private Vector3 _gripOffset = new Vector3(-0.035f, -0.02f, 0f);
        [SerializeField] private Vector3 _gunOffset = new Vector3(-0.065f, 0.04f, -0.025f);
        [SerializeField] private Vector3 _tiltEuler = new Vector3(-16f, -22f, -34f);
        [SerializeField, Min(0.1f)] private float _pullDistance = 0.36f;
        private LocalPose _gunRest, _leftRest, _magazineRest;
        private Renderer[] _magazineRenderers;
        private bool[] _rendererStates;
        private bool _captured;
        public bool IsReloading { get; private set; }

        private struct LocalPose
        {
            public Vector3 position, scale;
            public Quaternion rotation;
            public static LocalPose Capture(Transform target)
            {
                return new LocalPose { position = target.localPosition, rotation = target.localRotation, scale = target.localScale };
            }
            public void Apply(Transform target)
            {
                if (target == null) return;
                target.localPosition = position; target.localRotation = rotation; target.localScale = scale;
            }
        }

        private void Awake() { CaptureRestPose(); }

        private bool CaptureRestPose()
        {
            if (_captured) return true;
            if (_gun == null || _leftArm == null || _leftArm.parent == null || _magazine == null) return false;
            _gunRest = LocalPose.Capture(_gun);
            _leftRest = LocalPose.Capture(_leftArm);
            _magazineRest = LocalPose.Capture(_magazine);
            _magazineRenderers = _magazine.GetComponentsInChildren<Renderer>(true);
            _rendererStates = new bool[_magazineRenderers.Length];
            for (int i = 0; i < _rendererStates.Length; i++) _rendererStates[i] = _magazineRenderers[i].enabled;
            _captured = true;
            return true;
        }

        public void BeginReload()
        {
            if (!CaptureRestPose()) return;
            RestoreRestPose();
            IsReloading = true;
            SetProgress(0f);
        }

        public void SetProgress(float progress)
        {
            if (!IsReloading || !_captured || _gun == null || _leftArm == null || _magazine == null) return;
            float t = Mathf.Clamp01(progress);
            float hold = Smooth(0f, 0.18f, t) * (1f - Smooth(0.87f, 1f, t));
            float tap = t > 0.77f && t < 0.89f ? Mathf.Sin((t - 0.77f) / 0.12f * Mathf.PI) * 1.8f : 0f;
            _gun.localPosition = _gunRest.position + _gunOffset * hold;
            _gun.localRotation = _gunRest.rotation * Quaternion.Euler(_tiltEuler * hold + Vector3.right * tap);

            float pull;
            if (t < 0.36f) pull = Smooth(0.20f, 0.36f, t) * 0.66f;
            else if (t < 0.43f) pull = Mathf.Lerp(0.66f, 1f, Smooth(0.36f, 0.43f, t));
            else if (t < 0.52f) pull = 1f;
            else pull = 1f - Smooth(0.52f, 0.74f, t);
            _magazine.localPosition = _magazineRest.position + new Vector3(-0.015f, -_pullDistance, 0.018f) * pull;
            _magazine.localRotation = _magazineRest.rotation * Quaternion.Euler(new Vector3(6f, 0f, -8f) * pull);
            bool magazineVisible = !(t > 0.43f && t < 0.52f);
            for (int i = 0; i < _magazineRenderers.Length; i++)
                if (_magazineRenderers[i] != null) _magazineRenderers[i].enabled = magazineVisible && _rendererStates[i];

            Transform parent = _leftArm.parent;
            Vector3 restWorld = parent.TransformPoint(_leftRest.position);
            Vector3 gripWorld = _magazine.TransformPoint(_gripOffset);
            Vector3 receiverWorld = _gun.TransformPoint(new Vector3(-0.035f, 0.055f, -0.075f));
            float reachReceiver = Smooth(0.78f, 0.87f, t);
            float returnToAim = Smooth(0.88f, 1f, t);
            float grab = Smooth(0.08f, 0.22f, t);
            Vector3 targetWorld = Vector3.Lerp(gripWorld, receiverWorld, reachReceiver);
            targetWorld = Vector3.Lerp(targetWorld, restWorld, returnToAim);
            _leftArm.localPosition = Vector3.Lerp(_leftRest.position, parent.InverseTransformPoint(targetWorld), grab);
            Quaternion gripRotation = Quaternion.Inverse(parent.rotation) * _magazine.rotation * Quaternion.Euler(-8f, 0f, 16f);
            Quaternion returnRotation = Quaternion.Slerp(gripRotation, _leftRest.rotation, returnToAim);
            _leftArm.localRotation = Quaternion.Slerp(_leftRest.rotation, returnRotation, grab);
        }

        public void CompleteReload() { IsReloading = false; RestoreRestPose(); }
        public void CancelReload() { IsReloading = false; RestoreRestPose(); }

        private void RestoreRestPose()
        {
            if (!_captured) return;
            _gunRest.Apply(_gun); _leftRest.Apply(_leftArm); _magazineRest.Apply(_magazine);
            for (int i = 0; i < _magazineRenderers.Length; i++)
                if (_magazineRenderers[i] != null) _magazineRenderers[i].enabled = _rendererStates[i];
        }

        private static float Smooth(float begin, float end, float t)
        {
            float x = Mathf.Clamp01((t - begin) / (end - begin));
            return x * x * (3f - 2f * x);
        }

        private void OnDisable() { CancelReload(); }
    }
}
