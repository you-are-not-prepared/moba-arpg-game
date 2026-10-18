using System.Collections.Generic;
using System.IO;
using System.Linq;
using Moba.Abilities;
using Moba.Items;
using Moba.UI;
using Unity.AI.Navigation;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UIElements;

namespace Moba.EditorTools
{
    // Generates the whole game from code: shared assets (materials, items, creeps, heroes), one scene per map
    // layout (each with a baked NavMesh), the frontend scene (start screen + champion select) and the build list.
    // Re-running overwrites the generated assets, so tweak layouts and heroes here rather than by hand.
    public static class GameBuilder
    {
        const string Root = "Assets/_Project";
        const string MaterialsDir = Root + "/Materials";
        const string PrefabsDir = Root + "/Prefabs";
        const string ScenesDir = Root + "/Scenes";
        const string UiDir = Root + "/UI";
        const string ItemsDir = Root + "/Items";
        const string HeroesDir = Root + "/Heroes";
        const string PanelSettingsPath = UiDir + "/HudPanelSettings.asset";
        const string FrontendScenePath = ScenesDir + "/" + MatchSettings.FrontendScene + ".unity";
        const string LayoutCatalogPath = ScenesDir + "/MapLayoutCatalog.asset";
        const string ItemCatalogPath = ItemsDir + "/ItemCatalog.asset";
        const string RosterPath = HeroesDir + "/HeroRoster.asset";

        const float LaneHalfWidth = 5f;
        const float BaseRadius = 13f;
        const float MapHalfSize = 70f;

        // Vision radii (fog of war), already including the +20% bump over the original 12 / 7.5 / 10 / 12.
        const float HeroSight = 14.4f;
        const float CreepSight = 9f;
        const float TowerSight = 12f;
        const float CoreSight = 14.4f;

        // TEMPORARY for testing: every item, spell and mod costs 1g. Set to false to restore the real prices below.
        const bool TestPrices = true;
        static int Price(int realCost) => TestPrices ? 1 : realCost;

        // TEMPORARY for testing: colour inventory tiles by kind (items orange, spells blue, mods green).
        // Set to false to restore each asset's own colour below.
        const bool TestKindColors = true;
        static readonly Color TestItemColor = new(1f, 0.55f, 0.1f);
        static readonly Color TestSpellColor = new(0.2f, 0.45f, 0.95f);
        static readonly Color TestModColor = new(0.25f, 0.75f, 0.3f);

        class Mats
        {
            public Material Rock, Stone, Blue, Red, BluePad, RedPad, Marker, AttackMarker, RangeIndicator, Fire, Explosion, Arrow;
        }

        // ---------- Map layouts ----------

        class Decor
        {
            public string Name;
            public Vector3 Center;
            public float Radius;
            public Color Color;
        }

        class LayoutSpec
        {
            public string Id, Name, Description;
            public Vector3 BlueCore, RedCore;
            public List<LanePath> Lanes = new();
            public Color Ground, Lane;
            public int RockCount;
            public float RockClearance, RockMin, RockMax;
            public List<Decor> Decor = new();
            public string SceneName => $"Map_{char.ToUpperInvariant(Id[0])}{Id.Substring(1)}";
        }

        static LanePath Lane(string name, params Vector3[] points) => new() { name = name, points = points };

        static Vector3 P(float x, float z) => new(x, 0f, z);

        static List<LayoutSpec> Layouts() => new()
        {
            new LayoutSpec
            {
                Id = "classic", Name = "Classic",
                Description = "Three lanes (top, middle, bottom) with jungle between them. Two towers per lane per team; " +
                              "losing any lane's inner tower exposes the core.",
                BlueCore = P(-48, -48), RedCore = P(48, 48),
                Lanes =
                {
                    Lane("Top", P(-48, -48), P(-58, -30), P(-58, 50), P(-50, 58), P(30, 58), P(48, 48)),
                    Lane("Mid", P(-48, -48), P(48, 48)),
                    Lane("Bot", P(-48, -48), P(-30, -58), P(50, -58), P(58, -50), P(58, 30), P(48, 48)),
                },
                Ground = new Color(0.22f, 0.36f, 0.2f), Lane = new Color(0.62f, 0.52f, 0.36f),
                RockCount = 140, RockClearance = 4f, RockMin = 2f, RockMax = 6f,
            },
            new LayoutSpec
            {
                Id = "zones", Name = "Zones",
                Description = "Two lanes bending through four zones and past an open centre. " +
                              "Zone modifiers will come later; for now the zones are just terrain.",
                BlueCore = P(-55, 0), RedCore = P(55, 0),
                Lanes =
                {
                    Lane("North", P(-55, 0), P(-40, 22), P(-18, 30), P(0, 16), P(18, 30), P(40, 22), P(55, 0)),
                    Lane("South", P(-55, 0), P(-40, -22), P(-18, -30), P(0, -16), P(18, -30), P(40, -22), P(55, 0)),
                },
                Ground = new Color(0.24f, 0.33f, 0.24f), Lane = new Color(0.6f, 0.53f, 0.4f),
                RockCount = 110, RockClearance = 4f, RockMin = 2f, RockMax = 6f,
                Decor =
                {
                    new Decor { Name = "Zone_Fork", Center = P(-28, 32), Radius = 13f, Color = new Color(0.22f, 0.42f, 0.36f) },
                    new Decor { Name = "Zone_Volley", Center = P(28, 32), Radius = 13f, Color = new Color(0.24f, 0.34f, 0.46f) },
                    new Decor { Name = "Zone_Ember", Center = P(-28, -32), Radius = 13f, Color = new Color(0.46f, 0.3f, 0.2f) },
                    new Decor { Name = "Zone_Frost", Center = P(28, -32), Radius = 13f, Color = new Color(0.36f, 0.44f, 0.5f) },
                    new Decor { Name = "Centre", Center = P(0, 0), Radius = 11f, Color = new Color(0.42f, 0.4f, 0.36f) },
                },
            },
            new LayoutSpec
            {
                Id = "delve", Name = "Delve",
                Description = "One lane down a mine shaft to the deep core and back up the other side. " +
                              "Rock walls on every side; the fights happen in the tunnel.",
                BlueCore = P(-50, 50), RedCore = P(50, 50),
                Lanes =
                {
                    Lane("Shaft", P(-50, 50), P(-32, 26), P(-44, -4), P(-14, -44), P(0, -52),
                        P(14, -44), P(44, -4), P(32, 26), P(50, 50)),
                },
                Ground = new Color(0.17f, 0.16f, 0.18f), Lane = new Color(0.42f, 0.36f, 0.3f),
                RockCount = 320, RockClearance = 2.5f, RockMin = 3f, RockMax = 7f,
                Decor =
                {
                    new Decor { Name = "DeepCore", Center = P(0, -52), Radius = 12f, Color = new Color(0.3f, 0.26f, 0.34f) },
                },
            },
        };

