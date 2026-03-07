using System.Collections.Generic;
using UnityEngine;

namespace DungeonCrawlerCarl
{
    /// <summary>
    /// Coordinates enemy positioning within a room encounter.
    /// Assigns tactical roles and formation offsets so enemies don't all stack on the player.
    /// Attach to RoomController or create via EnemySpawner after a wave spawns.
    /// </summary>
    public class FormationCoordinator : MonoBehaviour
    {
        public enum TacticalRole
        {
            FrontLine,  // Short-range melee — approaches head on
            Flanker,    // Melee — circles to the side/behind
            Ranged,     // Stays at max attack range
            Support     // Stays behind front line
        }

        [Header("Formation Settings")]
        [SerializeField] private float _flankAngle = 90f;
        [SerializeField] private float _flankDistance = 3f;
        [SerializeField] private float _rangedStandoff = 1.5f;
        [SerializeField] private float _repositionInterval = 1f;

        private readonly List<FormationMember> _members = new();
        private Transform _target;
        private float _repositionTimer;

        public struct FormationMember
        {
            public EnemyAI AI;
            public TacticalRole Role;
            public Vector3 FormationOffset;
        }

        public IReadOnlyList<FormationMember> Members => _members;

        /// <summary>
        /// Register an enemy into the formation. Role is auto-assigned based on attack range.
        /// </summary>
        public void RegisterEnemy(EnemyAI enemyAI, float attackRange)
        {
            var role = AssignRole(attackRange, _members.Count);

            _members.Add(new FormationMember
            {
                AI = enemyAI,
                Role = role,
                FormationOffset = Vector3.zero
            });

            enemyAI.SetFormation(this);
        }

        public void UnregisterEnemy(EnemyAI enemyAI)
        {
            for (int i = _members.Count - 1; i >= 0; i--)
            {
                if (_members[i].AI == enemyAI)
                {
                    _members.RemoveAt(i);
                    break;
                }
            }
        }

        private void Update()
        {
            if (_members.Count == 0) return;

            // Find current target (usually the player)
            _target = FindTarget();
            if (_target == null) return;

            _repositionTimer -= Time.deltaTime;
            if (_repositionTimer <= 0f)
            {
                _repositionTimer = _repositionInterval;
                RecalculateFormation();
            }
        }

        private Transform FindTarget()
        {
            // Use first member's target, or find player
            foreach (var member in _members)
            {
                if (member.AI != null && member.AI.CurrentTarget != null)
                    return member.AI.CurrentTarget;
            }

            var playerObj = GameObject.FindWithTag(Constants.TAG_PLAYER);
            return playerObj != null ? playerObj.transform : null;
        }

        private void RecalculateFormation()
        {
            if (_target == null) return;

            int flankerIndex = 0;
            int totalMembers = _members.Count;

            for (int i = 0; i < totalMembers; i++)
            {
                var member = _members[i];
                if (member.AI == null || member.AI.CurrentState == EnemyAI.AIState.Dead) continue;

                Vector3 offset = CalculateOffset(member.Role, ref flankerIndex, totalMembers);
                member.FormationOffset = offset;
                _members[i] = member;
            }
        }

        private Vector3 CalculateOffset(TacticalRole role, ref int flankerIndex, int totalMembers)
        {
            switch (role)
            {
                case TacticalRole.FrontLine:
                    // Go straight at the target, slight spread
                    return Vector3.zero;

                case TacticalRole.Flanker:
                    // Alternate left/right flanks
                    float angle = (flankerIndex % 2 == 0 ? _flankAngle : -_flankAngle) * Mathf.Deg2Rad;
                    flankerIndex++;
                    return new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle)) * _flankDistance;

                case TacticalRole.Ranged:
                    // Stand back at range
                    return Vector3.back * _rangedStandoff;

                case TacticalRole.Support:
                    // Behind the front line
                    return Vector3.back * (_rangedStandoff + 2f);

                default:
                    return Vector3.zero;
            }
        }

        /// <summary>
        /// Get the world-space destination for an enemy, accounting for its formation offset.
        /// </summary>
        public Vector3 GetFormationDestination(EnemyAI enemyAI)
        {
            if (_target == null) return enemyAI.transform.position;

            for (int i = 0; i < _members.Count; i++)
            {
                if (_members[i].AI == enemyAI)
                {
                    Vector3 dirToTarget = (_target.position - enemyAI.transform.position).normalized;
                    dirToTarget.y = 0f;

                    // Rotate offset relative to the approach direction
                    Quaternion facing = dirToTarget != Vector3.zero
                        ? Quaternion.LookRotation(dirToTarget)
                        : Quaternion.identity;

                    return _target.position + facing * _members[i].FormationOffset;
                }
            }

            return _target.position;
        }

        private TacticalRole AssignRole(float attackRange, int existingCount)
        {
            // First enemy is always front line
            if (existingCount == 0) return TacticalRole.FrontLine;

            // Long attack range → ranged
            if (attackRange > Constants.DEFAULT_ATTACK_RANGE * 2f) return TacticalRole.Ranged;

            // Alternate between front line and flanker for melee
            return existingCount % 2 == 0 ? TacticalRole.FrontLine : TacticalRole.Flanker;
        }
    }
}
