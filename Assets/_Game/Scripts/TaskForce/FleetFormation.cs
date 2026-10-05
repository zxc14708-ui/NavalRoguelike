using UnityEngine;

namespace Game.TaskForce
{
    /// <summary>
    /// 편대 진형. 순서를 바꾸지 말고 뒤에만 덧붙인다(설정·검증이 값으로 읽는다).
    /// 2026-10-03 개편: 고를 수 있는 진형은 함대원형진 · 단종진 · 자율 셋(<see cref="FleetFormations.All"/>).
    /// 앞의 넷(복렬진·단열진·사열진·능형진)은 값 호환을 위해 남겨 둔 예전 진형이다(고르는 목록에는 없다).
    /// </summary>
    public enum FleetFormation
    {
        DoubleColumn, // (예전) 복렬진: 기함 양옆 뒤로 두 줄
        Column,       // 단종진: 기함 항적을 따라 한 줄(line ahead) — 슬롯 번호 순서로
        LineAbreast,  // (예전) 단열진
        Echelon,      // (예전) 사열진
        Diamond,      // (예전) 능형진
        Circular,     // 함대원형진: 기함을 가운데 두고 둥글게 — 1번 앞 · 2번 우현 · 3번 뒤 · 4번 좌현
        Autonomous,   // 자율: 호위함이 화면 안에서 역할에 맞게 스스로 움직인다
    }

    /// <summary>
    /// 진형 이름·설명·슬롯 배치. 슬롯은 기함 기준(x = 우현, y = 선수 방향, m)이며 기함 크기(선수·선미 끝, 반폭)에 맞춰 띄운다 —
    /// 블록을 붙여 기함이 길어지거나 넓어져도 겹치지 않는다.
    /// 2026-10-03부터 슬롯 번호 = 편대 슬롯 번호(1~4, 0부터 센다)이고 자리는 슬롯마다 고정이다(몇 척이 있든 같은 자리).
    /// 단종진은 슬롯 대신 기함 항적을 따라가고(<see cref="TrailDistance"/>), 자율은 편대가 매 순간 정한다(여기 값은 대체 위치).
    /// </summary>
    public static class FleetFormations
    {
        /// <summary>고를 수 있는 진형(G 키 순환 · 진형 선택판 순서).</summary>
        public static readonly FleetFormation[] All =
            { FleetFormation.Circular, FleetFormation.Column, FleetFormation.Autonomous };

        public static string Name(FleetFormation f) => f switch
        {
            FleetFormation.Circular => "함대원형진",
            FleetFormation.Column => "단종진",
            FleetFormation.Autonomous => "자율",
            FleetFormation.DoubleColumn => "복렬진",
            FleetFormation.LineAbreast => "단열진",
            FleetFormation.Echelon => "사열진",
            FleetFormation.Diamond => "능형진",
            _ => f.ToString(),
        };

        /// <summary>한 줄 설명: 모양과 쓰임새.</summary>
        public static string Summary(FleetFormation f) => f switch
        {
            FleetFormation.Circular => "기함을 가운데 두고 둥글게 — 1번 앞 · 2번 우현 · 3번 뒤 · 4번 좌현. 사방 경계, 앞 함이 먼저 적을 맞는다",
            FleetFormation.Column => "기함 항적을 따라 한 줄 — 1번이 바로 뒤, 번호 순서로. 선회가 자연스럽고 섬 사이에 강하다. 정면 화력은 기함뿐",
            FleetFormation.Autonomous => "호위함이 화면 안에서 스스로 움직인다 — 역할에 맞는 위협 쪽으로 나가 막고, 위협이 없으면 기함 주위를 돈다",
            FleetFormation.DoubleColumn => "기함 양옆 뒤로 두 줄",
            FleetFormation.LineAbreast => "기함과 나란히 가로 한 줄",
            FleetFormation.Echelon => "기함 우현 뒤로 비스듬히 한 줄",
            FleetFormation.Diamond => "기함을 가운데 두고 앞·좌·우·뒤",
            _ => "",
        };

