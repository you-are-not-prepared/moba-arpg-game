using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.AI.Navigation;
using Moba.Abilities;
using Moba.UI;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UIElements;

namespace Moba.EditorTools
{
    // Generates the single-lane slice: materials, hero/creep prefabs, and the Lane scene with a baked NavMesh.
    // Re-running overwrites the generated assets, so tweak layout here rather than by hand in the scene.
    public static class LaneSliceBuilder
    {
        const string Root = "Assets/_Project";
        const string MaterialsDir = Root + "/Materials";
        const string PrefabsDir = Root + "/Prefabs";
        const string ScenesDir = Root + "/Scenes";
        const string ScenePath = ScenesDir + "/Lane.unity";
        const string NavMeshPath = ScenesDir + "/Lane_NavMesh.asset";
        const string UiDir = Root + "/UI";
        const string PanelSettingsPath = UiDir + "/HudPanelSettings.asset";

        // The lane runs diagonally from the blue core (bottom-left) to the red core (top-right).
        static readonly Vector3 BlueCorePos = new(-45f, 0f, -45f);
        static readonly Vector3 RedCorePos = new(45f, 0f, 45f);
        const float LaneHalfWidth = 5f;
        const float BaseRadius = 13f;

        class Mats
        {
            public Material Ground, Lane, Rock, Stone, Blue, Red, BluePad, RedPad, Hero, Marker, AttackMarker, RangeIndicator;
        }

        [MenuItem("MOBA/Rebuild Lane Slice Scene")]
        public static void BuildFromMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Build();
        }

        public static void Build()
        {
            EnsureFolder(MaterialsDir);
            EnsureFolder(PrefabsDir);
            EnsureFolder(ScenesDir);

            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            Mats m = CreateMaterials();

            HeroController heroPrefab = CreateHeroPrefab(m);
            CreepAI meleePrefab = CreateCreepPrefab("Creep_Melee", m, ranged: false);
            CreepAI rangedPrefab = CreateCreepPrefab("Creep_Ranged", m, ranged: true);

            Vector3 laneDir = (RedCorePos - BlueCorePos).normalized;

            // --- Map geometry (everything under Map is baked into the NavMesh) ---
            var map = new GameObject("Map");
            var ground = Primitive(PrimitiveType.Plane, "Ground", map.transform, Vector3.zero, new Vector3(14f, 1f, 14f), m.Ground);
            ground.isStatic = true;
            var lane = Primitive(PrimitiveType.Cube, "Lane", map.transform, new Vector3(0f, 0.005f, 0f),
                new Vector3(LaneHalfWidth * 2f, 0.01f, Vector3.Distance(BlueCorePos, RedCorePos) + 10f), m.Lane);
            lane.transform.rotation = Quaternion.LookRotation(laneDir);
            lane.isStatic = true;
            // Base pads are decoration only: the ground below handles clicks (a cylinder's capsule collider would not).
            Visual(PrimitiveType.Cylinder, map.transform, BlueCorePos + Vector3.up * 0.01f,
                new Vector3(BaseRadius * 2f, 0.01f, BaseRadius * 2f), m.BluePad).name = "BluePad";
            Visual(PrimitiveType.Cylinder, map.transform, RedCorePos + Vector3.up * 0.01f,
                new Vector3(BaseRadius * 2f, 0.01f, BaseRadius * 2f), m.RedPad).name = "RedPad";
            ScatterRocks(map.transform, m.Rock, laneDir);

            var surface = map.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.Children;
            surface.useGeometry = NavMeshCollectGeometry.RenderMeshes;

            // --- Structures ---
            var structures = new GameObject("Structures").transform;
            Unit blueCore = CreateCore("Core_Blue", Team.Blue, BlueCorePos, structures, m);
            Unit redCore = CreateCore("Core_Red", Team.Red, RedCorePos, structures, m);

            Unit blueOuter = CreateTower("Tower_Blue_Outer", Team.Blue, LanePoint(0.33f), structures, m, null);
            Unit blueInner = CreateTower("Tower_Blue_Inner", Team.Blue, LanePoint(0.17f), structures, m, blueOuter);
            blueCore.protectedBy = blueInner;
            Unit redOuter = CreateTower("Tower_Red_Outer", Team.Red, LanePoint(0.67f), structures, m, null);
            Unit redInner = CreateTower("Tower_Red_Inner", Team.Red, LanePoint(0.83f), structures, m, redOuter);
            redCore.protectedBy = redInner;

            // --- Spawners ---
            CreepSpawner blueSpawner = CreateSpawner("Spawner_Blue", Team.Blue, BlueCorePos + laneDir * 6f,
                new[] { LanePoint(0.5f), RedCorePos }, m.Blue, meleePrefab, rangedPrefab);
            CreateSpawner("Spawner_Red", Team.Red, RedCorePos - laneDir * 6f,
                new[] { LanePoint(0.5f), BlueCorePos }, m.Red, meleePrefab, rangedPrefab);

            // --- Hero ---
            var heroSpawn = new GameObject("HeroSpawn").transform;
            heroSpawn.position = BlueCorePos - laneDir * 6f;
            var hero = (HeroController)PrefabUtility.InstantiatePrefab(heroPrefab);
            hero.transform.SetPositionAndRotation(heroSpawn.position, Quaternion.LookRotation(laneDir));
            hero.spawnPoint = heroSpawn;

            // --- Game + HUD ---
            var game = new GameObject("Game").AddComponent<GameManager>();
            game.hero = hero;
            game.playerSpawner = blueSpawner;

            var hudGo = new GameObject("HUD");
            var document = hudGo.AddComponent<UIDocument>();
            document.panelSettings = CreatePanelSettings();
            document.visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UiDir + "/Hud.uxml");
            hudGo.AddComponent<Hud>();

