using AIGraph;
using API;
using Enemies;
using HarmonyLib;
using LevelGeneration;
using Player;

namespace AggroFix {
    [HarmonyPatch]
    internal static class Fix {
        private static bool IsTargetReachable(AIG_CourseNode source, AIG_CourseNode target) {
            if (source == null || target == null) return false;
            if (source.NodeID == target.NodeID) return true;

            AIG_SearchID.IncrementSearchID();
            ushort searchID = AIG_SearchID.SearchID;
            Queue<AIG_CourseNode> queue = new Queue<AIG_CourseNode>();
            queue.Enqueue(source);

            while (queue.Count > 0) {
                AIG_CourseNode current = queue.Dequeue();
                current.m_searchID = searchID;
                foreach (AIG_CoursePortal portal in current.m_portals) {
                    LG_SecurityDoor? secDoor = portal.Gate?.SpawnedDoor?.TryCast<LG_SecurityDoor>();
                    if (secDoor != null) {
                        APILogger.Debug($"SecurityDoor {secDoor.m_serialNumber} - {secDoor.LastStatus.ToString()}");
                        if (secDoor.LastStatus != eDoorStatus.Open && secDoor.LastStatus != eDoorStatus.Opening)
                            continue;
                    }
                    AIG_CourseNode nextNode = portal.GetOppositeNode(current);
                    if (nextNode.m_searchID == searchID) continue;
                    if (nextNode.NodeID == target.NodeID) return true;
                    queue.Enqueue(nextNode);
                }
            }

            return false;
        }

        // Aggro fix
        private static bool patch = true;
        [HarmonyPatch(typeof(EnemyCourseNavigation), nameof(EnemyCourseNavigation.UpdateTracking))]
        [HarmonyPostfix]
        private static void UpdateTracking(EnemyCourseNavigation __instance) {
            if (!patch) return;
            if (!SNetwork.SNet.IsMaster) return;

            EnemyAgent enemy = __instance.m_owner;
            if (enemy.CourseNode == null || __instance.m_targetRef == null) return;
            switch (enemy.Locomotion.m_currentState.m_stateEnum) {
            case ES_StateEnum.Hibernate:
                return;
            }
            if (!IsTargetReachable(enemy.CourseNode, __instance.m_targetRef.m_agent.CourseNode)) {
                int index = UnityEngine.Random.RandomRangeInt(0, PlayerManager.PlayerAgentsInLevel.Count);
                PlayerAgent selected = PlayerManager.PlayerAgentsInLevel[index];
                while (PlayerManager.PlayerAgentsInLevel.Count != 1 && selected.GetInstanceID() == __instance.m_targetRef.m_agent.GetInstanceID()) {
                    index = (index + 1) % PlayerManager.PlayerAgentsInLevel.Count;
                    selected = PlayerManager.PlayerAgentsInLevel[index];
                }
                patch = false;
                enemy.AI.SetTarget(selected);
                patch = true;
            }
        }
    }
}
