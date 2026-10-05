using UnityEngine;

namespace Game.Modules.Runtime
{
    /// <summary>
    /// 회전 안테나를 가진 레이더. 레이더 화면(RadarScopeUI)이 안테나가 실제로 바라보는 방위에 맞춰 스윕을 그린다.
    /// </summary>
    public interface IRadarSource
    {
        /// <summary>모듈이 살아 있어 안테나가 돌고 있는가.</summary>
        bool RadarOperational { get; }

        /// <summary>이 레이더의 탐지거리(데이터 값).</summary>
        float RadarRange { get; }

        /// <summary>안테나 분당 회전수. 스윕 한 바퀴 = 60 / rpm 초.</summary>
        float AntennaRpm { get; }

        /// <summary>안테나 빔이 향한 월드 방위(도). 북(+Z) 0, 시계 방향 증가.</summary>
        float AntennaBearing { get; }
    }

    /// <summary>
    /// 안테나 방위 계산. 아트 모델의 안테나는 Blender 축이라 어느 로컬 축이 빔 방향인지 모델마다 다르다.
    /// 안테나 메시의 수평 두 축 중 얇은 축(판의 법선)을 빔 방향으로 보고 한 번만 구한 뒤,
    /// 이후에는 안테나 Transform의 실제 회전으로 방위를 읽는다 — 함선 선회·안테나 회전이 모두 그대로 반영된다.
    /// 안테나가 없거나(프리미티브 모델) 메시가 없으면 회전량을 직접 적산해 함선 방위에 더한다.
    /// </summary>
    public sealed class RadarAntennaTracker
    {
        private Vector3 _localBeam;
        private bool _calibrated;
        private bool _useTransform;
        private float _angle;   // 안테나가 없을 때 함선 기준 누적 회전(도)

        public void Advance(float rpm, float dt) => _angle = Mathf.Repeat(_angle + rpm * 6f * dt, 360f);

        public float Bearing(Transform antenna, Transform ship)
        {
            if (antenna != null)
            {
                if (!_calibrated) Calibrate(antenna);
                if (_useTransform)
                {
                    Vector3 dir = antenna.TransformDirection(_localBeam);
                    if (dir.x * dir.x + dir.z * dir.z > 1e-4f)
                        return Mathf.Repeat(Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg, 360f);
                }
            }
            float shipYaw = ship != null ? ship.eulerAngles.y : 0f;
            return Mathf.Repeat(shipYaw + _angle, 360f);
        }

        private void Calibrate(Transform antenna)
        {
            _calibrated = true;

            // 안테나 로컬 공간에서 메시 전체의 크기
            bool any = false;
            Vector3 min = Vector3.positiveInfinity, max = Vector3.negativeInfinity;
            foreach (var mf in antenna.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.sharedMesh == null) continue;
                var b = mf.sharedMesh.bounds;
                for (int i = 0; i < 8; i++)
                {
                    var corner = b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    var p = antenna.InverseTransformPoint(mf.transform.TransformPoint(corner));
                    min = Vector3.Min(min, p);
                    max = Vector3.Max(max, p);
                    any = true;
                }
            }
            if (!any) return;

            Vector3 size = Vector3.Scale(max - min, antenna.lossyScale);
            size = new Vector3(Mathf.Abs(size.x), Mathf.Abs(size.y), Mathf.Abs(size.z));

            // 월드 위와 가장 가까운 로컬 축은 회전축, 나머지 둘 중 얇은 쪽이 빔 방향
            Vector3 upLocal = antenna.InverseTransformDirection(Vector3.up);
            int upAxis = Mathf.Abs(upLocal.x) > Mathf.Abs(upLocal.y)
                ? (Mathf.Abs(upLocal.x) > Mathf.Abs(upLocal.z) ? 0 : 2)
                : (Mathf.Abs(upLocal.y) > Mathf.Abs(upLocal.z) ? 1 : 2);
            int a = (upAxis + 1) % 3, c = (upAxis + 2) % 3;
            int beamAxis = size[a] <= size[c] ? a : c;

            _localBeam = Vector3.zero;
            _localBeam[beamAxis] = 1f;
            _useTransform = true;
        }
    }
}
