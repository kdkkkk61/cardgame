using System.Collections.Generic;
using UnityEngine;

namespace CarDungeon
{
    public enum CardType { Attack, Skill }   // 빨강 / 파랑 (파워 타입은 추후)
    public enum CardShape { Circle, Square }  // 원=단일 / 네모=광역

    /// <summary>
    /// 카드 등급(레어도). [의도] 타입(뭘 하는가)과 별개 축 — '얼마나 강하고 귀한가'.
    /// 보상·상점 등장 빈도, 파워 예산, 가격을 등급이 결정. 시각=테두리 색.
    /// </summary>
    public enum Rarity { Common, Rare, Epic, Legendary } // 일반 / 희귀 / 영웅 / 전설

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
        public Rarity rarity = Rarity.Common;
        public int cost;
        public float castTime;   // 0 = 즉발
        public CardShape shape;
        public bool aimed;       // 점O = 조준 필요 (메테오)

        // 기본 효과 (임시 수치)
        public int damage;
        public int absorb;
        public int drawCount;
        public float slowSeconds;

        // 사용 시 PlayerState에 추가할 버프 (파워카드/다음카드강화 등). null이면 없음.
        public Modifier appliesMod;

        // [의도] 소진(exhaust): 사용 시 무덤이 아닌 '소진 더미'로 → 이번 전투엔 다시 안 나옴.
        //   지속 파워카드(전투 내내 버프)는 반드시 true. 안 그러면 재드로우→무한 스택.
        public bool exhaust;

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

        /// <summary>등급별 테두리 색 — 일반 회색 / 희귀 파랑 / 영웅 보라 / 전설 주황.</summary>
        public static Color RarityColor(Rarity r) => r switch
        {
            Rarity.Rare      => new Color(0.30f, 0.60f, 0.95f),
            Rarity.Epic      => new Color(0.66f, 0.40f, 0.92f),
            Rarity.Legendary => new Color(0.96f, 0.60f, 0.20f),
            _                => new Color(0.55f, 0.55f, 0.62f),
        };

        /// <summary>등급 한글 이름.</summary>
        public static string RarityName(Rarity r) => r switch
        {
            Rarity.Rare => "희귀", Rarity.Epic => "영웅", Rarity.Legendary => "전설", _ => "일반",
        };

