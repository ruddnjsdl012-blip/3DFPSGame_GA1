using UnityEngine;

namespace FreeModelsImportFixed20261026
{
    // 코드로 만드는(절차적) 1인칭 재장전 모션.
    // PlayerGunFire가 재장전 진행도(0~1)를 넘겨 주면 LateUpdate에서 총 / 왼팔 / 분리 탄창을 움직인다.
    //  - 일반 재장전(탄이 남음): 왼손이 탄창을 빼서 주머니(화면 밖)에 넣고, 새 탄창을 가져와 끼운다.
    //  - 빈 탄창 재장전: 빈 탄창은 떨어뜨리고, 새 탄창을 끼운 뒤 장전 손잡이를 당겼다 놓는다.
    // 팔 모델은 뼈대가 없는 한 덩어리라서, 손목 위치에 맞춰 팔뚝이 화면 밖 팔꿈치 쪽을
    // 계속 향하도록 회전시켜 팔이 몸에 붙어 있는 것처럼 보이게 한다.
    // 값은 Play 중 '미리보기 반복'을 켜고 Inspector에서 바로 조절할 수 있다.
    [DisallowMultipleComponent]
    public sealed class FreeModelsReloadMotion : MonoBehaviour
    {
        [Header("연결 (Tools > Free Models Fixed > Apply Reload Motion이 자동 연결)")]
        [SerializeField] private Transform _gun;
        [SerializeField] private Transform _leftArm;
        [SerializeField] private Transform _magazine;
        [Tooltip("탄창 기준 왼손목 위치 (총마다 자동 설정)")]
        [SerializeField] private Vector3 _gripOffset = new Vector3(-0.035f, -0.02f, 0f);

        [Header("총 자세 (총의 부모 기준 위치 / 각도)")]
        [SerializeField] private Vector3 _magPoseOffset = new Vector3(-0.055f, 0.035f, -0.03f);
        [SerializeField] private Vector3 _magPoseEuler = new Vector3(-14f, -18f, -30f);
        [SerializeField] private Vector3 _chargePoseOffset = new Vector3(-0.03f, 0.025f, -0.02f);
        [SerializeField] private Vector3 _chargePoseEuler = new Vector3(-6f, -12f, -18f);
        [Tooltip("재장전 중 손이 떨리는 듯한 미세한 흔들림")]
        [SerializeField, Min(0f)] private float _handShake = 1f;

        [Header("탄창")]
        [Tooltip("탄창이 빠지는 방향 (총 메시 기준)")]
        [SerializeField] private Vector3 _magazineAxis = new Vector3(-0.042f, -1f, 0.05f);
        [Tooltip("탄창이 홈에서 완전히 빠지기까지의 거리")]
        [SerializeField, Min(0.005f)] private float _railLength = 0.075f;
        [Tooltip("새 탄창을 끼우기 직전 홈 아래에서 멈추는 거리")]
        [SerializeField, Min(0f)] private float _alignGap = 0.05f;
        [Tooltip("손바닥으로 쳐 넣기 직전 남은 거리")]
        [SerializeField, Min(0f)] private float _seatGap = 0.01f;
        [Tooltip("새 탄창을 비스듬히 걸어 넣는 각도")]
        [SerializeField] private Vector3 _insertTiltEuler = new Vector3(12f, 0f, 0f);
        [Tooltip("탄창을 넣고 꺼내는 주머니 위치 (1인칭 뷰 기준, 화면 밖이어야 한다)")]
        [SerializeField] private Vector3 _pouchPoint = new Vector3(-0.08f, -0.55f, 0.3f);
        [SerializeField] private Vector3 _pouchMagEuler = new Vector3(30f, 0f, -25f);
        [Tooltip("탄창이 주머니까지 오가는 경로의 휘어짐 (1인칭 뷰 기준)")]
        [SerializeField] private Vector3 _magPathBulge = new Vector3(-0.05f, 0f, -0.05f);
        [Tooltip("빈 탄창이 떨어지며 도는 속도 (도/초)")]
        [SerializeField] private Vector3 _fallTumble = new Vector3(160f, 20f, -90f);

