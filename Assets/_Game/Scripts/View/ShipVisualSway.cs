using UnityEngine;

namespace Game.View
{
    /// <summary>
    /// 파도에 흔들리는 시각 효과 전용.
    /// 반드시 모델 전용 자식 Transform에만 적용한다. 이동/충돌 계산에는 영향을 주지 않는다.
    /// 파도 따라가기(2026-11-01): 움직이는 파도(OceanWaveTexture)의 높이를 선수·선미·좌우 현에서 읽어
    /// 오르내림·앞뒤 기울기·좌우 기울기를 만든다 — 큰 물결이 지나가면 배가 함께 들리고 기운다. 배가 길수록 덜 기운다.
    /// 파도 계산 전에는 예전처럼 사인 흔들림만. 적 수상함에도 붙는다(EnemyController가 ModelPivot에).
    /// </summary>
    public class ShipVisualSway : MonoBehaviour
    {
        [SerializeField] private float rollAmplitude = 2.5f;
        [SerializeField] private float pitchAmplitude = 1.2f;
        [SerializeField] private float rollSpeed = 0.7f;
        [SerializeField] private float pitchSpeed = 0.9f;
        [SerializeField] private float bobAmplitude = 0.12f;
        [Tooltip("물결 높이(0~1 기준) 1이 몇 m인가 — 오르내림·기울기 크기")]
        [SerializeField] private float waveHeight = 1.4f;
        [Tooltip("기울기 과장(쿼터뷰에서 보이게)")]
        [SerializeField] private float tiltBoost = 1.25f;

        private Vector3 _baseLocalPos;
        private Quaternion _baseLocalRot = Quaternion.identity;
        private float _seed;
        private BoxCollider _hull;
        private float _roll, _pitch, _bob;

        // 피격 흔들림(2026-11-20 타격감): 맞은 방향으로 잠깐 기울었다가 용수철처럼 돌아온다
        private Vector2 _kick, _kickVel;   // x = 앞뒤(도), y = 좌우(도)
        private const float KickSpring = 170f, KickDamping = 13f, KickMaxDegrees = 7f;

        /// <summary>검증용: 지금 파도로 생긴 오르내림(m)·앞뒤·좌우 기울기(도).</summary>
        public Vector3 WaveMotion => new(_bob, _pitch, _roll);
        /// <summary>검증용: 지금 피격으로 기운 정도(도, 앞뒤 · 좌우 합).</summary>
        public float KickDegrees => _kick.magnitude;

        /// <summary>
        /// 맞았을 때 흔들림. worldDir = 탄이 날아온 방향(탄의 진행 방향), strength 0~1(HitFlash.StrengthFor).
        /// 옆에서 맞으면 맞은 쪽이 들리며 반대로 기울고, 앞뒤에서 맞으면 앞뒤로 끄덕인다. 작은 배일수록 크게.
        /// </summary>
        public void Kick(Vector3 worldDir, float strength)
        {
            var root = transform.parent != null ? transform.parent : transform;
            Vector3 local = root.InverseTransformDirection(worldDir);
            local.y = 0f;
            if (local.sqrMagnitude < 1e-4f) return;
            local.Normalize();
            float length = _hull != null ? _hull.size.z * _hull.transform.lossyScale.z : 6f;
            float size = Mathf.Clamp(7f / Mathf.Max(2f, length), 0.55f, 1.5f);
            float deg = 3.2f * Mathf.Clamp01(strength) * size;
            // 각속도로 넣어 탁 치이는 느낌(바로 기운 자세로 튀지 않게)
            _kickVel += new Vector2(local.z * deg * 0.55f, -local.x * deg) * 24f;
        }

        /// <summary>없으면 붙인다(모델 전용 자식에).</summary>
        public static ShipVisualSway Ensure(Transform model)
        {
            if (model == null) return null;
            return model.TryGetComponent<ShipVisualSway>(out var s) ? s : model.gameObject.AddComponent<ShipVisualSway>();
        }

        private void Awake()
        {
            _baseLocalPos = transform.localPosition;
            _baseLocalRot = transform.localRotation;   // 모델 축 보정 회전이 있으면 그 위에 흔들림을 얹는다
            _awoken = true;
            _seed = Random.value * 100f;
            _hull = GetComponentInParent<BoxCollider>();
        }

        /// <summary>모델 자리를 바꿨으면(수면선 맞춤 등) 기준 자리를 다시 잡는다.</summary>
        public void Rebase() => _baseLocalPos = transform.localPosition;

        private bool _awoken;

        /// <summary>
        /// 흔들리지 않은 기본 자세의 월드 변환(2026-11-22 — 항해등처럼 모델에 붙일 것의 자리를 흔들림과 상관없이 정할 때).
        /// </summary>
        public Matrix4x4 RestLocalToWorld
        {
            get
            {
                Vector3 p = _awoken ? _baseLocalPos : transform.localPosition;
                Quaternion r = _awoken ? _baseLocalRot : transform.localRotation;
                var local = Matrix4x4.TRS(p, r, transform.localScale);
                return transform.parent != null ? transform.parent.localToWorldMatrix * local : local;
            }
        }

