using System.Collections.Generic;
using UnityEngine;

namespace Game.TaskForce
{
    /// <summary>
    /// 호위함 모델(Codex TaskForceEscortRedesign)의 가동부를 움직인다. 모델을 붙일 때 이름으로 찾아 만든다(<see cref="Attach"/>).
    ///   - 포탑: 선회 피벗(XxxPivot / XxxTrainPivot) → 고각 피벗(XxxElevation / XxxElevationPivot) → 포구·발사점.
    ///     함포(주포·후방포·CIWS·원격 기관총), 함대공(SAM), 유도로켓, 대함 발사관(선회만), 기만체 발사기.
    ///     <see cref="Engage"/>로 표적을 주면 그쪽으로 돌고(초당 회전 한도), 잠시 쏠 일이 없으면 제자리로 돌아온다.
    ///   - 센서: 탐색 레이더·안테나는 계속 돌고, 전자전 배열은 좌우로 훑고, 사통 레이더는 함포 표적을 따라간다.
    /// 축 보정과 상관없이 동작하도록 회전은 모두 "함의 위 방향"을 기준으로 월드에서 계산해 로컬로 되돌린다.
    /// 판정과는 무관한 연출이다(발사 판정은 EscortDefense가 즉시 처리).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EscortTurrets : MonoBehaviour
    {
        public enum MountKind { Gun, Sam, Strike, Decoy }

        private sealed class Mount
        {
            public MountKind Kind;
            public string Name;
            public Transform Yaw, Pitch;
            public Transform[] Muzzles;
            public Quaternion YawRest, PitchRest;
            public Vector3 BarrelLocal;      // 고각 피벗(없으면 선회 피벗) 기준 포신 방향
            public float Arc, MinElev, MaxElev, Speed;
            public Vector3 Target;
            public float HoldUntil;
            public int NextMuzzle;
            public bool Director;            // 사통 레이더: 함포 표적을 따라 돈다(쏘지 않음)
        }

        private readonly List<Mount> _mounts = new();
        private readonly List<Transform> _spin = new();
        private readonly List<(Transform t, Quaternion rest, float phase)> _sweep = new();
        private readonly List<Transform> _torpedoPoints = new();
        private Transform _ship;
        private Vector3 _lastGunTarget;
        private float _lastGunTargetUntil;

        public int MountCount => _mounts.Count;
        public int CountOf(MountKind kind) { int n = 0; foreach (var m in _mounts) if (m.Kind == kind && !m.Director) n++; return n; }

        /// <summary>모델에서 가동부를 찾아 붙인다. 가동부가 하나도 없으면 null.</summary>
        public static EscortTurrets Attach(GameObject model, Transform ship)
        {
            var rig = model.AddComponent<EscortTurrets>();
            rig._ship = ship;
            rig.Scan(model.transform);
            if (rig._mounts.Count == 0 && rig._spin.Count == 0 && rig._sweep.Count == 0 && rig._torpedoPoints.Count == 0)
            {
                Destroy(rig);
                return null;
            }
            return rig;
        }

        private static string Clean(string n)
        {
            int dot = n.IndexOf('.');
            return dot > 0 ? n.Substring(0, dot) : n;
        }

        private void Scan(Transform root)
        {
            var all = root.GetComponentsInChildren<Transform>(true);
            foreach (var t in all)
            {
                string n = Clean(t.name);
                switch (n)
                {
                    case "PrimaryGunPivot": AddMount(MountKind.Gun, t, "PrimaryGunElevation", "PrimaryGunMuzzle", 150f, -5f, 65f, 160f); break;
                    case "AftGunPivot": AddMount(MountKind.Gun, t, "AftGunElevation", "AftGunMuzzle", 150f, -5f, 65f, 160f); break;
                    case "CIWSPivot": AddMount(MountKind.Gun, t, "CIWSElevationPivot", "CIWSMuzzle", 170f, -5f, 80f, 260f); break;
                    case "RWS_PPivot": AddMount(MountKind.Gun, t, null, "RWS_PMuzzle", 110f, -5f, 45f, 200f); break;
                    case "RWS_SPivot": AddMount(MountKind.Gun, t, null, "RWS_SMuzzle", 110f, -5f, 45f, 200f); break;
                    case "SAM_TrainPivot": AddMount(MountKind.Sam, t, "SAM_ElevationPivot", "SAMLaunchPoint", 170f, 20f, 70f, 120f); break;
                    case "RocketTrainPivot": AddMount(MountKind.Strike, t, "RocketElevationPivot", "RocketLaunchPoint", 160f, 10f, 40f, 110f); break;
                    case "MissileBankPortPivot": AddMount(MountKind.Strike, t, null, "LaunchPoint_MissileBankPort", 75f, 0f, 0f, 70f); break;
                    case "MissileBankStarboardPivot": AddMount(MountKind.Strike, t, null, "LaunchPoint_MissileBankStarboard", 75f, 0f, 0f, 70f); break;
                    case "DecoyTrain_P": AddMount(MountKind.Decoy, t, "DecoyElevation_P", "DecoyLaunchPoint_P", 100f, 25f, 60f, 140f); break;
                    case "DecoyTrain_S": AddMount(MountKind.Decoy, t, "DecoyElevation_S", "DecoyLaunchPoint_S", 100f, 25f, 60f, 140f); break;
                    case "FireControlRadarPivot":
                        var fc = AddMount(MountKind.Gun, t, null, null, 170f, 0f, 0f, 200f);
                        if (fc != null) fc.Director = true;
                        break;
                    case "RadarPivot":
                    case "AntennaPivot":
                        _spin.Add(t);
                        break;
                    case "EWArrayPivot_P":
                    case "EWArrayPivot_S":
                        _sweep.Add((t, t.localRotation, n.EndsWith("P") ? 0f : 1.7f));
                        break;
                }
                if (n.StartsWith("TorpedoLaunchPoint")) _torpedoPoints.Add(t);
            }
            // 레이더 안의 레이더(AirSearchRadarPivot)는 바깥 RadarPivot과 함께 돈다 — 겹쳐 돌리지 않는다
            _spin.RemoveAll(s => { for (var p = s.parent; p != null; p = p.parent) if (_spin.Contains(p)) return true; return false; });
        }

        private Mount AddMount(MountKind kind, Transform yaw, string pitchName, string muzzlePrefix, float arc, float minElev, float maxElev, float speed)
        {
            Transform pitch = null;
            if (pitchName != null)
                foreach (var c in yaw.GetComponentsInChildren<Transform>(true))
                    if (Clean(c.name) == pitchName) { pitch = c; break; }
            var muzzles = new List<Transform>();
            if (muzzlePrefix != null)
                foreach (var c in yaw.GetComponentsInChildren<Transform>(true))
                    if (Clean(c.name).StartsWith(muzzlePrefix)) muzzles.Add(c);

            var m = new Mount
            {
                Kind = kind, Name = Clean(yaw.name), Yaw = yaw, Pitch = pitch, Muzzles = muzzles.ToArray(),
                YawRest = yaw.localRotation, PitchRest = pitch != null ? pitch.localRotation : Quaternion.identity,
                Arc = arc, MinElev = minElev, MaxElev = maxElev, Speed = speed,
            };
            // 포신 방향: 고각 피벗 → 포구들의 가운데(없으면 함의 앞)
            var basis = pitch != null ? pitch : yaw;
            Vector3 dir = Vector3.zero;
            if (muzzles.Count > 0)
            {
                Vector3 c = Vector3.zero;
                foreach (var mz in muzzles) c += mz.position;
                dir = c / muzzles.Count - basis.position;
            }
            if (_ship != null) dir -= Vector3.Project(dir, _ship.up) * (pitch == null ? 1f : 0f);
            if (dir.sqrMagnitude < 1e-6f) dir = _ship != null ? _ship.forward : Vector3.forward;
            m.BarrelLocal = basis.InverseTransformDirection(dir.normalized);
            _mounts.Add(m);
            return m;
        }

        /// <summary>
        /// 그 종류의 포탑 중 표적 쪽으로 쏠 수 있는 것을 표적으로 돌리고, 발사할 포구 위치를 준다.
        /// aligned = 그 포탑이 이미 표적을 향했는가(포탑이 없으면 true). 포탑이 없으면 false를 돌려준다.
        /// </summary>
        public bool Engage(MountKind kind, Vector3 target, out Vector3 muzzle, out bool aligned, float hold = 1.6f)
        {
            muzzle = default;
            aligned = true;
            Mount best = null;
            float bestOff = float.MaxValue;
            foreach (var m in _mounts)
            {
                if (m.Kind != kind || m.Director) continue;
                float off = Mathf.Abs(YawOffsetFromRest(m, target));
                if (off > m.Arc) continue;
                // 지금 향한 방향과 가장 가까운 포탑(덜 돌리는 쪽)
                float turn = AimError(m, target);
                if (turn < bestOff) { bestOff = turn; best = m; }
            }
            if (kind == MountKind.Gun) { _lastGunTarget = target; _lastGunTargetUntil = Time.time + hold; }
            if (best == null) return false;
            // 같은 종류의 다른 포탑도 쏠 수 있으면 함께 표적을 향한다(일제 선회)
            foreach (var m in _mounts)
                if (m.Kind == kind && !m.Director && Mathf.Abs(YawOffsetFromRest(m, target)) <= m.Arc) { m.Target = target; m.HoldUntil = Time.time + hold; }
            aligned = bestOff < 12f;
            if (best.Muzzles.Length > 0)
            {
                muzzle = best.Muzzles[best.NextMuzzle % best.Muzzles.Length].position;
                best.NextMuzzle++;
            }
            else muzzle = (best.Pitch != null ? best.Pitch : best.Yaw).position;
            return true;
        }

        private readonly Dictionary<string, Transform> _sockets = new();

        /// <summary>이름 붙은 소켓(VLSLaunchPoint, EWOrigin 등)의 위치. 없으면 false.</summary>
        public bool TrySocket(string name, out Vector3 position)
        {
            position = default;
            if (!_sockets.TryGetValue(name, out var t))
            {
                t = null;
                foreach (var c in GetComponentsInChildren<Transform>(true)) if (Clean(c.name) == name) { t = c; break; }
                _sockets[name] = t;
            }
            if (t == null) return false;
            position = t.position;
            return true;
        }

        /// <summary>대잠: 표적 쪽 현측의 어뢰 발사점(없으면 false).</summary>
        public bool TorpedoPoint(Vector3 target, out Vector3 point)
        {
            point = default;
            float best = float.MaxValue;
            foreach (var t in _torpedoPoints)
            {
                if (t == null) continue;
                float d = (t.position - target).sqrMagnitude;
                if (d < best) { best = d; point = t.position; }
            }
            return best < float.MaxValue;
        }

        private Vector3 Up => _ship != null ? _ship.up : Vector3.up;

        /// <summary>제자리(선회·고각 모두 처음 자세)에서의 포신 방향(월드).</summary>
        private Vector3 RestBarrel(Mount m)
        {
            var q = (m.Yaw.parent != null ? m.Yaw.parent.rotation : Quaternion.identity) * m.YawRest;
            if (m.Pitch == null) return q * m.BarrelLocal;
            // 선회 피벗과 고각 피벗 사이의 중간 노드(대개 없음)
            var chain = new List<Transform>();
            for (var p = m.Pitch.parent; p != null && p != m.Yaw; p = p.parent) chain.Add(p);
            for (int i = chain.Count - 1; i >= 0; i--) q *= chain[i].localRotation;
            return q * m.PitchRest * m.BarrelLocal;
        }

        private float YawOffsetFromRest(Mount m, Vector3 target)
        {
            Vector3 up = Up;
            Vector3 rest = Vector3.ProjectOnPlane(RestBarrel(m), up);
            Vector3 want = Vector3.ProjectOnPlane(target - m.Yaw.position, up);
            if (rest.sqrMagnitude < 1e-6f || want.sqrMagnitude < 1e-6f) return 0f;
            return Vector3.SignedAngle(rest, want, up);
        }

        private float AimError(Mount m, Vector3 target)
        {
            var basis = m.Pitch != null ? m.Pitch : m.Yaw;
            Vector3 barrel = basis.rotation * m.BarrelLocal;
            Vector3 up = Up;
            Vector3 a = Vector3.ProjectOnPlane(barrel, up), b = Vector3.ProjectOnPlane(target - basis.position, up);
            if (a.sqrMagnitude < 1e-6f || b.sqrMagnitude < 1e-6f) return 0f;
            return Vector3.Angle(a, b);
        }

        private void LateUpdate()
        {
            float dt = Time.deltaTime;
            Vector3 up = Up;
            foreach (var t in _spin) if (t != null) t.Rotate(up, 72f * dt, Space.World);   // 5초에 한 바퀴
            float time = Time.time;
            foreach (var (t, rest, phase) in _sweep)
            {
                if (t == null) continue;
                var parentRot = t.parent != null ? t.parent.rotation : Quaternion.identity;
                t.rotation = Quaternion.AngleAxis(Mathf.Sin(time * 0.9f + phase) * 25f, up) * parentRot * rest;
            }

            foreach (var m in _mounts)
            {
                if (m.Yaw == null) continue;
                bool active = time < m.HoldUntil;
                Vector3 target = m.Target;
                if (m.Director) { active = time < _lastGunTargetUntil; target = _lastGunTarget; }

                // 선회: 제자리 방향에서 표적까지의 각(사각 한도 안으로)
                float yaw = 0f, elev = 0f;
                if (active)
                {
                    yaw = Mathf.Clamp(YawOffsetFromRest(m, target), -m.Arc, m.Arc);
                    if (m.Pitch != null)
                    {
                        Vector3 d = target - m.Pitch.position;
                        float h = Vector3.ProjectOnPlane(d, up).magnitude;
                        elev = Mathf.Clamp(Mathf.Atan2(Vector3.Dot(d, up), Mathf.Max(0.01f, h)) * Mathf.Rad2Deg, m.MinElev, m.MaxElev);
                    }
                }
                var parent = m.Yaw.parent != null ? m.Yaw.parent.rotation : Quaternion.identity;
                var wantYawWorld = Quaternion.AngleAxis(yaw, up) * parent * m.YawRest;
                var wantYawLocal = Quaternion.Inverse(parent) * wantYawWorld;
                m.Yaw.localRotation = Quaternion.RotateTowards(m.Yaw.localRotation, wantYawLocal, m.Speed * dt);

                if (m.Pitch != null)
                {
                    var pParent = m.Pitch.parent != null ? m.Pitch.parent.rotation : Quaternion.identity;
                    var restWorld = pParent * m.PitchRest;
                    var wantPitchWorld = restWorld;
                    if (active)
                    {
                        // 지금 선회한 포신(고각은 제자리)을 같은 수직면 안에서 elev만큼 든다
                        Vector3 barrel = restWorld * m.BarrelLocal;
                        Vector3 flat = Vector3.ProjectOnPlane(barrel, up);
                        if (flat.sqrMagnitude > 1e-6f)
                        {
                            flat.Normalize();
                            float e = elev * Mathf.Deg2Rad;
                            Vector3 want = flat * Mathf.Cos(e) + up * Mathf.Sin(e);
                            wantPitchWorld = Quaternion.FromToRotation(barrel, want) * restWorld;
                        }
                    }
                    var wantPitchLocal = Quaternion.Inverse(pParent) * wantPitchWorld;
                    m.Pitch.localRotation = Quaternion.RotateTowards(m.Pitch.localRotation, wantPitchLocal, m.Speed * 0.7f * dt);
                }
            }
        }

        /// <summary>검증용: 이 종류 포탑들이 제자리에서 돌아간 최대 각도.</summary>
        public float MaxTurn(MountKind kind)
        {
            float best = 0f;
            foreach (var m in _mounts)
                if (m.Kind == kind && !m.Director && m.Yaw != null) best = Mathf.Max(best, Quaternion.Angle(m.Yaw.localRotation, m.YawRest));
            return best;
        }

        /// <summary>검증용: 이 종류 포탑 하나의 포신과 표적 방향의 수평 오차(°, 가장 작은 것).</summary>
        public float BestAimError(MountKind kind, Vector3 target)
        {
            float best = float.MaxValue;
            foreach (var m in _mounts) if (m.Kind == kind && !m.Director && m.Yaw != null) best = Mathf.Min(best, AimError(m, target));
            return best;
        }

        public int SpinnerCount => _spin.Count;
        public Transform FirstSpinner => _spin.Count > 0 ? _spin[0] : null;
    }
}
