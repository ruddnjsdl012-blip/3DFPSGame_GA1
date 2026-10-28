using UnityEngine;

// (예전 이름) 피 효과는 BloodEffects로 옮겼다. 좀비가 맞으면 Zombie.TakeDamage가 BloodEffects.Hit을 부른다.
// 다른 스크립트가 아직 이 이름을 부를 때를 위해 남겨 둔다.
public static class HitEffects
{
    public static void Blood(Vector3 point, Vector3 normal, Vector3 bulletDirection, bool headshot)
    {
        DamageInfo info = new DamageInfo();
        info.Value = 10f;
        info.HitPoint = point;
        info.HitNormal = normal;
        info.Direction = bulletDirection;
        info.IsHeadshot = headshot;
        BloodEffects.Hit(null, info);
    }
}
