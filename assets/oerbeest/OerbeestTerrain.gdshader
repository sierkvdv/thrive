shader_type spatial;

// Oerbeest land: beach near the water, grass on the land, rock on steep slopes (world space, no UV needed)
uniform sampler2D grass_albedo : source_color, filter_linear_mipmap, repeat_enable;
uniform sampler2D grass_normal : hint_normal, filter_linear_mipmap, repeat_enable;
uniform sampler2D beach_albedo : source_color, filter_linear_mipmap, repeat_enable;
uniform sampler2D beach_normal : hint_normal, filter_linear_mipmap, repeat_enable;
uniform sampler2D cliff_albedo : source_color, filter_linear_mipmap, repeat_enable;
uniform sampler2D cliff_normal : hint_normal, filter_linear_mipmap, repeat_enable;
uniform float tile_size = 9.0;
uniform float beach_height = 0.4;
uniform float sea_level = -1.5;

varying vec3 world_pos;
varying vec3 world_normal;

void vertex() {
	world_pos = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
	world_normal = normalize((MODEL_MATRIX * vec4(NORMAL, 0.0)).xyz);
}

void fragment() {
	vec2 uv = world_pos.xz / tile_size;
	vec2 uv_far = world_pos.xz / (tile_size * 4.3);

	vec3 grass = mix(texture(grass_albedo, uv).rgb, texture(grass_albedo, uv_far).rgb, 0.35);
	vec3 beach = texture(beach_albedo, uv * 0.8).rgb;
	vec3 cliff = texture(cliff_albedo, uv * 0.6).rgb;

	float beach_w = 1.0 - smoothstep(beach_height - 0.5, beach_height + 0.4, world_pos.y);
	float slope = 1.0 - world_normal.y;
	float cliff_w = smoothstep(0.22, 0.4, slope);

	vec3 col = mix(grass, beach, beach_w);
	col = mix(col, cliff, cliff_w);

	// Wet sand darkens just above the waterline
	col *= mix(0.75, 1.0, smoothstep(sea_level, sea_level + 0.6, world_pos.y));

	vec3 n_grass = texture(grass_normal, uv).rgb;
	vec3 n_beach = texture(beach_normal, uv * 0.8).rgb;
	vec3 n_cliff = texture(cliff_normal, uv * 0.6).rgb;
	vec3 n = mix(mix(n_grass, n_beach, beach_w), n_cliff, cliff_w);

	ALBEDO = col;
	NORMAL_MAP = n;
	ROUGHNESS = 0.92;
}
