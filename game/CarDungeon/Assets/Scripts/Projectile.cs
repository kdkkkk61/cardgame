using UnityEngine;

namespace CarDungeon
{
    /// <summary>
    /// 카드 사용 시 플레이어 → 보스로 날아가는 코스메틱 투사체.
    /// "카드를 낸다"는 손맛 피드백용 (데미지 처리는 CombatManager가 즉시 수행).
    /// </summary>
    public class Projectile : MonoBehaviour
    {
        Vector3 _to;
        float _speed;
        System.Action _onArrive;

        public static void Spawn(Vector3 from, Vector3 to, Color color, float speed, System.Action onArrive)
        {
            var go = ProtoSprites.Make("Projectile", ProtoSprites.Circle(), color, from, 0.22f, 8);
            var p = go.AddComponent<Projectile>();
            p._to = to; p._speed = speed; p._onArrive = onArrive;
        }

        void Update()
        {
            transform.position = Vector3.MoveTowards(transform.position, _to, _speed * Time.deltaTime);
            if ((transform.position - _to).sqrMagnitude < 0.04f)
            {
                _onArrive?.Invoke();
                Destroy(gameObject);
            }
        }
    }
}
