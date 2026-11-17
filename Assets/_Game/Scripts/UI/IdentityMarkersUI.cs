using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Game.Combat;
using Game.Core;
using Game.Data;
using Game.Enemies;

namespace Game.UI
{
    /// <summary>
    /// 적아 식별 표식(2026-10-11 점검 G01·U06). 기본 줌에서 어두운 함체끼리 섞여도 적·호위함을 바로 가르게,
    /// 화면 안의 함선 머리 위에 작은 표식을 단다. 색만이 아니라 모양으로도 가른다(회색조·색약에서도 구분):
    ///   적 수상함 ▼(붉은색) · 적 항공기 ▲(주황) · 드러난 적 잠수함 ◯(붉은 주황) · 아군 호위함 ◆(청록). 엘리트·보스는 크게.
    /// 기함은 화면 가운데라 달지 않는다. 미사일·어뢰는 위협 표시(MissileWarningUI)가 맡는다. 설정 › 화면에서 끌 수 있다.
    /// </summary>
    public sealed class IdentityMarkersUI : MonoBehaviour
    {
        private static readonly Color ShipColor = new(1f, 0.32f, 0.24f, 0.95f);
        private static readonly Color AirColor = new(1f, 0.62f, 0.2f, 0.95f);
        private static readonly Color SubColor = new(1f, 0.45f, 0.32f, 0.95f);
        private static readonly Color FriendColor = new(0.4f, 1f, 0.85f, 0.95f);
        private const int MaxMarkers = 48;

        private RectTransform _root;
        private Camera _camera;
        private readonly List<RawImage> _pool = new();

        /// <summary>검증용: 이번 프레임에 단 적·아군 표식 수.</summary>
        public int HostileShown { get; private set; }
        public int FriendlyShown { get; private set; }

        public static IdentityMarkersUI Create(Transform parent)
        {
            var go = new GameObject("Identity markers", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            rect.SetAsFirstSibling();   // 다른 HUD 패널 밑에 그린다
            var ui = go.AddComponent<IdentityMarkersUI>();
            ui._root = rect;
            return ui;
        }

        private void LateUpdate()
        {
            int used = 0;
            HostileShown = FriendlyShown = 0;
            var gm = GameManager.Instance;
            if (_camera == null) _camera = Camera.main;
            if (DisplaySettings.IdentityMarkers && gm != null && gm.State == GameState.Playing && _camera != null && gm.Player != null)
            {
                float scale = _root.lossyScale.x > 0f ? 1f / _root.lossyScale.x : 1f;   // 화면 픽셀 → 캔버스 단위
                foreach (var kind in new[] { TargetKind.Surface, TargetKind.Aircraft, TargetKind.Submarine })
                {
                    var list = TargetRegistry.HostileTo(CombatFaction.Player, kind);
                    for (int i = 0; i < list.Count && used < MaxMarkers; i++)
                    {
                        var t = list[i];
                        if (t == null || !t.IsAlive || t.Transform == null || !t.IsRevealed) continue;
                        if (t is SeaMine) continue;
                        bool big = t is EnemyController e && e.Definition != null && e.Definition.Rank != EnemyRank.Normal;
                        var (tex, color, rot, height) = kind switch
                        {
                            TargetKind.Aircraft => (HudTextures.Triangle, AirColor, 0f, 1.4f),
                            TargetKind.Submarine => (HudTextures.Ring, SubColor, 0f, 1.2f),
                            _ => (HudTextures.Triangle, ShipColor, 180f, 2.4f),
                        };
                        if (Place(used, t.Transform.position + Vector3.up * height, tex, color, rot, (big ? 20f : 13f), scale)) { used++; HostileShown++; }
                    }
                }
                var friends = TargetRegistry.Of(CombatFaction.Player, TargetKind.Surface);
                for (int i = 0; i < friends.Count && used < MaxMarkers; i++)
                {
                    var t = friends[i];
                    if (t == null || !t.IsAlive || t.Transform == null) continue;
                    if (ReferenceEquals(t.Transform, gm.Player.transform) || t is Game.Ship.ShipController) continue;
                    if (Place(used, t.Transform.position + Vector3.up * 2.2f, HudTextures.Diamond, FriendColor, 0f, 11f, scale)) { used++; FriendlyShown++; }
                }
            }
            for (int i = used; i < _pool.Count; i++) if (_pool[i].enabled) _pool[i].enabled = false;
        }

        private bool Place(int index, Vector3 world, Texture tex, Color color, float rotation, float size, float scale)
        {
            Vector3 sp = _camera.WorldToScreenPoint(world);
            if (sp.z < 0f || sp.x < 0f || sp.x > Screen.width || sp.y < 0f || sp.y > Screen.height) return false;
            while (_pool.Count <= index)
            {
                var go = new GameObject($"Marker {_pool.Count}", typeof(RectTransform), typeof(RawImage));
                go.transform.SetParent(_root, false);
                var img = go.GetComponent<RawImage>();
                img.raycastTarget = false;
                var r = img.rectTransform;
                r.anchorMin = r.anchorMax = Vector2.zero;
                r.pivot = new Vector2(0.5f, 0.5f);
                _pool.Add(img);
            }
            var marker = _pool[index];
            marker.enabled = true;
            marker.texture = tex;
            marker.color = color;
            var rt = marker.rectTransform;
            rt.anchoredPosition = new Vector2(sp.x, sp.y) * scale;
            rt.sizeDelta = new Vector2(size, size);
            rt.localRotation = Quaternion.Euler(0f, 0f, rotation);
            return true;
        }
    }
}
