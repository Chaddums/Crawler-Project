using System;
using System.Collections.Generic;
using Godot;

namespace JunkyardTD
{
    /// <summary>
    /// 30-second in-engine intro cinematic.
    /// AXIS (Sauron-style dark obelisk with glowing eye) launches probes.
    /// Player's probe streaks through space, slams into planet.
    /// "DO NOT DISAPPOINT ME."
    /// Press any key to skip.
    /// </summary>
    public partial class IntroCinematic : Node3D
    {
        private Camera3D _camera;
        private float _elapsed;
        private bool _skipped;
        private bool _finished;
        private bool _eyePulsed;
        private bool _probesLaunched;
        private bool _impactHit;
        private bool _axisSpoke;

        // Scene objects
        private Node3D _axisModel;
        private MeshInstance3D _axisEye;
        private MeshInstance3D _axisEyeRing;
        private Node3D _planet;
        private MeshInstance3D _probe;
        private Node3D _probeNode; // Wrapper for probe (real model or fallback sphere)
        private CanvasLayer _uiLayer;
        private Label _axisText;
        private Label _skipHint;
        private ColorRect _fadeRect;

        // Probes (the swarm AXIS launches)
        private readonly List<MeshInstance3D> _swarmProbes = new();

        // Planet surface
        private bool _surfaceBuilt;
        private Vector3 _surfaceCenter = new(0, 0, -58f);
        private Node3D _surfaceRoot;

        // AXIS eye drone (real model)
        private Node3D _axisEyeDrone;

        // AXIS silhouette (for loom phase)
        private Node3D _axisSilhouette;
        private MeshInstance3D _axisAura;
        private bool _silhouetteSpawned;

        // Audio
        private AudioStreamPlayer _ambientPlayer;
        private AudioStreamPlayer _sfxPlayer;

        // Timing (seconds)
        private const float T_STARS_FADE = 0.5f;
        private const float T_AXIS_APPEAR = 1.5f;
        private const float T_EYE_PULSE = 4f;
        private const float T_PROBES_LAUNCH = 5f;
        private const float T_FOLLOW_PROBE = 8f;
        private const float T_PLANET_VISIBLE = 12f;
        private const float T_SURFACE_CUT = 14.5f;  // Camera cuts to planet surface
        private const float T_IMPACT = 17f;
        private const float T_DUST_CLEAR = 19f;
        private const float T_AXIS_LOOMS = 20f;
        private const float T_TEXT_APPEAR = 22f;
        private const float T_FADE_OUT = 27f;
        private const float T_END = 30f;

        private static readonly RandomNumberGenerator _rng = new();

        public override void _Ready()
        {
            // Camera
            _camera = new Camera3D();
            _camera.Current = true;
            _camera.Fov = 60;
            AddChild(_camera);

            // Build scene
            BuildStarfield();
            BuildAXIS();
            BuildPlanet();
            BuildProbe();
            BuildUI();
            BuildAudio();

            // Start with black screen
            _fadeRect.Color = new Color(0, 0, 0, 1);

            // Environment — deep space skybox
            BuildSpaceEnvironment();

            // Initial camera position — looking at AXIS from distance
            _camera.Position = new Vector3(0, 5, 40);
            _camera.LookAt(Vector3.Zero, Vector3.Up);

            // Hide planet and probe initially
            _planet.Visible = false;
            _probeNode.Visible = false;
        }

        public override void _Process(double delta)
        {
            if (_finished) return;

            float dt = (float)delta;
            _elapsed += dt;

            UpdateCinematic(dt);
            UpdateAXISAnimation(dt);

            // Skip check
            if (_skipped || _elapsed >= T_END)
            {
                _finished = true;
                TransitionToGame();
            }
        }

        public override void _UnhandledInput(InputEvent @event)
        {
            if (_finished) return;

            if (@event is InputEventKey key && key.Pressed && !key.Echo)
            {
                _skipped = true;
                GetViewport().SetInputAsHandled();
            }
            else if (@event is InputEventMouseButton mb && mb.Pressed)
            {
                _skipped = true;
                GetViewport().SetInputAsHandled();
            }
        }

