using UnityEngine;

public enum ZombieKind
{
    Normal,
    Runner,  // 빠르고 약함
    Tank,    // 크고 느리고 아주 단단함, 맞으면 날아감
    Boomer,  // 부풀어 있고 죽으면 터짐 (주변 피해 + 시야 가림 + 좀비를 부름)
    Spitter  // 멀리서 산성액을 뱉음 (바닥에 웅덩이)
}

// 특수 좀비 설정. 일반 좀비를 복제한 뒤 Apply()로 스탯, 크기, 색, 전용 행동을 붙인다.
// (전용 모델 없이 지금 좀비 모델의 크기와 색으로 구분한다)
public static class ZombieVariants
{
    // 좀비 전체 크기 (CharacterController도 함께 커진다)
    public static float GetScale(ZombieKind kind)
    {
        switch (kind)
        {
            case ZombieKind.Runner: return 0.92f;
            case ZombieKind.Tank: return 1.6f;
            case ZombieKind.Boomer: return 1.08f;
            case ZombieKind.Spitter: return 1.0f;
            default: return 1f;
        }
    }

    public static void Apply(Zombie zombie, ZombieKind kind)
    {
        if (zombie == null || kind == ZombieKind.Normal)
        {
            return;
        }

        float scale = GetScale(kind);
        zombie.transform.localScale *= scale;
        zombie.name = $"{zombie.name}_{kind}";

        switch (kind)
        {
            case ZombieKind.Runner:
                // 체력 x0.55, 공격력 x0.7, 속도 x1.9, 공격 간격 x0.7
                zombie.SetupVariant(kind, 0.55f, 0.7f, 1.9f, 1f, 0.7f, Vector3.one);
                Tint(zombie, new Color(1f, 0.82f, 0.78f), Color.black);
                zombie.gameObject.AddComponent<RunnerZombie>();
                break;

            case ZombieKind.Tank:
                // 체력 x10, 공격력 x2.5, 속도 x0.75, 공격 거리 x1.1(몸이 커서 실제로는 더 멀리), 공격 간격 x1.6
                zombie.SetupVariant(kind, 10f, 2.5f, 0.75f, 1.1f, 1.6f, new Vector3(1.15f, 1f, 1.1f));
                zombie.IgnoreStagger = true;
                Tint(zombie, new Color(0.62f, 0.55f, 0.55f), Color.black);
                zombie.gameObject.AddComponent<TankZombie>();
                break;

            case ZombieKind.Boomer:
                // 체력 x1.3, 공격력 x0.8, 속도 x0.8, 몸이 옆으로 부풂
                zombie.SetupVariant(kind, 1.3f, 0.8f, 0.8f, 1f, 1.2f, new Vector3(1.45f, 0.95f, 1.45f));
                Tint(zombie, new Color(0.72f, 0.95f, 0.55f), new Color(0.04f, 0.1f, 0f));
                zombie.gameObject.AddComponent<BoomerZombie>();
                break;

            case ZombieKind.Spitter:
                // 체력 x0.8, 공격력 x0.6, 속도 x0.95
                zombie.SetupVariant(kind, 0.8f, 0.6f, 0.95f, 1f, 1f, new Vector3(0.9f, 1.06f, 0.9f));
                Tint(zombie, new Color(0.95f, 1f, 0.45f), new Color(0.08f, 0.1f, 0f));
                zombie.gameObject.AddComponent<SpitterZombie>();
                break;
        }
    }

    // 좀비 모델의 색을 바꾼다. (머티리얼을 복제하지 않고 MaterialPropertyBlock으로)
    public static void Tint(Zombie zombie, Color multiply, Color emission)
    {
        MaterialPropertyBlock block = new MaterialPropertyBlock();
        foreach (Renderer renderer in zombie.GetComponentsInChildren<Renderer>(true))
        {
            if (renderer is ParticleSystemRenderer || renderer is LineRenderer || renderer is TrailRenderer)
            {
                continue;
            }

            Material[] materials = renderer.sharedMaterials;
            for (int i = 0; i < materials.Length; i++)
            {
                Material material = materials[i];
                if (material == null)
                {
                    continue;
                }

                renderer.GetPropertyBlock(block, i);
                if (material.HasProperty("_BaseColor"))
                {
                    block.SetColor("_BaseColor", material.GetColor("_BaseColor") * multiply);
                }

                if (material.HasProperty("_Color"))
                {
                    block.SetColor("_Color", material.GetColor("_Color") * multiply);
                }

                if (emission.maxColorComponent > 0.001f && material.HasProperty("_EmissionColor")
                    && material.IsKeywordEnabled("_EMISSION"))
                {
                    block.SetColor("_EmissionColor", emission);
                }

                renderer.SetPropertyBlock(block, i);
            }
        }
    }
}