        [Header("왼팔")]
        [Tooltip("손목이 움직일 때 화면 밖 팔꿈치가 따라 움직이는 비율. 클수록 팔뚝 각도가 덜 변한다.")]
        [SerializeField, Range(0f, 1f)] private float _elbowFollow = 0.55f;
        [Tooltip("팔뚝 방향(손목→팔꿈치, 팔 모델 기준). 0이면 모델 크기로 자동 계산")]
        [SerializeField] private Vector3 _forearmDirection = Vector3.zero;
        [Tooltip("손이 이동하는 경로의 휘어짐 (1인칭 뷰 기준)")]
        [SerializeField] private Vector3 _handPathBulge = new Vector3(-0.03f, -0.04f, -0.03f);
        [Tooltip("탄창을 잡을 때 손 추가 회전")]
        [SerializeField] private Vector3 _magGripHandEuler = Vector3.zero;
        [Tooltip("장전 손잡이를 잡을 때 손 추가 회전")]
        [SerializeField] private Vector3 _chargeHandEuler = Vector3.zero;

        [Header("장전 손잡이 (빈 탄창 재장전)")]
        [Tooltip("손잡이 위치 (총 기준). 손목이 이 위치로 간다.")]
        [SerializeField] private Vector3 _chargingHandlePoint = new Vector3(-0.035f, 0.055f, -0.075f);
        [Tooltip("손잡이를 당기는 방향과 거리 (총 기준)")]
        [SerializeField] private Vector3 _chargePull = new Vector3(0f, 0f, -0.07f);
        [Tooltip("손잡이를 놓은 뒤 손이 튕겨 나가는 방향 (1인칭 뷰 기준)")]
        [SerializeField] private Vector3 _releaseFlick = new Vector3(-0.035f, 0.015f, -0.02f);

        [Header("반동 (스프링)")]
        [SerializeField, Min(1f)] private float _springStiffness = 260f;
        [SerializeField, Range(0.05f, 1f)] private float _springDamping = 0.42f;
        [Tooltip("탄창을 쳐 넣거나 노리쇠가 닫힐 때 총이 들썩이는 세기")]
        [SerializeField, Min(0f)] private float _impactScale = 1f;

        [Header("효과음 (선택, 비워 두면 소리 없음)")]
        [SerializeField] private AudioClip _magOutClip;
        [SerializeField] private AudioClip _magInClip;
        [SerializeField] private AudioClip _boltPullClip;
        [SerializeField] private AudioClip _boltReleaseClip;
        [SerializeField, Range(0f, 1f)] private float _volume = 0.8f;

        [Header("미리보기 (Play 중 값 조절용)")]
        [Tooltip("켜 두면 재장전 모션을 계속 반복한다. 값을 바꾸면 바로 반영된다.")]
        [SerializeField] private bool _previewLoop;
        [SerializeField] private bool _previewEmpty;
        [SerializeField, Min(0.3f)] private float _previewDuration = 3f;

        // ----- 단계별 타이밍 (전체 재장전 시간 대비 비율) -----
        private struct Timeline
        {
            public bool empty;
            public float tiltInEnd;      // 총을 기울이는 동작 끝
            public float reachStart;     // 왼손이 손잡이에서 떨어짐
            public float reachEnd;       // 왼손이 탄창을 잡음
            public float release;        // 탄창 멈치를 누름
            public float pullEnd;        // 탄창이 홈에서 완전히 빠짐
            public float handPouchStart; // (빈 탄창) 손이 새 탄창을 가지러 감
            public float dropEnd;        // 탄창이 화면 밖으로 사라짐
            public float fetchStart;     // 새 탄창을 꺼내 올라옴
            public float alignEnd;       // 새 탄창이 홈 아래에 비스듬히 정렬
            public float insertEnd;      // 탄창을 밀어 넣음
            public float seat;           // 손바닥으로 쳐서 고정
            public float chargeReachEnd; // (빈 탄창) 손이 장전 손잡이를 잡음
            public float chargePullEnd;  // (빈 탄창) 손잡이를 끝까지 당김
            public float chargeRelease;  // (빈 탄창) 손잡이를 놓음 (노리쇠 닫힘)
            public float returnStart;    // 왼손이 원래 자리로 돌아가기 시작
            public float returnEnd;
            public float tiltOutStart;   // 총이 조준 자세로 돌아가기 시작
        }

