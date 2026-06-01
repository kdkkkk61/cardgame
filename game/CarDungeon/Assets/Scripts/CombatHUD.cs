using UnityEngine;
using UnityEngine.InputSystem;

namespace CarDungeon
{
    /// <summary>
    /// 프로토타입 HUD (IMGUI) — screen_v1.html baseline 충실 재현.
    /// 대원칙: 하단 UI 박스 없음(투명 오버레이), 카드/HP/코스트가 전장 위에 떠 있음.
    /// 비주얼은 임시 — 정보축(색=분류, 원/네모=단일/광역)만 유지, 일러스트 자리는 비움.
    /// </summary>
    public class CombatHUD : MonoBehaviour
    {
        public CombatManager mgr;

        // 팔레트 (screen_v1.html :root)
        static readonly Color C_atk     = new Color(0.878f, 0.322f, 0.302f);
        static readonly Color C_skill   = new Color(0.302f, 0.561f, 0.878f);
        static readonly Color C_hp      = new Color(0.341f, 0.769f, 0.416f);
        static readonly Color C_barrier = new Color(0.561f, 0.718f, 0.839f);
        static readonly Color C_danger  = new Color(0.878f, 0.322f, 0.302f);
        static readonly Color C_aim     = new Color(0.329f, 0.839f, 0.816f);
        static readonly Color C_ink     = new Color(0.949f, 0.933f, 0.969f);
        static readonly Color C_inkDim  = new Color(0.608f, 0.580f, 0.659f);
        static readonly Color C_panel   = new Color(0.078f, 0.063f, 0.102f, 0.72f);

        GUIStyle _num, _lbl, _dim, _big, _cardName, _badge;
        bool _init;
        int _hover = -1;

        void Update()
        {
            if (mgr == null) return;
            var kb = Keyboard.current;
            if (kb != null && mgr.state == CombatState.Fighting)
            {
                if (kb.digit1Key.wasPressedThisFrame) mgr.PlayCard(0);
                if (kb.digit2Key.wasPressedThisFrame) mgr.PlayCard(1);
                if (kb.digit3Key.wasPressedThisFrame) mgr.PlayCard(2);
                if (kb.digit4Key.wasPressedThisFrame) mgr.PlayCard(3);
                if (kb.digit5Key.wasPressedThisFrame) mgr.PlayCard(4);
            }
            if (kb != null && mgr.state != CombatState.Fighting && kb.rKey.wasPressedThisFrame)
                UnityEngine.SceneManagement.SceneManager.LoadScene(
                    UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex);
        }

        void EnsureStyles()
        {
            if (_init) return;
            _num = Style(16, FontStyle.Bold, C_ink);
            _lbl = Style(11, FontStyle.Normal, C_inkDim);
            _dim = Style(10, FontStyle.Normal, C_inkDim);
            _big = Style(24, FontStyle.Bold, C_ink);
            _cardName = Style(11, FontStyle.Bold, C_ink); _cardName.alignment = TextAnchor.MiddleCenter;
            _badge = Style(13, FontStyle.Bold, Color.white); _badge.alignment = TextAnchor.MiddleCenter;
            _init = true;
        }
        static GUIStyle Style(int size, FontStyle fs, Color col)
            => new GUIStyle { fontSize = size, fontStyle = fs, normal = { textColor = col }, richText = true };

        void OnGUI()
        {
            if (mgr == null) return;
            EnsureStyles();
            float W = Screen.width, H = Screen.height;

            DrawAutoHitTelegraph();   // 전장: 자동명중 확정타 링 (캐릭터 둘레)
            DrawHpPanel();            // 좌상단
            DrawBossCluster(W);       // 상단 중앙
            DrawMinimap(W);           // 우상단
            DrawQueue(H);             // 하단 좌
            DrawCost(H);              // 좌하
            DrawHand(W, H);           // 하단 중앙 (부채꼴)
            DrawDeckArea(W, H);       // 하단 우
            DrawLog(W, H);
            DrawResult(W, H);
        }

        // ── 자동명중 확정타 텔레그래프: 공격 임박 시 캐릭터 둘레 빨간 링 ──
        void DrawAutoHitTelegraph()
        {
            if (mgr.state != CombatState.Fighting || mgr.player == null) return;
            if (mgr.cycleTimer > 3.5f) return; // 임박할 때만
            var cam = Camera.main; if (cam == null) return;
            Vector3 sp = cam.WorldToScreenPoint(mgr.player.transform.position);
            float y = Screen.height - sp.y;
            float pulse = Mathf.Lerp(46f, 60f, 0.5f + 0.5f * Mathf.Sin(Time.time * 6f));
            float a = Mathf.Lerp(0.4f, 0.95f, 0.5f + 0.5f * Mathf.Sin(Time.time * 6f));
            DrawTex(new Rect(sp.x - pulse, y - pulse, pulse * 2, pulse * 2),
                Tex.Ring(), new Color(C_danger.r, C_danger.g, C_danger.b, a));
        }

