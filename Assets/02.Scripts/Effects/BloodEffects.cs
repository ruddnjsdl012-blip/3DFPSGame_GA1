using System.Collections.Generic;
using UnityEngine;

// 좀비 혈흔 효과 (Left 4 Dead 느낌)
//
//  맞았을 때
//   1. 핏방울: 총알이 나가는 쪽으로 대부분 튀고(관통), 맞은 쪽으로도 조금 튄다. 가늘고 길게 늘어진 방울이
//      포물선을 그리며 떨어지고 바닥에 닿으면 사라진다.
//   2. 피 안개: 맞은 자리에서 '퍽' 하고 퍼졌다가 금방 옅어진다.
//   3. 상처에서 몇 방울 뚝뚝 떨어진다. (맞은 곳에서 가장 가까운 뼈를 따라다닌다)
//   4. 자국: 뒤쪽 벽/바닥과 발밑에 피 자국이 남고, 시간이 지나면 옅어진다.
//  죽었을 때: 크게 튀고, 쓰러진 자리에 피 웅덩이가 천천히 퍼진다.
//
//  종류마다 색과 양이 다르다: 일반/러너 = 검붉은 피, 탱크 = 많고 큼, 부머 = 누런 고름 섞인 피, 스피터 = 초록빛 산성 피
public static class BloodEffects
{
    private const int MaxDecals = 60;