        private void UpdateCinematic(float dt)
        {
            float t = _elapsed;

            // ── Phase 1: Stars fade in, AXIS appears (0-4s) ──
            if (t < T_AXIS_APPEAR)
            {
                float fade = Mathf.Clamp(t / T_STARS_FADE, 0, 1);
                _fadeRect.Color = new Color(0, 0, 0, 1f - fade);
                // Slow push toward AXIS
                _camera.Position = new Vector3(0, 5, Mathf.Lerp(40f, 25f, t / T_EYE_PULSE));
                _camera.LookAt(new Vector3(0, 8, 0), Vector3.Up);
            }

            // ── Phase 2: Eye pulses, probes launch (4-8s) ──
            if (t >= T_EYE_PULSE && !_eyePulsed)
            {
                _eyePulsed = true;
                PulseEye(3f);
                PlaySFX(GenerateImpactBoom(0.3f, 200f));
            }

            if (t >= T_PROBES_LAUNCH && !_probesLaunched)
            {
                _probesLaunched = true;
                LaunchProbeSwarm();
                PlaySFX(GenerateWhoosh(0.8f, 600f, 100f));
            }

            // Animate swarm probes flying outward
            foreach (var p in _swarmProbes)
            {
                if (p.IsInsideTree())
                {
                    var vel = (Vector3)p.GetMeta("velocity");
                    p.GlobalPosition += vel * dt;
                    // Fade out as they fly away
                    float dist = p.GlobalPosition.Length();
                    if (dist > 80f) p.Visible = false;
                }
            }

            // ── Phase 3: Camera follows one probe through space (8-12s) ──
            if (t >= T_FOLLOW_PROBE && t < T_PLANET_VISIBLE)
            {
                _probeNode.Visible = true;
                float probeT = (t - T_FOLLOW_PROBE) / (T_PLANET_VISIBLE - T_FOLLOW_PROBE);
                var probeStart = new Vector3(8f, 3f, 20f);
                var probeEnd = new Vector3(4f, 2f, -30f);
                _probeNode.GlobalPosition = probeStart.Lerp(probeEnd, probeT);
                _camera.Position = _probeNode.GlobalPosition + new Vector3(2f, 1.5f, 6f);
                _camera.LookAt(_probeNode.GlobalPosition + new Vector3(0, 0, -8), Vector3.Up);

                if (_probeNode.IsInsideTree())
                    SpawnProbeTrail();
            }

            // ── Phase 4a: Planet appears in space, probe approaches (12-14.5s) ──
            if (t >= T_PLANET_VISIBLE && !_planet.Visible)
            {
                _planet.Visible = true;
                _planet.GlobalPosition = new Vector3(0, -5, -80);
                _planet.Scale = Vector3.One * 20f;
            }

            if (t >= T_PLANET_VISIBLE && t < T_SURFACE_CUT)
            {
                float approachT = (t - T_PLANET_VISIBLE) / (T_SURFACE_CUT - T_PLANET_VISIBLE);
                _probeNode.GlobalPosition = new Vector3(4f, 2, -30f).Lerp(new Vector3(1f, -1f, -55f), approachT * approachT);
                if (_probeNode.IsInsideTree()) SpawnProbeTrail();
                _camera.Position = new Vector3(8, 3, -45f).Lerp(new Vector3(5, 0, -52f), approachT);
                _camera.LookAt(_probeNode.GlobalPosition, Vector3.Up);
                float planetScale = Mathf.Lerp(20f, 30f, approachT);
                _planet.Scale = Vector3.One * planetScale;
            }

            // ── Phase 4b: Cut to planet surface, probe streaks in from sky (14.5-17s) ──
            if (t >= T_SURFACE_CUT && !_surfaceBuilt)
            {
                BuildPlanetSurface();
                _surfaceBuilt = true;
                // Hide the space-scale planet sphere
                _planet.Visible = false;
                _probeNode.GlobalPosition = new Vector3(0, 60f, _surfaceCenter.Z - 20f);
                PlaySFX(GenerateWhoosh(1.5f, 800f, 60f));
            }

            if (t >= T_SURFACE_CUT && t < T_IMPACT)
            {
                float impactT = (t - T_SURFACE_CUT) / (T_IMPACT - T_SURFACE_CUT);
                // Camera on surface looking up at the sky
                _camera.Position = _surfaceCenter + new Vector3(6f, 2f, 8f);
                _camera.LookAt(_probeNode.GlobalPosition, Vector3.Up);
                var probeFrom = new Vector3(0, 60f, _surfaceCenter.Z - 20f);
                var probeTo = _surfaceCenter + new Vector3(0, 0.3f, 0);
                _probeNode.GlobalPosition = probeFrom.Lerp(probeTo, impactT * impactT * impactT);
                _probeNode.Visible = true;
                if (_probeNode.IsInsideTree()) SpawnProbeTrail();
            }

            if (t >= T_IMPACT && !_impactHit)
            {
                _impactHit = true;
                _probeNode.Visible = false;
                SpawnSurfaceImpact();
                PlaySFX(GenerateImpactBoom(1.5f, 60f));
                ScreenFlash();
            }

            // ── Phase 5: Dust clears, camera tilts up, AXIS silhouette looms in sky (19-22s) ──
            if (t >= T_DUST_CLEAR && !_silhouetteSpawned)
            {
                SpawnAxisSilhouette();
                _silhouetteSpawned = true;
                // Hide the original detailed AXIS model
                _axisModel.Visible = false;
            }

            if (t >= T_DUST_CLEAR && t < T_TEXT_APPEAR)
            {
                float loomT = (t - T_DUST_CLEAR) / (T_TEXT_APPEAR - T_DUST_CLEAR);
                // Camera near crater, slowly tilting up from impact to sky
                _camera.Position = _surfaceCenter + new Vector3(4f, 1.5f, 6f);
                var lookTarget = Vector3.Up.Lerp(new Vector3(0, 50, 0), Mathf.SmoothStep(0, 1, loomT));
                _camera.LookAt(_surfaceCenter + lookTarget, Vector3.Up);

                // Silhouette scales in
                float scale = Mathf.Lerp(0.1f, 4f, Mathf.SmoothStep(0, 1, loomT));
                _axisSilhouette.Scale = Vector3.One * scale;

                // Aura glow pulses
                if (_axisAura?.MaterialOverride is StandardMaterial3D auraMat)
                {
                    float auraPulse = 0.3f + 0.15f * Mathf.Sin(_elapsed * 2f);
                    auraMat.AlbedoColor = new Color(0.9f, 0.4f, 0.1f, auraPulse);
                }
            }

            // ── Phase 6: "DO NOT DISAPPOINT ME." (22-27s) ──
            if (t >= T_TEXT_APPEAR && !_axisSpoke)
            {
                _axisSpoke = true;
                PlaySFX(GenerateAxisVoiceTone(1.5f));
            }

            if (t >= T_TEXT_APPEAR && t < T_FADE_OUT)
            {
                // Typewriter effect
                string fullText = "DO NOT DISAPPOINT ME.";
                float charT = (t - T_TEXT_APPEAR) / 2f; // 2 seconds to type
                int chars = Mathf.Min((int)(charT * fullText.Length), fullText.Length);
                _axisText.Text = fullText[..chars];
                _axisText.Visible = true;
            }

            // ── Phase 7: Fade to black (27-30s) ──
            if (t >= T_FADE_OUT)
            {
                float fadeT = (t - T_FADE_OUT) / (T_END - T_FADE_OUT);
                _fadeRect.Color = new Color(0, 0, 0, fadeT);
            }
        }

        // ── AXIS model (Sauron-style) ──

        private float _eyePulseTimer;
        private float _eyeBaseEmission = 2f;

