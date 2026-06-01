using System.Collections.Generic;
using UnityEngine;

namespace CarDungeon
{
    public enum CardType { Attack, Skill }   // 빨강 / 파랑
    public enum CardShape { Circle, Square }  // 원=단일 / 네모=광역

    /// <summary>
    /// MAGE_PROTOTYPE 섹션 2 검증 덱(8장)의 데이터 모델.
    /// 모든 수치는 임시 — 프로토타입에서 튜닝.
    /// </summary>
    [System.Serializable]
    public class CardData
    {
        public string name;
        public CardType type;
        public int cost;
        public float castTime;   // 0 = 즉발
        public CardShape shape;
        public bool aimed;       // 점O = 조준 필요 (메테오)

        // 효과(임시) — 0이면 미적용
        public int damage;
        public int absorb;       // 베리어/마나실드 흡수량
        public int drawCount;    // 신속한 사고
        public float slowSeconds; // 냉기폭발/메테오 슬로우

        public string desc;      // 손패 호버 표시용

        public CardData(string name, CardType type, int cost, float castTime,
            CardShape shape, bool aimed, string desc)
        {
            this.name = name; this.type = type; this.cost = cost;
            this.castTime = castTime; this.shape = shape; this.aimed = aimed;
            this.desc = desc;
        }

        public Color TypeColor => type == CardType.Attack
            ? new Color(0.86f, 0.27f, 0.27f)   // 빨강(공격)
            : new Color(0.27f, 0.50f, 0.86f);  // 파랑(스킬)

        /// <summary>MAGE_PROTOTYPE 섹션 2 — 검증 덱 8장(각 1장).</summary>
        public static List<CardData> BuildPrototypeDeck()
        {
            var deck = new List<CardData>();

            var c1 = new CardData("마력탄", CardType.Attack, 1, 1f, CardShape.Square, false,
                "18 데미지 (광역·자동)") { damage = 18 };
            var c2 = new CardData("체인라이트닝", CardType.Attack, 2, 1.5f, CardShape.Square, false,
                "28 데미지 (광역·자동)") { damage = 28 };
            var c3 = new CardData("매직미사일", CardType.Attack, 1, 0f, CardShape.Circle, false,
                "12 데미지 (단일·즉발)") { damage = 12 };
            var c4 = new CardData("베리어", CardType.Skill, 1, 0f, CardShape.Circle, false,
                "30 흡수 (자기)") { absorb = 30 };
            var c5 = new CardData("마나실드", CardType.Skill, 2, 0f, CardShape.Circle, false,
                "60 흡수 (자기)") { absorb = 60 };
            var c6 = new CardData("신속한 사고", CardType.Skill, 1, 0f, CardShape.Circle, false,
                "드로우 +2") { drawCount = 2 };
            var c7 = new CardData("냉기폭발", CardType.Skill, 2, 1f, CardShape.Square, false,
                "12 데미지 + 슬로우 2초") { damage = 12, slowSeconds = 2f };
            var c8 = new CardData("메테오", CardType.Attack, 3, 3f, CardShape.Square, true,
                "45 데미지 (광역·조준)") { damage = 45 };

            deck.Add(c1); deck.Add(c2); deck.Add(c3); deck.Add(c4);
            deck.Add(c5); deck.Add(c6); deck.Add(c7); deck.Add(c8);
            return deck;
        }
    }

    /// <summary>큐에 등록된 시전중 카드.</summary>
    public class CastEntry
    {
        public CardData card;
        public float remaining;
        public float total;
        public CastEntry(CardData card)
        {
            this.card = card;
            this.total = card.castTime;
            this.remaining = card.castTime;
        }
    }
}
