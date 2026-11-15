using AggroFix.BepInEx;
using AmorLib.Utils;
using API;
using Enemies;
using HarmonyLib;
using Player;

namespace AggroFix {
    [HarmonyPatch]
    internal static class Fix {
        // Aggro fix
        private static bool patch = true;
        [HarmonyPatch(typeof(EnemyCourseNavigation), nameof(EnemyCourseNavigation.UpdateTracking))]
        [HarmonyPostfix]
        private static void UpdateTracking(EnemyCourseNavigation __instance) {
            if (!patch) return;
            if (!SNetwork.SNet.IsMaster) return;

            EnemyAgent enemy = __instance.m_owner;
            if (enemy.CourseNode == null || __instance.m_targetRef == null || __instance.m_targetRef.m_agent == null) return;
            switch (enemy.Locomotion.m_currentState.m_stateEnum) {
            case ES_StateEnum.Hibernate:
                return;
            }

            if (!ZoneGraphUtil.IsReachable(__instance.m_targetPlayerID, enemy.CourseNode, GraphLayer.Zone, ReachableMode.Changing)) {
                if (ConfigManager.Debug) APILogger.Debug($"Invalid current {__instance.m_targetRef.m_agent.Cast<PlayerAgent>().Owner.NickName}");

                bool foundValidTarget = false;
                var agents = PlayerManager.PlayerAgentsInLevel;
                var agentCount = agents.Count;
                int index = UnityEngine.Random.RandomRangeInt(0, agentCount);
                PlayerAgent selected = agents[index];

                for (int i = 0; i < agentCount; ++i) {
                    APILogger.Debug($"Checking {selected.Owner.NickName}");
                    if (selected.Alive && selected.PlayerSlotIndex != __instance.m_targetPlayerID && ZoneGraphUtil.IsReachable(selected.PlayerSlotIndex, enemy.CourseNode, GraphLayer.Zone, ReachableMode.Changing)) {
                        foundValidTarget = true;
                        break;
                    } else {
                        index = (index + 1) % agentCount;
                        selected = agents[index];
                    }
                }

                if (foundValidTarget) {
                    APILogger.Debug($"Rerouted to {selected.Owner.NickName}");
                    patch = false;
                    enemy.AI.SetTarget(selected);
                    patch = true;
                } else if (ConfigManager.Debug) {
                    APILogger.Debug($"No valid target found {__instance.m_targetRef.m_agent.Cast<PlayerAgent>().Owner.NickName}");
                }
            } else if (ConfigManager.Debug) {
                APILogger.Debug($"Current {__instance.m_targetRef.m_agent.Cast<PlayerAgent>().Owner.NickName}");
            }
        }
    }
}
