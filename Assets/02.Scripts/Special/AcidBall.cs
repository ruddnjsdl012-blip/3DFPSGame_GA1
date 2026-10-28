using UnityEngine;

// 스피터가 뱉는 산성 덩어리. 포물선으로 날아가 떨어진 자리에 산성 웅덩이를 만든다.
public class AcidBall : MonoBehaviour
{
    private static readonly Vector3 Gravity = new Vector3(0f, -9.81f, 0f);

    private Vector3 _velocity;
    private float _age;

    // from에서 target까지 flightTime초 만에 떨어지도록 던진다.
    public static void Launch(Vector3 from, Vector3 target, float flightTime)
    {
        GameObject ball = SpecialEffects.Shape(PrimitiveType.Sphere, new Color(0.55f, 1f, 0.15f), 1.5f);
        ball.name = "AcidBall";
        ball.transform.position = from;
        ball.transform.localScale = Vector3.one * 0.3f;

        Light light = ball.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = new Color(0.55f, 1f, 0.2f);
        light.range = 4f;
        light.intensity = 2.5f;

        AcidBall acid = ball.AddComponent<AcidBall>();
        acid._velocity = (target - from - 0.5f * Gravity * flightTime * flightTime) / flightTime;
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        _age += dt;
        Vector3 start = transform.position;
        _velocity += Gravity * dt;
        Vector3 end = start + _velocity * dt;

        // 처음 잠깐은 뱉은 스피터 몸에 부딪히지 않게 무시한다.
        if (_age > 0.12f && Physics.Linecast(start, end, out RaycastHit hit, ~0, QueryTriggerInteraction.Ignore))
        {
            Land(hit.point);
            return;
        }

        transform.position = end;
        if (_age > 4f)
        {
            Land(end);
        }
    }

    private void Land(Vector3 point)
    {
        // 맞은 곳 아래 바닥에 웅덩이
        Vector3 ground = point;
        if (Physics.Raycast(point + Vector3.up * 0.5f, Vector3.down, out RaycastHit floor, 6f, ~0, QueryTriggerInteraction.Ignore))
        {
            ground = floor.point;
        }

        SpecialEffects.Burst(point, new Color(0.55f, 1f, 0.15f), 0.9f);
        AcidPuddle.Create(ground, 2.6f, 7f);
        Destroy(gameObject);
    }
}
