using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace Game.EditorTools
{
    /// <summary>
    /// 한글 TMP 폰트(KoreanFont SDF)를 원본 글꼴(Art/Fonts/NotoSansKR-Regular.otf)에 맞춰 다시 잡는다(2026-10-08).
    /// 맑은 고딕(재배포 불가)을 Noto Sans KR(SIL OFL)로 바꿀 때 만들었다. 원본 파일 GUID를 그대로 두었으므로
    /// 폰트 에셋·UI 참조는 그대로이고, 글꼴 메트릭(FaceInfo)과 동적 아틀라스만 새로 잡는다.
    /// 게임 문구 몇 줄로 빠진 글자가 없는지도 확인한다(검사 뒤 아틀라스는 다시 비운다 — 실행 중 동적으로 굽는다).
    /// </summary>
    internal static class NavalKoreanFontRefresh
    {
        private const string AssetPath = NavalEditorUtil.Root + "/Art/Fonts/KoreanFont SDF.asset";
        private const string FontPath = NavalEditorUtil.Root + "/Art/Fonts/NotoSansKR-Regular.otf";
        private const string Sample = "출항 / 작전 시작 · 경어뢰 발사관 · 노봉 40mm 쌍열포 · 곡사포 · 충각 함수 · 기뢰 투하궤 · 함대공 미사일 · 레이더 · 0123456789 %+-×";

        [MenuItem("Naval/Art/Refresh Korean Font")]
        public static void Refresh()
        {
            var asset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetPath);
            var font = AssetDatabase.LoadAssetAtPath<Font>(FontPath);
            if (asset == null || font == null) { Debug.LogError($"[Font] 없음: {AssetPath} 또는 {FontPath}"); return; }

            var so = new SerializedObject(asset);
            so.FindProperty("m_SourceFontFile").objectReferenceValue = font;
            so.ApplyModifiedPropertiesWithoutUndo();

            FontEngine.InitializeFontEngine();
            int pointSize = Mathf.Max(1, (int)asset.faceInfo.pointSize);
            if (FontEngine.LoadFontFace(font, pointSize) != FontEngineError.Success)
            { Debug.LogError("[Font] 글꼴을 읽지 못했습니다."); return; }
            asset.faceInfo = FontEngine.GetFaceInfo();
            asset.ClearFontAssetData(true);

            bool all = asset.TryAddCharacters(Sample, out string missing);
            Debug.Log($"[Font] {asset.faceInfo.familyName} {asset.faceInfo.styleName} · 크기 {asset.faceInfo.pointSize} · 줄 높이 {asset.faceInfo.lineHeight:0.#} · " +
                      $"샘플 글자 모두 있음 {all}{(string.IsNullOrEmpty(missing) ? "" : $" · 빠진 글자 '{missing}'")}");

            asset.ClearFontAssetData(true);   // 저장은 빈 동적 아틀라스로(실행 중 필요한 글자만 굽는다)
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
        }
    }
}