        private void BuildAXIS()
        {
            _axisModel = new Node3D();
            _axisModel.Position = new Vector3(0, 8, 0);
            AddChild(_axisModel);

            // Procedural dark obelisk with power-surge edge glow
            var surgeColor = new Color(0.7f, 0.25f, 0.05f); // Amber/red edge glow

            var spire = new MeshInstance3D();
            var spireBox = new BoxMesh();
            spireBox.Size = new Vector3(2f, 16f, 2f);
            spire.Mesh = spireBox;
            ApplyAxisSurgeMaterial(spire, new Color(0.05f, 0.04f, 0.06f), surgeColor);
            _axisModel.AddChild(spire);

            // Tapered top
            var top = new MeshInstance3D();
            var topBox = new BoxMesh();
            topBox.Size = new Vector3(1.5f, 6f, 1.5f);
            top.Mesh = topBox;
            top.Position = new Vector3(0, 11f, 0);
            ApplyAxisSurgeMaterial(top, new Color(0.04f, 0.03f, 0.05f), surgeColor);
            _axisModel.AddChild(top);

            // Wider base
            var baseBlock = new MeshInstance3D();
            var baseMesh = new BoxMesh();
            baseMesh.Size = new Vector3(4f, 3f, 4f);
            baseBlock.Mesh = baseMesh;
            baseBlock.Position = new Vector3(0, -9f, 0);
            ApplyAxisSurgeMaterial(baseBlock, new Color(0.05f, 0.04f, 0.06f), surgeColor);
            _axisModel.AddChild(baseBlock);

            // Angular buttresses
            for (int i = 0; i < 4; i++)
            {
                var buttress = new MeshInstance3D();
                var bMesh = new BoxMesh();
                bMesh.Size = new Vector3(0.6f, 10f, 0.6f);
                buttress.Mesh = bMesh;
                float bAngle = i * Mathf.Pi / 2f;
                buttress.Position = new Vector3(
                    Mathf.Cos(bAngle) * 2.5f, -2f, Mathf.Sin(bAngle) * 2.5f);
                buttress.RotationDegrees = new Vector3(15f * Mathf.Cos(bAngle), 0, 15f * Mathf.Sin(bAngle));
                ApplyAxisSurgeMaterial(buttress, new Color(0.04f, 0.03f, 0.05f), surgeColor);
                _axisModel.AddChild(buttress);
            }

            // THE EYE — glowing torus ring
            _axisEyeRing = new MeshInstance3D();
            var ring = new TorusMesh();
            ring.InnerRadius = 1.8f;
            ring.OuterRadius = 2.2f;
            ring.Rings = 24;
            ring.RingSegments = 32;
            _axisEyeRing.Mesh = ring;
            _axisEyeRing.Position = new Vector3(0, 5f, 0);

            var ringMat = new StandardMaterial3D();
            ringMat.AlbedoColor = new Color(0.2f, 0.1f, 0.6f);
            ringMat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
            ringMat.EmissionEnabled = true;
            ringMat.Emission = new Color(0.4f, 0.15f, 0.9f);
            ringMat.EmissionEnergyMultiplier = 2f;
            _axisEyeRing.MaterialOverride = ringMat;
            _axisModel.AddChild(_axisEyeRing);

            // Eye core — glowing sphere inside the ring
            _axisEye = new MeshInstance3D();
            var eyeSphere = new SphereMesh();
            eyeSphere.Radius = 1.2f;
            eyeSphere.Height = 2.4f;
            _axisEye.Mesh = eyeSphere;
            _axisEye.Position = new Vector3(0, 5f, 0);

            var eyeMat = new StandardMaterial3D();
            eyeMat.AlbedoColor = new Color(0.5f, 0.2f, 1f);
            eyeMat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
            eyeMat.EmissionEnabled = true;
            eyeMat.Emission = new Color(0.6f, 0.2f, 1f);
            eyeMat.EmissionEnergyMultiplier = _eyeBaseEmission;
            _axisEye.MaterialOverride = eyeMat;
            _axisModel.AddChild(_axisEye);

            // Floating debris around AXIS
            for (int i = 0; i < 20; i++)
            {
                var debris = new MeshInstance3D();
                var dBox = new BoxMesh();
                float s = _rng.RandfRange(0.1f, 0.4f);
                dBox.Size = new Vector3(s, s, s * _rng.RandfRange(0.5f, 2f));
                debris.Mesh = dBox;
                debris.Position = new Vector3(
                    _rng.RandfRange(-8f, 8f),
                    _rng.RandfRange(-2f, 18f),
                    _rng.RandfRange(-8f, 8f));
                ApplyDarkMaterial(debris, new Color(0.08f, 0.06f, 0.1f));
                debris.SetMeta("orbit_speed", _rng.RandfRange(0.2f, 0.8f));
                debris.SetMeta("orbit_radius", debris.Position.Length());
                debris.SetMeta("orbit_angle", Mathf.Atan2(debris.Position.X, debris.Position.Z));
                _axisModel.AddChild(debris);
            }
        }

        private void UpdateAXISAnimation(float dt)
        {
            if (_axisModel == null) return;

            // Slow rotation
            _axisModel.RotateY(0.15f * dt);

            // Eye pulse
            _eyePulseTimer += dt;
            float pulse = 1f + 0.3f * Mathf.Sin(_eyePulseTimer * 2f);
            if (_axisEye?.MaterialOverride is StandardMaterial3D eyeMat)
            {
                eyeMat.EmissionEnergyMultiplier = _eyeBaseEmission * pulse;
            }

            // Eye ring counter-rotate
            _axisEyeRing?.RotateY(-0.3f * dt);

            // Orbit debris
            foreach (var child in _axisModel.GetChildren())
            {
                if (child is MeshInstance3D m && m.HasMeta("orbit_speed"))
                {
                    float speed = (float)m.GetMeta("orbit_speed");
                    float radius = (float)m.GetMeta("orbit_radius");
                    float angle = (float)m.GetMeta("orbit_angle") + dt * speed;
                    m.SetMeta("orbit_angle", angle);
                    float y = m.Position.Y;
                    m.Position = new Vector3(
                        Mathf.Sin(angle) * radius, y,
                        Mathf.Cos(angle) * radius);
                }
            }
        }

        private void PulseEye(float intensity)
        {
            _eyeBaseEmission = intensity;
            // Decay back to normal over time
            GetTree().CreateTimer(0.5).Timeout += () => _eyeBaseEmission = 2f;
        }

        // ── Planet ──

        private void BuildPlanet()
        {
            _planet = new Node3D();
            AddChild(_planet);

            var sphere = new MeshInstance3D();
            var sMesh = new SphereMesh();
            sMesh.Radius = 1f;
            sMesh.Height = 2f;
            sMesh.RadialSegments = 32;
            sMesh.Rings = 24;
            sphere.Mesh = sMesh;

            var mat = new StandardMaterial3D();
            mat.AlbedoColor = new Color(0.15f, 0.25f, 0.12f);
            mat.Roughness = 0.95f;
            mat.Metallic = 0f;
            mat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
            sphere.MaterialOverride = mat;
            _planet.AddChild(sphere);

            // No atmosphere sphere — it clips into the surface camera
        }