        // ---------- Heroes ----------

        class HeroSpec
        {
            public string Id, Name, Title, Glyph, Description;
            public Color Color;
            public float Health, Damage, Range, AttacksPerSecond, Speed, Mana;
            public string PassiveName, PassiveDescription;
            public int ExtraProjectiles;
            public float CooldownMultiplier = 1f, ItemStatMultiplier = 1f;
            public System.Func<Mats, AbilityDefinition> Ultimate;
        }

        static List<HeroSpec> Heroes() => new()
        {
            new HeroSpec
            {
                Id = "kael", Name = "Kael", Title = "Ranger", Glyph = "K", Color = new Color(0.35f, 0.78f, 0.45f),
                Description = "Floods the lane with projectiles and pins enemies down from long range.",
                Health = 560f, Damage = 32f, Range = 6.5f, AttacksPerSecond = 1.25f, Speed = 6.6f, Mana = 300f,
                PassiveName = "Split Shot", PassiveDescription = "Your projectile spells fire 1 extra projectile.",
                ExtraProjectiles = 1,
                Ultimate = m => SkybreakerArrow(m),
            },
            new HeroSpec
            {
                Id = "vesh", Name = "Vesh", Title = "Chronomancer", Glyph = "V", Color = new Color(0.65f, 0.45f, 0.95f),
                Description = "Casts more often than anyone, then does it all again.",
                Health = 580f, Damage = 27f, Range = 5.5f, AttacksPerSecond = 1.1f, Speed = 6.5f, Mana = 400f,
                PassiveName = "Quickened Mind", PassiveDescription = "All your cooldowns are 20% shorter.",
                CooldownMultiplier = 0.8f,
                Ultimate = _ => Rewind(),
            },
            new HeroSpec
            {
                Id = "brann", Name = "Brann", Title = "Artificer", Glyph = "B", Color = new Color(0.85f, 0.55f, 0.25f),
                Description = "Gets more out of every item and every mod.",
                Health = 660f, Damage = 30f, Range = 5f, AttacksPerSecond = 1.15f, Speed = 6.4f, Mana = 300f,
                PassiveName = "Masterwork Gear", PassiveDescription = "Items give 10% more stats.",
                ItemStatMultiplier = 1.1f,
                Ultimate = _ => OverchargedSockets(),
            },
        };

        static AbilityDefinition SkybreakerArrow(Mats m)
        {
            var spell = LoadOrCreate<ProjectileSpell>($"{HeroesDir}/Ult_SkybreakerArrow.asset");
            spell.displayName = "Skybreaker Arrow";
            spell.description = "Looses a huge arrow that flies across the map, piercing and stunning every enemy it passes.";
            spell.cost = 0;
            spell.glyph = "SA";
            spell.color = new Color(0.35f, 0.78f, 0.45f);
            spell.tags = SpellTags.Projectile;
            spell.targeting = AbilityTargeting.Point;
            spell.range = 0f;
            spell.cooldownByRank = new[] { 60f, 50f, 40f };
            spell.manaCostByRank = new[] { 100f };
            spell.baseDamage = 200f;
            spell.damagePerRank = 100f;
            spell.explosionRadius = 0f;
            spell.pierce = true;
            spell.stunDuration = 1.5f;
            spell.projectileSpeed = 26f;
            spell.maxDistance = 32f;
            spell.projectileSize = 1.4f;
            spell.spreadAngle = 8f;
            spell.projectileMaterial = m.Arrow;
            spell.explosionMaterial = m.Explosion;
            EditorUtility.SetDirty(spell);
            return spell;
        }

