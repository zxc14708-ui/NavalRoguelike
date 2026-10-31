using System.Linq;
using UnityEditor;

namespace Game.EditorTools
{
    /// <summary>
    /// 한글 동적 폰트(KoreanFont SDF)는 실행 중에 필요한 글자만 아틀라스에 굽는다. 에디터가 플레이·편집하며 구운 글자까지
    /// 디스크에 저장하면 에셋이 매번 바뀌어(수 MB) 커밋마다 섞였다(2026-10-31). 그래서 이 에셋은 저장 목록에서 뺀다 —
    /// 디스크에는 늘 빈 동적 아틀라스(NavalKoreanFontRefresh가 만든 상태)만 남고, 구운 글자는 그 에디터 세션 메모리에만 있다.
    /// 폰트 설정을 일부러 바꿔 저장할 때는 <see cref="AllowSave"/>를 켠 채 저장한다(NavalKoreanFontRefresh가 그렇게 한다).
    /// </summary>
    internal class NavalDynamicFontSaveGuard : AssetModificationProcessor
    {
        internal const string FontAssetPath = NavalEditorUtil.Root + "/Art/Fonts/KoreanFont SDF.asset";

        /// <summary>켜 두면 이번 저장에서 폰트 에셋도 디스크에 쓴다.</summary>
        internal static bool AllowSave;

        private static string[] OnWillSaveAssets(string[] paths)
        {
            if (AllowSave) return paths;
            return paths.Where(p => p != FontAssetPath).ToArray();
        }
    }
}