        // ── Probe ──

        private void BuildProbe()
        {
            // Clean glowing sphere — eye_drone model doesn't scale well for cinematic
            _probeNode = new Node3D();
            AddChild(_probeNode);

            var sphere = new MeshInstance3D();
            var sMesh = new SphereMesh();
            sMesh.Radius = 0.2f;
            sMesh.Height = 0.4f;
            sphere.Mesh = sMesh;

            var mat = new StandardMaterial3D();
            mat.AlbedoColor = new Color(0.9f, 0.7f, 0.2f);
            mat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
            mat.EmissionEnabled = true;
            mat.Emission = new Color(1f, 0.8f, 0.3f);
            mat.EmissionEnergyMultiplier = 4f;
            sphere.MaterialOverride = mat;
            _probeNode.AddChild(sphere);
        }

        private void SpawnProbeTrail()
        {
            if (_probeNode == null || !_probeNode.Visible || !_probeNode.IsInsideTree()) return;

            var dot = new MeshInstance3D();
            var sphere = new SphereMesh();
            sphere.Radius = 0.06f;
            sphere.Height = 0.12f;
            dot.Mesh = sphere;

            var mat = new StandardMaterial3D();
            mat.AlbedoColor = new Color(1f, 0.7f, 0.2f, 0.6f);
            mat.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
            mat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
            mat.EmissionEnabled = true;
            mat.Emission = new Color(1f, 0.6f, 0.1f);
            dot.MaterialOverride = mat;

            var fade = new AutoFadeNode(dot, 0.4f, 0.3f);
            AddChild(fade);
            fade.GlobalPosition = _probeNode.GlobalPosition +
                new Vector3(_rng.RandfRange(-0.1f, 0.1f), _rng.RandfRange(-0.1f, 0.1f), 0.2f);
        }

        // ── Probe swarm ──

        private void LaunchProbeSwarm()
        {
            for (int i = 0; i < 60; i++)
            {
                var p = new MeshInstance3D();
                var sphere = new SphereMesh();
                sphere.Radius = 0.05f;
                sphere.Height = 0.1f;
                p.Mesh = sphere;

                var mat = new StandardMaterial3D();
                mat.AlbedoColor = new Color(1f, 0.8f, 0.3f, 0.8f);
                mat.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
                mat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
                mat.EmissionEnabled = true;
                mat.Emission = new Color(1f, 0.7f, 0.2f);
                mat.EmissionEnergyMultiplier = 2f;
                p.MaterialOverride = mat;

                p.GlobalPosition = _axisModel.GlobalPosition + new Vector3(0, 5, 0);

                // Random outward velocity (spherical distribution)
                var dir = new Vector3(
                    _rng.RandfRange(-1f, 1f),
                    _rng.RandfRange(-0.5f, 0.5f),
                    _rng.RandfRange(-1f, 1f)).Normalized();
                p.SetMeta("velocity", dir * _rng.RandfRange(8f, 20f));

                AddChild(p);
                _swarmProbes.Add(p);
            }
        }

        // ── AXIS Silhouette (for loom-over-planet phase) ──

        private void SpawnAxisSilhouette()
        {
            _axisSilhouette = new Node3D();
            _axisSilhouette.GlobalPosition = _surfaceCenter + new Vector3(0, 80, -20f);
            _axisSilhouette.Scale = Vector3.One * 0.1f;
            AddChild(_axisSilhouette);

            var silMat = AssetLibrary.MakeSilhouette();

            // Procedural silhouette spire
            var spire = new MeshInstance3D();
            var spireBox = new BoxMesh();
            spireBox.Size = new Vector3(2f, 16f, 2f);
            spire.Mesh = spireBox;
            spire.MaterialOverride = silMat;
            _axisSilhouette.AddChild(spire);

            var silTop = new MeshInstance3D();
            var silTopBox = new BoxMesh();
            silTopBox.Size = new Vector3(1.5f, 6f, 1.5f);
            silTop.Mesh = silTopBox;
            silTop.Position = new Vector3(0, 11f, 0);
            silTop.MaterialOverride = silMat;
            _axisSilhouette.AddChild(silTop);

            var silBase = new MeshInstance3D();
            var silBaseBox = new BoxMesh();
            silBaseBox.Size = new Vector3(4f, 3f, 4f);
            silBase.Mesh = silBaseBox;
            silBase.Position = new Vector3(0, -9f, 0);
            silBase.MaterialOverride = silMat;
            _axisSilhouette.AddChild(silBase);

            // Eye ring — faint purple glow
            var eyeRing = new MeshInstance3D();
            var ring = new TorusMesh();
            ring.InnerRadius = 1.8f;
            ring.OuterRadius = 2.2f;
            ring.Rings = 24;
            ring.RingSegments = 32;
            eyeRing.Mesh = ring;
            eyeRing.Position = new Vector3(0, 5f, 0);
            eyeRing.MaterialOverride = AssetLibrary.MakeEmissive(new Color(0.4f, 0.1f, 0.6f), 1.5f);
            _axisSilhouette.AddChild(eyeRing);

            // Eye core
            var eyeCore = new MeshInstance3D();
            var coreSphere = new SphereMesh();
            coreSphere.Radius = 1f;
            coreSphere.Height = 2f;
            eyeCore.Mesh = coreSphere;
            eyeCore.Position = new Vector3(0, 5f, 0);
            eyeCore.MaterialOverride = AssetLibrary.MakeEmissive(new Color(0.5f, 0.15f, 0.8f), 2f);
            _axisSilhouette.AddChild(eyeCore);

            // AURA GLOW — large translucent sphere, faint red/amber edge
            _axisAura = new MeshInstance3D();
            var auraSphere = new SphereMesh();
            auraSphere.Radius = 22f;
            auraSphere.Height = 44f;
            _axisAura.Mesh = auraSphere;
            _axisAura.Position = new Vector3(0, 6f, 0);

            var auraMat = new StandardMaterial3D();
            auraMat.AlbedoColor = new Color(0.9f, 0.35f, 0.08f, 0.2f);
            auraMat.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
            auraMat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
            auraMat.EmissionEnabled = true;
            auraMat.Emission = new Color(0.8f, 0.3f, 0.05f);
            auraMat.EmissionEnergyMultiplier = 0.4f;
            auraMat.CullMode = BaseMaterial3D.CullModeEnum.Disabled; // See from both sides
            _axisAura.MaterialOverride = auraMat;
            _axisSilhouette.AddChild(_axisAura);
        }

