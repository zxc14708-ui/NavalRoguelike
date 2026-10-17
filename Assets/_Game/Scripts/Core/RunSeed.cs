using System;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// 런 시드(2026-10-11 점검 O07). 새 판(출항·훈련·재출격)마다 하나를 정해 UnityEngine.Random을 그 값으로 맞춘다.
    /// 일시정지·결과 화면·진단 파일에 보여 오류 제보 때 같은 조건을 다시 만들 수 있게 한다.
    /// 물리·프레임 시간까지 같지는 않으므로 완전히 똑같은 판이 되지는 않는다(웨이브 추첨·카드 추첨·적 행동 난수가 같아진다).
    ///   - 실행 인자 "-seed 12345"를 주면 그 값으로 시작한다(재현용).
    ///   - 이어하기는 저장된 시드에 스테이지 번호를 섞어 다시 맞춘다.
    /// </summary>
    public static class RunSeed
    {
        public static int Current { get; private set; }

        /// <summary>새 판 시작. seed가 없으면 실행 인자 → 시계 순.</summary>
        public static int Begin(int? seed = null)
        {
            Current = seed ?? FromArgs() ?? (Environment.TickCount & 0x7fffffff) % 1000000;
            UnityEngine.Random.InitState(Current);
            return Current;
        }

        /// <summary>이어하기: 저장된 시드로, 스테이지마다 다른 흐름이 되게 섞어서.</summary>
        public static void Resume(int seed, int stageIndex)
        {
            Current = seed;
            UnityEngine.Random.InitState(unchecked(seed + stageIndex * 7919));
        }

        private static int? FromArgs()
        {
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == "-seed" && int.TryParse(args[i + 1], out int s)) return s;
            return null;
        }
    }
}
