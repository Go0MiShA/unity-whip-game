using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

// Castle Guard - simplest Unity 2D game.
// Put this script on ONE empty GameObject in an empty scene and press Play.
// Everything (castle, player, thieves, spectators, UI) is created from code.
public class GameBootstrap : MonoBehaviour
{
    // ---------- Tweak these in the Inspector ----------
    public float playerSpeed = 4f;
    public float whipRange = 2.8f;
    public float whipCooldown = 0.35f;
    public int startLives = 3;
    public float startSpawnInterval = 1.6f;

    // ---------- Internals ----------
    static Sprite sq;
    Transform player, whipPivot;
    SpriteRenderer whipSr;
    float whipTimer, cooldownTimer, spawnTimer, gameTime;
    int score, lives;
    bool gameOver;
    Vector2 aimDir = Vector2.up;

    class Thief { public Transform t; public float speed; }
    readonly List<Thief> thieves = new List<Thief>();
    readonly List<Transform> spectators = new List<Transform>();

    void Start()
    {
        Texture2D tex = new Texture2D(1, 1);
        tex.SetPixel(0, 0, Color.white);
        tex.Apply();
        sq = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);

        Camera cam = Camera.main;
        if (cam == null)
        {
            GameObject c = new GameObject("Main Camera");
            c.tag = "MainCamera";
            cam = c.AddComponent<Camera>();
        }
        cam.orthographic = true;
        cam.orthographicSize = 6f;
        cam.transform.position = new Vector3(0, 0, -10);
        cam.backgroundColor = new Color(0.12f, 0.12f, 0.14f);
        cam.clearFlags = CameraClearFlags.SolidColor;

