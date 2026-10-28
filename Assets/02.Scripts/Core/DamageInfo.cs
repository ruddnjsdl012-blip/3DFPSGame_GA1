using UnityEngine;

public struct DamageInfo
{
    public float Value;            // 대미지 양
    public Vector3 HitPoint;       // 맞은 자리
    public Vector3 HitNormal;      // 맞은 자리가 수직으로 바라보는 방향
    public Vector3 Direction;      // 총알이 날아온 방향 (피가 튀는 방향. 없으면 HitNormal 반대쪽)
    public bool IsHeadshot;        // 머리에 맞았나
}