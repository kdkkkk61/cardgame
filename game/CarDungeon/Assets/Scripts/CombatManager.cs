using System.Collections.Generic;
using UnityEngine;

namespace CarDungeon
{
    public enum CombatState { Fighting, Won, Lost }

    /// <summary>
    /// 코어 전투 루프 (CORE_COMBAT 섹션 2).
    /// 사이클 카운트다운 → 카드 사용(코스트/큐) → 시간 0 = 보스 행동 → 새 사이클.
    /// 강제 종료 버튼 없음 — 시간은 무조건 끝까지 흐름.
    /// </summary>
    public class CombatManager : MonoBehaviour
    {
        // --- 규칙 파라미터 (MAGE_PROTOTYPE 섹션 1, 임시) ---
        public const int MaxCost = 5;
        public const int MaxHand = 10;
        public const int DrawPerCycle = 5;
        public const int MaxPlayerHP = 120;
        public const float BaseCycleTime = 10f; // 패턴 A 기준

        // --- 상태 ---
        public CombatState state = CombatState.Fighting;
        public int playerHP;
        public int barrier;          // 사이클 종료 시 소멸(CORE_COMBAT 8 / CARDS 5)
        public int cost;
        public float cycleTimer;
        public float cycleLength;
        public int cycleCount = 0;

        public List<CardData> deck = new();
        public List<CardData> hand = new();
        public List<CardData> discard = new();
        public List<CastEntry> queue = new();

        // 보스 패턴 A(자동명중) — Slice 1 유일 패턴
        public int pendingBossDamage = 25;

        public PlayerController player;
        public Boss boss;
        public string lastLog = "";

        // FX 이벤트 (HUD가 구독)
        public event System.Action<int> onDamageDealt; // 보스 피격 데미지
        public event System.Action onCardCast;         // 카드 사용 순간
        int _drawCounter = 0;

        public void Init(PlayerController player, Boss boss)
        {
            this.player = player;
            this.boss = boss;
            playerHP = MaxPlayerHP;
            deck = CardData.BuildPrototypeDeck();
            Shuffle(deck);
            StartCycle();
        }

        void Update()
        {
            if (state != CombatState.Fighting) return;

            // 큐(시전중) 진행
            for (int i = queue.Count - 1; i >= 0; i--)
            {
                queue[i].remaining -= Time.deltaTime;
                if (queue[i].remaining <= 0f)
                {
                    ResolveCard(queue[i].card);
                    queue.RemoveAt(i);
                }
            }

            // 사이클 시간
            cycleTimer -= Time.deltaTime;
            if (cycleTimer <= 0f)
                EndCycle();

            CheckWinLose();
        }

        // --- 사이클 ---
        void StartCycle()
        {
            cycleCount++;
            // 손패 디스카드 → 5장 드로우 → 코스트 풀회복
            discard.AddRange(hand);
            hand.Clear();
            cost = MaxCost;
            DrawCards(DrawPerCycle);
            cycleLength = BaseCycleTime;
            cycleTimer = cycleLength;
        }

        void EndCycle()
        {
            // 패턴 A: 자동명중 — 베리어로 흡수 후 잔여만 HP 피해
            int dmg = pendingBossDamage;
            int absorbed = Mathf.Min(barrier, dmg);
            barrier -= absorbed;
            int taken = dmg - absorbed;
            playerHP = Mathf.Max(0, playerHP - taken);
            lastLog = $"보스 마력폭발! {dmg} (흡수 {absorbed} / 피해 {taken})";

            barrier = 0; // 사이클 종료 시 베리어 소멸
            // 큐에 남은 장기 시전은 다음 사이클로 침범(유지)
            StartCycle();
        }

        // --- 카드 ---
        public void DrawCards(int n)
        {
            for (int i = 0; i < n; i++)
            {
                if (deck.Count == 0)
                {
                    if (discard.Count == 0) return; // 더 뽑을 카드 없음
                    deck.AddRange(discard);
                    discard.Clear();
                    Shuffle(deck);
                }
                if (hand.Count >= MaxHand)
                {
                    // 상한 초과분 자동 디스카드(페널티 없음)
                    discard.Add(deck[0]); deck.RemoveAt(0);
                    continue;
                }
                deck[0].drawSeq = _drawCounter++; // 드로우순 정렬용
                hand.Add(deck[0]); deck.RemoveAt(0);
            }
        }

        public bool CanPlay(CardData c) => state == CombatState.Fighting && cost >= c.cost;

        public void PlayCard(int handIndex)
        {
            if (handIndex < 0 || handIndex >= hand.Count) return;
            var c = hand[handIndex];
            if (!CanPlay(c)) return;

            cost -= c.cost;
            hand.RemoveAt(handIndex);
            onCardCast?.Invoke();        // 시전 손맛 피드백

            if (c.castTime <= 0f)
                ResolveCard(c);          // 즉발
            else
                queue.Add(new CastEntry(c)); // 시전 등록
        }

        void ResolveCard(CardData c)
        {
            if (c.absorb > 0) barrier += c.absorb;
            if (c.drawCount > 0) DrawCards(c.drawCount);

            if (c.damage > 0 && boss != null)
            {
                int dmg = c.damage;
                float slow = c.slowSeconds;
                Vector3 from = player != null ? player.transform.position : Vector3.zero;
                Color col = c.type == CardType.Attack
                    ? new Color(1f, 0.55f, 0.45f) : new Color(0.55f, 0.82f, 1f);
                // 투사체 도달 시 데미지/슬로우/플래시 적용 (낙하감)
                Projectile.Spawn(from, boss.transform.position, col, 16f, () =>
                {
                    if (boss == null) return;
                    boss.TakeDamage(dmg);
                    if (slow > 0) boss.ApplySlow(slow);
                    boss.Flash();
                    onDamageDealt?.Invoke(dmg);
                });
            }
            else if (c.slowSeconds > 0 && boss != null)
            {
                boss.ApplySlow(c.slowSeconds);
            }
            discard.Add(c);
        }

        void CheckWinLose()
        {
            if (boss != null && boss.IsDead) { state = CombatState.Won; lastLog = "보스 처치! 승리"; }
            else if (playerHP <= 0) { state = CombatState.Lost; lastLog = "쓰러졌다... 패배"; }
        }

        // 사이클 종료 시 받을 피해 미리보기(베리어 반영) — HP 바 표시용
        public int PreviewIncoming()
        {
            int dmg = pendingBossDamage;
            int absorbed = Mathf.Min(barrier, dmg);
            return dmg - absorbed;
        }

        static void Shuffle<T>(List<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }
}