        private static readonly Timeline TacticalTimeline = new Timeline
        {
            empty = false,
            tiltInEnd = 0.16f,
            reachStart = 0.03f, reachEnd = 0.17f,
            release = 0.19f, pullEnd = 0.28f,
            handPouchStart = 0.28f, dropEnd = 0.40f,
            fetchStart = 0.47f, alignEnd = 0.62f, insertEnd = 0.70f, seat = 0.74f,
            chargeReachEnd = 0.74f, chargePullEnd = 0.74f, chargeRelease = 0.74f,
            returnStart = 0.77f, returnEnd = 0.91f,
            tiltOutStart = 0.79f,
        };

        private static readonly Timeline EmptyTimeline = new Timeline
        {
            empty = true,
            tiltInEnd = 0.12f,
            reachStart = 0.02f, reachEnd = 0.12f,
            release = 0.13f, pullEnd = 0.17f,
            handPouchStart = 0.15f, dropEnd = 0.30f,
            fetchStart = 0.34f, alignEnd = 0.47f, insertEnd = 0.54f, seat = 0.575f,
            chargeReachEnd = 0.67f, chargePullEnd = 0.73f, chargeRelease = 0.765f,
            returnStart = 0.79f, returnEnd = 0.91f,
            tiltOutStart = 0.80f,
        };

        private struct LocalPose
        {
            public Vector3 position, scale;
            public Quaternion rotation;
            public static LocalPose Capture(Transform target)
            {
                return new LocalPose { position = target.localPosition, rotation = target.localRotation, scale = target.localScale };
            }
            public void Apply(Transform target)
            {
                if (target == null) return;
                target.localPosition = position; target.localRotation = rotation; target.localScale = scale;
            }
        }

        private LocalPose _gunRest, _leftRest, _magazineRest;
        private Vector3 _elbowRestView;
        private Renderer[] _magazineRenderers;
        private bool[] _rendererStates;
        private bool _captured;

        private bool _emptyReload;
        private float _duration = 3f;
        private float _progress;

        // 상태가 바뀔 때 현재 자세에서 새 자세로 부드럽게 넘어가기 위한 값
        private float _fadeDuration;
        private float _fadeElapsed;
        private Vector3 _fromGunPos, _fromMagPos, _fromArmPos;
        private Quaternion _fromGunRot, _fromMagRot, _fromArmRot;
        private bool _magazineHidden;
        private bool _driving;

        // 스프링 (총의 부모 기준 위치 / 각도)
        private Vector3 _springPos, _springVel, _springRot, _springRotVel;

        private AudioSource _audio;
        private bool _previewOwned;
        private float _previewTime;
        private float _previewWait;

        public bool IsReloading { get; private set; }

        // 탄창 멈치를 누른 뒤에는 재장전을 취소할 수 없다. (탄창이 순간이동하지 않도록)
        public bool IsMagazineOut => IsReloading && _progress >= Current.release - 0.005f;

        private Timeline Current => _emptyReload ? EmptyTimeline : TacticalTimeline;

        private void Awake()
        {
            CaptureRestPose();

            // 효과음 칸이 비어 있으면 기본 효과음(Resources/ZombieAudio)을 쓴다.
            if (_magOutClip == null) _magOutClip = Resources.Load<AudioClip>("ZombieAudio/reload_mag_out");
            if (_magInClip == null) _magInClip = Resources.Load<AudioClip>("ZombieAudio/reload_mag_in");
            if (_boltPullClip == null) _boltPullClip = Resources.Load<AudioClip>("ZombieAudio/reload_bolt_pull");
            if (_boltReleaseClip == null) _boltReleaseClip = Resources.Load<AudioClip>("ZombieAudio/reload_bolt_release");
        }

        private bool CaptureRestPose()
        {
            if (_captured) return true;
            if (_gun == null || _leftArm == null || _leftArm.parent == null || _magazine == null || _magazine.parent == null) return false;
            _gunRest = LocalPose.Capture(_gun);
            _leftRest = LocalPose.Capture(_leftArm);
            _magazineRest = LocalPose.Capture(_magazine);
            _magazineRenderers = _magazine.GetComponentsInChildren<Renderer>(true);
            _rendererStates = new bool[_magazineRenderers.Length];
            for (int i = 0; i < _rendererStates.Length; i++) _rendererStates[i] = _magazineRenderers[i].enabled;

            // 팔뚝이 향하는 화면 밖 '팔꿈치' 위치를 기억한다. (뷰 기준)
            Vector3 forearm = _forearmDirection;
            if (forearm.sqrMagnitude < 1e-8f)
            {
                MeshFilter filter = _leftArm.GetComponent<MeshFilter>();
                if (filter != null && filter.sharedMesh != null) forearm = filter.sharedMesh.bounds.center * 2f;
            }
            Vector3 wristWorld = _leftArm.position;
            Vector3 elbowWorld = forearm.sqrMagnitude > 1e-8f
                ? _leftArm.TransformPoint(forearm)
                : wristWorld + transform.TransformVector(new Vector3(-0.1f, -0.25f, -0.35f));
            _elbowRestView = transform.InverseTransformPoint(elbowWorld);

            _captured = true;
            return true;
        }