        // ── Planet surface (terraformed landscape) ──

        private void BuildPlanetSurface()
        {
            _surfaceRoot = new Node3D();
            _surfaceRoot.GlobalPosition = _surfaceCenter;
            AddChild(_surfaceRoot);

            // Ground plane — green/brown terrain
            var ground = new MeshInstance3D();
            var groundMesh = new PlaneMesh();
            groundMesh.Size = new Vector2(80, 80);
            ground.Mesh = groundMesh;
            var groundMat = new StandardMaterial3D();
            groundMat.AlbedoColor = new Color(0.18f, 0.28f, 0.12f);
            groundMat.Roughness = 0.95f;
            ground.MaterialOverride = groundMat;
            _surfaceRoot.AddChild(ground);

            // Rolling hills — scattered stretched boxes at slight angles
            for (int i = 0; i < 12; i++)
            {
                var hill = new MeshInstance3D();
                var hillMesh = new SphereMesh();
                float r = _rng.RandfRange(3f, 8f);
                hillMesh.Radius = r;
                hillMesh.Height = r * _rng.RandfRange(0.3f, 0.6f);
                hill.Mesh = hillMesh;
                hill.Position = new Vector3(
                    _rng.RandfRange(-30f, 30f),
                    -r * 0.1f,
                    _rng.RandfRange(-30f, 30f));

                var hillMat = new StandardMaterial3D();
                hillMat.AlbedoColor = new Color(
                    _rng.RandfRange(0.15f, 0.25f),
                    _rng.RandfRange(0.22f, 0.35f),
                    _rng.RandfRange(0.08f, 0.15f));
                hillMat.Roughness = 0.9f;
                hill.MaterialOverride = hillMat;
                _surfaceRoot.AddChild(hill);
            }

            // Buildings — placed deliberately in view of the surface camera
            // Camera will be at local (6, 2, 8) looking toward (0, 0, 0)
            // So place buildings to the left, right, and behind the impact zone
            var buildingPlacements = new (string asset, Vector3 pos)[] {
                (AssetLibrary.BLDG_OUTPOST, new Vector3(-10, 0, -5)),
                (AssetLibrary.BLDG_FUEL_TANKS, new Vector3(8, 0, -8)),
                (AssetLibrary.BLDG_BARRACKS, new Vector3(-5, 0, 6)),
            };
            foreach (var (asset, pos) in buildingPlacements)
            {
                var bldg = AssetLibrary.Instantiate(asset);
                if (bldg != null)
                {
                    bldg.Position = pos;
                    bldg.Scale = Vector3.One * 0.2f; // ~12 units wide
                    _surfaceRoot.AddChild(bldg);
                    GD.Print($"[Cinematic] Building at {pos}");
                }
                else
                    GD.PrintErr($"[Cinematic] FAILED: {asset}");
            }

            // Turrets — close to camera view
            var turretPositions = new Vector3[] {
                new(-4, 0, 2), new(5, 0, -2), new(-7, 0, -4), new(3, 0, 6)
            };
            var turretAssets = new[] { AssetLibrary.TURRET_A, AssetLibrary.TURRET_B, AssetLibrary.TURRET_C };
            for (int i = 0; i < turretPositions.Length; i++)
            {
                var turret = AssetLibrary.Instantiate(turretAssets[i % turretAssets.Length]);
                if (turret != null)
                {
                    turret.Position = turretPositions[i];
                    turret.Scale = Vector3.One * 0.4f; // ~5.5 units
                    _surfaceRoot.AddChild(turret);
                }
            }

            // Small props
            var propAssets = AssetLibrary.SmallProps;
            for (int i = 0; i < 8; i++)
            {
                var prop = AssetLibrary.Instantiate(propAssets[_rng.RandiRange(0, propAssets.Length - 1)]);
                if (prop != null)
                {
                    float px = _rng.RandfRange(-12f, 12f);
                    float pz = _rng.RandfRange(-12f, 12f);
                    if (Mathf.Abs(px) < 3f && Mathf.Abs(pz) < 3f) continue;
                    prop.Position = new Vector3(px, 0, pz);
                    prop.Scale = Vector3.One * 1.5f;
                    _surfaceRoot.AddChild(prop);
                }
            }

            // Keep procedural trees (they're nature, no asset for those)
            for (int i = 0; i < 20; i++)
            {
                float tx = _rng.RandfRange(-25f, 25f);
                float tz = _rng.RandfRange(-25f, 25f);
                if (Mathf.Abs(tx) < 5f && Mathf.Abs(tz) < 5f) continue;

                var tree = new Node3D();
                tree.Position = new Vector3(tx, 0, tz);

                var trunk = new MeshInstance3D();
                var trunkMesh = new CylinderMesh();
                float h = _rng.RandfRange(1.5f, 3.5f);
                trunkMesh.TopRadius = 0.15f;
                trunkMesh.BottomRadius = 0.25f;
                trunkMesh.Height = h;
                trunk.Mesh = trunkMesh;
                trunk.Position = new Vector3(0, h / 2f, 0);
                var trunkMat = new StandardMaterial3D();
                trunkMat.AlbedoColor = new Color(0.3f, 0.2f, 0.1f);
                trunkMat.Roughness = 0.95f;
                trunk.MaterialOverride = trunkMat;
                tree.AddChild(trunk);

                var canopy = new MeshInstance3D();
                var canopyMesh = new SphereMesh();
                float cr = _rng.RandfRange(0.8f, 1.8f);
                canopyMesh.Radius = cr;
                canopyMesh.Height = cr * 1.5f;
                canopy.Mesh = canopyMesh;
                canopy.Position = new Vector3(0, h + cr * 0.3f, 0);
                var canopyMat = new StandardMaterial3D();
                canopyMat.AlbedoColor = new Color(
                    _rng.RandfRange(0.1f, 0.2f),
                    _rng.RandfRange(0.35f, 0.55f),
                    _rng.RandfRange(0.05f, 0.15f));
                canopyMat.Roughness = 0.85f;
                canopy.MaterialOverride = canopyMat;
                tree.AddChild(canopy);

                _surfaceRoot.AddChild(tree);
            }

            // Surface lighting — warm sun
            var sun = new DirectionalLight3D();
            sun.RotationDegrees = new Vector3(-40, -20, 0);
            sun.LightColor = new Color(1f, 0.95f, 0.85f);
            sun.LightEnergy = 0.8f;
            sun.ShadowEnabled = true;
            _surfaceRoot.AddChild(sun);
        }

