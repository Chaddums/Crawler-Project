shader_type spatial;

// Fire/burning status effect
uniform float burn_amount : hint_range(0.0, 1.0) = 0.0;
uniform vec4 fire_color1 : source_color = vec4(1.0, 0.8, 0.2, 1.0);
uniform vec4 fire_color2 : source_color = vec4(1.0, 0.3, 0.0, 1.0);
uniform vec4 albedo_color : source_color = vec4(1.0, 1.0, 1.0, 1.0);
uniform float distortion_strength : hint_range(0.0, 0.1) = 0.03;

float hash(vec2 p) {
    return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453);
}

float noise(vec2 p) {
    vec2 i = floor(p);
    vec2 f = fract(p);
    f = f * f * (3.0 - 2.0 * f);
    return mix(mix(hash(i), hash(i + vec2(1,0)), f.x),
               mix(hash(i + vec2(0,1)), hash(i + vec2(1,1)), f.x), f.y);
}

void fragment() {
    vec2 distortion = vec2(noise(UV * 4.0 + vec2(TIME * 0.1, TIME * 0.2)));
    distortion = (distortion - 0.5) * distortion_strength * burn_amount;

    float fire_gradient = sin(UV.y * 10.0 - TIME * 5.0) * 0.5 + 0.5;
    vec3 fire_color = mix(fire_color1.rgb, fire_color2.rgb, fire_gradient);

    float burn_edge = smoothstep(0.3, 0.7, UV.y + burn_amount * 1.5 - 0.5);

    ALBEDO = mix(albedo_color.rgb, fire_color, burn_edge * burn_amount * 0.6);
    EMISSION = fire_color * burn_edge * burn_amount * 2.0;
}
