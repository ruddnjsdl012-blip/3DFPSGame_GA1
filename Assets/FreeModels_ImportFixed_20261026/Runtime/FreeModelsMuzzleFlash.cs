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
        [Header("총구 섬광 (주변을 밝히는 빛)")]
        [SerializeField, Min(0f)] private float _flashIntensity = 3.2f;
        [SerializeField, Range(0f, 1f)] private float _flashIntensityJitter = 0.35f;
        [SerializeField, Min(0.1f)] private float _flashRange = 8f;
        private float _lightUntil;
        private float _lightStart;
        private float _lightPeak;

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
                // 발사마다 세기를 조금씩 다르게 해서 번쩍임이 살아 있게 한다.
                _lightPeak = _flashIntensity * Random.Range(1f - _flashIntensityJitter, 1f + _flashIntensityJitter);
                _flashLight.range = _flashRange * Random.Range(0.85f, 1.15f);
                _flashLight.intensity = _lightPeak;
                _flashLight.enabled = true;
                _lightStart = Time.unscaledTime;
                _lightUntil = _lightStart + _lightDuration;
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
            if (_flashLight == null || _flashLight.enabled == false) return;
            if (Time.unscaledTime >= _lightUntil)
            {
                _flashLight.enabled = false;
                return;
            }

            // 켜진 순간 가장 밝고 빠르게 어두워진다.
            float t = Mathf.InverseLerp(_lightStart, _lightUntil, Time.unscaledTime);
            _flashLight.intensity = _lightPeak * (1f - t * t);
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
