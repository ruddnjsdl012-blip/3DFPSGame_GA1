using System.Collections.Generic;
using UnityEngine;

// 효과음을 이름으로 쉽게 재생하는 도우미.
// 소리 파일은 Assets/06.Sounds/Resources/L4DAudio/ 폴더에 있다.
//  - "gun_shot"처럼 부르면 gun_shot_1, gun_shot_2 ... 중 하나를 랜덤으로 고른다.
//  - 같은 이름의 파일로 바꿔 넣으면 코드 수정 없이 그 소리로 바뀐다.
public static class GameSounds
{
    private const string Folder = "L4DAudio/";

    private static readonly Dictionary<string, AudioClip[]> Cache = new Dictionary<string, AudioClip[]>();
    private static readonly Dictionary<string, float> LastPlayed = new Dictionary<string, float>();
    private static AudioSource _source2D;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Cache.Clear();
        LastPlayed.Clear();
        _source2D = null;
    }

    // 이름에 해당하는 소리 중 하나 (없으면 null)
    public static AudioClip Get(string name)
    {
        if (!Cache.TryGetValue(name, out AudioClip[] clips))
        {
            List<AudioClip> list = new List<AudioClip>();
            AudioClip single = Resources.Load<AudioClip>(Folder + name);
            if (single != null)
            {
                list.Add(single);
            }

            for (int i = 1; i <= 12; i++)
            {
                AudioClip clip = Resources.Load<AudioClip>(Folder + name + "_" + i);
                if (clip != null)
                {
                    list.Add(clip);
                }
            }

            clips = list.ToArray();
            Cache[name] = clips;
        }

        return clips.Length > 0 ? clips[Random.Range(0, clips.Length)] : null;
    }

    // 화면 전체에 들리는 소리 (총소리, 경고음 등)
    public static void Play2D(string name, float volume = 1f, float pitchJitter = 0.05f)
    {
        AudioClip clip = Get(name);
        if (clip == null)
        {
            return;
        }

        if (_source2D == null)
        {
            GameObject go = new GameObject("GameSounds2D");
            Object.DontDestroyOnLoad(go);
            _source2D = go.AddComponent<AudioSource>();
            _source2D.playOnAwake = false;
            _source2D.spatialBlend = 0f;
        }

        _source2D.pitch = 1f + Random.Range(-pitchJitter, pitchJitter);
        _source2D.PlayOneShot(clip, volume);
    }

    // 그 자리에서 나는 소리 (멀수록 작게). minInterval: 같은 이름의 소리가 너무 자주 겹치지 않게
    public static void PlayAt(string name, Vector3 position, float volume = 1f, float minInterval = 0f, float maxDistance = 40f)
    {
        if (minInterval > 0f && LastPlayed.TryGetValue(name, out float last) && Time.time - last < minInterval)
        {
            return;
        }

        AudioClip clip = Get(name);
        if (clip == null)
        {
            return;
        }

        LastPlayed[name] = Time.time;
        GameObject go = new GameObject("Sound_" + name);
        go.transform.position = position;
        AudioSource source = go.AddComponent<AudioSource>();
        source.clip = clip;
        source.volume = volume;
        source.pitch = 1f + Random.Range(-0.08f, 0.08f);
        source.spatialBlend = 1f;
        source.rolloffMode = AudioRolloffMode.Linear;
        source.minDistance = 2f;
        source.maxDistance = maxDistance;
        source.Play();
        Object.Destroy(go, clip.length / Mathf.Max(0.5f, source.pitch) + 0.1f);
    }
}
