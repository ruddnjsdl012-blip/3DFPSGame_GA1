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
        _fireTimer -= Time.deltaTime;

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

        Transform cameraTransform = mainCamera.transform;

        Ray ray = new Ray(
            cameraTransform.position,
            cameraTransform.forward);

        bool isHit = Physics.Raycast(
            ray,
            out RaycastHit hitInfo,
            _range);

        if (isHit == false)
        {
            return;
        }

        if (hitInfo.collider.gameObject == gameObject)
        {
            return;
        }

        Debug.Log(
            $"명중: {hitInfo.collider.gameObject.name}, 공격력: {_damage}",
            this);

        ParticleSystem effect = Instantiate(_hitEffectPrefab);

        effect.transform.position = hitInfo.point;
        effect.transform.forward = hitInfo.normal;
        effect.Play();
    }
}