using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// Validates static data registries — VineNodeRegistry, VineWaveRegistry,
    /// VineDraftScreen roles, and Constants. No scene transitions needed.
    /// S2: Updated wave tests for 20-wave continuous system.
    /// </summary>
    public class ContentTestSuite : ITestSuite
    {
        public string SuiteName => "content";

        public async Task Run(TestContext ctx)
        {
            GD.Print("[ContentTestSuite] Starting content validation...");

            TestNodeRegistryCompleteness(ctx);
            TestNodeNames(ctx);
            TestNodeDescriptions(ctx);
            TestNodeResourceCosts(ctx);
            TestNodeCategories(ctx);
            TestNodeMaxConnections(ctx);
            TestNodeTintColors(ctx);
            TestNodeIdsUnique(ctx);
            TestSensorNodesHaveRange(ctx);
            TestRoleCount(ctx);
            TestRoleSizes(ctx);
            TestRolesContainSensors(ctx);
            TestRolesContainEffects(ctx);
            TestRolesContainStructural(ctx);
            TestRolesDifferByAtLeast4(ctx);
            TestRolePoolsUnique(ctx);
            TestWavesExist(ctx);
            TestWaveSpawnGroups(ctx);
            TestEnemyHPEscalation(ctx);
            TestEnemySpeedSanity(ctx);
            TestAllFactionsAppear(ctx);
            TestWaveSequentialNumbering(ctx);
            TestWaveBonusResources(ctx);
            TestSpawnGroupIntervals(ctx);
            TestSpawnGroupEnemyNames(ctx);
            TestEconomyCheapestNode(ctx);
            TestConstantsMapWidth(ctx);
            TestConstantsMapHeight(ctx);
            TestConstantsCellSize(ctx);
            TestConstantsCoreLives(ctx);

            GD.Print("[ContentTestSuite] Content validation complete.");
            await Task.CompletedTask;
        }

        // ── 1. All 18 VineNodeType enum values have registry entries ──

        private void TestNodeRegistryCompleteness(TestContext ctx)
        {
            var allTypes = Enum.GetValues(typeof(VineNodeType)).Cast<VineNodeType>().ToList();

            foreach (var type in allTypes)
            {
                ctx.StartTest();
                var data = VineNodeRegistry.Get(type);
                ctx.AssertNotNull(data, $"content.node_registry.{type}",
                    $"VineNodeType.{type} has no registry entry");
            }
        }

        // ── 2. Every node has non-empty Name ──

        private void TestNodeNames(TestContext ctx)
        {
            foreach (var data in VineNodeRegistry.GetAll())
            {
                ctx.StartTest();
                ctx.Assert(!string.IsNullOrWhiteSpace(data.Name),
                    $"content.node_name.{data.Type}",
                    $"Node {data.Type} has empty Name");
            }
        }

        // ── 3. Every node has non-empty Description ──

        private void TestNodeDescriptions(TestContext ctx)
        {
            foreach (var data in VineNodeRegistry.GetAll())
            {
                ctx.StartTest();
                ctx.Assert(!string.IsNullOrWhiteSpace(data.Description),
                    $"content.node_description.{data.Type}",
                    $"Node {data.Type} has empty Description");
            }
        }

        // ── 4. Every node has ResourceCost > 0 ──

        private void TestNodeResourceCosts(TestContext ctx)
        {
            foreach (var data in VineNodeRegistry.GetAll())
            {
                ctx.StartTest();
                ctx.AssertGreater(data.ResourceCost, 0f,
                    $"content.node_gold_cost.{data.Type}",
                    $"Node {data.Type} ResourceCost should be > 0");
            }
        }

        // ── 5. Every node has valid Category ──

        private void TestNodeCategories(TestContext ctx)
        {
            foreach (var data in VineNodeRegistry.GetAll())
            {
                ctx.StartTest();
                bool valid = data.Category == VineNodeCategory.Structural
                          || data.Category == VineNodeCategory.Sensor
                          || data.Category == VineNodeCategory.Effect;
                ctx.Assert(valid,
                    $"content.node_category.{data.Type}",
                    $"Node {data.Type} has invalid Category: {data.Category}");
            }
        }

        // ── 6. Every node has MaxConnections in range 2-4 ──

        private void TestNodeMaxConnections(TestContext ctx)
        {
            foreach (var data in VineNodeRegistry.GetAll())
            {
                ctx.StartTest();
                ctx.AssertInRange(data.MaxConnections, 2, 4,
                    $"content.node_max_connections.{data.Type}",
                    $"Node {data.Type} MaxConnections out of range");
            }
        }

        // ── 7. Every node has non-null TintColor ──

        private void TestNodeTintColors(TestContext ctx)
        {
            foreach (var data in VineNodeRegistry.GetAll())
            {
                ctx.StartTest();
                // Color is a struct so it can never be null; check it is not default black
                bool nonDefault = data.TintColor.R > 0 || data.TintColor.G > 0 || data.TintColor.B > 0;
                ctx.Assert(nonDefault,
                    $"content.node_tint_color.{data.Type}",
                    $"Node {data.Type} has default (0,0,0) TintColor");
            }
        }

        // ── 29. Node IDs are unique across all nodes ──

        private void TestNodeIdsUnique(TestContext ctx)
        {
            ctx.StartTest();
            var allNodes = VineNodeRegistry.GetAll().ToList();
            var ids = allNodes.Select(n => n.Id).ToList();
            var uniqueIds = ids.Distinct().Count();
            ctx.AssertEqual(ids.Count, uniqueIds, "content.node_ids_unique",
                $"Duplicate node IDs found");
        }

        // ── 30. Sensor nodes have positive Range ──

        private void TestSensorNodesHaveRange(TestContext ctx)
        {
            var sensors = VineNodeRegistry.GetAll()
                .Where(n => n.Category == VineNodeCategory.Sensor)
                .ToList();

            foreach (var sensor in sensors)
            {
                ctx.StartTest();
                ctx.AssertGreater(sensor.Range, 0f,
                    $"content.sensor_range.{sensor.Type}",
                    $"Sensor {sensor.Type} should have Range > 0");
            }
        }

        // ── 8. 3 roles exist (RoleCount == 3) ──

        private void TestRoleCount(TestContext ctx)
        {
            ctx.StartTest();
            ctx.AssertEqual(3, VineDraftScreen.RoleCount, "content.role_count");
        }

        // ── 9. Each of 3 roles has exactly 8 nodes ──

        private void TestRoleSizes(TestContext ctx)
        {
            for (int i = 0; i < VineDraftScreen.RoleCount; i++)
            {
                ctx.StartTest();
                var nodes = VineDraftScreen.GetRoleNodes(i);
                string roleName = VineDraftScreen.GetRoleName(i);
                ctx.AssertEqual(8, nodes.Length,
                    $"content.role_size.{roleName}",
                    $"Role {roleName} should have 8 nodes");
            }
        }

        // ── 10. Each role contains at least 1 sensor node ──

        private void TestRolesContainSensors(TestContext ctx)
        {
            for (int i = 0; i < VineDraftScreen.RoleCount; i++)
            {
                ctx.StartTest();
                var nodes = VineDraftScreen.GetRoleNodes(i);
                string roleName = VineDraftScreen.GetRoleName(i);
                int sensorCount = nodes.Count(n =>
                    VineNodeRegistry.Get(n)?.Category == VineNodeCategory.Sensor);
                ctx.AssertGreaterEqual(sensorCount, 1,
                    $"content.role_has_sensor.{roleName}",
                    $"Role {roleName} should have at least 1 sensor node");
            }
        }

        // ── 11. Each role contains at least 1 effect node ──

        private void TestRolesContainEffects(TestContext ctx)
        {
            for (int i = 0; i < VineDraftScreen.RoleCount; i++)
            {
                ctx.StartTest();
                var nodes = VineDraftScreen.GetRoleNodes(i);
                string roleName = VineDraftScreen.GetRoleName(i);
                int effectCount = nodes.Count(n =>
                    VineNodeRegistry.Get(n)?.Category == VineNodeCategory.Effect);
                ctx.AssertGreaterEqual(effectCount, 1,
                    $"content.role_has_effect.{roleName}",
                    $"Role {roleName} should have at least 1 effect node");
            }
        }

        // ── 12. Each role contains at least 1 structural node ──

        private void TestRolesContainStructural(TestContext ctx)
        {
            for (int i = 0; i < VineDraftScreen.RoleCount; i++)
            {
                ctx.StartTest();
                var nodes = VineDraftScreen.GetRoleNodes(i);
                string roleName = VineDraftScreen.GetRoleName(i);
                int structCount = nodes.Count(n =>
                    VineNodeRegistry.Get(n)?.Category == VineNodeCategory.Structural);
                ctx.AssertGreaterEqual(structCount, 1,
                    $"content.role_has_structural.{roleName}",
                    $"Role {roleName} should have at least 1 structural node");
            }
        }

        // ── 13. Roles differ by at least 4 nodes (pairwise comparison) ──

        private void TestRolesDifferByAtLeast4(TestContext ctx)
        {
            for (int i = 0; i < VineDraftScreen.RoleCount; i++)
            {
                for (int j = i + 1; j < VineDraftScreen.RoleCount; j++)
                {
                    ctx.StartTest();
                    var nodesA = VineDraftScreen.GetRoleNodes(i).ToHashSet();
                    var nodesB = VineDraftScreen.GetRoleNodes(j).ToHashSet();
                    int shared = nodesA.Intersect(nodesB).Count();
                    int totalA = nodesA.Count;
                    int diff = totalA - shared;
                    string nameA = VineDraftScreen.GetRoleName(i);
                    string nameB = VineDraftScreen.GetRoleName(j);
                    ctx.AssertGreaterEqual(diff, 4,
                        $"content.role_diff.{nameA}_vs_{nameB}",
                        $"Roles {nameA} and {nameB} should differ by at least 4 nodes, differ by {diff}");
                }
            }
        }

        // ── 14. Role pool unique to each role (no two roles have identical arrays) ──

        private void TestRolePoolsUnique(TestContext ctx)
        {
            for (int i = 0; i < VineDraftScreen.RoleCount; i++)
            {
                for (int j = i + 1; j < VineDraftScreen.RoleCount; j++)
                {
                    ctx.StartTest();
                    var nodesA = VineDraftScreen.GetRoleNodes(i);
                    var nodesB = VineDraftScreen.GetRoleNodes(j);
                    bool identical = nodesA.Length == nodesB.Length
                                  && nodesA.SequenceEqual(nodesB);
                    string nameA = VineDraftScreen.GetRoleName(i);
                    string nameB = VineDraftScreen.GetRoleName(j);
                    ctx.Assert(!identical,
                        $"content.role_unique.{nameA}_vs_{nameB}",
                        $"Roles {nameA} and {nameB} have identical node arrays");
                }
            }
        }

        // ── 15. S2: All 20 waves exist in VineWaveRegistry ──

        private void TestWavesExist(TestContext ctx)
        {
            ctx.StartTest();
            ctx.AssertEqual(20, VineWaveRegistry.WaveCount, "content.wave_count");

            for (int w = 1; w <= 20; w++)
            {
                ctx.StartTest();
                var wave = VineWaveRegistry.Get(w);
                ctx.AssertNotNull(wave, $"content.wave_exists.{w}",
                    $"Wave {w} not found in registry");
            }
        }

        // ── 16. Every wave has at least 1 spawn group with Count > 0 ──

        private void TestWaveSpawnGroups(TestContext ctx)
        {
            foreach (var wave in VineWaveRegistry.GetAll())
            {
                ctx.StartTest();
                int groupsWithEnemies = wave.Surges.Count(g => g.Count > 0);
                ctx.AssertGreaterEqual(groupsWithEnemies, 1,
                    $"content.wave_has_groups.{wave.WaveNumber}",
                    $"Wave {wave.WaveNumber} should have at least 1 group with Count > 0");
            }
        }

        // ── 17. Enemy HP escalates across waves (wave N+1 max HP >= wave N max HP) ──

        private void TestEnemyHPEscalation(TestContext ctx)
        {
            var waves = VineWaveRegistry.GetAll();
            for (int i = 0; i < waves.Count - 1; i++)
            {
                ctx.StartTest();
                float maxHPCurrent = waves[i].Surges.Max(g => g.Health);
                float maxHPNext = waves[i + 1].Surges.Max(g => g.Health);
                int wCurrent = waves[i].WaveNumber;
                int wNext = waves[i + 1].WaveNumber;
                ctx.Assert(maxHPNext >= maxHPCurrent,
                    $"content.hp_escalation.wave{wCurrent}_to_{wNext}",
                    $"Wave {wNext} max HP ({maxHPNext}) < wave {wCurrent} max HP ({maxHPCurrent})");
            }
        }

        // ── 18. Enemy speed doesn't decrease dramatically (sanity check) ──

        private void TestEnemySpeedSanity(TestContext ctx)
        {
            foreach (var wave in VineWaveRegistry.GetAll())
            {
                foreach (var group in wave.Surges)
                {
                    ctx.StartTest();
                    ctx.AssertGreater(group.Speed, 0f,
                        $"content.speed_sanity.wave{wave.WaveNumber}.{group.EnemyName}",
                        $"Wave {wave.WaveNumber} {group.EnemyName} has non-positive speed");
                }
            }
        }

        // ── 19. All 4 factions appear across all waves ──

        private void TestAllFactionsAppear(TestContext ctx)
        {
            ctx.StartTest();
            var allFactions = new HashSet<VineEnemyFaction>();
            foreach (var wave in VineWaveRegistry.GetAll())
            {
                foreach (var group in wave.Surges)
                    allFactions.Add(group.Faction);
            }

            var expectedFactions = Enum.GetValues(typeof(VineEnemyFaction))
                .Cast<VineEnemyFaction>().ToHashSet();

            bool allPresent = expectedFactions.All(f => allFactions.Contains(f));
            var missing = expectedFactions.Except(allFactions).ToList();
            ctx.Assert(allPresent,
                "content.factions_all_appear",
                allPresent ? "" : $"Missing factions: {string.Join(", ", missing)}");
        }

        // ── 20. Economy: VINE_STARTING_RESOURCES can afford 3+ of cheapest node ──

        private void TestEconomyCheapestNode(TestContext ctx)
        {
            ctx.StartTest();
            int cheapest = VineNodeRegistry.GetAll().Min(n => n.ResourceCost);
            int canAfford = Constants.VINE_STARTING_RESOURCES / cheapest;
            ctx.AssertGreaterEqual(canAfford, 3,
                "content.economy_starting_gold",
                $"Starting gold {Constants.VINE_STARTING_RESOURCES} / cheapest node {cheapest} = {canAfford}, need >= 3");
        }

        // ── 21-24. Constants sanity checks ──

        private void TestConstantsMapWidth(TestContext ctx)
        {
            ctx.StartTest();
            ctx.AssertGreater(Constants.VINE_MAP_WIDTH, 0f,
                "content.constants_map_width",
                "VINE_MAP_WIDTH should be > 0");
        }

        private void TestConstantsMapHeight(TestContext ctx)
        {
            ctx.StartTest();
            ctx.AssertGreater(Constants.VINE_MAP_HEIGHT, 0f,
                "content.constants_map_height",
                "VINE_MAP_HEIGHT should be > 0");
        }

        private void TestConstantsCellSize(TestContext ctx)
        {
            ctx.StartTest();
            ctx.AssertGreater(Constants.VINE_CELL_SIZE, 0f,
                "content.constants_cell_size",
                "VINE_CELL_SIZE should be > 0");
        }

        private void TestConstantsCoreLives(TestContext ctx)
        {
            ctx.StartTest();
            ctx.AssertGreater(Constants.VINE_CORE_LIVES, 0f,
                "content.constants_core_lives",
                "VINE_CORE_LIVES should be > 0");
        }

        // ── 25. S2: Wave sequential numbering (1-20) ──

        private void TestWaveSequentialNumbering(TestContext ctx)
        {
            var waves = VineWaveRegistry.GetAll();
            for (int i = 0; i < waves.Count; i++)
            {
                ctx.StartTest();
                ctx.AssertEqual(i + 1, waves[i].WaveNumber,
                    $"content.wave_sequential.{i + 1}",
                    $"Wave at index {i} should have WaveNumber {i + 1}");
            }
        }

        // ── 26. Wave bonus resources is positive for all waves ──

        private void TestWaveBonusResources(TestContext ctx)
        {
            foreach (var wave in VineWaveRegistry.GetAll())
            {
                ctx.StartTest();
                ctx.AssertGreater(wave.BonusResources, 0f,
                    $"content.wave_bonus_gold.{wave.WaveNumber}",
                    $"Wave {wave.WaveNumber} BonusResources should be > 0");
            }
        }

        // ── 27. All spawn groups have positive SpawnInterval (exclude boss surges with Count==1 && IsBoss) ──

        private void TestSpawnGroupIntervals(TestContext ctx)
        {
            foreach (var wave in VineWaveRegistry.GetAll())
            {
                foreach (var group in wave.Surges)
                {
                    // S2: Boss surges with Count==1 may have SpawnInterval=0
                    if (group.IsBoss && group.Count == 1)
                        continue;

                    ctx.StartTest();
                    ctx.AssertGreater(group.SpawnInterval, 0f,
                        $"content.spawn_interval.wave{wave.WaveNumber}.{group.EnemyName}",
                        $"Wave {wave.WaveNumber} {group.EnemyName} SpawnInterval should be > 0");
                }
            }
        }

        // ── 28. All spawn groups have non-empty EnemyName ──

        private void TestSpawnGroupEnemyNames(TestContext ctx)
        {
            foreach (var wave in VineWaveRegistry.GetAll())
            {
                foreach (var group in wave.Surges)
                {
                    ctx.StartTest();
                    ctx.Assert(!string.IsNullOrWhiteSpace(group.EnemyName),
                        $"content.enemy_name.wave{wave.WaveNumber}.group{wave.Surges.IndexOf(group)}",
                        $"Wave {wave.WaveNumber} has a spawn group with empty EnemyName");
                }
            }
        }
    }
}