        static AbilityDefinition Rewind()
        {
            var spell = LoadOrCreate<CooldownResetSpell>($"{HeroesDir}/Ult_Rewind.asset");
            spell.displayName = "Rewind";
            spell.description = "Instantly resets the cooldowns of your Q, W and E spells.";
            spell.cost = 0;
            spell.glyph = "RW";
            spell.color = new Color(0.65f, 0.45f, 0.95f);
            spell.targeting = AbilityTargeting.Instant;
            spell.range = 0f;
            spell.cooldownByRank = new[] { 70f, 60f, 50f };
            spell.manaCostByRank = new[] { 80f };
            EditorUtility.SetDirty(spell);
            return spell;
        }

        static AbilityDefinition OverchargedSockets()
        {
            var spell = LoadOrCreate<ExtraSocketUltimate>($"{HeroesDir}/Ult_OverchargedSockets.asset");
            spell.displayName = "Overcharged Sockets";
            spell.description = "Passive once learned: Q, W and E each get a 4th mod socket.";
            spell.cost = 0;
            spell.glyph = "OS";
            spell.color = new Color(0.85f, 0.55f, 0.25f);
            spell.targeting = AbilityTargeting.Instant;
            spell.bonusSockets = 1;
            spell.cooldownByRank = new[] { 0f };
            spell.manaCostByRank = new[] { 0f };
            EditorUtility.SetDirty(spell);
            return spell;
        }

        // ---------- Entry points ----------

        [MenuItem("MOBA/Rebuild All Scenes")]
        public static void BuildAllFromMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            BuildAll();
        }

        public static void BuildAll()
        {
            foreach (string dir in new[] { MaterialsDir, PrefabsDir, ScenesDir, ItemsDir, HeroesDir })
                EnsureFolder(dir);

            // Shared assets, saved to disk first. A throwaway scene hosts the temporary objects prefab building creates.
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Mats m = CreateMaterials();
            CreateItems(m);
            CreateCreepPrefab("Creep_Melee", m, ranged: false);
            CreateCreepPrefab("Creep_Ranged", m, ranged: true);
            CreateHeroes(m);
            CreatePanelSettings();

            List<LayoutSpec> specs = Layouts();
            var layoutCatalog = LoadOrCreate<MapLayoutCatalog>(LayoutCatalogPath);
            layoutCatalog.layouts = specs.Select(Info).ToList();
            EditorUtility.SetDirty(layoutCatalog);
            AssetDatabase.SaveAssets();

            // Each scene loads its assets fresh: opening a new scene unloads assets the previous one held,
            // which turns references kept from before into nulls.
            var scenePaths = new List<string> { FrontendScenePath };
            foreach (LayoutSpec spec in specs) scenePaths.Add(BuildMapScene(spec));
            BuildFrontendScene();

            // The single-lane slice scene this replaces.
            AssetDatabase.DeleteAsset($"{ScenesDir}/Lane.unity");
            AssetDatabase.DeleteAsset($"{ScenesDir}/Lane_NavMesh.asset");

            EditorBuildSettings.scenes = scenePaths.Select(p => new EditorBuildSettingsScene(p, true)).ToArray();
            AssetDatabase.SaveAssets();
            EditorSceneManager.OpenScene(FrontendScenePath);
            Debug.Log($"[GameBuilder] Built {scenePaths.Count} scenes: {string.Join(", ", scenePaths.Select(Path.GetFileNameWithoutExtension))}");
        }

        static MapLayoutInfo Info(LayoutSpec spec) => new()
        {
            id = spec.Id,
            displayName = spec.Name,
            description = spec.Description,
            sceneName = spec.SceneName,
            blueCore = spec.BlueCore,
            redCore = spec.RedCore,
            lanes = spec.Lanes.Select(l => new LanePath { name = l.name, points = l.points.ToArray() }).ToArray(),
        };

        // ---------- Map scenes ----------

