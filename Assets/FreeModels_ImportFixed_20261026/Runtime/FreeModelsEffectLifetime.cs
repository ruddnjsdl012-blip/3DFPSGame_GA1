using UnityEngine;

namespace FreeModelsImportFixed20261026
{
    [DisallowMultipleComponent]
    public sealed class FreeModelsEffectLifetime : MonoBehaviour
    {
        [SerializeField, Min(0.1f)] private float _lifetime = 1.3f;
        private float _destroyAt;

        private void OnEnable() { _destroyAt = Time.unscaledTime + Mathf.Max(0.1f, _lifetime); }

        private void Update()
        {
            // Also clean up the final hit after the victory screen pauses game time.
            if (Time.unscaledTime >= _destroyAt) Destroy(gameObject);
        }
    }
}
