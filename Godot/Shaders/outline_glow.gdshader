shader_type spatial;
render_mode unshaded, cull_front;

// Glow outline — apply to a slightly scaled-up duplicate mesh behind the original
uniform vec4 outline_color : source_color = vec4(1.0, 1.0, 0.5, 1.0);
uniform float outline_thickness : hint_range(0.0, 0.1) = 0.03;
uniform float glow_intensity : hint_range(0.0, 3.0) = 1.5;
uniform float pulse_speed : hint_range(0.0, 5.0) = 2.0;

void vertex() {
    VERTEX += NORMAL * outline_thickness;
}

void fragment() {
    float pulse = sin(TIME * pulse_speed) * 0.2 + 0.8;
    ALBEDO = outline_color.rgb;
    EMISSION = outline_color.rgb * glow_intensity * pulse;
    ALPHA = outline_color.a * pulse;
}