        // ----- PlayerGunFire에서 부르는 함수 -----

        public void BeginReload() { BeginReload(false, 3f); }

        public void BeginReload(bool emptyMagazine, float duration)
        {
            if (!CaptureRestPose()) return;
            _previewOwned = false;
            _emptyReload = emptyMagazine;
            _duration = Mathf.Max(0.3f, duration);
            _progress = 0f;
            StartFade(0.1f, false);
            IsReloading = true;
        }

        public void SetProgress(float progress)
        {
            if (!IsReloading || !_captured) return;
            float next = Mathf.Clamp01(progress);
            if (next > _progress) FireEvents(_progress, next);
            _progress = next;
        }

        public void CompleteReload()
        {
            if (!IsReloading) return;
            FireEvents(_progress, 1f);
            _progress = 1f;
            IsReloading = false;
            Kick(new Vector3(0f, -0.06f, 0f), new Vector3(18f, 0f, 0f)); // 조준 자세로 돌아오며 살짝 내려앉음
            StartFade(0.08f, false);
        }

        public void CancelReload()
        {
            if (!IsReloading) return;
            IsReloading = false;
            StartFade(0.16f, _magazineHidden);
        }

        // ----- 매 프레임 -----

        private void Update()
        {
            if (!_previewLoop || !_captured)
            {
                _previewOwned = false;
                return;
            }

            if (IsReloading && !_previewOwned) return; // 실제 재장전 중에는 끼어들지 않는다.

            if (!IsReloading)
            {
                _previewWait -= Time.deltaTime;
                if (_previewWait > 0f) return;
                BeginReload(_previewEmpty, _previewDuration);
                _previewOwned = true;
                _previewTime = 0f;
            }

            _previewTime += Time.deltaTime;
            if (_previewTime >= _duration)
            {
                CompleteReload();
                _previewOwned = false;
                _previewWait = 0.6f;
            }
            else
            {
                SetProgress(_previewTime / _duration);
            }
        }

        private void LateUpdate()
        {
            if (!_captured || _gun == null || _leftArm == null || _magazine == null) return;

            float dt = Time.deltaTime;
            StepSpring(dt);
            if (_fadeElapsed < _fadeDuration) _fadeElapsed += dt;
            bool fading = _fadeElapsed < _fadeDuration;

            if (!IsReloading && !fading && !IsSpringMoving())
            {
                if (_driving)
                {
                    ResetSpring();
                    RestoreRestPose();
                    _driving = false;
                }
                return;
            }

            _driving = true;
            float fadeWeight = fading ? Smooth(0f, 1f, _fadeElapsed / _fadeDuration) : 1f;
            ApplyPose(IsReloading ? _progress : -1f, fadeWeight);
        }

