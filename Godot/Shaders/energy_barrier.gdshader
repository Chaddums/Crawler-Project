shader_type spatial;
render_mode unshaded, cull_disabled;

// Hex-pattern energy shield
uniform vec4 barrier_color : source_color = vec4(0.3, 0.7, 1.0, 0.8);
uniform float hex_scale : hint_range(1.0, 50.0) = 12.0;
uniform float pulse_speed : hint_range(0.0, 10.0) = 3.0;
uniform float edge_brightness : hint_range(0.0, 3.0) = 2.0;
uniform float opacity : hint_range(0.0, 1.0) = 0.6;

float hex_dist(vec2 p) {
    p = abs(p);
    float c = dot(p, normalize(vec2(1.0, 1.732)));
    return max(c, p.x);
}

void fragment() {
    vec2 uv = UV * hex_scale;
    vec2 r = vec2(1.0, 1.732);
    vec2 h = r * 0.5;

    vec2 a = mod(uv, r) - h;
    vec2 b = mod(uv - h, r) - h;
    vec2 gv = length(a) < length(b) ? a : b;

    float dist = hex_dist(gv);
    float hex = smoothstep(0.05, 0.02, abs(dist - 0.5));

    float pulse = sin(TIME * pulse_speed) * 0.3 + 0.7;

    ALBEDO = barrier_color.rgb;
    EMISSION = barrier_color.rgb * hex * edge_brightness * pulse;
    ALPHA = (0.15 + hex * 0.5) * opacity * pulse;
}