    private static ParticleSystem _drops;
    private static ParticleSystem _mist;
    private static Material _decalMaterial;
    private static Texture2D[] _splats;
    private static Texture2D _poolTexture;
    private static readonly Queue<BloodDecal> Decals = new Queue<BloodDecal>();
    private static readonly RaycastHit[] Hits = new RaycastHit[16];

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        _drops = null;
        _mist = null;
        _decalMaterial = null;
        _splats = null;
        _poolTexture = null;
        Decals.Clear();
    }

    // 게임이 시작할 때 미리 만들어 둔다. (첫 발에 끊기지 않게)
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Prewarm()
    {
        EnsureSystems();
        EnsureDecals();
    }

    // 종류별 피 색과 양
    private struct Style
    {
        public Color Color;    // 핏방울/자국 색
        public Color Mist;     // 피 안개 색
        public float Amount;   // 양 배율
        public float Size;     // 크기 배율
    }

    private static Style GetStyle(Zombie zombie)
    {
        SpecialZombie special = zombie != null ? zombie.GetComponent<SpecialZombie>() : null;
        float scale = zombie != null ? Mathf.Clamp(zombie.transform.lossyScale.y, 0.7f, 2.5f) : 1f;
        Style style = new Style
        {
            Color = new Color(0.32f, 0.015f, 0.01f),
            Mist = new Color(0.45f, 0.03f, 0.02f),
            Amount = 1f,
            Size = scale,
        };

        if (special == null)
        {
            return style;
        }

        switch (special.Type)
        {
            case SpecialType.Tank:
                style.Color = new Color(0.25f, 0.01f, 0.01f);
                style.Amount = 1.8f;
                break;
            case SpecialType.Boomer:
                style.Color = new Color(0.4f, 0.33f, 0.06f);
                style.Mist = new Color(0.5f, 0.48f, 0.12f);
                style.Amount = 1.4f;
                break;
            case SpecialType.Spitter:
                style.Color = new Color(0.16f, 0.32f, 0.04f);
                style.Mist = new Color(0.3f, 0.55f, 0.08f);
                break;
            case SpecialType.Runner:
                style.Amount = 0.8f;
                break;
        }

        return style;
    }

    // ===================== 맞았을 때 =====================

    public static void Hit(Zombie zombie, DamageInfo info)
    {
        EnsureSystems();
        Style style = GetStyle(zombie);
        Vector3 point = info.HitPoint;

        // 총알 방향: 총이 알려 주면 그걸, 아니면 맞은 면의 반대쪽(안쪽)
        Vector3 bullet = info.Direction.sqrMagnitude > 0.01f ? info.Direction.normalized : -info.HitNormal.normalized;
        if (bullet.sqrMagnitude < 0.01f && zombie != null)
        {
            bullet = (zombie.transform.position + Vector3.up - point).normalized;
        }

        Vector3 back = -bullet;  // 쏜 사람 쪽
        float amount = style.Amount * (info.IsHeadshot ? 1.8f : 1f) * Mathf.Clamp(info.Value / 10f, 0.6f, 2.5f);

        // 1. 관통해서 뒤로 튀는 핏방울 (대부분)
        int exitDrops = Mathf.RoundToInt(14 * amount);
        for (int i = 0; i < exitDrops; i++)
        {
            Vector3 dir = Cone(bullet + Vector3.up * 0.15f, 28f);
            EmitDrop(point + bullet * 0.15f * style.Size, dir * Random.Range(2.5f, 6.5f), style, Random.Range(0.012f, 0.03f));
        }

        // 2. 맞은 쪽으로 튀는 핏방울 (조금, 느리게)
        int entryDrops = Mathf.RoundToInt(6 * amount);
        for (int i = 0; i < entryDrops; i++)
        {
            Vector3 dir = Cone(back + Vector3.up * 0.4f, 45f);
            EmitDrop(point, dir * Random.Range(0.8f, 2.4f), style, Random.Range(0.01f, 0.022f));
        }

        // 헤드샷: 위로도 솟구친다.
        if (info.IsHeadshot)
        {
            for (int i = 0; i < 10; i++)
            {
                EmitDrop(point, Cone(Vector3.up + bullet * 0.6f, 35f) * Random.Range(2f, 4.5f), style, Random.Range(0.015f, 0.03f));
            }
        }

        // 3. 피 안개: 맞은 자리에서 퍽
        EmitMist(point + back * 0.05f, back * 0.4f, style, 0.22f * style.Size * (info.IsHeadshot ? 1.6f : 1f), 0.22f);
        EmitMist(point + bullet * 0.2f * style.Size, bullet * 1.2f, style, 0.35f * style.Size * (info.IsHeadshot ? 1.5f : 1f), 0.32f);

        // 4. 상처에서 뚝뚝
        if (zombie != null)
        {
            Transform bone = NearestBone(zombie, point);
            BloodDrip.Begin(bone != null ? bone : zombie.transform, point, style.Color, Random.Range(0.8f, 1.6f) * Mathf.Min(amount, 1.5f));
        }

        // 5. 자국: 뒤쪽 벽/바닥 (총알이 나간 방향으로 2.5m 안)
        if (FindSurface(point, bullet, 2.5f * style.Size, out RaycastHit wall))
        {
            Splat(wall.point, wall.normal, Random.Range(0.35f, 0.7f) * style.Size * Mathf.Sqrt(amount), style.Color);
        }

        // 발밑 바닥에도 핏방울 자국 (조금 뒤쪽)
        Vector3 floorFrom = point + bullet * Random.Range(0.3f, 1.2f) * style.Size + Vector3.up * 0.2f;
        if (FindSurface(floorFrom, Vector3.down, 4f, out RaycastHit floor))
        {
            Splat(floor.point, floor.normal, Random.Range(0.25f, 0.5f) * style.Size * Mathf.Sqrt(amount), style.Color);
        }
    }

    // ===================== 죽었을 때 =====================

    public static void Death(Zombie zombie)
    {
        if (zombie == null)
        {
            return;
        }

        EnsureSystems();
        Style style = GetStyle(zombie);
        Vector3 center = zombie.transform.position + Vector3.up * 1.1f * zombie.transform.lossyScale.y;

        // 사방으로 크게 한 번
        for (int i = 0; i < Mathf.RoundToInt(22 * style.Amount); i++)
        {
            EmitDrop(center + Random.insideUnitSphere * 0.2f * style.Size, Cone(Vector3.up, 75f) * Random.Range(1.5f, 4.5f), style, Random.Range(0.015f, 0.035f));
        }

        EmitMist(center, Vector3.up * 0.3f, style, 0.6f * style.Size, 0.45f);

        // 쓰러진 자리에 천천히 퍼지는 피 웅덩이
        if (FindSurface(zombie.transform.position + Vector3.up * 0.5f, Vector3.down, 3f, out RaycastHit floor))
        {
            BloodDecal pool = Splat(floor.point, floor.normal, 0.3f * style.Size, style.Color, true);
            if (pool != null)
            {
                pool.Grow(Random.Range(1.3f, 1.8f) * style.Size * Mathf.Sqrt(style.Amount), 3.5f);
            }
        }
    }

    // ===================== 파티클 =====================

    private static void EmitDrop(Vector3 position, Vector3 velocity, Style style, float size)
    {
        ParticleSystem.EmitParams p = new ParticleSystem.EmitParams();
        p.position = position;
        p.velocity = velocity;
        p.startSize = size * style.Size;
        p.startLifetime = Random.Range(0.45f, 0.9f);
        float shade = Random.Range(0.75f, 1.15f);
        p.startColor = new Color(style.Color.r * shade, style.Color.g * shade, style.Color.b * shade, 1f);
        _drops.Emit(p, 1);
    }

    // 상처에서 떨어지는 방울 (BloodDrip이 부른다)
    public static void EmitDrip(Vector3 position, Color color)
    {
        EnsureSystems();
        ParticleSystem.EmitParams p = new ParticleSystem.EmitParams();
        p.position = position;
        p.velocity = new Vector3(Random.Range(-0.1f, 0.1f), -Random.Range(0.2f, 0.6f), Random.Range(-0.1f, 0.1f));
        p.startSize = Random.Range(0.012f, 0.022f);
        p.startLifetime = 1.2f;
        p.startColor = color;
        _drops.Emit(p, 1);
    }

    private static void EmitMist(Vector3 position, Vector3 velocity, Style style, float size, float life)
    {
        for (int i = 0; i < 3; i++)
        {
            ParticleSystem.EmitParams p = new ParticleSystem.EmitParams();
            p.position = position + Random.insideUnitSphere * size * 0.25f;
            p.velocity = velocity * Random.Range(0.6f, 1.3f) + Random.insideUnitSphere * 0.2f;
            p.startSize = size * Random.Range(0.7f, 1.2f);
            p.startLifetime = life * Random.Range(0.8f, 1.2f);
            p.rotation = Random.Range(0f, 360f);
            Color c = style.Mist;
            c.a = Random.Range(0.55f, 0.8f);
            p.startColor = c;
            _mist.Emit(p, 1);
        }
    }

    private static Vector3 Cone(Vector3 axis, float angle)
    {
        axis.Normalize();
        Vector3 random = Random.insideUnitSphere * Mathf.Tan(angle * Mathf.Deg2Rad);
        return (axis + random).normalized;
    }

    // ===================== 자국 =====================

    public static BloodDecal Splat(Vector3 point, Vector3 normal, float size, Color color, bool isPool = false)
    {
        EnsureDecals();
        BloodDecal decal;
        if (Decals.Count >= MaxDecals)
        {
            decal = Decals.Dequeue();  // 가장 오래된 자국을 다시 쓴다.
            if (decal == null)
            {
                decal = BloodDecal.Create(_decalMaterial);
            }
        }
        else
        {
            decal = BloodDecal.Create(_decalMaterial);
        }

        Texture2D texture = isPool ? _poolTexture : _splats[Random.Range(0, _splats.Length)];
        decal.Place(point, normal, size, color, texture, isPool ? 40f : 25f);
        Decals.Enqueue(decal);
        return decal;
    }

    // 좀비와 플레이어는 지나쳐서, 그 뒤의 벽이나 바닥을 찾는다.
    private static bool FindSurface(Vector3 from, Vector3 direction, float distance, out RaycastHit result)
    {
        int count = Physics.RaycastNonAlloc(from, direction.normalized, Hits, distance, ~0, QueryTriggerInteraction.Ignore);
        result = new RaycastHit();
        float best = float.MaxValue;
        bool found = false;
        for (int i = 0; i < count; i++)
        {
            RaycastHit hit = Hits[i];
            if (hit.distance >= best || hit.collider.GetComponentInParent<Zombie>() != null
                || hit.collider.GetComponentInParent<Player>() != null || hit.collider is CharacterController)
            {
                continue;
            }

            best = hit.distance;
            result = hit;
            found = true;
        }

        return found;
    }

    private static Transform NearestBone(Zombie zombie, Vector3 point)
    {
        Animator animator = zombie.GetComponentInChildren<Animator>();
        if (animator == null || !animator.isHuman)
        {
            return null;
        }

        HumanBodyBones[] bones =
        {
            HumanBodyBones.Head, HumanBodyBones.Neck, HumanBodyBones.Chest, HumanBodyBones.Spine, HumanBodyBones.Hips,
            HumanBodyBones.LeftUpperArm, HumanBodyBones.RightUpperArm, HumanBodyBones.LeftLowerArm, HumanBodyBones.RightLowerArm,
            HumanBodyBones.LeftUpperLeg, HumanBodyBones.RightUpperLeg,
        };

        Transform best = null;
        float bestDistance = float.MaxValue;
        foreach (HumanBodyBones bone in bones)
        {
            Transform t = animator.GetBoneTransform(bone);
            if (t == null)
            {
                continue;
            }

            float distance = (t.position - point).sqrMagnitude;
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = t;
            }
        }

        return best;
    }

    // ===================== 만들기 =====================

    private static void EnsureSystems()
    {
        if (_drops != null && _mist != null)
        {
            return;
        }

        // 핏방울: 날아가는 방향으로 길게 늘어진 방울. 바닥/벽에 닿으면 사라진다.
        _drops = CreateSystem("BloodDrops", CreateParticleMaterial(DropTexture()), 1.4f, 1200);
        ParticleSystemRenderer dropRenderer = _drops.GetComponent<ParticleSystemRenderer>();
        dropRenderer.renderMode = ParticleSystemRenderMode.Stretch;
        dropRenderer.velocityScale = 0.035f;
        dropRenderer.lengthScale = 1.6f;
        ParticleSystem.CollisionModule collision = _drops.collision;
        collision.enabled = true;
        collision.type = ParticleSystemCollisionType.World;
        collision.mode = ParticleSystemCollisionMode.Collision3D;
        collision.quality = ParticleSystemCollisionQuality.Low;
        collision.bounce = 0f;
        collision.lifetimeLoss = 1f;  // 닿으면 사라진다.
        collision.radiusScale = 0.5f;
        ParticleSystem.ColorOverLifetimeModule dropColor = _drops.colorOverLifetime;
        dropColor.enabled = true;
        dropColor.color = new ParticleSystem.MinMaxGradient(FadeGradient(0.85f));

        // 피 안개: 부드럽게 퍼지고 커지면서 옅어진다.
        _mist = CreateSystem("BloodMist", CreateParticleMaterial(SoftTexture()), 0.05f, 200);
        ParticleSystem.SizeOverLifetimeModule mistSize = _mist.sizeOverLifetime;
        mistSize.enabled = true;
        mistSize.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.4f), new Keyframe(0.3f, 1f), new Keyframe(1f, 1.5f)));
        ParticleSystem.ColorOverLifetimeModule mistColor = _mist.colorOverLifetime;
        mistColor.enabled = true;
        mistColor.color = new ParticleSystem.MinMaxGradient(FadeGradient(0.1f));
        ParticleSystem.LimitVelocityOverLifetimeModule drag = _mist.limitVelocityOverLifetime;
        drag.enabled = true;
        drag.drag = 4f;
    }

    // 처음엔 불투명, fadeStart 이후 서서히 사라짐
    private static Gradient FadeGradient(float fadeStart)
    {
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, fadeStart), new GradientAlphaKey(0f, 1f) });
        return gradient;
    }

    private static ParticleSystem CreateSystem(string name, Material material, float gravity, int max)
    {
        GameObject go = new GameObject(name);
        Object.DontDestroyOnLoad(go);
        ParticleSystem system = go.AddComponent<ParticleSystem>();
        system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystem.MainModule main = system.main;
        main.loop = true;  // 계속 켜 두고 Emit()으로만 만든다.
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = gravity;
        main.maxParticles = max;

        ParticleSystem.EmissionModule emission = system.emission;
        emission.enabled = false;
        ParticleSystem.ShapeModule shape = system.shape;
        shape.enabled = false;

        ParticleSystemRenderer renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = material;
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        system.Play();
        return system;
    }

    private static Material CreateParticleMaterial(Texture2D texture)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader == null)
        {
            shader = Shader.Find("Sprites/Default");
        }

        Material material = new Material(shader);
        material.mainTexture = texture;
        if (material.HasProperty("_BaseMap"))
        {
            material.SetTexture("_BaseMap", texture);
        }

        MakeTransparent(material);
        return material;
    }

    private static void EnsureDecals()
    {
        if (_decalMaterial != null)
        {
            return;
        }

        // 자국은 빛을 받는 재질 (밤거리에서 너무 밝게 튀지 않게)
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
        {
            shader = Shader.Find("Standard");
        }

        _decalMaterial = new Material(shader);
        _decalMaterial.SetFloat("_Smoothness", 0.75f);  // 젖은 느낌
        MakeTransparent(_decalMaterial);

        _splats = new Texture2D[4];
        for (int i = 0; i < _splats.Length; i++)
        {
            _splats[i] = SplatTexture(i * 17.3f + 3f, false);
        }

        _poolTexture = SplatTexture(91.7f, true);
    }

    private static void MakeTransparent(Material material)
    {
        if (!material.HasProperty("_Surface"))
        {
            return;
        }

        material.SetFloat("_Surface", 1f);
        material.SetFloat("_Blend", 0f);
        material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        material.SetFloat("_ZWrite", 0f);
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
    }

    // ===================== 그림 (코드로 만든다) =====================

    // 가운데가 진하고 가장자리가 부드러운 방울
    private static Texture2D DropTexture()
    {
        const int size = 32;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x - 15.5f) / 15.5f;
                float dy = (y - 15.5f) / 15.5f;
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                float alpha = Mathf.Clamp01((1f - r) * 2.5f);
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        texture.Apply();
        return texture;
    }

    // 뭉게뭉게한 안개
    private static Texture2D SoftTexture()
    {
        const int size = 64;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x - 31.5f) / 31.5f;
                float dy = (y - 31.5f) / 31.5f;
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                float noise = Mathf.PerlinNoise(x * 0.12f, y * 0.12f);
                float alpha = Mathf.Clamp01(1f - r) * Mathf.Clamp01(1f - r);
                alpha *= 0.6f + 0.4f * noise;
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        texture.Apply();
        return texture;
    }

    // 튄 피 자국: 가운데 덩어리 + 울퉁불퉁한 가장자리 + 주변에 흩뿌려진 작은 방울과 꼬리
    // pool이면 둥글고 매끈한 웅덩이
    private static Texture2D SplatTexture(float seed, bool pool)
    {
        const int size = 128;
        float[,] alpha = new float[size, size];
        float c = (size - 1) * 0.5f;
        Random.State saved = Random.state;
        Random.InitState((int)(seed * 1000f));

        // 가운데 덩어리
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x - c) / c;
                float dy = (y - c) / c;
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                float angle = Mathf.Atan2(dy, dx);
                float wobble = pool ? 0.1f : 0.22f;
                float edge = (pool ? 0.78f : 0.42f) + wobble * (Mathf.PerlinNoise(seed + Mathf.Cos(angle) * 1.8f, seed + Mathf.Sin(angle) * 1.8f) - 0.5f) * 2f;
                alpha[x, y] = Mathf.Clamp01((edge - r) * 14f);
            }
        }

        if (!pool)
        {
            // 흩뿌려진 방울과 꼬리
            for (int i = 0; i < 28; i++)
            {
                float angle = Random.Range(0f, Mathf.PI * 2f);
                float distance = Random.Range(0.45f, 0.95f);
                float radius = Random.Range(0.02f, 0.07f) * (1.2f - distance);
                Vector2 center = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * distance;
                bool streak = Random.value < 0.35f;
                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        Vector2 p = new Vector2((x - c) / c, (y - c) / c);
                        float d;
                        if (streak)
                        {
                            // 가운데에서 바깥으로 뻗은 가는 꼬리
                            Vector2 dir = center.normalized;
                            float along = Vector2.Dot(p, dir);
                            float side = Mathf.Abs(dir.x * p.y - dir.y * p.x);
                            d = along > 0.35f && along < distance ? side / (radius * 0.45f) : 99f;
                        }
                        else
                        {
                            d = (p - center).magnitude / radius;
                        }

                        alpha[x, y] = Mathf.Max(alpha[x, y], Mathf.Clamp01((1f - d) * 4f));
                    }
                }
            }
        }

        Random.state = saved;

        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, true);
        texture.wrapMode = TextureWrapMode.Clamp;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                // 가장자리는 조금 진하고 안쪽은 살짝 옅게 (마른 피 느낌)
                float shade = 0.8f + 0.2f * Mathf.PerlinNoise(x * 0.07f + seed, y * 0.07f);
                texture.SetPixel(x, y, new Color(shade, shade, shade, alpha[x, y] * 0.92f));
            }
        }

        texture.Apply();
        return texture;
    }
}
