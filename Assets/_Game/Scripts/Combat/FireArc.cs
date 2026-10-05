using System.Collections.Generic;
using Game.Modules;
using Game.Ship;
using UnityEngine;

namespace Game.Combat
{
    /// <summary>
    /// 사격 금지 구역(Firing Cut-out Zone)이 적용된 사격각.
    /// 설치 방향(포신 중립) 기준 상대 각도를 5도 칸 72개로 나눠, 칸마다 두 가지를 기록한다.
    ///   - 사격에 필요한 최소 앙각: 그 방향의 구조물을 넘겨 쏘려면 표적이 이 각도보다 높아야 한다.
    ///     0이면 수면 표적도 쏠 수 있다. 막힌 방향은 최소 1이라 수면 표적은 절대 못 쏜다.
    ///   - 선회 금지: 포신이 그 방향으로 돌아가지도 않는다(함교·레이더·헬기데크 같은 상부 구조물).
    /// 기본값(default)은 제한 없음이다.
    /// </summary>
    public readonly struct FireArc
    {
        public const int Steps = 72;
        public const float StepDeg = 360f / Steps;

        private readonly byte[] _clearance;
        private readonly bool[] _noTrain;

        public FireArc(byte[] clearance, bool[] noTrain)
        {
            _clearance = clearance;
            _noTrain = noTrain;
        }

        public static FireArc Full => default;
        public bool IsFull => _clearance == null && _noTrain == null;

        public int ClearanceAt(int step) => _clearance == null ? 0 : _clearance[Wrap(step)];
        public bool NoTrainAt(int step) => _noTrain != null && _noTrain[Wrap(step)];

        /// <summary>수면 표적을 쏠 수 있는 칸인가.</summary>
        public bool IsOpenStep(int step) => ClearanceAt(step) == 0 && !NoTrainAt(step);

        /// <summary>수면 표적을 쏠 수 있는 각도의 합(도).</summary>
        public float Width
        {
            get
            {
                if (IsFull) return 360f;
                int n = 0;
                for (int i = 0; i < Steps; i++) if (IsOpenStep(i)) n++;
                return n * StepDeg;
            }
        }

        private static int Wrap(int i) => ((i % Steps) + Steps) % Steps;
        public static int StepOf(float relativeDeg) => Mathf.FloorToInt(Mathf.Repeat(relativeDeg, 360f) / StepDeg) % Steps;

        /// <summary>수면 표적 기준으로 이 방향이 열려 있는가.</summary>
        public bool Contains(float relativeDeg) => IsFull || IsOpenStep(StepOf(relativeDeg));

        /// <summary>이 방향·앙각의 표적을 쏠 수 있는가(구조물 너머 높은 표적은 허용).</summary>
        public bool AllowsFire(float relativeDeg, float elevationDeg)
        {
            if (IsFull) return true;
            int step = StepOf(relativeDeg);
            if (NoTrainAt(step)) return false;

            // 열린 칸(0)은 앙각과 상관없이 쏜다. 수면 표적은 포구보다 낮아 앙각이 음수다.
            int need = ClearanceAt(step);
            return need == 0 || elevationDeg >= need;
        }

        /// <summary>from에서 to까지 선회 금지 칸을 지나지 않고 돌아갈 수 있는가.</summary>
        public bool CanTrainTo(float fromDeg, float toDeg)
        {
            if (_noTrain == null) return true;
            if (NoTrainAt(StepOf(toDeg))) return false;
            float cw = Mathf.Repeat(toDeg - fromDeg, 360f);
            return CountNoTrainAlong(fromDeg, cw) == 0 || CountNoTrainAlong(fromDeg, cw - 360f) == 0;
        }

        /// <summary>
        /// 포신이 가야 할 각도. 선회 금지 방향의 표적이면, 지금 자리에서 돌아갈 수 있는 가장 가까운 경계(바로 안쪽)로 옮긴다.
        /// </summary>
        public float ClampToTrainable(float fromDeg, float relativeDeg)
        {
            if (_noTrain == null || !NoTrainAt(StepOf(relativeDeg))) return relativeDeg;

            int step = StepOf(relativeDeg);
            for (int d = 1; d <= Steps / 2; d++)
            {
                float upEdge = Mathf.DeltaAngle(0f, (step + d) * StepDeg + 0.5f);          // 위쪽 열린 칸의 아래 경계
                float downEdge = Mathf.DeltaAngle(0f, (step - d + 1) * StepDeg - 0.5f);    // 아래쪽 열린 칸의 위 경계
                bool up = !NoTrainAt(step + d) && CanTrainTo(fromDeg, upEdge);
                bool down = !NoTrainAt(step - d) && CanTrainTo(fromDeg, downEdge);
                if (!up && !down) continue;
                if (up && down)
                    return Mathf.Abs(Mathf.DeltaAngle(relativeDeg, upEdge)) <= Mathf.Abs(Mathf.DeltaAngle(relativeDeg, downEdge))
                        ? upEdge : downEdge;
                return up ? upEdge : downEdge;
            }
            return fromDeg;   // 갈 수 있는 곳이 없으면 제자리
        }

        /// <summary>
        /// from에서 to로 돌 부호 있는 각도. 선회 금지 칸을 지나지 않는 방향만 쓰고, 둘 다 되면 짧은 쪽.
        /// 어느 쪽도 막혔으면 돌지 않는다. 포신이 이미 금지 칸 안에 있으면(배치 직후 등) 빠져나오는 것은 허용한다.
        /// </summary>
        public float TraverseDelta(float fromDeg, float toDeg)
        {
            float cw = Mathf.Repeat(toDeg - fromDeg, 360f);
            float ccw = cw - 360f;
            float shortest = cw <= 180f ? cw : ccw;
            if (_noTrain == null) return shortest;

            bool cwClear = CountNoTrainAlong(fromDeg, cw) == 0;
            bool ccwClear = CountNoTrainAlong(fromDeg, ccw) == 0;
            if (cwClear && ccwClear) return shortest;
            if (cwClear) return cw;
            if (ccwClear) return ccw;
            return 0f;
        }

        private int CountNoTrainAlong(float fromDeg, float delta)
        {
            int n = 0;
            int samples = Mathf.CeilToInt(Mathf.Abs(delta) / StepDeg);
            float dir = Mathf.Sign(delta);
            bool leaving = NoTrainAt(StepOf(fromDeg));   // 시작 칸이 금지 구역이면 빠져나올 때까지는 세지 않는다
            for (int k = 1; k <= samples; k++)
            {
                float a = fromDeg + dir * Mathf.Min(k * StepDeg, Mathf.Abs(delta));
                bool blocked = NoTrainAt(StepOf(a));
                if (leaving) { if (!blocked) leaving = false; continue; }
                if (blocked) n++;
            }
            return n;
        }

        /// <summary>표시용 조각(시작 각도, 폭). cutOut=false는 수면 사격 가능 구간, true는 사격 금지 구역.</summary>
        public void GetRuns(List<(float start, float width)> runs, bool cutOut)
        {
            runs.Clear();
            if (IsFull)
            {
                if (!cutOut) runs.Add((0f, 360f));
                return;
            }

            // 조건이 바뀌는 칸에서 시작해야 원형으로 이어진 구간을 한 번에 센다
            int first = -1;
            for (int i = 0; i < Steps; i++)
                if (IsOpenStep(i) == cutOut) { first = i; break; }
            if (first < 0)
            {
                runs.Add((0f, 360f));   // 전부 같은 상태
                return;
            }

            int runStart = -1, runLen = 0;
            for (int k = 1; k <= Steps; k++)
            {
                int i = (first + k) % Steps;
                if (IsOpenStep(i) != cutOut)
                {
                    if (runLen == 0) runStart = i;
                    runLen++;
                    continue;
                }
                if (runLen > 0) runs.Add((runStart * StepDeg, runLen * StepDeg));
                runLen = 0;
            }
        }
    }

