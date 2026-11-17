using System.Collections.Generic;
using System.Linq;
using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// What a boss fight's boss does to a tower: The Silencer's field silences it (it stands dark
    /// and does nothing until the field moves off), The Overloader's burst takes a share of its
    /// full health at once.
    /// </summary>
    public partial class VineNode
    {
        private float _jamLeft, _jamSpark;
        private Color _jamColor;
        private Label3D _jamLabel;
        private Dictionary<MeshInstance3D, Material> _jamOverlays;
        private static ShaderMaterial _jamMat;

        /// <summary>Silenced by a boss's field: it does nothing.</summary>
        public bool IsSilenced => _jamLeft > 0f;

        /// <summary>Guns and support towers can be silenced; walls, drills and wrecks have nothing to silence.</summary>
        public bool CanBeSilenced => Data != null && Data.Category == VineNodeCategory.Effect
            && Data.Type is not (VineNodeType.BarrierWall or VineNodeType.SalvageDrill) && !IsWrecked && !IsDestroyed;

        /// <summary>
        /// Silence it for <paramref name="seconds"/> (the field renews it while the tower is inside).
        /// True when it was firing until now.
        /// </summary>
        public bool Silence(float seconds, Color color)
        {
            if (!CanBeSilenced || seconds <= 0f) return false;
            bool fresh = _jamLeft <= 0f;
            _jamLeft = Mathf.Max(_jamLeft, seconds);
            _jamColor = color;
            if (fresh)
            {
                JamLook(true);
                _jamSpark = 0.05f + GD.Randf() * 0.4f;
            }
            return fresh;
        }

        /// <summary>The silence runs down; true while it holds (the tower's own update is skipped).</summary>
        private bool TickJam(float dt)
        {
            if (_jamLeft <= 0f) return false;
            _jamLeft -= dt;
            if (_jamLeft <= 0f) { EndJam(); return false; }
            if ((_jamSpark -= dt) <= 0f)
            {
                _jamSpark = 0.6f + GD.Randf() * 0.9f;
                VfxFactory.SpawnImpact(GetTree(), GlobalPosition + Vector3.Up * Mathf.Max(0.7f, _visualTop * 0.8f), _jamColor, 0.45f);
            }
            return true;
        }

        private void EndJam()
        {
            _jamLeft = 0f;
            JamLook(false);
        }

        /// <summary>Dark, with static running through it, and a small label, so a silenced tower reads at a glance.</summary>
        private void JamLook(bool on)
        {
            if (on)
            {
                if (_modelRoot != null)
                {
                    _jamMat ??= new ShaderMaterial
                    {
                        Shader = new Shader
                        {
                            Code = @"
shader_type spatial;
render_mode unshaded, blend_mul, depth_draw_never, cull_back, shadows_disabled;
void fragment() {
    float line = step(0.82, fract(FRAGCOORD.y * 0.06 + TIME * 1.7));
    ALBEDO = vec3(0.34, 0.26, 0.42) + vec3(0.35, 0.1, 0.45) * line;
}"
                        }
                    };
                    _jamOverlays ??= new Dictionary<MeshInstance3D, Material>();
                    foreach (var mi in _modelRoot.FindChildren("*", "MeshInstance3D", true, false).OfType<MeshInstance3D>())
                    {
                        if (_jamOverlays.ContainsKey(mi)) continue;
                        _jamOverlays[mi] = mi.MaterialOverlay;
                        mi.MaterialOverlay = _jamMat;
                    }
                }
                if (!IsInstanceValid(_jamLabel))
                {
                    _jamLabel = new Label3D
                    {
                        Name = "JamLabel", Text = "SILENCED", FontSize = 16, OutlineSize = 5, FixedSize = true, PixelSize = 0.0015f,
                        OutlineModulate = new Color(0.08f, 0f, 0.1f, 0.95f),
                        Position = new Vector3(0, Mathf.Max(1.0f, _visualTop + 0.45f), 0), Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
                        NoDepthTest = true,
                    };
                    AddChild(_jamLabel);
                }
                _jamLabel.Modulate = _jamColor.Lightened(0.3f);
                _jamLabel.Visible = true;
            }
            else
            {
                if (_jamOverlays != null)
                {
                    foreach (var kv in _jamOverlays)
                        if (IsInstanceValid(kv.Key)) kv.Key.MaterialOverlay = kv.Value;
                    _jamOverlays.Clear();
                }
                if (IsInstanceValid(_jamLabel)) _jamLabel.Visible = false;
            }
        }

        /// <summary>
        /// The Overloader's burst: <paramref name="share"/> of full health gone at once (a tower
        /// fortified by Repair Pulse takes less). True when that wrecked it.
        /// </summary>
        public bool Overload(float share)
        {
            if (!_hasHealth || IsDestroyed || IsWrecked || Data == null || share <= 0f) return false;
            float amount = NodeMaxHealth * share * (_fortifyLeft > 0f ? PowerType.FortifyTaken : 1f);
            NodeCurrentHealth -= amount;
            Losses.Hit(TowerLoss.Boss, amount);
            _damageFlashTimer = 0.25f;
            if (_modelRoot != null) FlashModel(true, new Color(1f, 0.7f, 0.3f));
            if (NodeCurrentHealth > 0f) return false;
            Losses.Wrecked(TowerLoss.Boss);
            if (WrecksAtZero) Wreck();
            else DestroyNode();
            return true;
        }
    }
}
