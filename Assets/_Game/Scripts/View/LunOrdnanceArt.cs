using UnityEngine;

namespace Game.View
{
    /// <summary>
    /// 룬급 폭탄 · 로켓 겉모습(2026-11-21 Codex Stage4Ekranoplan_v1/LunOrdnance — 요청서 D-4 · D-5).
    /// 룬급 전용 탄 프리팹(PRJ_LunBomb · PRJ_LunRocket — 보스 포탄 복사본, 동작은 ArtilleryShell 그대로)에 처음 나올 때 한 번
    /// Resources/Effects/Stage4의 모델을 Y 180° 피벗 아래에 붙이고 예전 공 모양 포탄은 숨긴다(풀에서 다시 나와도 그대로).
    /// 로켓은 꼬리(Exhaust)에서 불꽃 · 연기를 뿜는다.
    /// </summary>
    public sealed class LunOrdnanceArt : MonoBehaviour
    {
        private const string BombPath = "Effects/Stage4/PRJ_LunBomb", RocketPath = "Effects/Stage4/PRJ_LunRocket";
        private static GameObject s_bomb, s_rocket;
        private Transform _exhaust;
        private bool _rocket;


        /// <summary>검증용: 모델이 붙었나.</summary>
        public bool Dressed { get; private set; }

        public static void Dress(GameObject shell, bool rocket)
        {
            if (shell == null) return;
            if (shell.TryGetComponent<LunOrdnanceArt>(out var art) && art.Dressed) return;
            if (art == null) art = shell.AddComponent<LunOrdnanceArt>();
            art.Build(rocket);
        }

        private void Build(bool rocket)
        {
            _rocket = rocket;
            var prefab = rocket ? (s_rocket ??= Resources.Load<GameObject>(RocketPath)) : (s_bomb ??= Resources.Load<GameObject>(BombPath));
            if (prefab == null) return;
            var old = transform.Find("Shell");
            if (old != null && old.TryGetComponent<Renderer>(out var r)) r.enabled = false;
            var pivot = new GameObject("ModelPivot").transform;
            pivot.SetParent(transform, false);
            pivot.localRotation = Quaternion.Euler(0f, 180f, 0f);   // 4스테이지 모델과 같은 보정 — 기수가 +Z
            var model = Instantiate(prefab, pivot, false);
            model.name = prefab.name;
            foreach (var c in model.GetComponentsInChildren<Collider>(true)) Destroy(c);
            foreach (var mr in model.GetComponentsInChildren<Renderer>(true)) mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            foreach (var t in model.GetComponentsInChildren<Transform>(true)) if (t.name == "Exhaust") { _exhaust = t; break; }
            Dressed = true;
            if (_rocket && _exhaust != null) CombatLights.Register(_exhaust);
        }

        // 풀에서 다시 나오면 추진 불빛을 다시 받는다(꺼지면 CombatLights가 알아서 뺀다)
        private void OnEnable() { if (Dressed && _rocket && _exhaust != null) CombatLights.Register(_exhaust); }

        private MissileTrail.State _trail;

        private void Update()
        {
            if (!_rocket || _exhaust == null) return;
            MissileTrail.Tick(ref _trail, _exhaust.position, transform.forward, true, Time.deltaTime);   // 미사일과 같은 연기 꼬리
        }

        private void OnDisable() => _trail = default;

        private static float DisplaySettingsDensity() => Game.Core.DisplaySettings.DecorDensity;
    }
}
