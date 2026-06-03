using UnityEngine;

namespace CarDungeon
{
    /// <summary>
    /// Slice 1: 제자리 보스. HP만 보유, 사이클 종료 시 패턴 A(자동명중) 발동.
    /// Slice 2에서 추적/정주 이동 + 패턴 B(바닥 장판) 추가 예정.
    /// </summary>
    public class Boss : MonoBehaviour
    {
        public int maxHP = 600;   // 임시 — 전투 3~5분 되도록 튜닝(MAGE_PROTOTYPE 4)
        public int hp;

        public float slowTimer = 0f; // 냉기폭발/메테오 슬로우 잔여(초)

        // 이동 (Slice 2)
        public enum MoveMode { Chase, Stationary }
        public MoveMode moveMode = MoveMode.Chase;
        public float moveSpeed = 2.0f;
        public Transform target;     // 추적 대상(플레이어)
        public Vector2 arenaHalf = new Vector2(7.5f, 4.2f);

        SpriteRenderer _sr;
        Color _baseColor;
        float _flash = 0f;

        void Awake()
        {
            hp = maxHP;
            _sr = GetComponent<SpriteRenderer>();
            if (_sr != null) _baseColor = _sr.color;
        }

        void Update()
        {
            if (slowTimer > 0f) slowTimer -= Time.deltaTime;

            // 추적 이동 (슬로우 시 절반 속도)
            if (moveMode == MoveMode.Chase && target != null)
            {
                float sp = moveSpeed * (slowTimer > 0f ? 0.5f : 1f);
                Vector3 p = Vector3.MoveTowards(transform.position, target.position, sp * Time.deltaTime);
                p.x = Mathf.Clamp(p.x, -arenaHalf.x, arenaHalf.x);
                p.y = Mathf.Clamp(p.y, -arenaHalf.y, arenaHalf.y);
                transform.position = p;
            }

            if (_flash > 0f && _sr != null)
            {
                _flash -= Time.deltaTime * 4f;
                _sr.color = Color.Lerp(_baseColor, Color.white, Mathf.Clamp01(_flash));
            }
        }

        public void Flash() { _flash = 1f; }

        public void TakeDamage(int dmg)
        {
            hp = Mathf.Max(0, hp - dmg);
        }

        public void ApplySlow(float seconds)
        {
            slowTimer = Mathf.Max(slowTimer, seconds);
        }

        public bool IsDead => hp <= 0;
        public float HpRatio => maxHP > 0 ? (float)hp / maxHP : 0f;
    }
}