        // ── 좌상단: HP + 베리어 + 미리보기 (투명, 박스 없음) ──
        void DrawHpPanel()
        {
            float x = 16, y = 14, w = 240;
            GUI.Label(new Rect(x, y, w, 18),
                $"<size=11>HP</size>  <b>{mgr.playerHP} / {CombatManager.MaxPlayerHP}</b>" +
                (mgr.barrier > 0 ? $"  <color=#8fb7d6>(+{mgr.barrier})</color>" : ""), _num);

            var bar = new Rect(x, y + 22, w, 16);
            DrawTex(bar, Tex.White(), new Color(0.141f, 0.114f, 0.161f));
            float hpR = (float)mgr.playerHP / CombatManager.MaxPlayerHP;
            DrawTex(new Rect(bar.x, bar.y, bar.width * hpR, bar.height), Tex.White(), C_hp);

            // 베리어: 우측 끝 빗금 구간
            if (mgr.barrier > 0)
            {
                float br = Mathf.Clamp01((float)mgr.barrier / CombatManager.MaxPlayerHP);
                var brRect = new Rect(bar.xMax - bar.width * br, bar.y, bar.width * br, bar.height);
                DrawTex(brRect, Tex.Hatch(), C_barrier);
            }
            // 미리보기: 맞으면 깎일 구간 (베리어 반영) 점멸
            int incoming = mgr.PreviewIncoming();
            if (incoming > 0 && mgr.playerHP > 0)
            {
                float after = Mathf.Max(0, mgr.playerHP - incoming) / (float)CombatManager.MaxPlayerHP;
                float blink = 0.3f + 0.45f * (Mathf.Sin(Time.time * 6f) * 0.5f + 0.5f);
                DrawTex(new Rect(bar.x + bar.width * after, bar.y, bar.width * (hpR - after), bar.height),
                    Tex.White(), new Color(C_danger.r, C_danger.g, C_danger.b, blink));
            }
            string note = incoming > 0
                ? $"보스 예고 피해 → HP -{incoming}"
                : (mgr.barrier > 0 ? "베리어가 흡수 → 안전" : "방어 없음 — 예고 피해 그대로");
            GUI.Label(new Rect(x, y + 42, w + 60, 16), note, _dim);
        }

        // ── 상단 중앙: 보스 HP + 원형 카운트다운 ──
        void DrawBossCluster(float W)
        {
            float w = 240, x = W / 2 - w / 2, y = 16;
            var bar = new Rect(x, y, w, 12);
            DrawTex(bar, Tex.White(), new Color(0.165f, 0.133f, 0.188f));
            DrawTex(new Rect(bar.x, bar.y, bar.width * mgr.boss.HpRatio, bar.height), Tex.White(), C_atk);
            GUI.Label(new Rect(x, y - 18, w, 16),
                $"보스  {mgr.boss.hp}/{mgr.boss.maxHP}" +
                (mgr.boss.slowTimer > 0 ? $"  <color=#54d6d0>[슬로우 {mgr.boss.slowTimer:0.0}s]</color>" : ""),
                new GUIStyle(_lbl) { alignment = TextAnchor.MiddleCenter, normal = { textColor = C_ink } });

            // 원형 시간 게이지 (체력바 옆)
            float fill = mgr.cycleLength > 0 ? mgr.cycleTimer / mgr.cycleLength : 0;
            var g = new Rect(bar.xMax + 8, y - 7, 26, 26);
            Color gc = mgr.cycleTimer <= 3f ? C_danger : C_inkDim;
            DrawTex(g, Tex.Radial(fill), gc);
            DrawTex(new Rect(g.x + 5, g.y + 5, 16, 16), Tex.Circle(), new Color(0.086f, 0.071f, 0.110f));
            GUI.Label(g, $"{Mathf.CeilToInt(mgr.cycleTimer)}",
                new GUIStyle(_dim) { alignment = TextAnchor.MiddleCenter, normal = { textColor = C_ink } });
        }

