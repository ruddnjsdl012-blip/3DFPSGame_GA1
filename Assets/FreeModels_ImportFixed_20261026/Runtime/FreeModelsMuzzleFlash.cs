using UnityEngine;

namespace FreeModelsImportFixed20261026
{
    [DisallowMultipleComponent]
    public sealed class FreeModelsMuzzleFlash : MonoBehaviour
    {
        [SerializeField] private ParticleSystem _flame;
        [SerializeField] private ParticleSystem _sparks;
        [SerializeField] private ParticleSystem _smoke;
        [SerializeField] private Light _flashLight;
        [SerializeField, Min(0.01f)] private float _lightDuration = 0.045f;
        private float _lightUntil;

        private void OnEnable()
        {
            if (_flashLight != null) _flashLight.enabled = false;
        }

        // PlayerGunFire.Shoot calls this once per actual shot.
        public void PlayShot()
        {
            Emit(_flame, 1);
            Emit(_sparks, 3);
            Emit(_smoke, 1);
            if (_flashLight != null)
            {
                _flashLight.enabled = true;
                _lightUntil = Time.unscaledTime + _lightDuration;
            }
        }

        private static void Emit(ParticleSystem system, int count)
        {
            if (system == null) return;
            if (!system.isPlaying) system.Play(false);
            system.Emit(count);
        }

        private void Update()
        {
            if (_flashLight != null && _flashLight.enabled && Time.unscaledTime >= _lightUntil)
                _flashLight.enabled = false;
        }

        private void OnDisable()
        {
            if (_flashLight != null) _flashLight.enabled = false;
            if (_flame != null) _flame.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (_sparks != null) _sparks.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (_smoke != null) _smoke.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }
}
