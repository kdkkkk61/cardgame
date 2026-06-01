using UnityEngine;
using UnityEngine.InputSystem;

namespace CarDungeon
{
    /// <summary>
    /// 프로토타입 HUD (IMGUI). screen_v1.html baseline의 기능 버전:
    /// HP+베리어+미리보기 / 보스 HP+카운트다운 / 큐 / 코스트 / 손패 부채(버튼).
    /// 비주얼은 임시 — 검증용. 나중에 uGUI + 픽셀 아트로 교체.
    /// </summary>
    public class CombatHUD : MonoBehaviour
    {
        public CombatManager mgr;
        GUIStyle _box, _label, _card, _small;
        bool _init;

        void Update()
        {
            if (mgr == null) return;

            // 숫자키 1~5 = 손패 사용
            var kb = Keyboard.current;
            if (kb != null && mgr.state == CombatState.Fighting)
            {
                if (kb.digit1Key.wasPressedThisFrame) mgr.PlayCard(0);
                if (kb.digit2Key.wasPressedThisFrame) mgr.PlayCard(1);
                if (kb.digit3Key.wasPressedThisFrame) mgr.PlayCard(2);
                if (kb.digit4Key.wasPressedThisFrame) mgr.PlayCard(3);
                if (kb.digit5Key.wasPressedThisFrame) mgr.PlayCard(4);
            }
            // 결과 화면에서 R = 재시작
            if (kb != null && mgr.state != CombatState.Fighting && kb.rKey.wasPressedThisFrame)
                UnityEngine.SceneManagement.SceneManager.LoadScene(
                    UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex);
        }

        void EnsureStyles()
        {
            if (_init) return;
            _box = new GUIStyle(GUI.skin.box);
            _label = new GUIStyle(GUI.skin.label) { fontSize = 15, normal = { textColor = Color.white } };
            _small = new GUIStyle(GUI.skin.label) { fontSize = 12, normal = { textColor = new Color(0.8f, 0.8f, 0.85f) } };
            _card = new GUIStyle(GUI.skin.button) { fontSize = 13, wordWrap = true, alignment = TextAnchor.UpperCenter };
            _init = true;
        }

        void OnGUI()
        {
            if (mgr == null) return;
            EnsureStyles();
            float W = Screen.width, H = Screen.height;

            // ===== 좌상단: HP + 베리어 + 미리보기 =====
            GUI.Box(new Rect(12, 12, 320, 86), GUIContent.none);
            GUI.Label(new Rect(24, 18, 300, 22),
                $"HP {mgr.playerHP}/{CombatManager.MaxPlayerHP}" +
                (mgr.barrier > 0 ? $"  (+{mgr.barrier} 베리어)" : ""), _label);

            // HP 바 + 미리보기 구간(베리어 반영)
            var barRect = new Rect(24, 46, 296, 18);
            float hpRatio = (float)mgr.playerHP / CombatManager.MaxPlayerHP;
            DrawBar(barRect, hpRatio, new Color(0.3f, 0.85f, 0.4f), new Color(0.2f, 0.2f, 0.2f));
            int incoming = mgr.PreviewIncoming();
            if (incoming > 0 && mgr.playerHP > 0)
            {
                float after = Mathf.Max(0, mgr.playerHP - incoming) / (float)CombatManager.MaxPlayerHP;
                var prev = new Rect(barRect.x + barRect.width * after, barRect.y,
                    barRect.width * (hpRatio - after), barRect.height);
                DrawSolid(prev, new Color(0.9f, 0.25f, 0.25f, 0.85f)); // 맞으면 깎일 구간
            }
            GUI.Label(new Rect(24, 70, 300, 20), $"다음 피해 미리보기: -{incoming}", _small);

            // ===== 상단 중앙: 보스 HP + 카운트다운 =====
            float bw = 420;
            GUI.Box(new Rect(W / 2 - bw / 2, 12, bw, 70), GUIContent.none);
            GUI.Label(new Rect(W / 2 - bw / 2 + 12, 16, bw, 22),
                $"보스   {mgr.boss.hp}/{mgr.boss.maxHP}" +
                (mgr.boss.slowTimer > 0 ? $"   [슬로우 {mgr.boss.slowTimer:0.0}s]" : ""), _label);
            DrawBar(new Rect(W / 2 - bw / 2 + 12, 44, bw - 24, 16),
                mgr.boss.HpRatio, new Color(0.9f, 0.35f, 0.35f), new Color(0.2f, 0.2f, 0.2f));
            // 카운트다운
            string warn = mgr.cycleTimer <= 3f ? "  ⚠ 공격 임박!" : "";
            GUI.Label(new Rect(W / 2 - bw / 2 + 12, 62, bw, 18),
                $"사이클 {mgr.cycleTimer:0.0}s / {mgr.cycleLength:0}s{warn}", _small);

            // ===== 하단 좌: 큐 + 코스트 =====
            GUI.Box(new Rect(12, H - 150, 250, 138), GUIContent.none);
            GUI.Label(new Rect(24, H - 144, 240, 20), $"코스트  {mgr.cost}/{CombatManager.MaxCost}", _label);
            GUI.Label(new Rect(24, H - 120, 240, 18), "시전 중(큐):", _small);
            float qy = H - 100;
            if (mgr.queue.Count == 0)
                GUI.Label(new Rect(34, qy, 220, 18), "—", _small);
            foreach (var e in mgr.queue)
            {
                GUI.Label(new Rect(34, qy, 130, 18), e.card.name, _small);
                DrawBar(new Rect(150, qy + 2, 95, 12), 1f - e.remaining / e.total,
                    new Color(0.8f, 0.7f, 0.3f), new Color(0.2f, 0.2f, 0.2f));
                qy += 20;
            }

            // ===== 하단 중앙: 손패 =====
            DrawHand(W, H);

            // ===== 로그 =====
            if (!string.IsNullOrEmpty(mgr.lastLog))
                GUI.Label(new Rect(W / 2 - 200, H - 168, 400, 20),
                    mgr.lastLog, new GUIStyle(_small) { alignment = TextAnchor.MiddleCenter });

            // ===== 결과 화면 =====
            if (mgr.state != CombatState.Fighting)
            {
                string msg = mgr.state == CombatState.Won ? "승  리" : "패  배";
                GUI.Box(new Rect(W / 2 - 160, H / 2 - 70, 320, 140), GUIContent.none);
                GUI.Label(new Rect(W / 2 - 160, H / 2 - 50, 320, 40),
                    msg, new GUIStyle(_label) { fontSize = 34, alignment = TextAnchor.MiddleCenter });
                GUI.Label(new Rect(W / 2 - 160, H / 2 + 6, 320, 24),
                    $"사이클 {mgr.cycleCount}회", new GUIStyle(_small) { alignment = TextAnchor.MiddleCenter });
                GUI.Label(new Rect(W / 2 - 160, H / 2 + 32, 320, 24),
                    "[R] 다시 시작", new GUIStyle(_small) { alignment = TextAnchor.MiddleCenter });
            }
        }