        /// <summary>흔들림 없는 기준 자세로 되돌린다(풀에서 다시 나올 때 수면선을 맞추기 전).</summary>
        public void ResetPose()
        {
            transform.localRotation = _baseLocalRot;
            transform.localPosition = _baseLocalPos;
            _roll = _pitch = _bob = 0f;
            _kick = _kickVel = Vector2.zero;
        }

        private void LateUpdate()
        {
            float t = Time.time + _seed;
            float roll = Mathf.Sin(t * rollSpeed) * rollAmplitude;
            float pitch = Mathf.Sin(t * pitchSpeed + 1.3f) * pitchAmplitude;
            float bob = Mathf.Sin(t * rollSpeed * 1.3f) * bobAmplitude;

            if (OceanWaveTexture.HeightReady)
            {
                var root = transform.parent != null ? transform.parent : transform;
                Vector3 size = _hull != null ? Vector3.Scale(_hull.size, _hull.transform.lossyScale) : new Vector3(2f, 1f, 6f);
                SampleWave(root, size.z, size.x, waveHeight, tiltBoost, out float wantBob, out float wantPitch, out float wantRoll);
                float k = 1f - Mathf.Exp(-Time.deltaTime * 5f);
                _bob = Mathf.Lerp(_bob, wantBob, k);
                _pitch = Mathf.Lerp(_pitch, wantPitch, k);
                _roll = Mathf.Lerp(_roll, wantRoll, k);
                // 파도가 주가 되고, 정해진 흔들림은 잔잔하게만 남긴다
                roll = roll * 0.35f + _roll;
                pitch = pitch * 0.35f + _pitch;
                bob = bob * 0.35f + _bob;
            }

            if (_kick.sqrMagnitude > 1e-5f || _kickVel.sqrMagnitude > 1e-4f)
            {
                // 프레임이 느려도 같은 속도로 돌아오게 잘게 나눠 계산한다(한 번에 1/60초 이하)
                float remaining = Mathf.Min(Time.deltaTime, 0.25f);
                while (remaining > 1e-5f)
                {
                    float dt = Mathf.Min(remaining, 1f / 60f);
                    remaining -= dt;
                    _kickVel += (-KickSpring * _kick - KickDamping * _kickVel) * dt;
                    _kick += _kickVel * dt;
                }
                _kick = Vector2.ClampMagnitude(_kick, KickMaxDegrees);
                if (_kick.sqrMagnitude < 1e-5f && _kickVel.sqrMagnitude < 1e-4f) _kick = _kickVel = Vector2.zero;
                pitch += _kick.x;
                roll += _kick.y;
            }

            transform.localRotation = Quaternion.Euler(pitch, 0f, roll) * _baseLocalRot;
            transform.localPosition = _baseLocalPos + Vector3.up * bob;
        }

        /// <summary>
        /// 그 배 자리의 파도로 생기는 오르내림(m)·앞뒤 기울기·좌우 기울기(도, ±6° 안) — 선수·선미·좌우 현 네 점의 물결 높이로(부드럽게 하지 않은 값).
        /// 호위함처럼 이 컴포넌트를 쓰지 않는 배도 같은 파도를 따르게 공용으로 둔다(2026-11-03).
        /// </summary>
        public static void SampleWave(Transform root, float length, float beam, float waveHeight, float tiltBoost,
                                      out float bob, out float pitch, out float roll)
        {
            length = Mathf.Max(2f, length); beam = Mathf.Max(1f, beam);
            Vector3 c = root.position;
            Vector3 fwd = root.forward; fwd.y = 0f; fwd = fwd.sqrMagnitude > 1e-4f ? fwd.normalized : Vector3.forward;
            Vector3 right = new(fwd.z, 0f, -fwd.x);
            float now = Time.timeSinceLevelLoad;
            float H(Vector3 p) => OceanWaveTexture.SampleHeight(new Vector2(p.x, p.z), now);
            float hb = H(c + fwd * (length * 0.45f)), hs = H(c - fwd * (length * 0.45f));
            float hp = H(c - right * (beam * 0.5f)), hr = H(c + right * (beam * 0.5f));
            bob = ((hb + hs + hp + hr) * 0.25f - 0.5f) * waveHeight;
            pitch = Mathf.Clamp(Mathf.Atan2((hs - hb) * waveHeight, length) * Mathf.Rad2Deg * tiltBoost, -6f, 6f);   // 선수가 높으면 들린다
            roll = Mathf.Clamp(Mathf.Atan2((hr - hp) * waveHeight, beam) * Mathf.Rad2Deg * tiltBoost, -6f, 6f);      // 우현이 높으면 우현이 들린다
        }
    }
}