        private void ApplyPose(float t, float fadeWeight)
        {
            bool reloading = t >= 0f;
            Timeline tl = Current;

            // 1) 총
            Vector3 gunOffset = Vector3.zero;
            Vector3 gunEuler = Vector3.zero;
            if (reloading)
            {
                float toCharge = tl.empty ? Smooth(tl.seat, tl.chargeReachEnd, t) : 0f;
                float back = 1f - Smooth(tl.tiltOutStart, 1f, t);
                float wMag = Smooth(0f, tl.tiltInEnd, t) * (1f - toCharge) * back;
                float wCharge = toCharge * back;
                gunOffset = _magPoseOffset * wMag + _chargePoseOffset * wCharge;
                gunEuler = _magPoseEuler * wMag + _chargePoseEuler * wCharge;

                float shake = (wMag + wCharge) * _handShake;
                float time = Time.time;
                gunOffset += new Vector3(Mathf.PerlinNoise(time * 0.9f, 1.3f) - 0.5f, Mathf.PerlinNoise(2.1f, time * 0.9f) - 0.5f, 0f) * (0.004f * shake);
                gunEuler += new Vector3(Mathf.PerlinNoise(time * 0.8f, 7.7f) - 0.5f, 0f, Mathf.PerlinNoise(5.2f, time * 0.8f) - 0.5f) * (1.2f * shake);
            }

            Vector3 gunPos = _gunRest.position + gunOffset + _springPos;
            Quaternion gunRot = _gunRest.rotation * Quaternion.Euler(gunEuler + _springRot);
            _gun.localPosition = Vector3.Lerp(_fromGunPos, gunPos, fadeWeight);
            _gun.localRotation = Quaternion.Slerp(_fromGunRot, gunRot, fadeWeight);

            // 2) 탄창 (총이 움직인 뒤에 계산)
            Vector3 magPos = _magazineRest.position;
            Quaternion magRot = _magazineRest.rotation;
            bool magVisible = true;
            if (reloading) EvaluateMagazine(tl, t, out magPos, out magRot, out magVisible);
            _magazine.localPosition = Vector3.Lerp(_fromMagPos, magPos, fadeWeight);
            _magazine.localRotation = Quaternion.Slerp(_fromMagRot, magRot, fadeWeight);
            SetMagazineVisible(magVisible);

            // 3) 왼팔 (탄창이 움직인 뒤에 계산)
            Vector3 wrist = RestWristWorld();
            Vector3 extraEuler = Vector3.zero;
            if (reloading) EvaluateHand(tl, t, out wrist, out extraEuler);
            Transform parent = _leftArm.parent;
            Vector3 armPos = parent.InverseTransformPoint(wrist);
            Quaternion armRot = HandLocalRotation(wrist, extraEuler);
            _leftArm.localPosition = Vector3.Lerp(_fromArmPos, armPos, fadeWeight);
            _leftArm.localRotation = Quaternion.Slerp(_fromArmRot, armRot, fadeWeight);
        }

        // ----- 탄창 -----

        private void EvaluateMagazine(Timeline tl, float t, out Vector3 pos, out Quaternion rot, out bool visible)
        {
            visible = true;

            if (t < tl.release)
            {
                RailPose(0f, 0f, out pos, out rot);
            }
            else if (t < tl.pullEnd)
            {
                // 일반: 손으로 뽑아낸다(감속). 빈 탄창: 무게로 미끄러져 나온다(가속).
                float u = Phase(tl.release, tl.pullEnd, t);
                float s = _railLength * (tl.empty ? u * u : EaseOut(u));
                RailPose(s, 0f, out pos, out rot);
            }
            else if (t < tl.dropEnd)
            {
                if (tl.empty)
                {
                    // 빈 탄창은 그대로 떨어진다.
                    RailPose(_railLength, 0f, out Vector3 start, out Quaternion startRot);
                    float pullSeconds = Mathf.Max(0.01f, (tl.pullEnd - tl.release) * _duration);
                    float exitSpeed = 2f * _railLength / pullSeconds;
                    float tau = (t - tl.pullEnd) * _duration;
                    Transform magParent = _magazine.parent;
                    pos = start + MagAxis() * (exitSpeed * tau)
                          + magParent.InverseTransformVector(Vector3.down * (0.5f * 9.81f * tau * tau));
                    rot = startRot * Quaternion.Euler(_fallTumble * tau);
                }
                else
                {
                    // 일반: 손에 쥔 탄창을 주머니(화면 밖)로 가져간다.
                    float u = Phase(tl.pullEnd, tl.dropEnd, t);
                    RailPose(_railLength, 0f, out Vector3 a, out Quaternion aRot);
                    PouchPose(out Vector3 b, out Quaternion bRot);
                    pos = Arc(a, b, EaseIn(u), MagBulgeLocal());
                    rot = Quaternion.Slerp(aRot, bRot, Smooth01(u));
                }
            }
            else if (t < tl.fetchStart)
            {
                // 화면 밖에서 새 탄창으로 바꾼다.
                PouchPose(out pos, out rot);
                visible = false;
            }
            else if (t < tl.alignEnd)
            {
                // 새 탄창을 꺼내 홈 아래까지 가져온다. (감속하며 도착)
                float u = Phase(tl.fetchStart, tl.alignEnd, t);
                PouchPose(out Vector3 a, out Quaternion aRot);
                RailPose(_alignGap, 1f, out Vector3 b, out Quaternion bRot);
                pos = Arc(a, b, EaseOut(u), MagBulgeLocal());
                rot = Quaternion.Slerp(aRot, bRot, Smooth01(u));
            }
            else if (t < tl.insertEnd)
            {
                // 비스듬히 걸고 세우면서 밀어 넣는다.
                float k = Smooth01(Phase(tl.alignEnd, tl.insertEnd, t));
                RailPose(Mathf.Lerp(_alignGap, _seatGap, k), 1f - k, out pos, out rot);
            }
            else if (t < tl.seat)
            {
                // 손바닥으로 '탁' 쳐서 고정한다. (가속)
                float u = Phase(tl.insertEnd, tl.seat, t);
                RailPose(Mathf.Lerp(_seatGap, 0f, u * u), 0f, out pos, out rot);
            }
            else
            {
                RailPose(0f, 0f, out pos, out rot);
            }
        }