            var optionsGo = new GameObject("OptionsMenu");
            var optionsDocument = optionsGo.AddComponent<UIDocument>();
            optionsDocument.panelSettings = document.panelSettings;
            optionsDocument.visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UiDir + "/OptionsMenu.uxml");
            optionsDocument.sortingOrder = 10f;
            optionsGo.AddComponent<OptionsMenu>();

            // --- Camera ---
            Camera mainCam = Camera.main;
            mainCam.gameObject.AddComponent<CinemachineBrain>();
            mainCam.farClipPlane = 300f;

            var rig = new GameObject("CameraRig").AddComponent<MobaCamera>();
            rig.transform.position = hero.transform.position;

            var vcam = new GameObject("CM MobaCamera").AddComponent<CinemachineCamera>();
            Vector3 offset = new(0f, 22f, -13f);
            vcam.transform.SetPositionAndRotation(rig.transform.position + offset, Quaternion.LookRotation(-offset));
            vcam.Follow = rig.transform;
            LensSettings lens = LensSettings.Default;
            lens.FieldOfView = 40f;
            lens.FarClipPlane = 300f;
            vcam.Lens = lens;
            var follow = vcam.gameObject.AddComponent<CinemachineFollow>();
            follow.FollowOffset = offset;
            follow.TrackerSettings.BindingMode = Unity.Cinemachine.TargetTracking.BindingMode.WorldSpace;
            follow.TrackerSettings.PositionDamping = Vector3.zero;
            rig.hero = hero;
            rig.follow = follow;
            game.cameraRig = rig;

            // --- Lighting ---
            var sun = Object.FindFirstObjectByType<Light>();
            if (sun != null) sun.transform.rotation = Quaternion.Euler(55f, -35f, 0f);

            // --- Save, bake, save again ---
            EditorSceneManager.SaveScene(scene, ScenePath);
            surface.BuildNavMesh();
            AssetDatabase.DeleteAsset(NavMeshPath);
            AssetDatabase.CreateAsset(surface.navMeshData, NavMeshPath);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();

            AddSceneToBuildFirst(ScenePath);
            Debug.Log($"[LaneSliceBuilder] Built {ScenePath}");
        }

        static Vector3 LanePoint(float t) => Vector3.Lerp(BlueCorePos, RedCorePos, t);

        // ---------- Prefabs ----------

        static HeroController CreateHeroPrefab(Mats m)
        {
            GameObject go = UnitRoot("Hero", radius: 0.5f, height: 2f);
            Visual(PrimitiveType.Capsule, go.transform, new Vector3(0f, 1f, 0f), new Vector3(1f, 1f, 1f), m.Hero);
            Visual(PrimitiveType.Cube, go.transform, new Vector3(0f, 1.4f, 0.45f), new Vector3(0.3f, 0.2f, 0.3f), m.Stone);

            ConfigureUnit(go, Team.Blue, UnitKind.Hero, health: 600f, radius: 0.5f, barHeight: 2.6f);
            ConfigureAttacker(go, damage: 30f, range: 5.5f, aps: 1.2f, projectileSpeed: 28f, size: 0.35f,
                origin: new Vector3(0f, 1.4f, 0.5f), mat: m.Hero);
            ConfigureAgent(go, radius: 0.5f, height: 2f, speed: 6.5f);
            var abilities = go.AddComponent<AbilityCaster>();
            abilities.maxMana = 300f;
            abilities.manaRegenPerSecond = 2f;
            go.AddComponent<HeroProgression>();
            var hero = go.AddComponent<HeroController>();
            hero.moveMarkerMaterial = m.Marker;
            hero.attackMoveMarkerMaterial = m.AttackMarker;
            hero.rangeIndicatorMaterial = m.RangeIndicator;

            return SavePrefab(go, "Hero").GetComponent<HeroController>();
        }

        static CreepAI CreateCreepPrefab(string name, Mats m, bool ranged)
        {
            float radius = ranged ? 0.35f : 0.4f;
            float height = ranged ? 1f : 1.2f;
            GameObject go = UnitRoot(name, radius, height);
            Visual(ranged ? PrimitiveType.Cylinder : PrimitiveType.Capsule, go.transform,
                new Vector3(0f, height * 0.5f, 0f), new Vector3(radius * 2f, height * 0.5f, radius * 2f), m.Blue);

            Unit unit = ConfigureUnit(go, Team.Blue, UnitKind.Creep, health: ranged ? 80f : 130f, radius: radius, barHeight: height + 0.5f);
            unit.goldBounty = ranged ? 14 : 21;
            unit.xpBounty = ranged ? 30f : 60f;
            if (ranged)
                ConfigureAttacker(go, damage: 16f, range: 5f, aps: 0.8f, projectileSpeed: 18f, size: 0.2f,
                    origin: new Vector3(0f, height, 0.3f), mat: m.Blue);
            else
                ConfigureAttacker(go, damage: 12f, range: 0.6f, aps: 1f, projectileSpeed: 0f, size: 0f,
                    origin: Vector3.zero, mat: null);
            ConfigureAgent(go, radius, height, speed: 3.5f);
            var ai = go.AddComponent<CreepAI>();
            ai.aggroRange = 6f;

            return SavePrefab(go, name).GetComponent<CreepAI>();
        }

        // ---------- Scene objects ----------

        static Unit CreateTower(string name, Team team, Vector3 pos, Transform parent, Mats m, Unit protectedBy)
        {
            const float radius = 1.2f, height = 5f;
            GameObject go = UnitRoot(name, radius, height);
            go.transform.SetParent(parent);
            go.transform.position = pos;
            Visual(PrimitiveType.Cylinder, go.transform, new Vector3(0f, height * 0.5f, 0f),
                new Vector3(radius * 2f, height * 0.5f, radius * 2f), m.Stone);
            Visual(PrimitiveType.Cube, go.transform, new Vector3(0f, height + 0.4f, 0f), new Vector3(1.6f, 0.8f, 1.6f),
                team == Team.Blue ? m.Blue : m.Red);

            Unit unit = ConfigureUnit(go, team, UnitKind.Tower, health: 1500f, radius: radius, barHeight: height + 1.6f);
            unit.goldBounty = 150;
            unit.xpBounty = 100f;
            unit.protectedBy = protectedBy;
            Attacker attacker = ConfigureAttacker(go, damage: 85f, range: 7.5f, aps: 0.9f, projectileSpeed: 22f, size: 0.45f,
                origin: new Vector3(0f, height + 0.4f, 0f), mat: team == Team.Blue ? m.Blue : m.Red);
            attacker.faceTarget = false;
            go.AddComponent<TowerAI>();
            AddObstacle(go, radius, height);
            return unit;
        }

        static Unit CreateCore(string name, Team team, Vector3 pos, Transform parent, Mats m)
        {
            const float radius = 2.5f, height = 4f;
            GameObject go = UnitRoot(name, radius, height);
            go.transform.SetParent(parent);
            go.transform.position = pos;
            Visual(PrimitiveType.Cube, go.transform, new Vector3(0f, 1.2f, 0f), new Vector3(3.4f, 2.4f, 3.4f), m.Stone)
                .transform.localRotation = Quaternion.Euler(0f, 45f, 0f);
            Visual(PrimitiveType.Sphere, go.transform, new Vector3(0f, 3.2f, 0f), Vector3.one * 2f,
                team == Team.Blue ? m.Blue : m.Red);

            Unit unit = ConfigureUnit(go, team, UnitKind.Core, health: 2500f, radius: radius, barHeight: height + 1f);
            AddObstacle(go, radius, height);
            return unit;
        }

        static CreepSpawner CreateSpawner(string name, Team team, Vector3 pos, Vector3[] lane, Material teamMat,
            CreepAI melee, CreepAI ranged)
        {
            var spawner = new GameObject(name).AddComponent<CreepSpawner>();
            spawner.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(lane[0] - pos));
            spawner.team = team;
            spawner.teamMaterial = teamMat;
            spawner.meleePrefab = melee;
            spawner.rangedPrefab = ranged;
            spawner.lane = lane;
            return spawner;
        }

        static void ScatterRocks(Transform parent, Material mat, Vector3 laneDir)
        {
            var rocks = new GameObject("Rocks").transform;
            rocks.SetParent(parent);
            var rng = new System.Random(1337);
            float Range(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            Vector3 laneNormal = new(-laneDir.z, 0f, laneDir.x);

            int placed = 0;
            for (int attempt = 0; attempt < 400 && placed < 70; attempt++)
            {
                var pos = new Vector3(Range(-66f, 66f), 0f, Range(-66f, 66f));
                if (Mathf.Abs(Vector3.Dot(pos, laneNormal)) < LaneHalfWidth + 5f) continue;
                if (Vector3.Distance(pos, BlueCorePos) < BaseRadius + 3f || Vector3.Distance(pos, RedCorePos) < BaseRadius + 3f) continue;

                var size = new Vector3(Range(2f, 6f), Range(1.2f, 3.5f), Range(2f, 6f));
                GameObject rock = Primitive(PrimitiveType.Cube, $"Rock_{placed:00}", rocks, pos + Vector3.up * (size.y * 0.5f), size, mat);
                rock.transform.rotation = Quaternion.Euler(0f, Range(0f, 360f), 0f);
                rock.isStatic = true;
                placed++;
            }
        }

        // ---------- Helpers ----------

        static GameObject UnitRoot(string name, float radius, float height)
        {
            var go = new GameObject(name);
            var col = go.AddComponent<CapsuleCollider>();
            col.radius = radius;
            col.height = height;
            col.center = new Vector3(0f, height * 0.5f, 0f);
            return go;
        }

        static GameObject Visual(PrimitiveType type, Transform parent, Vector3 localPos, Vector3 localScale, Material mat)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.name = type.ToString();
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = localScale;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            return go;
        }

        static GameObject Primitive(PrimitiveType type, string name, Transform parent, Vector3 pos, Vector3 scale, Material mat)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            return go;
        }

        static Unit ConfigureUnit(GameObject go, Team team, UnitKind kind, float health, float radius, float barHeight)
        {
            var unit = go.AddComponent<Unit>();
            unit.team = team;
            unit.kind = kind;
            unit.maxHealth = health;
            unit.radius = radius;
            unit.barHeight = barHeight;
            unit.destroyOnDeath = kind != UnitKind.Hero;
            return unit;
        }

        static Attacker ConfigureAttacker(GameObject go, float damage, float range, float aps, float projectileSpeed,
            float size, Vector3 origin, Material mat)
        {
            var a = go.AddComponent<Attacker>();
            a.damage = damage;
            a.range = range;
            a.attacksPerSecond = aps;
            a.projectileSpeed = projectileSpeed;
            a.projectileSize = size;
            a.projectileOrigin = origin;
            a.projectileMaterial = mat;
            return a;
        }

        static void ConfigureAgent(GameObject go, float radius, float height, float speed)
        {
            var agent = go.AddComponent<NavMeshAgent>();
            agent.radius = radius;
            agent.height = height;
            agent.baseOffset = 0f;
            agent.speed = speed;
            agent.acceleration = 40f;
            agent.angularSpeed = 900f;
            agent.stoppingDistance = 0.1f;
        }

        static void AddObstacle(GameObject go, float radius, float height)
        {
            var obstacle = go.AddComponent<NavMeshObstacle>();
            obstacle.shape = NavMeshObstacleShape.Capsule;
            obstacle.radius = radius;
            obstacle.height = height;
            obstacle.center = new Vector3(0f, height * 0.5f, 0f);
            obstacle.carving = true;
        }

        static GameObject SavePrefab(GameObject go, string name)
        {
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, $"{PrefabsDir}/{name}.prefab");
            Object.DestroyImmediate(go);
            return prefab;
        }

        static Mats CreateMaterials() => new()
        {
            Ground = Mat("Ground", new Color(0.22f, 0.36f, 0.2f)),
            Lane = Mat("Lane", new Color(0.62f, 0.52f, 0.36f)),
            Rock = Mat("Rock", new Color(0.36f, 0.33f, 0.3f)),
            Stone = Mat("Stone", new Color(0.72f, 0.72f, 0.7f)),
            Blue = Mat("Team_Blue", new Color(0.2f, 0.45f, 1f)),
            Red = Mat("Team_Red", new Color(0.95f, 0.25f, 0.2f)),
            BluePad = Mat("Pad_Blue", new Color(0.2f, 0.27f, 0.42f)),
            RedPad = Mat("Pad_Red", new Color(0.42f, 0.22f, 0.2f)),
            Hero = Mat("Hero", new Color(1f, 0.82f, 0.25f)),
            Marker = Mat("MoveMarker", new Color(0.3f, 1f, 0.4f)),
            AttackMarker = Mat("AttackMoveMarker", new Color(1f, 0.3f, 0.25f)),
            RangeIndicator = UnlitVertexColorMat("RangeIndicator"),
        };

        // Line renderers tint through vertex colour, which the Lit shader ignores.
        static Material UnlitVertexColorMat(string name)
        {
            string path = $"{MaterialsDir}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.SetColor("_BaseColor", Color.white);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        static PanelSettings CreatePanelSettings()
        {
            var settings = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsPath);
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<PanelSettings>();
                AssetDatabase.CreateAsset(settings, PanelSettingsPath);
            }
            settings.themeStyleSheet = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(UiDir + "/MobaTheme.tss");
            settings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            settings.referenceResolution = new Vector2Int(1920, 1080);
            settings.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            settings.match = 0.5f;
            EditorUtility.SetDirty(settings);
            return settings;
        }

        static Material Mat(string name, Color color)
        {
            string path = $"{MaterialsDir}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.SetColor("_BaseColor", color);
            mat.SetFloat("_Smoothness", 0.15f);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        static void AddSceneToBuildFirst(string path)
        {
            List<EditorBuildSettingsScene> scenes = EditorBuildSettings.scenes.Where(s => s.path != path).ToList();
            scenes.Insert(0, new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
