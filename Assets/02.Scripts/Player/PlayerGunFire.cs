using UnityEngine;

public class PlayerGunFire : MonoBehaviour
{
    [SerializeField] private float _range = 100f;
    [SerializeField] private float _damage = 10f;
    [SerializeField] private ParticleSystem _hitEffectPrefab;
    [SerializeField] private float _fireinterval = 0.1f;
    private float _fireTimer = 0f;

    private void Update()
    {
        // 발사 대기 시간을 줄인다.
        _fireTimer -= Time.deltaTime;

        // 왼쪽 버튼을 누르고 있는 동안 발사 간격마다 발사한다.
        if (Input.GetButton("Fire1") && _fireTimer <= 0f)
        {
            _fireTimer = _fireinterval;
            Shoot();
        }
    }

    private void Shoot()
    {
        Camera mainCamera = Camera.main;

        if (mainCamera == null)
        {
            Debug.LogError(
                "Main Camera의 Tag를 MainCamera로 설정해 주세요.",
                this);
            return;
        }

        // 카메라 위치에서 카메라가 바라보는 방향으로 광선을 만든다.
        Transform cameraTransform = mainCamera.transform;
        Ray ray = new Ray(
            cameraTransform.position,
            cameraTransform.forward);

        // 광선을 한 번 발사하고 명중 정보를 받는다.
        bool isHit = Physics.Raycast(
            ray,
            out RaycastHit hitInfo,
            _range);

        if (isHit == false)
        {
            return;
        }

        // 이 스크립트가 붙은 자기 자신에게 명중하면 처리하지 않는다.
        if (hitInfo.collider.gameObject == gameObject)
        {
            return;
        }

        // 명중한 대상과 설정된 공격력을 확인한다.
        Debug.Log(
            $"명중: {hitInfo.collider.gameObject.name}, 공격력: {_damage}",
            this);

        ParticleSystem effect = Instantiate(_hitEffectPrefab);
        effect.transform.position = hitInfo.point; // 맞은 자리
        effect.transform.forward = hitInfo.normal; // 수직 위치로 회전
        effect.Play();
    }
}