        BuildCastle();
        BuildPlayer();
        BuildSpectators();
        lives = startLives;
        spawnTimer = 1f;
    }

    // =========================== BUILDING THE WORLD ===========================
    Transform Box(Transform parent, string name, Vector2 pos, Vector2 size, Color col, int order)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localScale = new Vector3(size.x, size.y, 1);
        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sq;
        sr.color = col;
        sr.sortingOrder = order;
        return go.transform;
    }

    void BuildCastle()
    {
        Transform root = new GameObject("Castle").transform;
        Color stone = new Color(0.55f, 0.55f, 0.58f);
        Color stoneDark = new Color(0.45f, 0.45f, 0.48f);
        Color wall = new Color(0.33f, 0.33f, 0.37f);

        // courtyard floor
        Box(root, "Floor", Vector2.zero, new Vector2(30, 16), stone, 0);
        // checker tiles
        for (int x = -8; x <= 8; x++)
            for (int y = -6; y <= 4; y++)
                if ((x + y) % 2 == 0)
                    Box(root, "Tile", new Vector2(x, y), new Vector2(1, 1), stoneDark, 1);

        // red carpet from gate to the middle
        Box(root, "Carpet", new Vector2(0, 1.2f), new Vector2(2.2f, 8f), new Color(0.6f, 0.12f, 0.15f), 2);

        // top wall
        Box(root, "TopWall", new Vector2(0, 5.3f), new Vector2(30, 1.8f), wall, 3);
        for (int x = -9; x <= 9; x += 2)
            Box(root, "Merlon", new Vector2(x, 6.3f), new Vector2(1f, 0.6f), wall, 3);
        // gate
        Box(root, "Gate", new Vector2(0, 5.0f), new Vector2(2.6f, 2.2f), new Color(0.35f, 0.22f, 0.12f), 4);
        Box(root, "GateBars", new Vector2(0, 5.0f), new Vector2(0.15f, 2.2f), new Color(0.15f, 0.1f, 0.06f), 5);
        // towers
        Box(root, "TowerL", new Vector2(-6.5f, 4.3f), new Vector2(2f, 3.4f), new Color(0.4f, 0.4f, 0.45f), 4);
        Box(root, "TowerR", new Vector2(6.5f, 4.3f), new Vector2(2f, 3.4f), new Color(0.4f, 0.4f, 0.45f), 4);
        Box(root, "FlagL", new Vector2(-6.5f, 6.2f), new Vector2(0.9f, 0.5f), new Color(0.7f, 0.15f, 0.15f), 5);
        Box(root, "FlagR", new Vector2(6.5f, 6.2f), new Vector2(0.9f, 0.5f), new Color(0.7f, 0.15f, 0.15f), 5);

        // side walls
        Box(root, "WallLeft", new Vector2(-9.2f, 0), new Vector2(1.2f, 16), wall, 3);
        Box(root, "WallRight", new Vector2(9.2f, 0), new Vector2(1.2f, 16), wall, 3);
    }

    // Builds a simple blocky person.
    // hooded = true  -> thief outfit (dark hood + mask, no skin shown)
    Transform BuildPerson(string name, Vector2 pos, Color skin, Color cloth, Color hair, bool hooded, int order)
    {
        Transform p = new GameObject(name).transform;
        p.position = pos;
        Color legs = hooded ? new Color(0.15f, 0.15f, 0.18f) : new Color(0.35f, 0.3f, 0.2f);
        Box(p, "LegL", new Vector2(-0.14f, -0.55f), new Vector2(0.22f, 0.45f), legs, order);
        Box(p, "LegR", new Vector2(0.14f, -0.55f), new Vector2(0.22f, 0.45f), legs, order);
        Box(p, "Body", new Vector2(0, 0), new Vector2(0.6f, 0.75f), cloth, order + 1);
        Box(p, "ArmL", new Vector2(-0.4f, 0.03f), new Vector2(0.18f, 0.6f), cloth, order + 1);
        Box(p, "ArmR", new Vector2(0.4f, 0.03f), new Vector2(0.18f, 0.6f), cloth, order + 1);
        if (hooded)
        {
            Box(p, "Hood", new Vector2(0, 0.66f), new Vector2(0.56f, 0.56f), new Color(0.12f, 0.12f, 0.15f), order + 2);
            Box(p, "Mask", new Vector2(0, 0.62f), new Vector2(0.36f, 0.14f), Color.black, order + 3);
            Box(p, "Sack", new Vector2(0.0f, -0.05f), new Vector2(0.4f, 0.4f), new Color(0.5f, 0.38f, 0.2f), order);
        }
        else
        {
            Box(p, "Head", new Vector2(0, 0.66f), new Vector2(0.5f, 0.5f), skin, order + 2);
            Box(p, "Hair", new Vector2(0, 0.92f), new Vector2(0.52f, 0.16f), hair, order + 3);
        }
        return p;
    }

    void BuildPlayer()
    {
        // white man, light khaki-green clothes
        Color skin = new Color(0.98f, 0.86f, 0.77f);
        Color khaki = new Color(0.62f, 0.72f, 0.45f);
        Color hair = new Color(0.35f, 0.22f, 0.1f);
        player = BuildPerson("Player", new Vector2(0, 0), skin, khaki, hair, false, 20);
        // little hat for the guard look
        Box(player, "Cap", new Vector2(0, 0.97f), new Vector2(0.58f, 0.12f), new Color(0.5f, 0.6f, 0.35f), 24);

        // whip: pivot in the hand, thin brown bar pointing along +X
        whipPivot = new GameObject("WhipPivot").transform;
        whipPivot.SetParent(player, false);
        whipPivot.localPosition = new Vector3(0.4f, 0.2f, 0);
        Transform bar = Box(whipPivot, "Whip", new Vector2(whipRange * 0.5f, 0), new Vector2(whipRange, 0.08f), new Color(0.35f, 0.2f, 0.08f), 30);
        whipSr = bar.GetComponent<SpriteRenderer>();
        whipSr.enabled = false;
    }

    void BuildSpectators()
    {
        // crowd behind the player (bottom of the screen), watching
        Color[] skins =
        {
            new Color(0.72f, 0.5f, 0.35f), new Color(0.6f, 0.4f, 0.27f),
            new Color(0.78f, 0.56f, 0.4f), new Color(0.55f, 0.36f, 0.24f)
        };
        Color[] clothes =
        {
            new Color(0.3f, 0.45f, 0.7f), new Color(0.75f, 0.55f, 0.2f),
            new Color(0.6f, 0.25f, 0.3f), new Color(0.35f, 0.6f, 0.5f)
        };
        for (int i = 0; i < 9; i++)
        {
            float x = -6.4f + i * 1.6f;
            float y = -4.4f + (i % 2) * 0.5f;
            Transform s = BuildPerson("Spectator" + i, new Vector2(x, y),
                skins[i % skins.Length], clothes[(i * 3) % clothes.Length],
                new Color(0.1f, 0.07f, 0.05f), false, 8);
            spectators.Add(s);
        }
    }

    // =========================== INPUT HELPERS ===========================
    Vector2 MoveInput()
    {
        float x = 0, y = 0;
#if ENABLE_INPUT_SYSTEM
        Keyboard k = Keyboard.current;
        if (k != null)
        {
            if (k.dKey.isPressed || k.rightArrowKey.isPressed) x += 1;
            if (k.aKey.isPressed || k.leftArrowKey.isPressed) x -= 1;
            if (k.wKey.isPressed || k.upArrowKey.isPressed) y += 1;
            if (k.sKey.isPressed || k.downArrowKey.isPressed) y -= 1;
        }
#else
        x = Input.GetAxisRaw("Horizontal");
        y = Input.GetAxisRaw("Vertical");
#endif
        return new Vector2(x, y).normalized;
    }

    bool AttackPressed()
    {
#if ENABLE_INPUT_SYSTEM
        return (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame) ||
               (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame);
#else
        return Input.GetKeyDown(KeyCode.Space) || Input.GetMouseButtonDown(0);
#endif
    }

    bool RestartPressed()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.R);