        // ── Surface impact (crater + flying debris + dust) ──

        private void SpawnSurfaceImpact()
        {
            var impactPos = _surfaceCenter + new Vector3(0, 0.2f, 0);

            // Crater ring — dark scorched earth
            var crater = new MeshInstance3D();
            var craterMesh = new CylinderMesh();
            craterMesh.TopRadius = 3f;
            craterMesh.BottomRadius = 3.5f;
            craterMesh.Height = 0.3f;
            crater.Mesh = craterMesh;
            crater.GlobalPosition = impactPos;
            var craterMat = new StandardMaterial3D();
            craterMat.AlbedoColor = new Color(0.08f, 0.06f, 0.04f);
            craterMat.Roughness = 1f;
            crater.MaterialOverride = craterMat;
            AddChild(crater);

            // Central flash — small, brief
            var flash = new MeshInstance3D();
            var flashSphere = new SphereMesh();
            flashSphere.Radius = 0.5f;
            flashSphere.Height = 1f;
            flash.Mesh = flashSphere;
            var flashMat = new StandardMaterial3D();
            flashMat.AlbedoColor = new Color(1f, 0.9f, 0.5f, 0.9f);
            flashMat.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
            flashMat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
            flashMat.EmissionEnabled = true;
            flashMat.Emission = new Color(1f, 0.8f, 0.3f);
            flashMat.EmissionEnergyMultiplier = 3f;
            flash.MaterialOverride = flashMat;
            var flashFade = new AutoFadeNode(flash, 0.5f, 2f);
            AddChild(flashFade);
            flashFade.GlobalPosition = impactPos;

            // Earth/dirt debris flying upward
            for (int i = 0; i < 30; i++)
            {
                var frag = new DeathFragment();
                AddChild(frag);
                frag.GlobalPosition = impactPos + new Vector3(
                    _rng.RandfRange(-1f, 1f), 0, _rng.RandfRange(-1f, 1f));
                // Earth-colored debris
                Color debrisColor = _rng.Randf() > 0.5f
                    ? new Color(0.3f, 0.22f, 0.12f)  // Dirt
                    : new Color(0.15f, 0.25f, 0.1f);  // Grass chunks
                frag.Initialize(debrisColor);
            }

            // Dust cloud — expanding translucent spheres
            for (int i = 0; i < 5; i++)
            {
                var dust = new MeshInstance3D();
                var dustSphere = new SphereMesh();
                dustSphere.Radius = 0.5f;
                dustSphere.Height = 1f;
                dust.Mesh = dustSphere;
                var dustMat = new StandardMaterial3D();
                dustMat.AlbedoColor = new Color(0.4f, 0.35f, 0.25f, 0.4f);
                dustMat.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
                dustMat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
                dust.MaterialOverride = dustMat;

                var dustFade = new AutoFadeNode(dust, 2f, 4f);
                AddChild(dustFade);
                dustFade.GlobalPosition = impactPos + new Vector3(
                    _rng.RandfRange(-2f, 2f), _rng.RandfRange(0.5f, 2f), _rng.RandfRange(-2f, 2f));
            }

            // Shockwave ring on the ground
            var ring = new MeshInstance3D();
            var ringMesh = new TorusMesh();
            ringMesh.InnerRadius = 0.5f;
            ringMesh.OuterRadius = 0.8f;
            ringMesh.Rings = 24;
            ringMesh.RingSegments = 32;
            ring.Mesh = ringMesh;
            var ringMat = new StandardMaterial3D();
            ringMat.AlbedoColor = new Color(1f, 0.7f, 0.3f, 0.6f);
            ringMat.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
            ringMat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
            ringMat.EmissionEnabled = true;
            ringMat.Emission = new Color(1f, 0.6f, 0.2f);
            ring.MaterialOverride = ringMat;
            var ringFade = new AutoFadeNode(ring, 1.2f, 8f);
            AddChild(ringFade);
            ringFade.GlobalPosition = impactPos;
        }

        // ── Impact explosion (space version — kept for reference) ──

        private void SpawnImpactExplosion()
        {
            var impactPos = new Vector3(0, -3, -60);

            // Central flash
            var flash = new MeshInstance3D();
            var sphere = new SphereMesh();
            sphere.Radius = 2f;
            sphere.Height = 4f;
            flash.Mesh = sphere;

            var mat = new StandardMaterial3D();
            mat.AlbedoColor = new Color(1f, 0.9f, 0.5f, 1f);
            mat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
            mat.EmissionEnabled = true;
            mat.Emission = new Color(1f, 0.8f, 0.3f);
            mat.EmissionEnergyMultiplier = 5f;
            flash.MaterialOverride = mat;

            var flashFade = new AutoFadeNode(flash, 0.8f, 3f);
            AddChild(flashFade);
            flashFade.GlobalPosition = impactPos;

            // Debris ring
            for (int i = 0; i < 20; i++)
            {
                var d = new MeshInstance3D();
                var dBox = new BoxMesh();
                float s = _rng.RandfRange(0.1f, 0.4f);
                dBox.Size = new Vector3(s, s, s);
                d.Mesh = dBox;

                var dMat = new StandardMaterial3D();
                dMat.AlbedoColor = new Color(0.4f, 0.3f, 0.2f);
                dMat.Roughness = 0.9f;
                d.MaterialOverride = dMat;

                var frag = new DeathFragment();
                AddChild(frag);
                frag.GlobalPosition = impactPos;
                frag.Initialize(new Color(0.5f, 0.35f, 0.2f));
            }
        }

        private void ScreenFlash()
        {
            _fadeRect.Color = new Color(1, 0.95f, 0.8f, 0.6f);
            GetTree().CreateTimer(0.1).Timeout += () =>
            {
                if (_fadeRect != null) _fadeRect.Color = new Color(0, 0, 0, 0);
            };
        }

        // ── Starfield ──

