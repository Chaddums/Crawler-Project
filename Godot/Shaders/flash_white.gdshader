shader_type spatial;

// Hit flash — animate flash_amount 0→1→0 quickly
uniform float flash_amount : hint_range(0.0, 1.0) = 0.0;
uniform vec4 flash_color : source_color = vec4(1.0, 1.0, 1.0, 1.0);
uniform vec4 albedo_color : source_color = vec4(1.0, 1.0, 1.0, 1.0);

void fragment() {
    ALBEDO = mix(albedo_color.rgb, flash_color.rgb, flash_amount);
    EMISSION = flash_color.rgb * flash_amount * 2.0;
}
