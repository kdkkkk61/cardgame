using System.Collections.Generic;
using UnityEngine;

namespace CarDungeon
{
    public enum CardType { Attack, Skill }   // 빨강 / 파랑
    public enum CardShape { Circle, Square }  // 원=단일 / 네모=광역

    // ============================================================================
    //  CARD_SYSTEM.md 3계층 데이터 모델 + 적용 파이프라인
    //  [의도] 강화·버프·NPC기술을 전부 '같은 통로'로 적용 → 콘텐츠 추가 시 전투 코드 불변.
    // ============================================================================

    /// <summary>
    /// CardDefinition — 카드의 '원본 템플릿'(불변). 게임 전체에서 종류당 1개 공유.
    /// 강화해도 이건 안 바뀜 (수정은 CardInstance에).
    /// </summary>
    public class CardDefinition
    {
        public string name;
        public CardType type;
        public int cost;
        public float castTime;   // 0 = 즉발
        public CardShape shape;
        public bool aimed;       // 점O = 조준 필요 (메테오)

        // 기본 효과 (임시 수치)
        public int damage;
        public int absorb;
        public int drawCount;
        public float slowSeconds;

        public string desc;

        public CardDefinition(string name, CardType type, int cost, float castTime,
            CardShape shape, bool aimed, string desc)
        {
            this.name = name; this.type = type; this.cost = cost;
            this.castTime = castTime; this.shape = shape; this.aimed = aimed;
            this.desc = desc;
        }

        public Color TypeColor => type == CardType.Attack
            ? new Color(0.86f, 0.27f, 0.27f) : new Color(0.27f, 0.50f, 0.86f);

        /// <summary>마법사 검증 덱 정의(8종) — MAGE_PROTOTYPE §2.</summary>
        public static List<CardDefinition> Catalog()
        {
            return new List<CardDefinition>
            {
                new CardDefinition("마력탄", CardType.Attack, 1, 1f, CardShape.Square, false, "18 데미지 (광역·자동)") { damage = 18 },
                new CardDefinition("체인라이트닝", CardType.Attack, 2, 1.5f, CardShape.Square, false, "28 데미지 (광역·자동)") { damage = 28 },
                new CardDefinition("매직미사일", CardType.Attack, 1, 0f, CardShape.Circle, false, "12 데미지 (단일·즉발)") { damage = 12 },
                new CardDefinition("베리어", CardType.Skill, 1, 0f, CardShape.Circle, false, "30 흡수 (자기)") { absorb = 30 },
                new CardDefinition("마나실드", CardType.Skill, 2, 0f, CardShape.Circle, false, "60 흡수 (자기)") { absorb = 60 },
                new CardDefinition("신속한 사고", CardType.Skill, 1, 0f, CardShape.Circle, false, "드로우 +2") { drawCount = 2 },
                new CardDefinition("냉기폭발", CardType.Skill, 2, 1f, CardShape.Square, false, "12 데미지 + 슬로우 2초") { damage = 12, slowSeconds = 2f },
                new CardDefinition("메테오", CardType.Attack, 3, 3f, CardShape.Square, true, "45 데미지 (광역·조준)") { damage = 45 },
            };
        }
    }

    /// <summary>
    /// CardInstance — 내 덱의 '그 카드 한 장'(가변). def를 참조 + 이 한 장만의 수정 상태.
    /// ★ 강화 '유지'의 정체 = 강화는 여기에 기록 → 런 동안 그 카드에 영구히 따라다님.
    /// 덱/손패/무덤/큐는 전부 이 인스턴스의 리스트.
    /// </summary>
    public class CardInstance
    {
        public CardDefinition def;
        public int upgradeLevel = 0;
        public int bonusDamage = 0;   // 강화로 붙은 추가 데미지
        public int bonusAbsorb = 0;   // 강화로 붙은 추가 흡수
        public int drawSeq = 0;       // 드로우순 정렬용

        public CardInstance(CardDefinition def) { this.def = def; }

        // 전투/UI 편의 포워딩 (변하지 않는 속성은 def 그대로)
        public string name => def.name;
        public CardType type => def.type;
        public int cost => def.cost;
        public float castTime => def.castTime;
        public CardShape shape => def.shape;
        public bool aimed => def.aimed;
        public string desc => def.desc;
    }

    /// <summary>
    /// PlayerState — 런 전체 버프/스탯(가변). 출처(강화/NPC/유품)는 달라도 결과는 여기로 모임.
    /// [의도] 캐릭터 단위 버프는 전부 여기 한 곳 → 전투는 이 값만 읽으면 됨.
    /// </summary>
    public class PlayerState
    {
        public float damageMult = 1f;   // 모든 공격 데미지 배율
        public float barrierMult = 1f;  // 흡수(베리어) 배율
        public int drawBonus = 0;       // 사이클당 드로우 가산
        public int costBonus = 0;       // 코스트 상한 가산
        public int maxHpBonus = 0;      // 최대 HP 가산
        // 향후: grantedAbilities(부여 기술), 유품/각인 효과 리스트 등
    }

    /// <summary>
    /// 적용 파이프라인 — [기본값 → 카드강화(Instance) → 플레이어버프(PlayerState) → 최종].
    /// [의도] 전투 코드는 def 값을 직접 안 읽고 '반드시' 이 헬퍼만 호출 → 모든 효과가 일관 계산.
    /// </summary>
    public static class CardMath
    {
        public static int Damage(CardInstance c, PlayerState ps)
            => Mathf.Max(0, Mathf.RoundToInt((c.def.damage + c.bonusDamage) * ps.damageMult));

        public static int Absorb(CardInstance c, PlayerState ps)
            => Mathf.Max(0, Mathf.RoundToInt((c.def.absorb + c.bonusAbsorb) * ps.barrierMult));

        public static int DrawCount(CardInstance c)
            => c.def.drawCount;

        public static float Slow(CardInstance c)
            => c.def.slowSeconds;
    }

    /// <summary>큐에 등록된 시전중 카드.</summary>
    public class CastEntry
    {
        public CardInstance card;
        public float remaining;
        public float total;
        public Vector2 targetPos;  // 조준 카드(메테오) 낙하 지점
        public bool aimed;
        public CastEntry(CardInstance card)
        {
            this.card = card;
            this.total = card.castTime;
            this.remaining = card.castTime;
            this.aimed = card.aimed;
        }
    }
}