    /// <summary>
    /// 선체 구조물을 맞추지 않도록 사격 금지 구역을 계산한다.
    ///
    /// 1) 붙어 있는 블록(대각선 포함 8칸) 중 무기를 가리는 높이의 블록: 그 칸이 차지하는 방향을 사격 금지.
    ///    낮음 블록은 사선 아래라 가리지 않고, 중간 블록은 중간 무기를 가린다(거치대에 올리면 넘겨 쏜다).
    ///    포신은 그 위로 넘어 돌 수 있다.
    /// 2) 상부 구조물(함교·레이더·헬기데크 — 높음 등급 블록): **거리와 상관없이** 그 방향을 사격 금지하고,
    ///    포신이 그쪽으로 **돌아가지도 않는다**(선회 금지).
    ///
    /// 칸의 네 모서리를 무기 중심에서 본 각도로 방향 폭을 잡고, 양옆에 여유 각도를 더한다.
    /// 구조물 높이와 거리로 넘겨 쏠 수 있는 최소 앙각도 계산한다 — 머리 위로 떨어지는 미사일은 CIWS가 구조물 너머로 쏠 수 있다.
    /// 부서진 블록도 칸을 차지하므로 계속 가린다.
    /// </summary>
    public static class FireArcCalculator
    {
        private const int Steps = FireArc.Steps;
        private const float StepDeg = FireArc.StepDeg;