        // ── 우상단: 미니맵 (자리 확인용 플레이스홀더) ──
        void DrawMinimap(float W)
        {
            float cell = 13, gap = 3;
            int cols = 4, rows = 3;
            float bw = cols * cell + (cols - 1) * gap + 12;
            float bh = rows * cell + (rows - 1) * gap + 12;
            float x = W - bw - 12, y = 12;
            DrawTex(new Rect(x, y, bw, bh), Tex.White(), C_panel);
            int[] map = { 0, 2, 0, 0,  3, 1, 3, 2,  0, 3, 4, 0 }; // 0없음1현재2미발견3클리어4보스
            for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
            {
                int v = map[r * cols + c];
                if (v == 0) continue;
                Color col = v == 1 ? new Color(0.725f, 0.639f, 0.910f)
                    : v == 2 ? new Color(0.098f, 0.082f, 0.125f)
                    : v == 3 ? new Color(0.212f, 0.290f, 0.239f)
                    : new Color(0.369f, 0.122f, 0.173f);
                DrawTex(new Rect(x + 6 + c * (cell + gap), y + 6 + r * (cell + gap), cell, cell), Tex.White(), col);
            }
        }

        // ── 하단 좌: 큐 (시전중 표시, 병렬) ──
        void DrawQueue(float H)
        {
            float x = 16, y = H - 150;
            GUI.Label(new Rect(x, y, 120, 14), "시전 중", _dim);
            y += 16;
            foreach (var e in mgr.queue)
            {
                var row = new Rect(x, y, 96, 18);
                DrawTex(row, Tex.White(), C_panel);
                GUI.Label(new Rect(row.x + 4, row.y + 1, 60, 16), e.card.name, _dim);
                var pb = new Rect(row.x + 4, row.yMax - 5, row.width - 8, 4);
                DrawTex(pb, Tex.White(), new Color(0.165f, 0.133f, 0.188f));
                DrawTex(new Rect(pb.x, pb.y, pb.width * (1f - e.remaining / e.total), pb.height),
                    Tex.White(), e.card.type == CardType.Attack ? C_atk : C_skill);
                y += 22;
            }
        }

        // ── 좌하: 코스트 (숫자만) ──
        void DrawCost(float H)
        {
            GUI.Label(new Rect(16, H - 96, 120, 30), $"<color=#9b94a8><size=11>코스트</size></color>", _dim);
            GUI.Label(new Rect(16, H - 84, 120, 30), $"{mgr.cost} / {CombatManager.MaxCost}", _big);
        }

        // ── 하단 중앙: 손패 부채꼴 ──
        void DrawHand(float W, float H)
        {
            int n = mgr.hand.Count;
            if (n == 0) { _hover = -1; return; }

            const float CW = 104, CH = 144, SP = 64, STEP = 5f;
            float baseY = H - CH - 6;
            float mid = (n - 1) / 2f;
            Vector2 mouse = Event.current.mousePosition;

            // 호버 판정 (오른쪽/위 카드가 위 → 뒤에서부터)
            _hover = -1;
            for (int i = n - 1; i >= 0; i--)
            {
                float off = i - mid;
                float cx = W / 2 + off * SP;
                float cy = baseY + Mathf.Abs(off) * 6f;
                var foot = new Rect(cx - SP / 2, cy, SP, CH);
                if (foot.Contains(mouse)) { _hover = i; break; }
            }

            // 비호버 카드(부채꼴 회전)
            for (int i = 0; i < n; i++)
            {
                if (i == _hover) continue;
                float off = i - mid;
                float cx = W / 2 + off * SP;
                float cy = baseY + Mathf.Abs(off) * 6f;
                DrawCard(mgr.hand[i], i, cx, cy, off * STEP, 1f, false);
            }
            // 호버 카드(똑바로 + 확대 + 떠오름, 맨 앞)
            if (_hover >= 0)
            {
                float off = _hover - mid;
                float cx = W / 2 + off * SP;
                float cy = baseY + Mathf.Abs(off) * 6f - 60f;
                DrawCard(mgr.hand[_hover], _hover, cx, cy, 0f, 1.45f, true);
            }
        }