        /// <summary>마법사 검증 덱 정의(8종) — MAGE_PROTOTYPE §2.</summary>
        public static List<CardDefinition> Catalog()
        {
            return new List<CardDefinition>
            {
                new CardDefinition("마력탄", CardType.Attack, 1, 1f, CardShape.Square, false, "18 데미지 (광역·자동)") { damage = 18, rarity = Rarity.Common },
                new CardDefinition("체인라이트닝", CardType.Attack, 2, 1.5f, CardShape.Square, false, "28 데미지 (광역·자동)") { damage = 28, rarity = Rarity.Rare },
                new CardDefinition("매직미사일", CardType.Attack, 1, 0f, CardShape.Circle, false, "12 데미지 (단일·즉발)") { damage = 12, rarity = Rarity.Common },
                new CardDefinition("베리어", CardType.Skill, 1, 0f, CardShape.Circle, false, "30 흡수 (자기)") { absorb = 30, rarity = Rarity.Common },
                new CardDefinition("마나실드", CardType.Skill, 2, 0f, CardShape.Circle, false, "60 흡수 (자기)") { absorb = 60, rarity = Rarity.Rare },
                new CardDefinition("신속한 사고", CardType.Skill, 1, 0f, CardShape.Circle, false, "드로우 +2") { drawCount = 2, rarity = Rarity.Common },
                new CardDefinition("냉기폭발", CardType.Skill, 2, 1f, CardShape.Square, false, "12 데미지 + 슬로우 2초") { damage = 12, slowSeconds = 2f, rarity = Rarity.Rare },
                new CardDefinition("메테오", CardType.Attack, 3, 3f, CardShape.Square, true, "45 데미지 (광역·조준)") { damage = 45, rarity = Rarity.Epic },

                // ── 데모: 버프 카드 (모디파이어 시스템 검증) ──
                // 파워카드 = 이 전투 동안 지속 + 소진(한 번만, 무한 스택 방지)
                new CardDefinition("집중", CardType.Skill, 1, 0f, CardShape.Circle, false, "이 전투 동안 데미지 +50% (소진)")
                    { appliesMod = new Modifier(ModStat.DamageMult, 0.5f, ModScope.Combat, -1, "집중"), exhaust = true, rarity = Rarity.Legendary },
                // 다음 카드 강화 = 다음 공격 1회만
                new CardDefinition("예리함", CardType.Skill, 1, 0f, CardShape.Circle, false, "다음 공격 +12 데미지")
                    { appliesMod = new Modifier(ModStat.DamageFlat, 12f, ModScope.NextAttack, 1, "예리함"), rarity = Rarity.Rare },
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

    // ── 모디파이어 시스템 (버프의 단위) ──
    public enum ModStat { DamageFlat, DamageMult, AbsorbFlat, AbsorbMult, DrawBonus, CostBonus, MaxHpBonus }

    /// <summary>버프의 '지속 범위'. 파워카드=Combat, 다음카드강화=NextCard, 유품=Run 등.</summary>
    public enum ModScope { Run, Combat, Cycle, NextCard, NextAttack }

    /// <summary>
    /// Modifier — 버프 한 개. "무엇을 / 얼마나 / 얼마나 오래 / 몇 번".
    /// [의도] 모든 버프(강화·파워카드·다음카드·유품·NPC)를 이 한 단위로 표현 →
    ///   전투는 종류를 몰라도 됨. 범위(scope)가 소멸 시점을, charges가 1회용 여부를 정함.
    /// </summary>
    public class Modifier
    {
        public ModStat stat;
        public float value;
        public ModScope scope;
        public int charges;   // -1 = 무제한(범위로만 소멸), N = N번 적용 후 소멸
        public string label;  // 표시용

        public Modifier(ModStat stat, float value, ModScope scope, int charges = -1, string label = "")
        { this.stat = stat; this.value = value; this.scope = scope; this.charges = charges; this.label = label; }

        public Modifier Clone() => new Modifier(stat, value, scope, charges, label);
    }

    /// <summary>
    /// PlayerState — 런/전투 동안 걸린 모든 모디파이어 목록. 출처(강화/파워카드/유품/NPC) 무관하게 여기로 모임.
    /// [의도] 전투는 Sum()으로 합산값만 읽음. 범위별 소멸/1회용 소비를 여기서 관리.
    /// </summary>
    public class PlayerState
    {
        public readonly List<Modifier> mods = new();

        public void Add(Modifier m) => mods.Add(m);

        /// <summary>해당 스탯의 모든 모디파이어 합.</summary>
        public float Sum(ModStat s)
        {
            float t = 0; foreach (var m in mods) if (m.stat == s) t += m.value; return t;
        }
        public int SumI(ModStat s) => Mathf.RoundToInt(Sum(s));

        /// <summary>범위 만료 시 일괄 제거(전투/사이클 시작 등).</summary>
        public void ClearScope(ModScope sc) => mods.RemoveAll(m => m.scope == sc);

        /// <summary>카드 1장 사용 후 1회용(다음카드/다음공격) 모디파이어 소비.</summary>
        public void ConsumeOnPlay(bool isAttack)
        {
            for (int i = mods.Count - 1; i >= 0; i--)
            {
                var m = mods[i];
                if (m.charges < 0) continue;
                bool applies = m.scope == ModScope.NextCard
                    || (m.scope == ModScope.NextAttack && isAttack);
                if (!applies) continue;
                m.charges--;
                if (m.charges <= 0) mods.RemoveAt(i);
            }
        }
    }

    /// <summary>
    /// 적용 파이프라인 — [기본값 → 카드강화(Instance) → 플레이어버프(PlayerState) → 최종].
    /// [의도] 전투 코드는 def 값을 직접 안 읽고 '반드시' 이 헬퍼만 호출 → 모든 효과가 일관 계산.
    /// </summary>
    public static class CardMath
    {
        // [의도] 카드데미지(base + 강화, 전부 가산) × (1 + Σ버프%) → 소수점 내림.
        //   · 카드 강화는 전부 더하기(고정 +N, 또는 %강화 = +내림(base×pct)를 bonusDamage에 누적).
        //   · 버프층(축복/파워/영구)만 ×(1+합). 버프끼리 '합쳐서 1번' 적용 = 곱연산 폭주 아님.
        //   · 무한 강화해도 카드 단위가 선형이라 통제됨.
        public static int Damage(CardInstance c, PlayerState ps)
        {
            if (c.def.damage <= 0) return 0;
            float cardDmg = c.def.damage + c.bonusDamage + ps.Sum(ModStat.DamageFlat);
            float mult = 1f + ps.Sum(ModStat.DamageMult);
            return Mathf.Max(0, Mathf.FloorToInt(cardDmg * mult)); // 내림
        }

        public static int Absorb(CardInstance c, PlayerState ps)
        {
            if (c.def.absorb <= 0) return 0;
            float cardAbs = c.def.absorb + c.bonusAbsorb + ps.Sum(ModStat.AbsorbFlat);
            float mult = 1f + ps.Sum(ModStat.AbsorbMult);
            return Mathf.Max(0, Mathf.FloorToInt(cardAbs * mult)); // 내림
        }

        public static int DrawCount(CardInstance c) => c.def.drawCount;
        public static float Slow(CardInstance c) => c.def.slowSeconds;
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
