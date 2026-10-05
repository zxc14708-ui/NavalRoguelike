using UnityEngine;
using Game.Combat;
using Game.Core;

namespace Game.Enemies
{
    /// <summary>
    /// 전자전 코르벳의 방해 장비(2026-10-05, 스테이지 3). 같은 오브젝트의 적(EnemyController)이 살아 있고
    /// 플레이어가 jamRange 안이면 플레이어 레이더 탐지 거리를 줄인다(<see cref="EnemyJamming"/>, ×0.75).
    /// 방해 안테나(AntennaPivot_…)가 좌우로 훑고, pulseInterval초마다 전자전 파동 이펙트를 낸다.
    /// 방해가 처음 걸리면 화면에 알린다. 이 배를 격침하면 바로 풀린다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EwJammer : MonoBehaviour
    {
        [SerializeField] private float jamRange = 60f;
        [SerializeField] private GameObject pulseEffect;
        [SerializeField] private float pulseInterval = 3f;
        [SerializeField] private Transform[] antennas;
        [SerializeField] private float sweepDegrees = 35f;
        [SerializeField] private float sweepSpeed = 1.4f;

        private EnemyController _owner;
        private Quaternion[] _antennaRest;
        private float _pulseTimer;
        private bool _on;

        private void Awake()
        {
            _owner = GetComponent<EnemyController>();
            if (antennas != null)
            {
                _antennaRest = new Quaternion[antennas.Length];
                for (int i = 0; i < antennas.Length; i++) if (antennas[i] != null) _antennaRest[i] = antennas[i].localRotation;
            }
        }

        private void OnDisable() => SetOn(false);

        private void Update()
        {
            var player = GameManager.Instance != null && GameManager.Instance.Player != null ? GameManager.Instance.Player.transform : null;
            bool on = _owner != null && _owner.IsAlive && player != null &&
                      (player.position - transform.position).sqrMagnitude <= jamRange * jamRange;
            SetOn(on);

            if (antennas != null && _antennaRest != null)
            {
                float yaw = Mathf.Sin(Time.time * sweepSpeed) * sweepDegrees;
                for (int i = 0; i < antennas.Length; i++)
                {
                    var a = antennas[i];
                    if (a == null) continue;
                    Vector3 axis = a.parent != null ? a.parent.InverseTransformDirection(transform.up) : Vector3.up;
                    a.localRotation = Quaternion.AngleAxis(i % 2 == 0 ? yaw : -yaw, axis) * _antennaRest[i];
                }
            }

            if (!on) return;
            _pulseTimer -= Time.deltaTime;
            if (_pulseTimer > 0f) return;
            _pulseTimer = pulseInterval;
            if (pulseEffect != null) PooledEffect.Spawn(pulseEffect, transform.position + Vector3.up * 2f, 1.2f);
        }

        private void SetOn(bool on)
        {
            if (on == _on) return;
            _on = on;
            bool wasJammed = EnemyJamming.IsJammed;
            EnemyJamming.Set(this, on);
            if (on && !wasJammed)
            {
                GameEvents.RaiseBossPhaseChanged($"적 전자전 방해 — 레이더 탐지 거리 -{(1f - EnemyJamming.JammedRadarMultiplier) * 100f:0}%");
                CombatLog.Add("전자전", $"{(_owner != null && _owner.Definition != null ? _owner.Definition.DisplayName : "전자전 코르벳")} 방해 시작 — 레이더 탐지 거리 ×{EnemyJamming.JammedRadarMultiplier}");
            }
        }
    }
}
