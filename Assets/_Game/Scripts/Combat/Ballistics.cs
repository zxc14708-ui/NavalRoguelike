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
    }
}
