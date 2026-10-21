using UnityEngine;

[RequireComponent(typeof(ParticleSystem))]
public class PooledBombExplosion : MonoBehaviour
{
    private BombPool _pool;
    private ParticleSystem _particles;

    private bool _playing;
    private float _remainingTime;

    internal void Initialize(BombPool pool)
    {
        _pool = pool;
        _particles = GetComponent<ParticleSystem>();

        ParticleSystem[] systems =
            GetComponentsInChildren<ParticleSystem>(true);

        for (int i = 0; i < systems.Length; i++)
        {
            ParticleSystem.MainModule main = systems[i].main;

            main.loop = false;
            main.playOnAwake = false;
            main.stopAction = ParticleSystemStopAction.None;
        }

        _particles.Stop(
            true,
            ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    internal void PlayAt(Vector3 position, Quaternion rotation)
    {
        transform.SetPositionAndRotation(position, rotation);

        _remainingTime = 10f;
        _playing = true;

        gameObject.SetActive(true);

        _particles.Stop(
            true,
            ParticleSystemStopBehavior.StopEmittingAndClear);

        _particles.Play(true);
    }

    private void Update()
    {
        if (_playing == false)
        {
            return;
        }

        _remainingTime -= Time.deltaTime;

        // 자식 파티클까지 모두 끝난 뒤 반납한다.
        if (_particles.IsAlive(true) == false || _remainingTime <= 0f)
        {
            _pool.Return(this);
        }
    }

    internal void Deactivate()
    {
        _playing = false;
        _remainingTime = 0f;

        _particles.Stop(
            true,
            ParticleSystemStopBehavior.StopEmittingAndClear);

        gameObject.SetActive(false);
    }

    private void OnDisable()
    {
        if (_playing && _pool != null)
        {
            _pool.Return(this);
        }
    }
}