        /// <summary>구조물 방향 양옆에 더하는 여유 각도.</summary>
        private const float AzimuthMargin = 5f;
        /// <summary>구조물 꼭대기를 넘겨 쏠 때 더하는 여유 앙각.</summary>
        private const float ElevationMargin = 5f;

        // 블록 등급별 대략의 높이(갑판 위, m)와 포구 높이. 거치대 받침 높이는 ModuleFactory.raiseHeight와 같게 둔다.
        private const float LowTop = 0.9f, MidTop = 1.4f, HighTop = 3.0f;
        private const float RaiseHeight = 1.2f;
        private const float TurretMuzzle = 1.0f, CiwsMuzzle = 1.6f;

        // 격자 방향과 그 방향의 Unity 로컬 선회각. +X(선수)=0, +Z(우현)=90.
        private static readonly GridCoord[] Neighbors4 =
        {
            new(1, 0), new(0, 1), new(-1, 0), new(0, -1),
        };

        /// <summary>주변 블록에 따라 사격각이 바뀌는 포탑형 무기인가(선회 금지 포함). VLS는 수직 발사라 제외.</summary>
        public static bool UsesNeighborArc(ModuleType type)
            => type == ModuleType.Autocannon || type == ModuleType.GuidedRocket || type == ModuleType.NavalGun;

        /// <summary>사격 금지 구역이 있는 무기인가(정비 화면 표시용). CIWS는 상부 구조물만 적용.</summary>
        public static bool HasCutout(ModuleType type) => UsesNeighborArc(type) || type == ModuleType.Ciws;

        /// <summary>설치 방향 기준 선회각(도). 회전 단계 × 90.</summary>
        public static float FacingYaw(int rotationSteps) => (((rotationSteps % 4) + 4) % 4) * 90f;

        /// <summary>이웃 블록이 이 높이의 무기를 가리는가.</summary>
        public static bool Blocks(ModuleHeight neighbor, ModuleHeight shooter)
            => neighbor != ModuleHeight.Low && neighbor >= shooter;

        /// <summary>올리려면 가리는 이웃이 이만큼 있어야 한다(기관포·76mm 제외).</summary>
        public const int RaiseRequiredBlockedSides = 3;

        /// <summary>이 무기를 가리는 이웃 수(상하좌우, 무기 원래 높이 기준).</summary>
        public static int CountBlockingNeighbors(ShipGrid grid, ModuleInstance weapon)
        {
            if (grid == null || weapon?.Definition == null) return 0;

            int count = 0;
            foreach (var dir in Neighbors4)
            {
                var n = grid.Get(weapon.Origin + dir);
                if (n?.Definition != null && n != weapon && Blocks(n.EffectiveHeight, weapon.Definition.HeightClass)) count++;
            }
            return count;
        }

        /// <summary>막힌 면 수와 상관없이 언제든 올릴 수 있는 무기(포탑형: 기관포, 76mm).</summary>
        public static bool RaisesAnywhere(ModuleType type)
            => type == ModuleType.Autocannon || type == ModuleType.NavalGun;

        /// <summary>
        /// 승강 거치대에 올릴 수 있는가. 기관포·76mm는 언제든,
        /// 그 밖의 사격각 무기(유도로켓)는 3면 이상이 막혔을 때만.
        /// </summary>
        public static bool CanRaise(ShipGrid grid, ModuleInstance weapon)
            => weapon?.Definition != null && !weapon.IsRaised && !weapon.IsDestroyed
               && UsesNeighborArc(weapon.Definition.Type)
               && (RaisesAnywhere(weapon.Definition.Type)
                   || CountBlockingNeighbors(grid, weapon) >= RaiseRequiredBlockedSides);

        /// <summary>포탑형 무기(기관포·76mm·유도로켓). 이웃 블록 + 상부 구조물, 상부 구조물 방향은 선회 금지.</summary>
        public static FireArc Compute(ShipGrid grid, GridCoord origin, int rotationSteps, ModuleHeight shooterHeight)
        {
            float muzzle = shooterHeight >= ModuleHeight.High ? TurretMuzzle + RaiseHeight : TurretMuzzle;
            return Build(grid, origin, rotationSteps, shooterHeight, muzzle, includeNeighbors: true, trainCutout: true);
        }

        /// <summary>CIWS. 높은 받침이라 이웃 블록은 넘겨 쏘고, 상부 구조물만 사격 금지(선회는 자유).</summary>
        public static FireArc ComputeCiws(ShipGrid grid, GridCoord origin, int rotationSteps)
            => Build(grid, origin, rotationSteps, ModuleHeight.Mid, CiwsMuzzle, includeNeighbors: false, trainCutout: false);

