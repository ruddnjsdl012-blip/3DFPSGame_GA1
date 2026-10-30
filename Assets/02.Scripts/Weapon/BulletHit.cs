using UnityEngine;

// 총알 한 발 맞히기 (리볼버가 쓴다. AR은 PlayerGunFire.Shoot에 같은 내용이 있다)
//  - 레이를 쏴서 맞은 자리에 총알 자국 / 좀비면 피(Zombie가 BloodEffects로) + 번쩍임 + 히트 마커 + 소리
//  - 좀비 머리면 headshotMultiplier 배 대미지
public static class BulletHit
{
    public static bool Fire(Ray ray, float range, float damage, float headshotMultiplier, ParticleSystem impactPrefab)
    {
        if (!Physics.Raycast(ray, out RaycastHit hit, range, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            return false;
        }

        Zombie zombie = hit.collider.GetComponentInParent<Zombie>();
        bool hitZombie = zombie != null;
        if (!hitZombie && impactPrefab != null)
        {
            ParticleSystem effect = Object.Instantiate(impactPrefab, hit.point, Quaternion.LookRotation(hit.normal));
            effect.Play();
        }

        GameSounds.PlayAt(hitZombie ? "hit_flesh" : "impact_concrete", hit.point, hitZombie ? 0.7f : 0.8f, 0.03f);

        bool alive = hitZombie && !zombie.IsDead;
        bool headshot = alive && IsHeadshot(zombie, hit);
        DamageInfo info = new DamageInfo();
        info.Value = headshot ? damage * headshotMultiplier : damage;
        info.HitPoint = hit.point;
        info.HitNormal = hit.normal;
        info.Direction = ray.direction;
        info.IsHeadshot = headshot;
        hit.collider.GetComponent<IDamageable>()?.TakeDamage(info);
        if (hitZombie && hit.collider.GetComponent<IDamageable>() == null)
        {
            zombie.TakeDamage(info);  // 콜라이더가 자식에 있는 좀비
        }

        if (alive)
        {
            ZombieFeedback(zombie, headshot);
        }

        return true;
    }

    // 좀비를 맞혔을 때: 번쩍임, 조준점 히트 마커, 소리 (도끼도 쓴다)
    public static void ZombieFeedback(Zombie zombie, bool headshot)
    {
        ZombieHitFlash.Flash(zombie.gameObject, headshot);
        if (UI_Crosshair.Instance != null)
        {
            UI_Crosshair.Instance.ShowHit(zombie.IsDead, headshot);
        }

        GameSounds.Play2D(headshot ? "ui_headshot" : "ui_hitmarker", headshot ? 0.7f : 0.45f, 0.05f);
    }

    public static bool IsHeadshot(Zombie zombie, RaycastHit hit)
    {
        Animator animator = zombie.GetComponentInChildren<Animator>();
        if (animator != null && animator.isHuman)
        {
            Transform head = animator.GetBoneTransform(HumanBodyBones.Head);
            if (head != null)
            {
                return hit.point.y > head.position.y - 0.12f * zombie.transform.lossyScale.y;
            }
        }

        Bounds bounds = hit.collider.bounds;
        return hit.point.y > bounds.max.y - bounds.size.y * 0.15f;
    }
}