        private Vector3 MagAxis()
        {
            return _magazineAxis.sqrMagnitude > 1e-8f ? _magazineAxis.normalized : Vector3.down;
        }

        private void RailPose(float distance, float tilt, out Vector3 pos, out Quaternion rot)
        {
            pos = _magazineRest.position + MagAxis() * distance;
            rot = _magazineRest.rotation * Quaternion.Euler(_insertTiltEuler * tilt);
        }

        private void PouchPose(out Vector3 pos, out Quaternion rot)
        {
            pos = _magazine.parent.InverseTransformPoint(transform.TransformPoint(_pouchPoint));
            rot = _magazineRest.rotation * Quaternion.Euler(_pouchMagEuler);
        }

        private Vector3 MagBulgeLocal()
        {
            return _magazine.parent.InverseTransformVector(transform.TransformVector(_magPathBulge));
        }

        // 탄창이 (localPos, localRot)에 있을 때 왼손목이 있어야 할 월드 위치
        private Vector3 GripWorld(Vector3 localPos, Quaternion localRot)
        {
            return _magazine.parent.TransformPoint(localPos + localRot * Vector3.Scale(_magazineRest.scale, _gripOffset));
        }

        // ----- 왼손 -----

        private void EvaluateHand(Timeline tl, float t, out Vector3 wrist, out Vector3 extraEuler)
        {
            Vector3 rest = RestWristWorld();
            Vector3 grip = _magazine.TransformPoint(_gripOffset); // 현재 탄창을 쥔 위치
            Vector3 bulge = transform.TransformVector(_handPathBulge);
            float gripWeight = 0f;
            float chargeWeight = 0f;

            if (t < tl.reachStart)
            {
                wrist = rest;
            }
            else if (t < tl.reachEnd)
            {
                float s = Smooth01(Phase(tl.reachStart, tl.reachEnd, t));
                wrist = Arc(rest, grip, s, bulge);
                gripWeight = s;
            }
            else if (!tl.empty)
            {
                // 일반 재장전: 탄창을 놓지 않는다. 탄창이 손을 끌고 다닌다.
                if (t < tl.returnStart)
                {
                    wrist = grip;
                    gripWeight = 1f;
                }
                else if (t < tl.returnEnd)
                {
                    float s = Smooth01(Phase(tl.returnStart, tl.returnEnd, t));
                    wrist = Arc(grip, rest, s, bulge);
                    gripWeight = 1f - s;
                }
                else
                {
                    wrist = rest;
                }
            }
            else
            {
                // 빈 탄창 재장전
                RailPose(0f, 0f, out Vector3 seatedPos, out Quaternion seatedRot);
                Vector3 gripSeated = GripWorld(seatedPos, seatedRot);
                PouchPose(out Vector3 pouchPos, out Quaternion pouchRot);
                Vector3 pouchHand = GripWorld(pouchPos, pouchRot);
                Vector3 handle = _gun.TransformPoint(_chargingHandlePoint);
                Vector3 pull = _gun.TransformVector(_chargePull);
                Vector3 released = handle + pull + transform.TransformVector(_releaseFlick);

                if (t < tl.handPouchStart)
                {
                    wrist = gripSeated; // 멈치를 누르고 탄창이 떨어지게 둔다.
                    gripWeight = 1f;
                }
                else if (t < tl.dropEnd)
                {
                    float s = Smooth01(Phase(tl.handPouchStart, tl.dropEnd, t));
                    wrist = Arc(gripSeated, pouchHand, s, bulge);
                    gripWeight = 1f;
                }
                else if (t < tl.fetchStart)
                {
                    wrist = pouchHand;
                    gripWeight = 1f;
                }
                else if (t < tl.seat)
                {
                    wrist = grip; // 새 탄창이 손을 끌고 올라온다.
                    gripWeight = 1f;
                }
                else if (t < tl.chargeReachEnd)
                {
                    float s = Smooth01(Phase(tl.seat, tl.chargeReachEnd, t));
                    wrist = Arc(gripSeated, handle, s, bulge);
                    gripWeight = 1f - s;
                    chargeWeight = s;
                }
                else if (t < tl.chargePullEnd)
                {
                    wrist = handle + pull * EaseOut(Phase(tl.chargeReachEnd, tl.chargePullEnd, t));
                    chargeWeight = 1f;
                }
                else if (t < tl.chargeRelease)
                {
                    wrist = handle + pull;
                    chargeWeight = 1f;
                }
                else if (t < tl.returnStart)
                {
                    wrist = Vector3.Lerp(handle + pull, released, EaseOut(Phase(tl.chargeRelease, tl.returnStart, t)));
                    chargeWeight = 1f;
                }
                else if (t < tl.returnEnd)
                {
                    float s = Smooth01(Phase(tl.returnStart, tl.returnEnd, t));
                    wrist = Arc(released, rest, s, bulge);
                    chargeWeight = 1f - s;
                }
                else
                {
                    wrist = rest;
                }
            }

            extraEuler = _magGripHandEuler * gripWeight + _chargeHandEuler * chargeWeight;
        }