        void DrawHand(float W, float H)
        {
            int n = mgr.hand.Count;
            if (n == 0) return;
            float cw = 124, ch = 132, gap = 8;
            float total = n * cw + (n - 1) * gap;
            float x0 = W / 2 - total / 2;
            float y = H - ch - 8;

            for (int i = 0; i < n; i++)
            {
                var c = mgr.hand[i];
                var rect = new Rect(x0 + i * (cw + gap), y, cw, ch);

                // 카드 배경(타입 색) — 사용 가능 여부로 밝기 조절
                bool playable = mgr.CanPlay(c);
                Color bg = c.TypeColor * (playable ? 1f : 0.5f);
                DrawSolid(rect, bg);
                // 조준 필요(메테오) 표시
                if (c.aimed) DrawSolid(new Rect(rect.x, rect.y, rect.width, 4),
                    new Color(0.2f, 0.9f, 0.9f));

                string label =
                    $"{i + 1}. {c.name}\n\n" +
                    $"코스트 {c.cost}\n" +
                    (c.castTime <= 0 ? "즉발" : $"시전 {c.castTime}s") + "\n" +
                    (c.shape == CardShape.Circle ? "● 단일" : "■ 광역") +
                    (c.aimed ? " ◎조준" : "") + "\n\n" + c.desc;

                GUI.Label(new Rect(rect.x + 6, rect.y + 6, rect.width - 12, rect.height - 12),
                    label, new GUIStyle(_small) { wordWrap = true, normal = { textColor = Color.white } });

                if (GUI.Button(rect, GUIContent.none) && playable)
                    mgr.PlayCard(i);
            }
        }

        static Texture2D _white;
        static Texture2D White()
        {
            if (_white == null) { _white = new Texture2D(1, 1); _white.SetPixel(0, 0, Color.white); _white.Apply(); }
            return _white;
        }
        static void DrawSolid(Rect r, Color c)
        {
            var old = GUI.color; GUI.color = c;
            GUI.DrawTexture(r, White()); GUI.color = old;
        }
        static void DrawBar(Rect r, float ratio, Color fill, Color back)
        {
            DrawSolid(r, back);
            DrawSolid(new Rect(r.x, r.y, r.width * Mathf.Clamp01(ratio), r.height), fill);
        }
    }
}
