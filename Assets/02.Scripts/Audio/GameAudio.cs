using System.Collections.Generic;
using UnityEngine;

// 효과음을 이름으로 쉽게 재생하는 도우미.
// 소리 파일은 Assets/ZombieAudio/Resources/ZombieAudio/ 폴더에 있다.
//  - "gun_shot"처럼 부르면 gun_shot, gun_shot_1, gun_shot_2 ... 중 하나를 랜덤으로 고른다.
//  - 같은 이름의 파일로 바꿔 넣으면 코드 수정 없이 그 소리로 바뀐다. (예: 더 좋은 총소리로 교체)
public static class GameAudio
{
    private const string Folder = "ZombieAudio/";
    private const int MaxVariations = 12;

    private static readonly Dictionary<string, AudioClip[]> Cache = new Dictionary<string, AudioClip[]>();
    private static readonly Dictionary<string, float> LastPlayTime = new Dictionary<string, float>();
    private static GameAudioPlayer _player;

    // 전체 볼륨 (0~1). 설정 메뉴에서 바꾼다.
    public static float MasterVolume { get; set; } = 1f;

    // 설정 화면에서 바꾸는 효과음/음악 볼륨 (GameSettings가 설정)
    public static float SfxVolume { get; set; } = 1f;
    public static float MusicVolume { get; set; } = 1f;

    // 실제로 곱해지는 값. 이름이 amb_(환경음)나 sting_(음악)으로 시작하면 음악으로 친다.
    public static float SfxScale => MasterVolume * SfxVolume;
    public static float MusicScale => MasterVolume * MusicVolume;

    public static bool IsMusic(string name)
    {
        return name != null && (name.StartsWith("amb_") || name.StartsWith("sting_"));
    }

    public static float ScaleFor(string name)
    {
        return IsMusic(name) ? MusicScale : SfxScale;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Cache.Clear();
        LastPlayTime.Clear();
        _player = null;
    }

    // 이름에 해당하는 소리 중 하나 (없으면 null)
    public static AudioClip Get(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return null;
        }

        if (Cache.TryGetValue(name, out AudioClip[] clips) == false)
        {
            List<AudioClip> list = new List<AudioClip>();
            AudioClip single = Resources.Load<AudioClip>(Folder + name);
            if (single != null)
            {
                list.Add(single);
            }

            for (int i = 1; i <= MaxVariations; i++)
            {
                AudioClip clip = Resources.Load<AudioClip>(Folder + name + "_" + i);
                if (clip == null)
                {
                    break;
                }

                list.Add(clip);
            }

            clips = list.ToArray();
            Cache[name] = clips;
        }

        return clips.Length == 0 ? null : clips[Random.Range(0, clips.Length)];
    }

    // 화면에 붙어 있는 소리 (총소리, 내 발소리, UI 등)
    public static AudioSource Play2D(string name, float volume = 1f, float pitchJitter = 0.04f, float minInterval = 0f)
    {
        return Play(name, Vector3.zero, false, volume, pitchJitter, minInterval, 0f);
    }

    // 위치에서 나는 소리 (좀비, 탄착, 폭발 등). 멀어질수록 작아진다.
    public static AudioSource PlayAt(string name, Vector3 position, float volume = 1f, float pitchJitter = 0.06f,
        float minInterval = 0f, float maxDistance = 40f)
    {
        return Play(name, position, true, volume, pitchJitter, minInterval, maxDistance);
    }

    // 반복 재생용 소리 (환경음, 심장 박동 등). 직접 Play/Stop 한다.
    public static AudioSource CreateLoop(string name, Transform parent, float volume, bool spatial)
    {
        AudioClip clip = Get(name);
        if (clip == null)
        {
            return null;
        }

        GameObject go = new GameObject("Loop_" + name);
        go.transform.SetParent(parent, false);
        AudioSource source = go.AddComponent<AudioSource>();
        source.clip = clip;
        source.loop = true;
        source.playOnAwake = false;
        source.volume = volume * ScaleFor(name);
        source.spatialBlend = spatial ? 1f : 0f;
        source.dopplerLevel = 0f;
        return source;
    }

    private static AudioSource Play(string name, Vector3 position, bool spatial, float volume, float pitchJitter,
        float minInterval, float maxDistance)
    {
        // 같은 소리가 너무 촘촘하게 겹치지 않게 (예: 좀비 30마리가 동시에 신음)
        if (minInterval > 0f)
        {
            if (LastPlayTime.TryGetValue(name, out float last) && Time.unscaledTime - last < minInterval)
            {
                return null;
            }

            LastPlayTime[name] = Time.unscaledTime;
        }

        AudioClip clip = Get(name);
        float scale = ScaleFor(name);
        if (clip == null || scale <= 0f)
        {
            return null;
        }

        if (_player == null)
        {
            _player = new GameObject("GameAudio").AddComponent<GameAudioPlayer>();
        }

        return _player.Play(clip, position, spatial, volume * scale, 1f + Random.Range(-pitchJitter, pitchJitter), maxDistance);
    }
}
