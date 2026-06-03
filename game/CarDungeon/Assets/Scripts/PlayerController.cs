using UnityEngine;
using UnityEngine.InputSystem;

namespace CarDungeon
{
    /// <summary>
    /// 탑다운 WASD 이동. 마법사는 필중이라 무빙 = 거리 조절 / 회피 전용
    /// (CORE_COMBAT 섹션 6). 보스 접촉 시 넉백 + 잠깐 무적(i-frame).
    /// </summary>
    public class PlayerController : MonoBehaviour
    {
        public float speed = 5f;
        public Vector2 arenaHalf = new Vector2(7.5f, 4.2f);
        public bool movementLocked = false; // 완전 정지(예약용)
        public float moveScale = 1f;        // 조준 중 저속 이동 등 배율

        public float invulnTimer = 0f;
        public bool IsInvuln => invulnTimer > 0f;

        Vector2 _knock;
        const float KnockDecay = 14f;
        SpriteRenderer _sr;
        Color _baseColor;

        void Awake()
        {
            _sr = GetComponent<SpriteRenderer>();
            if (_sr != null) _baseColor = _sr.color;
        }

        /// <summary>보스 접촉 피격 반응 — 밀려나고 잠깐 무적.</summary>
        public void HitReact(Vector2 dir, float force, float invuln)
        {
            _knock = dir.sqrMagnitude > 0.0001f ? dir.normalized * force : Vector2.up * force;
            invulnTimer = invuln;
        }

        void Update()
        {
            Vector3 p = transform.position;

            // 넉백 (입력과 무관하게 적용 후 감쇠)
            if (_knock.sqrMagnitude > 0.0001f)
            {
                p += (Vector3)(_knock * Time.deltaTime);
                _knock = Vector2.MoveTowards(_knock, Vector2.zero, KnockDecay * Time.deltaTime);
            }

            // 입력 이동
            var kb = Keyboard.current;
            if (!movementLocked && kb != null)
            {
                Vector2 dir = Vector2.zero;
                if (kb.wKey.isPressed || kb.upArrowKey.isPressed) dir.y += 1;
                if (kb.sKey.isPressed || kb.downArrowKey.isPressed) dir.y -= 1;
                if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) dir.x -= 1;
                if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) dir.x += 1;
                if (dir.sqrMagnitude > 1f) dir.Normalize();
                p += (Vector3)(dir * speed * moveScale * Time.deltaTime);
            }

            p.x = Mathf.Clamp(p.x, -arenaHalf.x, arenaHalf.x);
            p.y = Mathf.Clamp(p.y, -arenaHalf.y, arenaHalf.y);
            transform.position = p;

            // 무적 깜빡임
            if (invulnTimer > 0f)
            {
                invulnTimer -= Time.deltaTime;
                if (_sr != null)
                {
                    float a = 0.35f + 0.4f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 30f));
                    _sr.color = new Color(_baseColor.r, _baseColor.g, _baseColor.b, a);
                }
            }
            else if (_sr != null && _sr.color.a != 1f)
            {
                _sr.color = _baseColor;
            }
        }
    }
}
