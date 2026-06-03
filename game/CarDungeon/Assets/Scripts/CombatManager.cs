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

        public List<CardInstance> deck = new();
        public List<CardInstance> hand = new();
        public List<CardInstance> discard = new();
        public List<CastEntry> queue = new();

        // 런 전체 버프/스탯 (CARD_SYSTEM.md). 강화·NPC·유품이 전부 여기로 모임.
        public PlayerState playerState = new();
        public int MaxHp => MaxPlayerHP + playerState.SumI(ModStat.MaxHpBonus);

        // 보스 패턴 2종 (MAGE_PROTOTYPE 4) — 번갈아
        public enum BossPattern { MagicBurst, FireFloor }
        public BossPattern pattern = BossPattern.MagicBurst;
        public const int MagicBurstDmg = 25;  // 자동명중 (베리어 흡수)
        public const int FireFloorDmg = 35;   // 회피가능 (무빙으로 피함)
        public const float MagicBurstTime = 10f;
        public const float FireFloorTime = 8f;
        public Vector2 aoeCenter;             // 화염 장판 중심
        public float aoeRadius = 2.2f;

        // 메테오 조준 (MAGE_PROTOTYPE 5)
        public const float MeteorRadius = 1.6f;
        public bool IsAiming;
        public Vector2 aimPos;
        CardInstance _aimCard;

        // [설계 의도] 조준 카드(메테오 등)는 위치를 찍는 동안 손이 묶여 자동 카드보다 불리.
        //   그 불리함의 보상 = "조준 시작 순간 0.7초간 화면 슬로우(속도 70%)" 한 번뿐(B안 확정).
        //   ★ 핵심 원칙: 슬로우는 '딱 0.7초만'. 조준을 질질 끌어도 계속 느려지면 안 됨
        //     (계속 느리면 조준=안전카드화 → 페이스 붕괴). 버스트 후엔 즉시 정상 속도·정상 이동.
        public const float AimSlowDuration = 0.7f;
        public const float AimSlowScale = 0.7f;  // 화면 속도 비율(1=정상)
        float _aimSlowTimer = 0f;

        public PlayerController player;
        public Boss boss;
        public string lastLog = "";

        // 접촉 데미지 (보스에 닿으면 — 추적 보스의 거리관리 압박)
        public const int ContactDamage = 8;
        public const float ContactRadius = 0.95f;
        public const float ContactInvuln = 0.8f;
        public const float ContactKnockback = 9f;  // 더 크게 밀림

        // FX 이벤트 (HUD가 구독)
        public event System.Action<int> onDamageDealt; // 보스 피격 데미지
        public event System.Action onCardCast;         // 카드 사용 순간
        public event System.Action onPlayerHit;        // 플레이어 피격(화면 붉게)
        int _drawCounter = 0;

        public void Init(PlayerController player, Boss boss)
        {
            this.player = player;
            this.boss = boss;
            if (boss != null && player != null) boss.target = player.transform;
            Time.timeScale = 1f; // 조준 슬로우 잔여 방지
            playerHP = MaxHp;
            // 정의(불변) → 인스턴스(가변)로 덱 구성. 강화는 인스턴스에 쌓임.
            deck = new List<CardInstance>();
            foreach (var def in CardDefinition.Catalog()) deck.Add(new CardInstance(def));
            Shuffle(deck);
            pattern = BossPattern.FireFloor; // StartCycle에서 토글 → 첫 패턴 MagicBurst
            StartCycle();
        }

        void Update()
        {
            if (state != CombatState.Fighting) return;

            // [의도] 조준 슬로우는 '딱 AimSlowDuration(0.7초)'만. unscaledDeltaTime으로 재서
            //   슬로우(timeScale<1) 영향 없이 실제 0.7초가 지나면 정상 속도 복귀. 조준은 계속 가능하지만
            //   더는 안 느려짐 → "조준 어드밴티지는 잠깐만" 원칙 유지.
            if (_aimSlowTimer > 0f)
            {
                _aimSlowTimer -= Time.unscaledDeltaTime;
                if (_aimSlowTimer <= 0f) Time.timeScale = 1f;
            }

            // 큐(시전중) 진행
            for (int i = queue.Count - 1; i >= 0; i--)
            {
                queue[i].remaining -= Time.deltaTime;
                if (queue[i].remaining <= 0f)
                {
                    ResolveEntry(queue[i]);
                    queue.RemoveAt(i);
                }
            }

            // [의도] 보스-플레이어는 절대 겹치지 않는다(물리적 몸). 추적 보스에 붙으면
            //   ① 밀려나고(겹침 분리) ② 접촉 데미지(베리어 우선 흡수) ③ 0.8초 무적.
            //   → 마법사는 필중이라 무빙=거리 조절. 보스에 붙으면 손해 = "거리 관리"가 진짜 압박이 됨.
            //   (정주/돌진 등은 향후 몹 특성·이동 패턴으로. 지금은 추적 기본)
            if (boss != null && player != null)
            {
                Vector2 pp = player.transform.position, bp = boss.transform.position;
                Vector2 d = pp - bp;
                float dist = d.magnitude;
                if (dist < ContactRadius)
                {
                    // 1) 겹침 분리: 플레이어를 보스 밖으로 밀어냄 (절대 안 겹침)
                    Vector2 dir = dist > 0.0001f ? d / dist : Vector2.up;
                    Vector3 np = player.transform.position + (Vector3)(dir * (ContactRadius - dist));
                    np.x = Mathf.Clamp(np.x, -player.arenaHalf.x, player.arenaHalf.x);
                    np.y = Mathf.Clamp(np.y, -player.arenaHalf.y, player.arenaHalf.y);
                    player.transform.position = np;

                    // 2) 접촉 데미지 (무적 아닐 때만 + 넉백) — 베리어가 먼저 흡수
                    if (!player.IsInvuln)
                    {
                        int absorbed = Mathf.Min(barrier, ContactDamage);
                        barrier -= absorbed;
                        int taken = ContactDamage - absorbed;
                        playerHP = Mathf.Max(0, playerHP - taken);
                        player.HitReact(dir, ContactKnockback, ContactInvuln);
                        onPlayerHit?.Invoke();
                        lastLog = $"보스와 충돌! {ContactDamage} (흡수 {absorbed} / 피해 {taken})";
                    }
                }
            }

            // 사이클 시간
            cycleTimer -= Time.deltaTime;
            if (cycleTimer <= 0f)
                EndCycle();

            CheckWinLose();
        }

        // --- 사이클 ---
        // [의도] 한 사이클 = 보스가 다음 행동을 예고하고 시간이 흐르는 한 텀(CORE_COMBAT §2).
        //   강제 종료 버튼 없음 — 시간은 무조건 끝까지 흐른다(빨리 끝낼 유인 제거).
        //   사이클 시작 시: 손패 전부 버리고 5장 새로, 코스트 풀회복(슬더슬식 에너지).
        void StartCycle()
        {
            cycleCount++;
            // 손패 디스카드 → 5장 드로우 → 코스트 풀회복
            discard.AddRange(hand);
            hand.Clear();
            playerState.ClearScope(ModScope.Cycle);          // 이번 사이클용 버프 만료
            cost = MaxCost + playerState.SumI(ModStat.CostBonus);          // 버프로 코스트 상한 확장 가능
            DrawCards(DrawPerCycle + playerState.SumI(ModStat.DrawBonus)); // 버프로 드로우 증가 가능

            // [의도] 패턴을 번갈아 → 같은 보스라도 사이클 시간/대응이 달라짐(CORE_COMBAT 축2).
            //   A 마력폭발=긴 사이클(준비 시간) / B 화염장판=짧은 사이클(긴급 회피).
            pattern = pattern == BossPattern.MagicBurst
                ? BossPattern.FireFloor : BossPattern.MagicBurst;
            if (pattern == BossPattern.MagicBurst)
                cycleLength = MagicBurstTime;
            else
            {
                cycleLength = FireFloorTime;
                // [의도] 화염 장판 = 플레이어의 '지금 위치'를 노림 → 가만 있으면 맞음.
                //   반드시 무빙으로 영역을 벗어나야 회피 = "무빙이 의미를 갖는 순간".
                aoeCenter = player != null ? (Vector2)player.transform.position : Vector2.zero;
            }
            cycleTimer = cycleLength;
        }

        // [의도] 사이클 종료 = 보스 행동 발동. 패턴별 방어 수단이 다른 게 핵심:
        //   · 마력폭발(자동명중) → 못 피함, '베리어 카드'로만 흡수 (카드로만 막는다 §8)
        //   · 화염장판(회피가능) → '무빙'으로만 피함, 베리어 무관 (둘을 헷갈리면 죽음)
        void EndCycle()
        {
            if (pattern == BossPattern.MagicBurst)
            {
                // 자동명중 — 베리어로 흡수 후 잔여만 HP 피해
                int dmg = MagicBurstDmg;
                int absorbed = Mathf.Min(barrier, dmg);
                barrier -= absorbed;
                int taken = dmg - absorbed;
                playerHP = Mathf.Max(0, playerHP - taken);
                if (taken > 0) onPlayerHit?.Invoke();
                lastLog = $"마력폭발! {dmg} (흡수 {absorbed} / 피해 {taken})";
            }
            else
            {
                // 회피가능 — 영역 안이면 풀피해, 무빙으로 이탈하면 0 (베리어 무관)
                bool inside = player != null &&
                    ((Vector2)player.transform.position - aoeCenter).sqrMagnitude <= aoeRadius * aoeRadius;
                if (inside)
                {
                    playerHP = Mathf.Max(0, playerHP - FireFloorDmg);
                    onPlayerHit?.Invoke();
                    lastLog = $"화염 장판 적중! -{FireFloorDmg} (영역 못 벗어남)";
                }
                else lastLog = "화염 장판 회피 성공!";
            }

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

        public bool CanPlay(CardInstance c) => state == CombatState.Fighting && cost >= c.cost;

        public void PlayCard(int handIndex)
        {
            if (handIndex < 0 || handIndex >= hand.Count) return;
            var c = hand[handIndex];
            if (!CanPlay(c)) return;

            // [의도] 조준 카드는 즉시 발동이 아니라 '위치 지정' 단계로. 코스트 차감은 확정(ConfirmAim) 때.
            //   취소하면 코스트 안 나감 = 조준은 실수해도 되는 행위(단, 시간은 흐름).
            if (c.aimed) { BeginAim(handIndex); return; }

            cost -= c.cost;
            hand.RemoveAt(handIndex);
            onCardCast?.Invoke();        // 시전 손맛 피드백

            if (c.castTime <= 0f)
                ResolveEntry(new CastEntry(c)); // 즉발
            else
                queue.Add(new CastEntry(c));    // 시전 등록
        }

        // --- 메테오 조준 ---
        public void BeginAim(int handIndex)
        {
            if (IsAiming) return;
            var c = hand[handIndex];
            if (!CanPlay(c)) return;
            IsAiming = true; _aimCard = c;
            // 조준 시작 = 슬로우 버스트 1회만. 이후 _aimSlowTimer가 0이 되면 Update에서 정상 속도 복귀.
            // (저속 이동 같은 '지속 패널티'는 두지 않음 — 조준 중에도 이동은 정상 속도)
            Time.timeScale = AimSlowScale;
            _aimSlowTimer = AimSlowDuration;
        }

        public void ConfirmAim(Vector2 worldPos)
        {
            if (!IsAiming) return;
            int idx = hand.IndexOf(_aimCard);
            EndAimState();
            if (idx < 0) return;
            cost -= _aimCard.cost;
            hand.RemoveAt(idx);
            onCardCast?.Invoke();
            var e = new CastEntry(_aimCard) { targetPos = worldPos };
            queue.Add(e);
        }

        public void CancelAim() { if (IsAiming) EndAimState(); }

        void EndAimState()
        {
            IsAiming = false;
            _aimSlowTimer = 0f;
            Time.timeScale = 1f;
            if (player != null) { player.moveScale = 1f; player.movementLocked = false; }
        }

        void ResolveEntry(CastEntry e)
        {
            var c = e.card;
            // [의도] 효과 수치는 '반드시' 파이프라인(CardMath)으로 → 강화·버프가 자동 반영.
            int dmg = CardMath.Damage(c, playerState);
            int absorb = CardMath.Absorb(c, playerState);
            int draw = CardMath.DrawCount(c);
            float slow = CardMath.Slow(c);

            if (absorb > 0) barrier += absorb;
            if (draw > 0) DrawCards(draw);

            // [의도] 파워카드/다음카드강화 = 사용 시 PlayerState에 버프 추가 (범위가 소멸 시점 결정).
            if (c.def.appliesMod != null)
            {
                playerState.Add(c.def.appliesMod.Clone());
                lastLog = $"{c.name} 발동 — {c.def.appliesMod.label} 적용";
            }
            // 1회용(다음카드/다음공격) 버프 소비 — 이 카드가 그 버프를 '썼으므로'.
            playerState.ConsumeOnPlay(c.type == CardType.Attack);

            // [의도] 메테오(조준) = 마법사의 '수싸움' 카드. 찍은 자리에 3초 뒤 낙하 →
            //   보스가 그 사이 빠져나가면 헛방. 그래서 냉기폭발(슬로우)로 묶고 쏘는 콤보가 핵심.
            //   "슬로우 안 걸고 깔면 헛방"이 학습 포인트(MAGE_PROTOTYPE §3).
            if (c.aimed) // 메테오 — 조준 지점에 낙하, 보스가 그 안에 있어야 명중
            {
                Vector3 from = new Vector3(e.targetPos.x, e.targetPos.y + 6f, 0);
                Projectile.Spawn(from, e.targetPos, new Color(1f, 0.6f, 0.3f), 20f, () =>
                {
                    if (boss == null) return;
                    bool hit = ((Vector2)boss.transform.position - e.targetPos).sqrMagnitude
                        <= MeteorRadius * MeteorRadius;
                    if (hit)
                    {
                        boss.TakeDamage(dmg); boss.Flash(); onDamageDealt?.Invoke(dmg);
                        lastLog = $"메테오 명중! -{dmg}";
                    }
                    else lastLog = "메테오 헛방 (보스가 빠져나감)";
                });
                discard.Add(c);
                return;
            }

            if (dmg > 0 && boss != null)
            {
                Vector3 from = player != null ? player.transform.position : Vector3.zero;
                Color col = c.type == CardType.Attack
                    ? new Color(1f, 0.55f, 0.45f) : new Color(0.55f, 0.82f, 1f);
                Projectile.Spawn(from, boss.transform.position, col, 16f, () =>
                {
                    if (boss == null) return;
                    boss.TakeDamage(dmg);
                    if (slow > 0) boss.ApplySlow(slow);
                    boss.Flash();
                    onDamageDealt?.Invoke(dmg);
                });
            }
            else if (slow > 0 && boss != null)
            {
                boss.ApplySlow(slow);
            }
            discard.Add(c);
        }

        void CheckWinLose()
        {
            if (boss != null && boss.IsDead) { state = CombatState.Won; lastLog = "보스 처치! 승리"; }
            else if (playerHP <= 0) { state = CombatState.Lost; lastLog = "쓰러졌다... 패배"; }
        }

        // 사이클 종료 시 받을 피해 미리보기 — HP 바 표시용
        public int PreviewIncoming()
        {
            if (pattern == BossPattern.MagicBurst)
            {
                // 자동명중: 베리어가 흡수
                int absorbed = Mathf.Min(barrier, MagicBurstDmg);
                return MagicBurstDmg - absorbed;
            }
            // 화염 장판: 지금 영역 안이면 풀피해 예고, 벗어나 있으면 0 (베리어 무관)
            bool inside = player != null &&
                ((Vector2)player.transform.position - aoeCenter).sqrMagnitude <= aoeRadius * aoeRadius;
            return inside ? FireFloorDmg : 0;
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