        /// <summary>종류에 맞는 계산을 고른다(정비 화면용).</summary>
        public static FireArc ComputeFor(ModuleType type, ShipGrid grid, GridCoord origin, int rotationSteps, ModuleHeight shooterHeight)
            => type == ModuleType.Ciws ? ComputeCiws(grid, origin, rotationSteps) : Compute(grid, origin, rotationSteps, shooterHeight);

        private static FireArc Build(ShipGrid grid, GridCoord origin, int rotationSteps, ModuleHeight shooterHeight,
                                     float muzzle, bool includeNeighbors, bool trainCutout)
        {
            if (grid == null) return FireArc.Full;

            var clearance = new byte[Steps];
            var noTrain = trainCutout ? new bool[Steps] : null;
            float facing = FacingYaw(rotationSteps);
            float cell = grid.CellSize;
            var self = grid.Get(origin);
            bool any = false;

            // 1) 붙어 있는 블록(8칸). 상부 구조물은 2)에서 따로 처리한다.
            if (includeNeighbors)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    for (int dz = -1; dz <= 1; dz++)
                    {
                        if (dx == 0 && dz == 0) continue;
                        var n = grid.Get(new GridCoord(origin.X + dx, origin.Z + dz));
                        if (n?.Definition == null || n == self) continue;
                        if (n.Definition.HeightClass == ModuleHeight.High) continue;
                        if (!Blocks(n.EffectiveHeight, shooterHeight)) continue;

                        AddCell(clearance, null, facing, dx, dz, TopOf(n), muzzle, cell);
                        any = true;
                    }
                }
            }

            // 2) 상부 구조물: 배 위 어디에 있든
            foreach (var m in grid.Modules)
            {
                if (m?.Definition == null || m == self || m.Definition.HeightClass != ModuleHeight.High) continue;
                foreach (var c in m.OccupiedCoords)
                {
                    AddCell(clearance, noTrain, facing, c.X - origin.X, c.Z - origin.Z, TopOf(m), muzzle, cell);
                    any = true;
                }
            }

            return any ? new FireArc(clearance, noTrain) : FireArc.Full;
        }

        private static float TopOf(ModuleInstance m)
        {
            float top = m.Definition.HeightClass switch
            {
                ModuleHeight.Low => LowTop,
                ModuleHeight.High => HighTop,
                _ => MidTop,
            };
            return m.IsRaised ? top + RaiseHeight : top;
        }

        /// <summary>무기 중심에서 (dx, dz)칸이 차지하는 방향을 금지 구역으로 표시한다.</summary>
        private static void AddCell(byte[] clearance, bool[] noTrain, float facing, int dx, int dz,
                                    float top, float muzzle, float cell)
        {
            if (dx == 0 && dz == 0) return;

            // 방향 폭: 칸의 네 모서리를 본 각도
            float centerYaw = Mathf.Atan2(dz, dx) * Mathf.Rad2Deg;
            float min = 0f, max = 0f;
            for (int sx = -1; sx <= 1; sx += 2)
            {
                for (int sz = -1; sz <= 1; sz += 2)
                {
                    float cornerYaw = Mathf.Atan2(dz + sz * 0.5f, dx + sx * 0.5f) * Mathf.Rad2Deg;
                    float d = Mathf.DeltaAngle(centerYaw, cornerYaw);
                    min = Mathf.Min(min, d);
                    max = Mathf.Max(max, d);
                }
            }
            float from = Mathf.DeltaAngle(facing, centerYaw) + min - AzimuthMargin;
            float span = (max - min) + AzimuthMargin * 2f;

            // 넘겨 쏠 수 있는 최소 앙각: 칸의 가장 가까운 가장자리까지의 수평 거리와 꼭대기 높이
            float nearX = Mathf.Max(Mathf.Abs(dx) - 0.5f, 0f);
            float nearZ = Mathf.Max(Mathf.Abs(dz) - 0.5f, 0f);
            float dist = Mathf.Max(Mathf.Sqrt(nearX * nearX + nearZ * nearZ) * cell, 0.3f);
            float elevation = Mathf.Atan2(top - muzzle, dist) * Mathf.Rad2Deg + ElevationMargin;
            byte need = (byte)Mathf.Clamp(Mathf.CeilToInt(elevation), 1, 90);   // 막힌 방향은 수면 표적을 절대 못 쏜다

            // 칸의 가운데 각도(2.5, 7.5, ...)로 판정해 경계가 구간 끝에 맞게 한다
            for (int i = 0; i < Steps; i++)
            {
                float a = i * StepDeg + StepDeg * 0.5f;
                if (Mathf.Repeat(a - from, 360f) >= span) continue;
                if (need > clearance[i]) clearance[i] = need;
                if (noTrain != null) noTrain[i] = true;
            }
        }
    }
}