        static string BuildMapScene(LayoutSpec spec)
        {
            string scenePath = $"{ScenesDir}/{spec.SceneName}.unity";
            string navMeshPath = $"{ScenesDir}/{spec.SceneName}_NavMesh.asset";
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            Mats m = CreateMaterials();
            var catalog = AssetDatabase.LoadAssetAtPath<ItemCatalog>(ItemCatalogPath);
            var roster = AssetDatabase.LoadAssetAtPath<HeroRoster>(RosterPath);
            var panel = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsPath);
            var melee = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabsDir}/Creep_Melee.prefab").GetComponent<CreepAI>();
            var ranged = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabsDir}/Creep_Ranged.prefab").GetComponent<CreepAI>();
            if (catalog == null || roster == null || panel == null || melee == null || ranged == null)
                Debug.LogError($"[GameBuilder] {spec.SceneName}: a shared asset failed to load; the scene will have missing references.");

            // --- Map geometry (everything under Map is baked into the NavMesh) ---
            var map = new GameObject("Map");
            Primitive(PrimitiveType.Plane, "Ground", map.transform, Vector3.zero, new Vector3(MapHalfSize / 5f, 1f, MapHalfSize / 5f),
                Mat($"Ground_{spec.Id}", spec.Ground)).isStatic = true;
            BuildLanes(spec, map.transform, Mat($"Lane_{spec.Id}", spec.Lane));
            foreach (Decor d in spec.Decor)
                Visual(PrimitiveType.Cylinder, map.transform, d.Center + Vector3.up * 0.006f,
                    new Vector3(d.Radius * 2f, 0.01f, d.Radius * 2f), Mat($"Decor_{d.Name}", d.Color)).name = d.Name;
            Visual(PrimitiveType.Cylinder, map.transform, spec.BlueCore + Vector3.up * 0.01f,
                new Vector3(BaseRadius * 2f, 0.01f, BaseRadius * 2f), m.BluePad).name = "BluePad";
            Visual(PrimitiveType.Cylinder, map.transform, spec.RedCore + Vector3.up * 0.01f,
                new Vector3(BaseRadius * 2f, 0.01f, BaseRadius * 2f), m.RedPad).name = "RedPad";
            Transform rocks = ScatterRocks(spec, map.transform, m.Rock);

            var surface = map.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.Children;
            surface.useGeometry = NavMeshCollectGeometry.RenderMeshes;

            // --- Structures: two towers per lane per team; any lane's inner tower falling exposes the core ---
            var structures = new GameObject("Structures").transform;
            Unit blueCore = CreateCore("Core_Blue", Team.Blue, spec.BlueCore, structures, m);
            Unit redCore = CreateCore("Core_Red", Team.Red, spec.RedCore, structures, m);
            var blueInners = new List<Unit>();
            var redInners = new List<Unit>();
            foreach (LanePath lane in spec.Lanes)
            {
                Unit blueOuter = CreateTower($"Tower_Blue_{lane.name}_Outer", Team.Blue, PointAlong(lane.points, 0.33f), structures, m);
                Unit blueInner = CreateTower($"Tower_Blue_{lane.name}_Inner", Team.Blue, PointAlong(lane.points, 0.17f), structures, m);
                blueInner.protectedBy = new[] { blueOuter };
                blueInners.Add(blueInner);
                Unit redOuter = CreateTower($"Tower_Red_{lane.name}_Outer", Team.Red, PointAlong(lane.points, 0.67f), structures, m);
                Unit redInner = CreateTower($"Tower_Red_{lane.name}_Inner", Team.Red, PointAlong(lane.points, 0.83f), structures, m);
                redInner.protectedBy = new[] { redOuter };
                redInners.Add(redInner);
            }
            blueCore.protectedBy = blueInners.ToArray();
            redCore.protectedBy = redInners.ToArray();

            // --- Creep spawners: one per lane per team ---
            CreepSpawner playerSpawner = null;
            foreach (LanePath lane in spec.Lanes)
            {
                Vector3[] forward = lane.points;
                Vector3[] backward = lane.points.Reverse().ToArray();
                CreepSpawner blue = CreateSpawner($"Spawner_Blue_{lane.name}", Team.Blue, forward, m.Blue, melee, ranged);
                CreateSpawner($"Spawner_Red_{lane.name}", Team.Red, backward, m.Red, melee, ranged);
                if (playerSpawner == null || lane.name == "Mid") playerSpawner = blue;
            }

            // --- Hero spawn (the hero itself is spawned at runtime by MatchBootstrap) ---
            Vector3 outward = Flat(spec.BlueCore).normalized;
            var heroSpawn = new GameObject("HeroSpawn").transform;
            heroSpawn.SetPositionAndRotation(spec.BlueCore + outward * 6f, Quaternion.LookRotation(-outward));

            // --- Shops (buy/sell only inside the base) ---
            Shop blueShop = CreateShop("Shop_Blue", Team.Blue, spec.BlueCore, catalog, m.Blue, m);
            Shop redShop = CreateShop("Shop_Red", Team.Red, spec.RedCore, catalog, m.Red, m);

            // --- Game, layout, fog, HUD ---
            var game = new GameObject("Game").AddComponent<GameManager>();
            game.playerSpawner = playerSpawner;
            game.playerShop = blueShop;
            game.gameObject.AddComponent<MapLayout>().info = Info(spec);

            CreateFogOfWar(new[] { rocks, structures, blueShop.transform, redShop.transform });

            var hudGo = new GameObject("HUD");
            var document = hudGo.AddComponent<UIDocument>();
            document.panelSettings = panel;
            document.visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UiDir + "/Hud.uxml");
            hudGo.AddComponent<Hud>();

            var optionsGo = new GameObject("OptionsMenu");
            var optionsDocument = optionsGo.AddComponent<UIDocument>();
            optionsDocument.panelSettings = panel;
            optionsDocument.visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UiDir + "/OptionsMenu.uxml");
            optionsDocument.sortingOrder = 10f;
            optionsGo.AddComponent<OptionsMenu>();

            // --- Camera ---
            Camera mainCam = Camera.main;
            mainCam.gameObject.AddComponent<CinemachineBrain>();
            mainCam.farClipPlane = 300f;

            var rig = new GameObject("CameraRig").AddComponent<MobaCamera>();
            rig.transform.position = heroSpawn.position;
            rig.boundsMin = new Vector2(-MapHalfSize + 5f, -MapHalfSize + 5f);
            rig.boundsMax = new Vector2(MapHalfSize - 5f, MapHalfSize - 5f);

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
            rig.follow = follow;
            game.cameraRig = rig;

            var bootstrap = new GameObject("MatchBootstrap").AddComponent<MatchBootstrap>();
            bootstrap.roster = roster;
            bootstrap.heroSpawn = heroSpawn;
            bootstrap.game = game;
            bootstrap.cameraRig = rig;

            // --- Lighting ---
            var sun = Object.FindFirstObjectByType<Light>();
            if (sun != null) sun.transform.rotation = Quaternion.Euler(55f, -35f, 0f);

            // --- Save, bake, save again ---
            EditorSceneManager.SaveScene(scene, scenePath);
            surface.BuildNavMesh();
            AssetDatabase.DeleteAsset(navMeshPath);
            AssetDatabase.CreateAsset(surface.navMeshData, navMeshPath);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            return scenePath;
        }

        static void BuildLanes(LayoutSpec spec, Transform parent, Material mat)
        {
            var lanes = new GameObject("Lanes").transform;
            lanes.SetParent(parent, false);
            foreach (LanePath lane in spec.Lanes)
            {
                Vector3[] pts = lane.points;
                for (int i = 0; i < pts.Length - 1; i++)
                {
                    Vector3 a = pts[i], b = pts[i + 1];
                    GameObject strip = Primitive(PrimitiveType.Cube, $"{lane.name}_{i}", lanes, (a + b) * 0.5f + Vector3.up * 0.005f,
                        new Vector3(LaneHalfWidth * 2f, 0.01f, Vector3.Distance(a, b)), mat);
                    strip.transform.rotation = Quaternion.LookRotation(b - a);
                    strip.isStatic = true;
                }
                // Round joints so bends don't show gaps.
                for (int i = 1; i < pts.Length - 1; i++)
                    Visual(PrimitiveType.Cylinder, lanes, pts[i] + Vector3.up * 0.005f,
                        new Vector3(LaneHalfWidth * 2f, 0.005f, LaneHalfWidth * 2f), mat).name = $"{lane.name}_joint_{i}";
            }
        }

        static Transform ScatterRocks(LayoutSpec spec, Transform parent, Material mat)
        {
            var rocks = new GameObject("Rocks").transform;
            rocks.SetParent(parent);
            // Seeded from the layout id (string.GetHashCode isn't stable between runs), so rocks never move on a rebuild.
            var rng = new System.Random(spec.Id.Aggregate(17, (h, c) => unchecked(h * 31 + c)) & 0x7fffffff);
            float Range(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            float limit = MapHalfSize - 4f;

            int placed = 0;
            for (int attempt = 0; attempt < spec.RockCount * 8 && placed < spec.RockCount; attempt++)
            {
                var pos = new Vector3(Range(-limit, limit), 0f, Range(-limit, limit));
                var size = new Vector3(Range(spec.RockMin, spec.RockMax), Range(1.2f, 3.5f), Range(spec.RockMin, spec.RockMax));
                float footprint = Mathf.Max(size.x, size.z) * 0.6f;
                if (DistanceToLanes(spec, pos) < LaneHalfWidth + spec.RockClearance + footprint) continue;
                if (Vector3.Distance(pos, spec.BlueCore) < BaseRadius + 3f + footprint
                    || Vector3.Distance(pos, spec.RedCore) < BaseRadius + 3f + footprint) continue;
                if (spec.Decor.Any(d => Vector3.Distance(pos, d.Center) < d.Radius + footprint)) continue;

                GameObject rock = Primitive(PrimitiveType.Cube, $"Rock_{placed:000}", rocks, pos + Vector3.up * (size.y * 0.5f), size, mat);
                rock.transform.rotation = Quaternion.Euler(0f, Range(0f, 360f), 0f);
                rock.isStatic = true;
                placed++;
            }
            return rocks;
        }

        static float DistanceToLanes(LayoutSpec spec, Vector3 p)
        {
            float best = float.MaxValue;
            foreach (LanePath lane in spec.Lanes)
                for (int i = 0; i < lane.points.Length - 1; i++)
                    best = Mathf.Min(best, DistanceToSegment(p, lane.points[i], lane.points[i + 1]));
            return best;
        }

        static float DistanceToSegment(Vector3 p, Vector3 a, Vector3 b)
        {
            Vector3 ab = Flat(b - a), ap = Flat(p - a);
            float t = Mathf.Clamp01(Vector3.Dot(ap, ab) / Mathf.Max(ab.sqrMagnitude, 0.0001f));
            return (ap - ab * t).magnitude;
        }

        // Point at fraction t (0..1) of a polyline's length.
        static Vector3 PointAlong(Vector3[] pts, float t)
        {
            float total = 0f;
            for (int i = 0; i < pts.Length - 1; i++) total += Vector3.Distance(pts[i], pts[i + 1]);
            return PointAtDistance(pts, total * t, out _);
        }

        static Vector3 PointAtDistance(Vector3[] pts, float distance, out int segment)
        {
            for (int i = 0; i < pts.Length - 1; i++)
            {
                float len = Vector3.Distance(pts[i], pts[i + 1]);
                if (distance <= len)
                {
                    segment = i;
                    return Vector3.Lerp(pts[i], pts[i + 1], len > 0f ? distance / len : 0f);
                }
                distance -= len;
            }
            segment = pts.Length - 2;
            return pts[^1];
        }

        static Vector3 Flat(Vector3 v) => new(v.x, 0f, v.z);

        // ---------- Frontend ----------

        static void BuildFrontendScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            var roster = AssetDatabase.LoadAssetAtPath<HeroRoster>(RosterPath);
            var layouts = AssetDatabase.LoadAssetAtPath<MapLayoutCatalog>(LayoutCatalogPath);
            var panel = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsPath);
            if (roster == null || layouts == null || panel == null)
                Debug.LogError("[GameBuilder] Frontend: a shared asset failed to load; the scene will have missing references.");

            Camera cam = Camera.main;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.047f, 0.059f, 0.086f);

            var go = new GameObject("Frontend");
            var document = go.AddComponent<UIDocument>();
            document.panelSettings = panel;
            document.visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UiDir + "/Frontend.uxml");
            var frontend = go.AddComponent<FrontendController>();
            frontend.roster = roster;
            frontend.layouts = layouts;

            EditorSceneManager.SaveScene(scene, FrontendScenePath);
        }

        // ---------- Prefabs ----------

        static HeroRoster CreateHeroes(Mats m)
        {
            var roster = LoadOrCreate<HeroRoster>(RosterPath);
            roster.heroes = new List<HeroDefinition>();
            foreach (HeroSpec spec in Heroes())
            {
                var def = LoadOrCreate<HeroDefinition>($"{HeroesDir}/Hero_{spec.Name}.asset");
                def.id = spec.Id;
                def.displayName = spec.Name;
                def.title = spec.Title;
                def.description = spec.Description;
                def.color = spec.Color;
                def.glyph = spec.Glyph;
                def.passiveName = spec.PassiveName;
                def.passiveDescription = spec.PassiveDescription;
                def.extraProjectiles = spec.ExtraProjectiles;
                def.cooldownMultiplier = spec.CooldownMultiplier;
                def.itemStatMultiplier = spec.ItemStatMultiplier;
                def.ultimate = spec.Ultimate(m);
                def.prefab = CreateHeroPrefab(spec, m);
                EditorUtility.SetDirty(def);
                roster.heroes.Add(def);
            }
            EditorUtility.SetDirty(roster);
            return roster;
        }

        static HeroController CreateHeroPrefab(HeroSpec spec, Mats m)
        {
            Material body = Mat($"Hero_{spec.Name}", spec.Color);
            GameObject go = UnitRoot($"Hero_{spec.Name}", radius: 0.5f, height: 2f);
            Visual(PrimitiveType.Capsule, go.transform, new Vector3(0f, 1f, 0f), new Vector3(1f, 1f, 1f), body);
            Visual(PrimitiveType.Cube, go.transform, new Vector3(0f, 1.4f, 0.45f), new Vector3(0.3f, 0.2f, 0.3f), m.Stone);

            ConfigureUnit(go, Team.Blue, UnitKind.Hero, health: spec.Health, radius: 0.5f, barHeight: 2.6f).sightRadius = HeroSight;
            ConfigureAttacker(go, damage: spec.Damage, range: spec.Range, aps: spec.AttacksPerSecond, projectileSpeed: 28f,
                size: 0.35f, origin: new Vector3(0f, 1.4f, 0.5f), mat: body);
            ConfigureAgent(go, radius: 0.5f, height: 2f, speed: spec.Speed);
            // No turn rate: instant facing plus near-instant velocity changes.
            go.GetComponent<NavMeshAgent>().acceleration = 1000f;
            go.AddComponent<InstantTurn>();
            go.AddComponent<Inventory>();
            var abilities = go.AddComponent<AbilityCaster>();
            abilities.maxMana = spec.Mana;
            abilities.manaRegenPerSecond = 2f;
            go.AddComponent<HeroProgression>();
            var hero = go.AddComponent<HeroController>();
            hero.moveMarkerMaterial = m.Marker;
            hero.attackMoveMarkerMaterial = m.AttackMarker;
            hero.rangeIndicatorMaterial = m.RangeIndicator;

            return SavePrefab(go, $"Hero_{spec.Name}").GetComponent<HeroController>();
        }

        static CreepAI CreateCreepPrefab(string name, Mats m, bool ranged)
        {
            float radius = ranged ? 0.35f : 0.4f;
            float height = ranged ? 1f : 1.2f;
            GameObject go = UnitRoot(name, radius, height);
            Visual(ranged ? PrimitiveType.Cylinder : PrimitiveType.Capsule, go.transform,
                new Vector3(0f, height * 0.5f, 0f), new Vector3(radius * 2f, height * 0.5f, radius * 2f), m.Blue);

            Unit unit = ConfigureUnit(go, Team.Blue, UnitKind.Creep, health: ranged ? 80f : 130f, radius: radius, barHeight: height + 0.5f);
            unit.sightRadius = CreepSight;
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

        static Unit CreateTower(string name, Team team, Vector3 pos, Transform parent, Mats m)
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
            unit.sightRadius = TowerSight;
            unit.goldBounty = 150;
            unit.xpBounty = 100f;
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
            unit.sightRadius = CoreSight;
            AddObstacle(go, radius, height);
            return unit;
        }

        // Creeps spawn a few metres down the lane from their core and follow it to the enemy core.
        static CreepSpawner CreateSpawner(string name, Team team, Vector3[] lane, Material teamMat, CreepAI melee, CreepAI ranged)
        {
            Vector3 start = PointAtDistance(lane, 6f, out int segment);
            Vector3[] waypoints = lane.Skip(segment + 1).ToArray();
            var spawner = new GameObject(name).AddComponent<CreepSpawner>();
            spawner.transform.SetPositionAndRotation(start, Quaternion.LookRotation(Flat(waypoints[0] - start)));
            spawner.team = team;
            spawner.teamMaterial = teamMat;
            spawner.meleePrefab = melee;
            spawner.rangedPrefab = ranged;
            spawner.lane = waypoints;
            return spawner;
        }

        // Ground overlay just above the lane/pads; tall props (rocks, structures, shops) are dimmed instead.
        static FogOfWar CreateFogOfWar(Transform[] tintRoots)
        {
            var fog = new GameObject("FogOfWar").AddComponent<FogOfWar>();
            fog.worldMin = new Vector2(-MapHalfSize, -MapHalfSize);
            fog.worldMax = new Vector2(MapHalfSize, MapHalfSize);
            fog.tintRoots = tintRoots;

            GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = "FogOverlay";
            Object.DestroyImmediate(quad.GetComponent<Collider>());
            quad.transform.SetParent(fog.transform, false);
            quad.transform.SetPositionAndRotation(new Vector3(0f, 0.12f, 0f), Quaternion.Euler(90f, 0f, 0f));
            quad.transform.localScale = new Vector3(MapHalfSize * 2f, MapHalfSize * 2f, 1f);
            var renderer = quad.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = FogMaterial();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.enabled = false; // FogOfWar enables it at runtime, so the scene isn't fogged while editing
            fog.overlay = renderer;
            return fog;
        }

        static Material FogMaterial()
        {
            string path = $"{MaterialsDir}/FogOverlay.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Moba/FogOverlay"));
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.shader = Shader.Find("Moba/FogOverlay");
            EditorUtility.SetDirty(mat);
            return mat;
        }

        // Shop range is centred on the base; the stall is decoration (no colliders, so it never blocks clicks).
        static Shop CreateShop(string name, Team team, Vector3 basePos, ItemCatalog catalog, Material teamMat, Mats m)
        {
            Vector3 outward = Flat(basePos).normalized;
            Vector3 side = new(-outward.z, 0f, outward.x);
            Vector3 stallPos = basePos + side * 7f;

            var shop = new GameObject(name).AddComponent<Shop>();
            shop.transform.position = basePos;
            shop.team = team;
            shop.catalog = catalog;
            shop.radius = BaseRadius + 1f;

            var stall = new GameObject("Stall").transform;
            stall.SetParent(shop.transform, false);
            stall.position = stallPos;
            stall.rotation = Quaternion.LookRotation(basePos - stallPos);
            Visual(PrimitiveType.Cube, stall, new Vector3(0f, 0.5f, 0f), new Vector3(2.6f, 1f, 1.2f), m.Stone);
            Visual(PrimitiveType.Cube, stall, new Vector3(-1.1f, 1.4f, -0.4f), new Vector3(0.15f, 1.8f, 0.15f), m.Stone);
            Visual(PrimitiveType.Cube, stall, new Vector3(1.1f, 1.4f, -0.4f), new Vector3(0.15f, 1.8f, 0.15f), m.Stone);
            Visual(PrimitiveType.Cube, stall, new Vector3(0f, 2.4f, 0f), new Vector3(3f, 0.25f, 1.8f), teamMat);
            Visual(PrimitiveType.Sphere, stall, new Vector3(0f, 1.25f, 0.1f), new Vector3(0.6f, 0.5f, 0.6f), m.Explosion);
            return shop;
        }

        // ---------- Items, spells, mods ----------

        // Placeholder catalog: AD items, one spell and three mods. Re-running keeps asset GUIDs (assets are updated in place).
        static ItemCatalog CreateItems(Mats m)
        {
            var items = new List<InventoryItem>
            {
                Item("IronBlade", "Iron Blade", 350, 10f, "IB", new Color(0.55f, 0.6f, 0.68f),
                    "A plain, dependable blade."),
                Item("WarPick", "War Pick", 875, 25f, "WP", new Color(0.62f, 0.45f, 0.3f),
                    "Heavy head, heavier hits."),
                Item("Greatsword", "Greatsword", 1300, 40f, "GS", new Color(0.35f, 0.45f, 0.75f),
                    "Two hands, no regrets."),
                Item("Executioner", "Executioner", 3000, 70f, "EX", new Color(0.75f, 0.25f, 0.25f),
                    "The finishing blow, sharpened."),
                Fireball(m),
                Mod("Mod_Volley", "Volley", 500, "VO", new Color(0.3f, 0.55f, 0.85f),
                    "The spell fires 2 extra projectiles in a fan.",
                    SpellTags.Projectile, mod => mod.extraProjectiles = 2),
                Mod("Mod_Fork", "Fork", 450, "FK", new Color(0.35f, 0.7f, 0.45f),
                    "Projectiles split in two when they hit a target, flying off at a fixed angle.",
                    SpellTags.Projectile, mod =>
                    {
                        mod.forkCount = 1;
                        mod.forkAngle = 30f;
                    }),
                Mod("Mod_Searing", "Searing", 550, "SE", new Color(0.85f, 0.4f, 0.15f),
                    "Hits deal more damage and set targets on fire.",
                    SpellTags.None, mod =>
                    {
                        mod.damageMultiplier = 1.3f;
                        mod.burnFraction = 0.6f;
                        mod.burnDuration = 3f;
                    }),
            };

            var catalog = LoadOrCreate<ItemCatalog>(ItemCatalogPath);
            catalog.items = items;
            EditorUtility.SetDirty(catalog);
            return catalog;
        }

        static ItemDefinition Item(string id, string displayName, int cost, float attackDamage, string glyph, Color color,
            string description)
        {
            var item = LoadOrCreate<ItemDefinition>($"{ItemsDir}/{id}.asset");
            item.displayName = displayName;
            item.cost = Price(cost);
            item.attackDamage = attackDamage;
            item.glyph = glyph;
            item.color = TestKindColors ? TestItemColor : color;
            item.description = description;
            EditorUtility.SetDirty(item);
            return item;
        }

        static ProjectileSpell Fireball(Mats m)
        {
            var spell = LoadOrCreate<ProjectileSpell>($"{ItemsDir}/Spell_Fireball.asset");
            spell.displayName = "Fireball";
            spell.description = "Hurls a fireball that explodes on the first enemy it hits.";
            spell.cost = Price(450);
            spell.glyph = "FB";
            spell.color = TestKindColors ? TestSpellColor : new Color(0.9f, 0.42f, 0.1f);
            spell.tags = SpellTags.Projectile | SpellTags.AreaOfEffect | SpellTags.Fire;
            spell.targeting = AbilityTargeting.Point;
            spell.range = 0f; // skillshot: fires immediately toward the clicked point
            spell.cooldownByRank = new[] { 1.5f, 1.4f, 1.3f, 1.2f, 1f };
            spell.manaCostByRank = new[] { 30f, 32f, 34f, 36f, 38f };
            spell.baseDamage = 60f;
            spell.damagePerRank = 25f;
            spell.explosionRadius = 2.5f;
            spell.pierce = false;
            spell.stunDuration = 0f;
            spell.projectileSpeed = 18f;
            spell.maxDistance = 14f;
            spell.projectileSize = 0.6f;
            spell.spreadAngle = 15f;
            spell.projectileMaterial = m.Fire;
            spell.explosionMaterial = m.Explosion;
            EditorUtility.SetDirty(spell);
            return spell;
        }

        static ModDefinition Mod(string id, string displayName, int cost, string glyph, Color color, string description,
            SpellTags requiredTags, System.Action<ModDefinition> configure)
        {
            var mod = LoadOrCreate<ModDefinition>($"{ItemsDir}/{id}.asset");
            mod.displayName = displayName;
            mod.description = description;
            mod.cost = Price(cost);
            mod.glyph = glyph;
            mod.color = TestKindColors ? TestModColor : color;
            mod.requiredTags = requiredTags;
            // Reset effect fields so re-running reflects only what `configure` sets.
            mod.extraProjectiles = 0;
            mod.forkCount = 0;
            mod.forkAngle = 30f;
            mod.damageMultiplier = 1f;
            mod.burnFraction = 0f;
            mod.burnDuration = 3f;
            configure(mod);
            EditorUtility.SetDirty(mod);
            return mod;
        }

        // ---------- Helpers ----------

        static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null) return asset;
            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

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
            Rock = Mat("Rock", new Color(0.36f, 0.33f, 0.3f)),
            Stone = Mat("Stone", new Color(0.72f, 0.72f, 0.7f)),
            Blue = Mat("Team_Blue", new Color(0.2f, 0.45f, 1f)),
            Red = Mat("Team_Red", new Color(0.95f, 0.25f, 0.2f)),
            BluePad = Mat("Pad_Blue", new Color(0.2f, 0.27f, 0.42f)),
            RedPad = Mat("Pad_Red", new Color(0.42f, 0.22f, 0.2f)),
            Marker = Mat("MoveMarker", new Color(0.3f, 1f, 0.4f)),
            AttackMarker = Mat("AttackMoveMarker", new Color(1f, 0.3f, 0.25f)),
            Fire = EmissiveMat("Fire", new Color(1f, 0.45f, 0.1f), 2.5f),
            Explosion = EmissiveMat("FireExplosion", new Color(1f, 0.6f, 0.15f), 1.5f),
            Arrow = EmissiveMat("Arrow", new Color(0.5f, 1f, 0.6f), 2f),
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

        static Material EmissiveMat(string name, Color color, float intensity)
        {
            Material mat = Mat(name, color);
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", color * intensity);
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            EditorUtility.SetDirty(mat);
            return mat;
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
    }
}
