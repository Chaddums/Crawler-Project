shader_type spatial;

// Ice/frozen status effect
uniform float freeze_amount : hint_range(0.0, 1.0) = 0.0;
uniform vec4 ice_color : source_color = vec4(0.5, 0.8, 1.0, 1.0);
uniform vec4 albedo_color : source_color = vec4(1.0, 1.0, 1.0, 1.0);
uniform float crystal_intensity : hint_range(0.0, 1.0) = 0.5;

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
    float ice_pattern = noise(UV * 12.0 + vec2(TIME * 0.02, 0.0));

    vec3 frozen_color = mix(albedo_color.rgb, ice_color.rgb, freeze_amount * 0.6);
    frozen_color += vec3(ice_pattern) * crystal_intensity * freeze_amount;

    ALBEDO = frozen_color;
    EMISSION = ice_color.rgb * freeze_amount * 0.5;
    METALLIC = freeze_amount * 0.8;
    ROUGHNESS = mix(0.8, 0.1, freeze_amount);
}
