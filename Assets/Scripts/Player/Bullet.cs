using UnityEngine;

/// <summary>
/// 子弹：水平飞行，击中敌人时杀死敌人，击中地形或超时后销毁自身。
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class Bullet : MonoBehaviour
{
    [SerializeField] public float speed = 12f;
    [SerializeField] public float lifetime = 2.5f;

    private float direction;

    void Awake()
    {
        var sr = GetComponent<SpriteRenderer>();

        // 如果没有 Sprite，运行时生成红色小圆
        if (sr.sprite == null)
            sr.sprite = MakeCircleSprite(32, Color.red);

        // 确保渲染在最前面
        var playerGO = GameObject.FindWithTag("Player");
        if (playerGO != null)
        {
            var psr = playerGO.GetComponent<SpriteRenderer>();
            if (psr != null)
            {
                sr.sortingLayerName = psr.sortingLayerName;
                sr.sortingOrder     = psr.sortingOrder + 1;
            }
        }

        transform.localScale = Vector3.one * 0.25f;
    }

    static Sprite MakeCircleSprite(int size, Color color)
    {
        var tex  = new Texture2D(size, size, TextureFormat.RGBA32, false);
        float cx = size * 0.5f, cy = size * 0.5f, r = size * 0.5f;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = x - cx, dy = y - cy;
                tex.SetPixel(x, y, (dx*dx + dy*dy <= r*r) ? color : Color.clear);
            }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
    }

    public void Init(float dir)
    {
        direction = Mathf.Sign(dir);
        Destroy(gameObject, lifetime);
    }

    void FixedUpdate()
    {
        transform.Translate(Vector2.right * direction * speed * Time.fixedDeltaTime);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        // 击中敌人
        var enemy = other.GetComponent<Enemy>();
        if (enemy != null)
        {
            enemy.Die();
            Destroy(gameObject);
            return;
        }

        // 击中地形（Ground 层）
        if (((1 << other.gameObject.layer) & LayerMask.GetMask("Ground")) != 0)
        {
            Destroy(gameObject);
        }
    }
}