        void DrawCard(CardData c, int index, float cx, float cy, float rot, float scale, bool hover)
        {
            const float CW = 104, CH = 144;
            float w = CW * scale, h = CH * scale;
            var rect = new Rect(cx - w / 2, cy, w, h);
            Matrix4x4 m = GUI.matrix;
            if (Mathf.Abs(rot) > 0.01f)
                GUIUtility.RotateAroundPivot(rot, new Vector2(cx, cy + h));

            bool playable = mgr.CanPlay(c);
            // 카드 바탕
            DrawTex(rect, Tex.Round(), new Color(0.169f, 0.141f, 0.200f, playable ? 0.98f : 0.7f));
            DrawTexBorder(rect, hover ? new Color(0.541f, 0.478f, 0.651f) : new Color(0.290f, 0.247f, 0.341f));

            // 일러스트 슬롯 (모양 = 정보축)
            float artS = 50 * scale;
            var art = new Rect(cx - artS / 2, rect.y + 18 * scale, artS, artS);
            DrawTex(art, c.shape == CardShape.Circle ? Tex.Circle() : Tex.Round(),
                new Color(0.082f, 0.067f, 0.110f));
            DrawTexBorder(art, new Color(0.416f, 0.365f, 0.490f),
                c.shape == CardShape.Circle);
            GUI.Label(art, c.shape == CardShape.Circle ? "단일" : "광역",
                new GUIStyle(_dim) { alignment = TextAnchor.MiddleCenter, fontSize = Mathf.RoundToInt(10 * scale) });

            // 이름
            GUI.Label(new Rect(rect.x, art.yMax + 4 * scale, w, 16 * scale), c.name,
                new GUIStyle(_cardName) { fontSize = Mathf.RoundToInt(11 * scale) });

            // 효과(호버 시) + 단축키
            if (hover)
                GUI.Label(new Rect(rect.x + 4, art.yMax + 22 * scale, w - 8, 40 * scale),
                    $"{(c.castTime <= 0 ? "즉발" : c.castTime + "초")} · {c.desc}",
                    new GUIStyle(_dim) { alignment = TextAnchor.UpperCenter, wordWrap = true,
                        fontSize = Mathf.RoundToInt(9 * scale) });
            if (index < 5)
                GUI.Label(new Rect(rect.x + 4, rect.yMax - 16 * scale, w - 8, 14 * scale),
                    $"[{index + 1}]", new GUIStyle(_dim) { alignment = TextAnchor.MiddleCenter,
                        fontSize = Mathf.RoundToInt(9 * scale) });

            // 코스트 배지 (좌상단, 색=분류) + 조준 레티클
            float bs = 26 * scale;
            var badge = new Rect(rect.x - bs * 0.3f, rect.y - bs * 0.3f, bs, bs);
            DrawTex(badge, Tex.Circle(), c.type == CardType.Attack ? C_atk : C_skill);
            GUI.Label(badge, $"{c.cost}", new GUIStyle(_badge) { fontSize = Mathf.RoundToInt(13 * scale) });
            if (c.aimed)
                DrawTex(new Rect(badge.x - 4, badge.y - 4, bs + 8, bs + 8), Tex.Ring(), C_aim);

            // 클릭
            if (GUI.Button(rect, GUIContent.none, GUIStyle.none) && playable)
                mgr.PlayCard(index);

            GUI.matrix = m;
        }

        // ── 하단 우: 정렬 + 덱 ──
        void DrawDeckArea(float W, float H)
        {
            float x = W - 70, y = H - 100;
            string[] s = { "A", "B", "C" };
            for (int i = 0; i < 3; i++)
            {
                var r = new Rect(x + i * 24, y, 22, 22);
                DrawTex(r, Tex.White(), C_panel);
                GUI.Label(r, s[i], new GUIStyle(_dim) { alignment = TextAnchor.MiddleCenter });
            }
            var pile = new Rect(x, y + 28, 54, 70);
            DrawTex(pile, Tex.Round(), new Color(0.169f, 0.141f, 0.200f));
            DrawTexBorder(pile, new Color(0.290f, 0.247f, 0.341f));
            GUI.Label(new Rect(pile.x, pile.y + 18, pile.width, 20),
                $"덱 {mgr.deck.Count}", new GUIStyle(_dim) { alignment = TextAnchor.MiddleCenter });
            GUI.Label(new Rect(pile.x, pile.y + 40, pile.width, 16),
                $"무덤 {mgr.discard.Count}", new GUIStyle(_dim) { alignment = TextAnchor.MiddleCenter });
        }

        void DrawLog(float W, float H)
        {
            if (string.IsNullOrEmpty(mgr.lastLog)) return;
            GUI.Label(new Rect(W / 2 - 200, 50, 400, 18), mgr.lastLog,
                new GUIStyle(_dim) { alignment = TextAnchor.MiddleCenter, normal = { textColor = C_ink } });
        }