        /// <summary>그 진형에서 slot(0~3)번 슬롯의 자리 이름(정비 화면·진형 선택판).</summary>
        public static string SlotLabel(FleetFormation f, int slot) => f switch
        {
            FleetFormation.Circular => slot switch { 0 => "앞", 1 => "우현", 2 => "뒤", _ => "좌현" },
            FleetFormation.Column => $"뒤 {slot + 1}번째",
            FleetFormation.Autonomous => "스스로 판단",
            _ => $"{slot + 1}번 자리",
        };

        /// <summary>
        /// slot(0부터)번 슬롯. fore/aft = 기함 선수·선미 끝(로컬 z, aft는 음수), halfBeam = 기함 반폭.
        /// count는 예전 진형(복렬진)만 쓴다 — 새 진형은 슬롯마다 자리가 고정이다.
        /// 단종진은 항적 거리로 따로 계산하므로 여기서는 그 거리만큼 정후방(직선)을 돌려준다(항적이 없을 때의 대체 위치).
        /// </summary>
        public static Vector2 Slot(FleetFormation f, int index, int count, float fore, float aft, float halfBeam)
        {
            float side = halfBeam + 12f;   // 기함 현측에서 호위함 중심까지(호위함 반폭·여유 포함)
            switch (f)
            {
                case FleetFormation.Column:
                    return new Vector2(0f, -TrailDistance(index, aft));

                case FleetFormation.Circular:
                case FleetFormation.Autonomous:
                {
                    // 기함 한가운데를 중심으로 한 원: 반지름 = 기함 가장 먼 끝 + 13m. 1번 앞 → 시계 방향.
                    float mid = (fore + aft) * 0.5f;
                    float r = Mathf.Max(fore - mid, mid - aft, halfBeam) + 13f;
                    return index switch
                    {
                        0 => new Vector2(0f, mid + r),
                        1 => new Vector2(r, mid),
                        2 => new Vector2(0f, mid - r),
                        _ => new Vector2(-r, mid),
                    };
                }

                case FleetFormation.LineAbreast:
                {
                    float x = side + (index / 2) * 14f;
                    return new Vector2(index % 2 == 0 ? x : -x, 0f);
                }

                case FleetFormation.Echelon:
                    return new Vector2(halfBeam + 9f + index * 9f, aft - 4f - index * 9f);

                case FleetFormation.Diamond:
                {
                    float mid = (fore + aft) * 0.5f;
                    return index switch
                    {
                        0 => new Vector2(side + 3f, mid),
                        1 => new Vector2(-(side + 3f), mid),
                        2 => new Vector2(0f, fore + 14f),
                        _ => new Vector2(0f, aft - 14f),
                    };
                }

                default: // (예전) 복렬진
                    return count switch
                    {
                        1 => new Vector2(side, aft - 8f),
                        2 => new Vector2(index == 0 ? -side : side, aft - 8f),
                        3 => index switch
                        {
                            0 => new Vector2(-side, aft - 8f),
                            1 => new Vector2(side, aft - 8f),
                            _ => new Vector2(0f, aft - 20f),
                        },
                        _ => index switch
                        {
                            0 => new Vector2(-(side + 1f), aft - 7f),
                            1 => new Vector2(side + 1f, aft - 7f),
                            2 => new Vector2(-(side - 3f), aft - 21f),
                            _ => new Vector2(side - 3f, aft - 21f),
                        },
                    };
            }
        }

        /// <summary>단종진: slot번 함이 기함 중심에서 항적을 따라 떨어지는 거리(m). 슬롯마다 고정(빈 슬롯 자리는 비워 둔다).</summary>
        public static float TrailDistance(int index, float aft) => -aft + 9f + index * 12f;
    }
}
