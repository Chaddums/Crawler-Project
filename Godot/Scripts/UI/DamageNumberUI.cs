using Godot;

namespace JunkbotArena
{
    /// <summary>
    /// Spawns floating damage numbers at hit points.
    /// Subscribes to OnDamageDealt and creates Label3D nodes that float up and fade.
    /// Crits start at 1.5x scale with Back easing pop, plus random horizontal drift.
    /// </summary>
    public partial class DamageNumberUI : Node
    {
        public override void _Ready()
        {
            GameEvents.OnDamageDealt += SpawnDamageNumber;
        }

        private void SpawnDamageNumber(DamageInfo damage)
        {
            if (damage.FinalDamage <= 0) return;

            var label = new Label3D();
            label.Text = damage.IsCritical
                ? $"{damage.FinalDamage:F0}!"
                : $"{damage.FinalDamage:F0}";

            label.FontSize = damage.IsCritical ? 36 : 24;
            label.Modulate = GetDamageColor(damage);
            label.OutlineModulate = new Color(0, 0, 0);
            label.OutlineSize = 3;
            label.Billboard = BaseMaterial3D.BillboardModeEnum.Enabled;
            label.NoDepthTest = true;

            // Crits start big
            if (damage.IsCritical)
                label.Scale = new Vector3(1.5f, 1.5f, 1.5f);

            GetTree().Root.AddChild(label);

            // Random horizontal offset (set after AddChild to avoid !is_inside_tree error)
            float rx = (float)GD.RandRange(-0.5, 0.5);
            float rz = (float)GD.RandRange(-0.5, 0.5);
            label.GlobalPosition = damage.HitPoint + new Vector3(rx, 2f, rz);

            // Random horizontal drift
            float driftX = (float)GD.RandRange(-0.6, 0.6);

            // Float up and fade out
            var tween = label.CreateTween();
            tween.SetParallel(true);
            tween.TweenProperty(label, "position",
                label.Position + new Vector3(driftX, 1.5f, 0), 0.8f)
                .SetTrans(Tween.TransitionType.Quad)
                .SetEase(Tween.EaseType.Out);
            tween.TweenProperty(label, "modulate:a", 0f, 0.8f)
                .SetTrans(Tween.TransitionType.Quad)
                .SetEase(Tween.EaseType.In);

            // Crit pop: scale down from 1.5 to 1.0 with Back easing
            if (damage.IsCritical)
            {
                tween.TweenProperty(label, "scale", Vector3.One, 0.3f)
                    .SetTrans(Tween.TransitionType.Back)
                    .SetEase(Tween.EaseType.Out);
            }

            tween.SetParallel(false);
            tween.TweenCallback(Callable.From(label.QueueFree));
        }

        private static Color GetDamageColor(DamageInfo damage)
        {
            if (damage.IsCritical)
                return new Color(1f, 0.85f, 0.2f); // Gold for crits

            return damage.DamageType switch
            {
                DamageType.Fire => new Color(1f, 0.4f, 0.1f),
                DamageType.Ice => new Color(0.3f, 0.7f, 1f),
                DamageType.Lightning => new Color(0.8f, 0.8f, 1f),
                DamageType.Poison => new Color(0.3f, 0.9f, 0.3f),
                DamageType.Dark => new Color(0.6f, 0.2f, 0.8f),
                DamageType.Holy => new Color(1f, 1f, 0.7f),
                _ => new Color(1f, 1f, 1f), // White for physical
            };
        }

        public override void _ExitTree()
        {
            GameEvents.OnDamageDealt -= SpawnDamageNumber;
        }
    }
}