#endif
    }

    Vector3 MouseWorld()
    {
#if ENABLE_INPUT_SYSTEM
        Vector2 m = Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;
#else
        Vector2 m = Input.mousePosition;
#endif
        Vector3 w = Camera.main.ScreenToWorldPoint(new Vector3(m.x, m.y, 10f));
        w.z = 0;
        return w;
    }

    // =========================== GAME LOOP ===========================
    void Update()
    {
        // crowd cheering animation
        for (int i = 0; i < spectators.Count; i++)
        {
            Vector3 p = spectators[i].position;
            float baseY = -4.4f + (i % 2) * 0.5f;
            p.y = baseY + Mathf.Abs(Mathf.Sin(Time.time * 3f + i)) * 0.12f;
            spectators[i].position = p;
        }

        if (gameOver)
        {
            if (RestartPressed()) Restart();
            return;
        }

        gameTime += Time.deltaTime;

        // move player
        Vector3 pos = player.position + (Vector3)(MoveInput() * playerSpeed * Time.deltaTime);
        pos.x = Mathf.Clamp(pos.x, -7.5f, 7.5f);
        pos.y = Mathf.Clamp(pos.y, -2.8f, 3.2f);
        player.position = pos;

        // aim at mouse
        Vector2 toMouse = (Vector2)(MouseWorld() - player.position);
        if (toMouse.sqrMagnitude > 0.05f) aimDir = toMouse.normalized;

        // whip attack
        cooldownTimer -= Time.deltaTime;
        if (AttackPressed() && cooldownTimer <= 0f)
        {
            cooldownTimer = whipCooldown;
            whipTimer = 0.12f;
            Attack();
        }
        whipTimer -= Time.deltaTime;
        whipSr.enabled = whipTimer > 0f;
        float ang = Mathf.Atan2(aimDir.y, aimDir.x) * Mathf.Rad2Deg;
        whipPivot.rotation = Quaternion.Euler(0, 0, ang);

        // spawn thieves from the top
        spawnTimer -= Time.deltaTime;
        if (spawnTimer <= 0f)
        {
            SpawnThief();
            float interval = Mathf.Max(0.5f, startSpawnInterval - gameTime * 0.015f);
            spawnTimer = interval;
        }

        // move thieves toward the player
        for (int i = thieves.Count - 1; i >= 0; i--)
        {
            Thief th = thieves[i];
            Vector3 dir = (player.position - th.t.position);
            th.t.position += dir.normalized * th.speed * Time.deltaTime;

            if (dir.magnitude < 0.7f)
            {
                Destroy(th.t.gameObject);
                thieves.RemoveAt(i);
                lives--;
                if (lives <= 0) { gameOver = true; }
            }
        }
    }

    void SpawnThief()
    {
        float x = Random.Range(-7f, 7f);
        Color[] cloth = { new Color(0.2f, 0.2f, 0.26f), new Color(0.3f, 0.15f, 0.18f), new Color(0.18f, 0.25f, 0.2f) };
        Transform t = BuildPerson("Thief", new Vector2(x, 7f), Color.black, cloth[Random.Range(0, cloth.Length)], Color.black, true, 14);
        thieves.Add(new Thief { t = t, speed = Random.Range(1.4f, 2.2f) + gameTime * 0.02f });
    }

    void Attack()
    {
        for (int i = thieves.Count - 1; i >= 0; i--)
        {
            Vector2 to = thieves[i].t.position - whipPivot.position;
            if (to.magnitude <= whipRange + 0.4f && Vector2.Angle(aimDir, to) <= 60f)
            {
                Destroy(thieves[i].t.gameObject);
                thieves.RemoveAt(i);
                score++;
            }
        }
    }

    void Restart()
    {
        foreach (Thief th in thieves) if (th.t != null) Destroy(th.t.gameObject);
        thieves.Clear();
        score = 0;
        lives = startLives;
        gameTime = 0;
        spawnTimer = 1f;
        player.position = Vector3.zero;
        gameOver = false;
    }

    // =========================== UI ===========================
    void OnGUI()
    {
        GUIStyle big = new GUIStyle(GUI.skin.label) { fontSize = 28, fontStyle = FontStyle.Bold };
        big.normal.textColor = Color.white;
        GUI.Label(new Rect(15, 10, 400, 40), "Score: " + score + "    Lives: " + lives, big);

        GUIStyle small = new GUIStyle(GUI.skin.label) { fontSize = 16 };
        small.normal.textColor = Color.white;
        GUI.Label(new Rect(15, 50, 600, 30), "WASD / Arrows = move   |   Mouse = aim   |   Click or Space = whip", small);

        if (gameOver)
        {
            GUIStyle over = new GUIStyle(GUI.skin.label)
            {
                fontSize = 48,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            over.normal.textColor = new Color(1f, 0.85f, 0.3f);
            GUI.Label(new Rect(0, Screen.height / 2 - 60, Screen.width, 60), "THE THIEVES GOT IN!", over);
            GUI.Label(new Rect(0, Screen.height / 2, Screen.width, 60), "Score " + score + " - press R to restart", over);
        }
    }
}
