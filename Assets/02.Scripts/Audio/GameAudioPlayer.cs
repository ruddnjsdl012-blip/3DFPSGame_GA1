using UnityEngine;

// GameAudio가 자동으로 만드는 소리 재생기. AudioSource를 미리 여러 개 만들어 돌려 쓴다.
[DisallowMultipleComponent]
public class GameAudioPlayer : MonoBehaviour
{
    private const int PoolSize = 32;

    private AudioSource[] _sources;
    private float[] _startTimes;

    private void Awake()
    {
        _sources = new AudioSource[PoolSize];
        _startTimes = new float[PoolSize];
        for (int i = 0; i < PoolSize; i++)
        {
            GameObject go = new GameObject("Voice_" + i);
            go.transform.SetParent(transform, false);
            AudioSource source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.dopplerLevel = 0f;
            source.rolloffMode = AudioRolloffMode.Logarithmic;
            source.minDistance = 2f;
            _sources[i] = source;
        }
    }

    public AudioSource Play(AudioClip clip, Vector3 position, bool spatial, float volume, float pitch, float maxDistance)
    {
        // 쉬고 있는 소리를 쓰고, 다 바쁘면 가장 오래 재생한 것을 끊고 쓴다.
        int index = -1;
        float oldest = float.MaxValue;
        for (int i = 0; i < _sources.Length; i++)
        {
            if (_sources[i].isPlaying == false)
            {
                index = i;
                break;
            }

            if (_startTimes[i] < oldest)
            {
                oldest = _startTimes[i];
                index = i;
            }
        }

        AudioSource source = _sources[index];
        source.Stop();
        source.transform.position = position;
        source.spatialBlend = spatial ? 1f : 0f;
        source.maxDistance = Mathf.Max(source.minDistance + 1f, maxDistance);
        source.clip = clip;
        source.volume = Mathf.Clamp01(volume);
        source.pitch = pitch;
        // 일시 정지(AudioListener.pause) 중에 새로 나는 소리는 메뉴 소리이므로 멈추지 않게 한다.
        source.ignoreListenerPause = AudioListener.pause;
        source.Play();
        _startTimes[index] = Time.unscaledTime;
        return source;
    }
}