        private void BuildStarfield()
        {
            for (int i = 0; i < 200; i++)
            {
                var star = new MeshInstance3D();
                var sphere = new SphereMesh();
                float size = _rng.RandfRange(0.02f, 0.08f);
                sphere.Radius = size;
                sphere.Height = size * 2;
                star.Mesh = sphere;

                star.Position = new Vector3(
                    _rng.RandfRange(-100f, 100f),
                    _rng.RandfRange(-50f, 80f),
                    _rng.RandfRange(-120f, -20f));

                var mat = new StandardMaterial3D();
                float brightness = _rng.RandfRange(0.5f, 1f);
                mat.AlbedoColor = new Color(brightness, brightness, brightness * 0.9f);
                mat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
                mat.EmissionEnabled = true;
                mat.Emission = mat.AlbedoColor;
                mat.EmissionEnergyMultiplier = _rng.RandfRange(0.5f, 2f);
                star.MaterialOverride = mat;

                AddChild(star);
            }
        }

        // ── UI ──

        private void BuildUI()
        {
            _uiLayer = new CanvasLayer();
            _uiLayer.Layer = 10;
            AddChild(_uiLayer);

            // Fade rect
            _fadeRect = new ColorRect();
            _fadeRect.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            _fadeRect.Color = new Color(0, 0, 0, 1);
            _fadeRect.MouseFilter = Control.MouseFilterEnum.Ignore;
            _uiLayer.AddChild(_fadeRect);

            // AXIS text
            _axisText = new Label();
            _axisText.SetAnchorsPreset(Control.LayoutPreset.Center);
            _axisText.GrowHorizontal = Control.GrowDirection.Both;
            _axisText.GrowVertical = Control.GrowDirection.Both;
            _axisText.OffsetLeft = -300;
            _axisText.OffsetRight = 300;
            _axisText.OffsetTop = 80;
            _axisText.HorizontalAlignment = HorizontalAlignment.Center;
            _axisText.AddThemeFontSizeOverride("font_size", 36);
            _axisText.AddThemeColorOverride("font_color", new Color(0.7f, 0.3f, 1f));
            _axisText.Text = "";
            _axisText.Visible = false;
            _uiLayer.AddChild(_axisText);

            // Skip hint
            _skipHint = new Label();
            _skipHint.SetAnchorsPreset(Control.LayoutPreset.BottomRight);
            _skipHint.OffsetLeft = -200;
            _skipHint.OffsetTop = -40;
            _skipHint.Text = "Press any key to skip";
            _skipHint.AddThemeFontSizeOverride("font_size", 14);
            _skipHint.AddThemeColorOverride("font_color", new Color(0.4f, 0.4f, 0.4f));
            _uiLayer.AddChild(_skipHint);
        }

        // ── Audio (procedural) ──

        private void BuildAudio()
        {
            _ambientPlayer = new AudioStreamPlayer();
            AddChild(_ambientPlayer);

            // Start ambient drone
            var drone = GenerateAmbientDrone(30f);
            _ambientPlayer.Stream = drone;
            _ambientPlayer.VolumeDb = -8f;
            _ambientPlayer.Play();

            GD.Print("[Cinematic] Ambient drone started");
        }

        private void PlaySFX(AudioStream stream)
        {
            var player = new AudioStreamPlayer();
            player.Stream = stream;
            player.VolumeDb = -3f;
            AddChild(player);
            player.Play();
            player.Finished += () => player.QueueFree();
            GD.Print("[Cinematic] SFX played");
        }

        /// <summary>
        /// Deep ominous drone — layered sine waves with slow modulation.
        /// </summary>
        private static AudioStreamWav GenerateAmbientDrone(float duration)
        {
            int sampleRate = 22050;
            int samples = (int)(sampleRate * duration);
            var data = new byte[samples * 2]; // 16-bit PCM

            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)sampleRate;

                // Base: deep drone at 55Hz
                float val = Mathf.Sin(t * Mathf.Tau * 55f) * 0.3f;
                // Harmonic: 82.5Hz (fifth)
                val += Mathf.Sin(t * Mathf.Tau * 82.5f) * 0.15f;
                // Sub-bass throb
                val += Mathf.Sin(t * Mathf.Tau * 27.5f) * 0.2f * (0.5f + 0.5f * Mathf.Sin(t * 0.3f));
                // Dissonance — slow detuned layer
                val += Mathf.Sin(t * Mathf.Tau * 57f) * 0.08f;
                // Metallic resonance
                val += Mathf.Sin(t * Mathf.Tau * 220f) * 0.03f * (0.5f + 0.5f * Mathf.Sin(t * 0.7f));

                // Fade in over first 2 seconds
                float fadeIn = Mathf.Min(t / 2f, 1f);
                val *= fadeIn * 0.6f;

                short sample = (short)(Mathf.Clamp(val, -1f, 1f) * 32000);
                data[i * 2] = (byte)(sample & 0xFF);
                data[i * 2 + 1] = (byte)((sample >> 8) & 0xFF);
            }

