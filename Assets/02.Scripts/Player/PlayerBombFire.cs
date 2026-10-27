using Input = GameInput;
using System;
using UnityEngine;

// Q 키를 누르면 시선이 바라보는 방향으로 수류탄을 던진다. (마우스 오른쪽 버튼은 정조준)
// 수류탄은 개수가 정해져 있고, 수류탄 상자나 좀비가 떨어뜨린 수류탄을 주워 채운다.
public class PlayerBombFire : MonoBehaviour
{
    [SerializeField] private Transform _firePoint;
    [SerializeField] private Bomb _bombPrefab;
    [SerializeField] private float _throwPower = 15f;
    [SerializeField] private KeyCode _throwKey = KeyCode.Q;

    [Header("개수")]
    [SerializeField, Min(0)] private int _startBombs = 2;
    [SerializeField, Min(1)] private int _maxBombs = 4;

    private int _bombCount;

    public KeyCode ThrowKey => _throwKey;
    public int BombCount => _bombCount;
    public int MaxBombs => _maxBombs;
    public bool NeedsBombs => _bombCount < _maxBombs;

    public event Action<int> BombCountChanged; // 개수가 바뀌었을 때
    public event Action EmptyThrown;           // 수류탄이 없는데 던지려 했을 때

    private void Awake()
    {
        _maxBombs = Mathf.Max(1, _maxBombs);
        _bombCount = Mathf.Clamp(_startBombs, 0, _maxBombs);
    }

    private void Update()
    {
        if (GameManager.IsPlaying == false)
        {
            return;
        }

        // 1. 던지기 키 입력 받기
        if (Input.GetKeyDown(_throwKey))
        {
            if (_bombCount > 0)
            {
                Shoot();
            }
            else
            {
                GameAudio.Play2D("gun_empty", 0.6f, 0.05f, 0.1f);
                EmptyThrown?.Invoke();
            }
        }
    }

    // 수류탄을 주웠을 때. 실제로 늘어난 개수를 돌려준다.
    public int AddBombs(int amount)
    {
        int added = Mathf.Clamp(amount, 0, _maxBombs - _bombCount);
        if (added > 0)
        {
            _bombCount += added;
            BombCountChanged?.Invoke(_bombCount);
        }

        return added;
    }

    private void Shoot()
    {
        Camera view = Camera.main;
        if (_bombPrefab == null || _firePoint == null || view == null)
        {
            return;
        }

        _bombCount--;
        BombCountChanged?.Invoke(_bombCount);
        GameAudio.Play2D("bomb_throw", 0.6f, 0.08f);

        // 2. 발사 위치에서 수류탄 생성하기
        Bomb bomb = Instantiate(_bombPrefab, _firePoint.position, Quaternion.identity);

        // 3. 생성된 수류탄을 시선 방향으로 던지기
        Rigidbody rigidbody = bomb.GetComponent<Rigidbody>();
        if (rigidbody != null)
        {
            rigidbody.AddForce(view.transform.forward * _throwPower, ForceMode.Impulse);
        }
    }
}
