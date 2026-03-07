shader_type spatial;
render_mode unshaded, cull_disabled;

// Dissolve death effect — animate dissolve_amount from 0→1
uniform float dissolve_amount : hint_range(0.0, 1.0) = 0.0;
uniform vec4 edge_color : source_color = vec4(1.0, 0.5, 0.0, 1.0);
uniform float edge_width : hint_range(0.0, 0.2) = 0.06;
uniform vec4 albedo_color : source_color = vec4(1.0, 1.0, 1.0, 1.0);

// Procedural noise since we don't require a texture
float hash(vec2 p) {
    return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453);
}

float noise(vec2 p) {
    vec2 i = floor(p);
    vec2 f = fract(p);
    f = f * f * (3.0 - 2.0 * f);
    float a = hash(i);
    float b = hash(i + vec2(1.0, 0.0));
    float c = hash(i + vec2(0.0, 1.0));
    float d = hash(i + vec2(1.0, 1.0));
    return mix(mix(a, b, f.x), mix(c, d, f.x), f.y);
}

void fragment() {
    float n = noise(UV * 8.0);

    float dissolve_edge = dissolve_amount + edge_width;
    float alpha = smoothstep(dissolve_amount, dissolve_edge, n);

    // Glowing edge band
    float edge = smoothstep(dissolve_amount, dissolve_amount + edge_width, n);
    edge = edge * (1.0 - smoothstep(dissolve_amount + edge_width * 0.5, dissolve_edge, n));

    ALBEDO = mix(albedo_color.rgb, edge_color.rgb, edge);
    ALPHA = albedo_color.a * alpha;
    EMISSION = edge_color.rgb * edge * 3.0;
}