        private Vector3 RestWristWorld()
        {
            return _leftArm.parent.TransformPoint(_leftRest.position);
        }

        // 손목이 wrist에 있을 때, 팔뚝이 화면 밖 팔꿈치를 향하도록 하는 로컬 회전
        private Quaternion HandLocalRotation(Vector3 wrist, Vector3 extraEuler)
        {
            Transform parent = _leftArm.parent;
            Vector3 restWrist = parent.TransformPoint(_leftRest.position);
            Vector3 elbowRest = transform.TransformPoint(_elbowRestView);
            Vector3 elbow = elbowRest + (wrist - restWrist) * _elbowFollow;

            Vector3 restAxis = elbowRest - restWrist;
            Vector3 axis = elbow - wrist;
            Quaternion swing = restAxis.sqrMagnitude > 1e-8f && axis.sqrMagnitude > 1e-8f
                ? Quaternion.FromToRotation(restAxis, axis)
                : Quaternion.identity;

            Quaternion restWorld = parent.rotation * _leftRest.rotation;
            Quaternion world = swing * restWorld * Quaternion.Euler(extraEuler);
            return Quaternion.Inverse(parent.rotation) * world;
        }

        // ----- 이벤트 (반동 / 소리) -----

        private void FireEvents(float from, float to)
        {
            Timeline tl = Current;
            if (Crossed(from, to, tl.release))
            {
                Kick(new Vector3(0f, 0.04f, 0f), new Vector3(-20f, 0f, 0f));
                PlayClip(_magOutClip);
            }

            if (!tl.empty && Crossed(from, to, tl.pullEnd))
            {
                Kick(new Vector3(0f, 0.06f, 0f), new Vector3(-18f, 0f, 6f)); // 탄창이 빠지며 총이 가벼워짐
            }

            if (Crossed(from, to, tl.seat))
            {
                Kick(new Vector3(0f, 0.32f, 0.02f), new Vector3(95f, 0f, -25f)); // 탄창을 쳐 넣음
                PlayClip(_magInClip);
            }

            if (tl.empty)
            {
                if (Crossed(from, to, tl.chargeReachEnd)) PlayClip(_boltPullClip);
                if (Crossed(from, to, tl.chargePullEnd)) Kick(new Vector3(0f, 0f, -0.08f), new Vector3(-20f, 0f, 0f));
                if (Crossed(from, to, tl.chargeRelease))
                {
                    Kick(new Vector3(0f, 0.05f, 0.18f), new Vector3(-70f, 10f, 18f)); // 노리쇠가 닫힘
                    PlayClip(_boltReleaseClip);
                }
            }
        }