        void DrawResult(float W, float H)
        {
            if (mgr.state == CombatState.Fighting) return;
            DrawTex(new Rect(0, 0, W, H), Tex.White(), new Color(0, 0, 0, 0.55f));
            string msg = mgr.state == CombatState.Won ? "승  리" : "패  배";
            Color col = mgr.state == CombatState.Won ? C_hp : C_danger;
            GUI.Label(new Rect(0, H / 2 - 50, W, 50), msg,
                new GUIStyle(_big) { fontSize = 40, alignment = TextAnchor.MiddleCenter, normal = { textColor = col } });
            GUI.Label(new Rect(0, H / 2 + 8, W, 24), $"사이클 {mgr.cycleCount}회 · [R] 다시 시작",
                new GUIStyle(_dim) { alignment = TextAnchor.MiddleCenter });
        }

        // ── 그리기 헬퍼 ──
        static void DrawTex(Rect r, Texture2D t, Color c)
        {
            var old = GUI.color; GUI.color = c;
            GUI.DrawTexture(r, t); GUI.color = old;
        }
        static void DrawTexBorder(Rect r, Color c, bool circle = false)
        {
            var old = GUI.color; GUI.color = c;
            if (circle) { GUI.DrawTexture(r, Tex.Ring()); }
            else
            {
                GUI.DrawTexture(new Rect(r.x, r.y, r.width, 1), Tex.White());
                GUI.DrawTexture(new Rect(r.x, r.yMax - 1, r.width, 1), Tex.White());
                GUI.DrawTexture(new Rect(r.x, r.y, 1, r.height), Tex.White());
                GUI.DrawTexture(new Rect(r.xMax - 1, r.y, 1, r.height), Tex.White());
            }
            GUI.color = old;
        }
    }

    /// <summary>IMGUI용 런타임 생성 텍스처 캐시.</summary>
    static class Tex
    {
        static Texture2D _white, _circle, _ring, _round, _hatch;
        static Texture2D[] _radial = new Texture2D[21];

        public static Texture2D White()
        {
            if (_white == null) { _white = Solid(1, Color.white); }
            return _white;
        }
        static Texture2D Solid(int s, Color c)
        {
            var t = new Texture2D(s, s, TextureFormat.RGBA32, false);
            var px = new Color[s * s];
            for (int i = 0; i < px.Length; i++) px[i] = c;
            t.SetPixels(px); t.Apply(); return t;
        }
        public static Texture2D Circle()
        {
            if (_circle == null) _circle = MakeCircle(64, true);
            return _circle;
        }
        public static Texture2D Ring()
        {
            if (_ring == null) _ring = MakeCircle(64, false);
            return _ring;
        }
        static Texture2D MakeCircle(int s, bool fill)
        {
            var t = new Texture2D(s, s, TextureFormat.RGBA32, false);
            float r = s / 2f;
            for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                float dx = x - r + 0.5f, dy = y - r + 0.5f;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                bool on = fill ? d <= r - 1 : (d <= r - 1 && d >= r - 4);
                t.SetPixel(x, y, on ? Color.white : Color.clear);
            }
            t.Apply(); return t;
        }
        public static Texture2D Round()  // 모서리 둥근 사각(근사: 그냥 솔리드)
        {
            if (_round == null) _round = Solid(1, Color.white);
            return _round;
        }
        public static Texture2D Hatch()  // 베리어 빗금
        {
            if (_hatch == null)
            {
                int s = 16; var t = new Texture2D(s, s, TextureFormat.RGBA32, false);
                for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                    t.SetPixel(x, y, ((x + y) % 6 < 3) ? Color.white : new Color(1, 1, 1, 0.45f));
                t.Apply(); t.wrapMode = TextureWrapMode.Repeat; _hatch = t;
            }
            return _hatch;
        }
        // 원형 게이지 (fill 0~1, 12시 시작 시계방향). 0.05 버킷 캐시.
        public static Texture2D Radial(float fill)
        {
            int bucket = Mathf.Clamp(Mathf.RoundToInt(fill * 20f), 0, 20);
            if (_radial[bucket] != null) return _radial[bucket];
            int s = 48; var t = new Texture2D(s, s, TextureFormat.RGBA32, false);
            float r = s / 2f, f = bucket / 20f;
            for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                float dx = x - r + 0.5f, dy = y - r + 0.5f;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                if (d > r - 1) { t.SetPixel(x, y, Color.clear); continue; }
                float ang = Mathf.Atan2(dx, dy) * Mathf.Rad2Deg; // 12시=0, 시계방향
                if (ang < 0) ang += 360f;
                t.SetPixel(x, y, ang / 360f <= f ? Color.white : new Color(1, 1, 1, 0.12f));
            }
            t.Apply(); _radial[bucket] = t; return t;
        }
    }
}