            var stream = new AudioStreamWav();
            stream.Format = AudioStreamWav.FormatEnum.Format16Bits;
            stream.MixRate = sampleRate;
            stream.Stereo = false;
            stream.Data = data;
            stream.LoopMode = AudioStreamWav.LoopModeEnum.Forward;
            stream.LoopEnd = samples;
            return stream;
        }

        /// <summary>
        /// Whoosh/sweep sound for probe launch.
        /// </summary>
        private static AudioStreamWav GenerateWhoosh(float duration, float startFreq, float endFreq)
        {
            int sampleRate = 22050;
            int samples = (int)(sampleRate * duration);
            var data = new byte[samples * 2];
            var rng = new RandomNumberGenerator();

            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)sampleRate;
                float norm = t / duration;

                float freq = Mathf.Lerp(startFreq, endFreq, norm);
                float val = 0f;

                // Noise-based whoosh
                val += Mathf.Sin(t * Mathf.Tau * freq) * 0.3f;
                val += Mathf.Sin(t * Mathf.Tau * freq * 1.5f) * 0.15f;
                // Add noise
                val += rng.RandfRange(-1f, 1f) * 0.2f * (1f - norm);

                // Envelope: quick attack, gradual fade
                float env = Mathf.Sin(norm * Mathf.Pi);
                val *= env * 0.7f;

                short sample = (short)(Mathf.Clamp(val, -1f, 1f) * 32000);
                data[i * 2] = (byte)(sample & 0xFF);
                data[i * 2 + 1] = (byte)((sample >> 8) & 0xFF);
            }

            var stream = new AudioStreamWav();
            stream.Format = AudioStreamWav.FormatEnum.Format16Bits;
            stream.MixRate = sampleRate;
            stream.Data = data;
            return stream;
        }

        /// <summary>
        /// Deep impact boom.
        /// </summary>
        private static AudioStreamWav GenerateImpactBoom(float duration, float freq)
        {
            int sampleRate = 22050;
            int samples = (int)(sampleRate * duration);
            var data = new byte[samples * 2];
            var rng = new RandomNumberGenerator();

            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)sampleRate;
                float norm = t / duration;

                // Decaying sine at low frequency
                float val = Mathf.Sin(t * Mathf.Tau * freq * (1f - norm * 0.5f)) * Mathf.Exp(-norm * 4f);
                // Sub hit
                val += Mathf.Sin(t * Mathf.Tau * freq * 0.5f) * Mathf.Exp(-norm * 3f) * 0.5f;
                // Noise crack on attack
                if (norm < 0.1f)
                    val += rng.RandfRange(-1f, 1f) * (1f - norm * 10f) * 0.4f;

                val *= 0.8f;

                short sample = (short)(Mathf.Clamp(val, -1f, 1f) * 32000);
                data[i * 2] = (byte)(sample & 0xFF);
                data[i * 2 + 1] = (byte)((sample >> 8) & 0xFF);
            }

            var stream = new AudioStreamWav();
            stream.Format = AudioStreamWav.FormatEnum.Format16Bits;
            stream.MixRate = sampleRate;
            stream.Data = data;
            return stream;
        }

        /// <summary>
        /// Menacing low tone for AXIS "speaking" — not voice, just ominous presence.
        /// </summary>
        private static AudioStreamWav GenerateAxisVoiceTone(float duration)
        {
            int sampleRate = 22050;
            int samples = (int)(sampleRate * duration);
            var data = new byte[samples * 2];

            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)sampleRate;
                float norm = t / duration;

                // Dark chord: root + flat fifth (tritone = evil)
                float val = Mathf.Sin(t * Mathf.Tau * 65f) * 0.3f;   // Low C
                val += Mathf.Sin(t * Mathf.Tau * 92f) * 0.2f;         // Tritone
                val += Mathf.Sin(t * Mathf.Tau * 130f) * 0.1f;        // Octave
                // Growl modulation
                val *= 1f + 0.3f * Mathf.Sin(t * Mathf.Tau * 3f);
                // Envelope
                float env = Mathf.Sin(norm * Mathf.Pi) * 0.7f;
                val *= env;

                short sample = (short)(Mathf.Clamp(val, -1f, 1f) * 32000);
                data[i * 2] = (byte)(sample & 0xFF);
                data[i * 2 + 1] = (byte)((sample >> 8) & 0xFF);
            }

            var stream = new AudioStreamWav();
            stream.Format = AudioStreamWav.FormatEnum.Format16Bits;
            stream.MixRate = sampleRate;
            stream.Data = data;
            return stream;
        }

        // ── Debug: measure asset sizes ──

        private void DebugAssetSizes()
        {
            var testAssets = new[] {
                AssetLibrary.AXIS_REPEATER,
                AssetLibrary.AXIS_EYE_DRONE,
                AssetLibrary.BLDG_OUTPOST,
                AssetLibrary.BLDG_FUEL_TANKS,
                AssetLibrary.TURRET_A,
                AssetLibrary.PROP_CRATE_A,
                AssetLibrary.PROP_BARREL
            };

            foreach (var path in testAssets)
            {
                var instance = AssetLibrary.Instantiate(path);
                if (instance != null)
                {
                    AddChild(instance);
                    instance.Position = new Vector3(0, -1000, 0); // Off screen
                    var aabb = AssetLibrary.GetCombinedAABB(instance);
                    GD.Print($"[AssetSize] {System.IO.Path.GetFileName(path)} -> AABB size: {aabb.Size} (max dim: {Mathf.Max(aabb.Size.X, Mathf.Max(aabb.Size.Y, aabb.Size.Z)):F1})");
                    instance.QueueFree();
                }
                else
                {
                    GD.PrintErr($"[AssetSize] FAILED to load: {path}");
                }
            }
        }

        // ── Space Environment ──

        private void BuildSpaceEnvironment()
        {
            var worldEnv = new WorldEnvironment();
            var env = new Godot.Environment();
            env.BackgroundMode = Godot.Environment.BGMode.Color;
            env.BackgroundColor = new Color(0.01f, 0.005f, 0.02f); // Near-black with slight purple
            env.AmbientLightColor = new Color(0.05f, 0.03f, 0.08f);
            env.AmbientLightEnergy = 0.2f;
            env.TonemapMode = Godot.Environment.ToneMapper.Filmic;
            env.GlowEnabled = true;
            env.GlowIntensity = 0.6f;
            env.GlowBloom = 0.3f;
            env.FogEnabled = false;
            worldEnv.Environment = env;
            AddChild(worldEnv);
        }

        // ── Helpers ──

        private static void ApplyDarkMaterial(MeshInstance3D mesh, Color color)
        {
            var mat = new StandardMaterial3D();
            mat.AlbedoColor = color;
            mat.Roughness = 0.95f;
            mat.Metallic = 0.3f;
            // Subtle dark emission for visibility against starfield
            mat.EmissionEnabled = true;
            mat.Emission = color * 0.5f;
            mat.EmissionEnergyMultiplier = 0.2f;
            mesh.MaterialOverride = mat;
        }

        /// <summary>
        /// Apply AXIS power-surge material — dark base with faint emissive edge glow.
        /// Uses Fresnel-like effect via rim lighting: dark center, glowing edges.
        /// </summary>
        private static void ApplyAxisSurgeMaterial(MeshInstance3D mesh, Color baseColor, Color glowColor)
        {
            var mat = new StandardMaterial3D();
            mat.AlbedoColor = baseColor;
            mat.Roughness = 0.9f;
            mat.Metallic = 0.4f;
            mat.EmissionEnabled = true;
            mat.Emission = glowColor;
            mat.EmissionEnergyMultiplier = 0.6f;
            // Grow pushes vertices outward slightly — combined with emission gives edge glow feel
            mat.GrowAmount = 0.02f;
            mat.Grow = true;
            mesh.MaterialOverride = mat;
        }

        private void TransitionToGame()
        {
            GameManager.Instance?.StartVineBattle();
        }
    }
}
