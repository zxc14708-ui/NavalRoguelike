using UnityEngine;

namespace Game.View
{
    /// <summary>
    /// 파도에 흔들리는 시각 효과 전용.
    /// 반드시 모델 전용 자식 Transform에만 적용한다. 이동/충돌 계산에는 영향을 주지 않는다.
    /// </summary>
    public class ShipVisualSway : MonoBehaviour
    {
        [SerializeField] private float rollAmplitude = 2.5f;
        [SerializeField] private float pitchAmplitude = 1.2f;
        [SerializeField] private float rollSpeed = 0.7f;
        [SerializeField] private float pitchSpeed = 0.9f;
        [SerializeField] private float bobAmplitude = 0.12f;

        private Vector3 _baseLocalPos;
        private float _seed;

        private void Awake()
        {
            _baseLocalPos = transform.localPosition;
            _seed = Random.value * 100f;
        }

        private void LateUpdate()
        {
            float t = Time.time + _seed;
            float roll = Mathf.Sin(t * rollSpeed) * rollAmplitude;
            float pitch = Mathf.Sin(t * pitchSpeed + 1.3f) * pitchAmplitude;
            float bob = Mathf.Sin(t * rollSpeed * 1.3f) * bobAmplitude;

            transform.localRotation = Quaternion.Euler(pitch, 0f, roll);
            transform.localPosition = _baseLocalPos + Vector3.up * bob;
        }
    }
}
