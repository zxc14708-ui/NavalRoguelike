using UnityEngine;

namespace Game.Combat
{
    /// <summary>탄도 계산 모음. 무기 런타임이 직접 수식을 들고 있지 않게 한다.</summary>
    public static class Ballistics
    {
        /// <summary>
        /// 등속으로 움직이는 표적과 직선 탄이 만나는 지점을 구한다.
        /// |D + V·t| = s·t 를 t에 대해 풀어 가장 이른 양의 해를 쓴다(D = 표적 - 사수).
        /// 탄이 표적보다 느려 만날 수 없으면 현재 위치를 돌려준다.
        /// </summary>
        public static Vector3 PredictIntercept(Vector3 shooter, Vector3 target, Vector3 targetVelocity,
                                               float projectileSpeed)
        {
            if (projectileSpeed <= 0.01f) return target;

            Vector3 d = target - shooter;
            float a = Vector3.Dot(targetVelocity, targetVelocity) - projectileSpeed * projectileSpeed;
            float b = 2f * Vector3.Dot(d, targetVelocity);
            float c = Vector3.Dot(d, d);

            float t;
            if (Mathf.Abs(a) < 0.0001f)
            {
                // 속도가 같으면 1차식
                if (Mathf.Abs(b) < 0.0001f) return target;
                t = -c / b;
            }
            else
            {
                float disc = b * b - 4f * a * c;
                if (disc < 0f) return target;

                float sqrt = Mathf.Sqrt(disc);
                float t1 = (-b - sqrt) / (2f * a);
                float t2 = (-b + sqrt) / (2f * a);
                t = Mathf.Min(t1, t2);
                if (t < 0f) t = Mathf.Max(t1, t2);
            }

            return t > 0f ? target + targetVelocity * t : target;
        }

        /// <summary>
        /// 수평 속력이 일정한 포물선 탄(곡사포 — ArtilleryShell: 수평은 등속, 높이는 최고점 apex의 포물선)이
        /// 수면 위를 등속으로 움직이는 표적과 만나는 착탄점과 비행 시간(2026-11-01).
        /// lib_fts(forrestthewoods, 퍼블릭 도메인) `solve_ballistic_arc_lateral`의 이동 표적 풀이 방식을 참고해 새로 썼다 —
        /// 수평면에서 |D + V·t| = s·t의 가장 이른 양의 해(코드 미복사). 높이는 apex가 정하므로 수평만 풀면 된다.
        /// minFlight보다 빨리 만나면 minFlight 뒤의 표적 자리로 쏜다(탄이 그만큼은 난다). 만날 수 없으면 false(지금 자리, 수평 거리 / 속력).
        /// </summary>
        public static bool SolveLateralIntercept(Vector3 shooter, Vector3 target, Vector3 targetVelocity, float lateralSpeed,
                                                 float minFlight, out Vector3 impact, out float flight)
        {
            Vector3 d = target - shooter; d.y = 0f;
            Vector3 v = targetVelocity; v.y = 0f;
            float s = Mathf.Max(0.01f, lateralSpeed);
            float a = Vector3.Dot(v, v) - s * s;
            float b = 2f * Vector3.Dot(d, v);
            float c = Vector3.Dot(d, d);
            float t = -1f;
            if (Mathf.Abs(a) < 1e-4f) { if (Mathf.Abs(b) > 1e-4f) t = -c / b; }
            else
            {
                float disc = b * b - 4f * a * c;
                if (disc >= 0f)
                {
                    float sq = Mathf.Sqrt(disc);
                    float t1 = (-b - sq) / (2f * a), t2 = (-b + sq) / (2f * a);
                    t = Mathf.Min(t1, t2);
                    if (t <= 0f) t = Mathf.Max(t1, t2);
                }
            }
            bool ok = t > 0f;
            flight = ok ? Mathf.Max(minFlight, t) : Mathf.Max(minFlight, d.magnitude / s);
            impact = ok ? target + v * flight : target;
            impact.y = 0f;
            return ok;
        }

        /// <summary>
        /// seconds 뒤 표적이 있을 곳(수면, y = 0) — 지금 속력과 선회율(도/초)을 그대로 유지한다고 보고 원호를 따라간다(12걸음).
        /// 선회 중인 표적은 직선 예측(위치 + 속도 × 시간)으로는 원 바깥으로 빗나간다(Ww2Aim과 같은 생각, 적 표적용).
        /// </summary>
        public static Vector3 PredictTurning(Vector3 position, Vector3 velocity, float yawRateDeg, float seconds)
        {
            Vector3 p = position; p.y = 0f;
            Vector3 v = velocity; v.y = 0f;
            if (seconds <= 0f) return p;
            const int steps = 12;
            float dt = seconds / steps;
            var turn = Quaternion.Euler(0f, yawRateDeg * dt, 0f);
            for (int i = 0; i < steps; i++) { v = turn * v; p += v * dt; }
            return p;
        }
    }
}