        private static bool Crossed(float from, float to, float point)
        {
            return from < point && to >= point;
        }

        private void PlayClip(AudioClip clip)
        {
            if (clip == null) return;
            if (_audio == null)
            {
                _audio = GetComponent<AudioSource>();
                if (_audio == null)
                {
                    _audio = gameObject.AddComponent<AudioSource>();
                    _audio.playOnAwake = false;
                    _audio.spatialBlend = 0f;
                }
            }
            _audio.PlayOneShot(clip, _volume * GameAudio.MasterVolume);
        }

        // ----- 스프링 -----

        private void Kick(Vector3 linear, Vector3 angular)
        {
            _springVel += linear * _impactScale;
            _springRotVel += angular * _impactScale;
        }

        private void StepSpring(float dt)
        {
            if (dt <= 0f) return;
            float k = _springStiffness;
            float c = 2f * Mathf.Sqrt(k) * _springDamping;
            int steps = Mathf.Clamp(Mathf.CeilToInt(dt / 0.008f), 1, 8);
            float h = Mathf.Min(dt, 0.1f) / steps;
            for (int i = 0; i < steps; i++)
            {
                _springVel += (-k * _springPos - c * _springVel) * h;
                _springPos += _springVel * h;
                _springRotVel += (-k * _springRot - c * _springRotVel) * h;
                _springRot += _springRotVel * h;
            }
        }

        private bool IsSpringMoving()
        {
            return _springPos.sqrMagnitude > 1e-9f || _springVel.sqrMagnitude > 1e-7f
                || _springRot.sqrMagnitude > 1e-4f || _springRotVel.sqrMagnitude > 1e-2f;
        }

        private void ResetSpring()
        {
            _springPos = _springVel = _springRot = _springRotVel = Vector3.zero;
        }

        // ----- 전환 / 복구 -----

        private void StartFade(float duration, bool snapMagazine)
        {
            _fromGunPos = _gun.localPosition; _fromGunRot = _gun.localRotation;
            _fromArmPos = _leftArm.localPosition; _fromArmRot = _leftArm.localRotation;
            if (snapMagazine)
            {
                _fromMagPos = _magazineRest.position; _fromMagRot = _magazineRest.rotation;
            }
            else
            {
                _fromMagPos = _magazine.localPosition; _fromMagRot = _magazine.localRotation;
            }
            _fadeDuration = duration;
            _fadeElapsed = 0f;
            _driving = true;
        }

        private void SetMagazineVisible(bool visible)
        {
            if (_magazineHidden == !visible) return;
            _magazineHidden = !visible;
            for (int i = 0; i < _magazineRenderers.Length; i++)
                if (_magazineRenderers[i] != null) _magazineRenderers[i].enabled = visible && _rendererStates[i];
        }

        private void RestoreRestPose()
        {
            if (!_captured) return;
            _gunRest.Apply(_gun); _leftRest.Apply(_leftArm); _magazineRest.Apply(_magazine);
            _magazineHidden = true; // 강제로 다시 켠다.
            SetMagazineVisible(true);
        }

        private void OnDisable()
        {
            IsReloading = false;
            _previewOwned = false;
            _fadeDuration = 0f;
            _fadeElapsed = 0f;
            _driving = false;
            ResetSpring();
            RestoreRestPose();
        }

        // ----- 보간 함수 -----

        private static float Phase(float begin, float end, float t)
        {
            return end > begin ? Mathf.Clamp01((t - begin) / (end - begin)) : (t >= end ? 1f : 0f);
        }

        private static float Smooth(float begin, float end, float t)
        {
            return Smooth01(Phase(begin, end, t));
        }

        private static float Smooth01(float x)
        {
            x = Mathf.Clamp01(x);
            return x * x * (3f - 2f * x);
        }

        private static float EaseOut(float x)
        {
            x = 1f - Mathf.Clamp01(x);
            return 1f - x * x;
        }

        private static float EaseIn(float x)
        {
            x = Mathf.Clamp01(x);
            return x * x;
        }

        // a에서 b로 휘어진 경로 (2차 베지어)
        private static Vector3 Arc(Vector3 a, Vector3 b, float s, Vector3 bulge)
        {
            Vector3 control = (a + b) * 0.5f + bulge;
            float u = 1f - s;
            return u * u * a + 2f * u * s * control + s * s * b;
        }
    }
}
