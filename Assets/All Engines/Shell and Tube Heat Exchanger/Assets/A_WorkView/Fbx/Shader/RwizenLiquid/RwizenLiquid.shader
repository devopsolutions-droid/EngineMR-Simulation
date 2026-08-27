// Made with Amplify Shader Editor
// Available at the Unity Asset Store - http://u3d.as/y3X 
Shader "Rwizen/SimplePotionLiquid"
{
	Properties
	{
		_EdgeLength ( "Edge length", Range( 2, 50 ) ) = 2
		_Speed("Speed", Float) = 0
		_Color("Color", Color) = (1,0,0,0)
		_Size("Size", Float) = 0.1
		_Albedo("Albedo", 2D) = "white" {}
		_Height("Height", Float) = 0
		_Falloff("Falloff", Float) = 0.02
		_Opacity("Opacity", Float) = 0.76
		_WavePatternColor("Wave Pattern Color", Color) = (0.2470588,0.7764706,0.9098039,1)
		_WavePattern("Wave Pattern", 2D) = "white" {}
		[IntRange]_WavePatternSize("Wave Pattern Size", Range( 1 , 20)) = 5
		_WavePatternPower("Wave Pattern Power", Range( 0 , 100)) = 5
		_WaveAnimSpeed("Wave Anim Speed", Range( -10 , 10)) = 3
		_WavesPattern("WavesPattern", 2D) = "white" {}
		[HideInInspector] _texcoord( "", 2D ) = "white" {}
		[HideInInspector] __dirty( "", Int ) = 1
	}

	SubShader
	{
		Tags{ "RenderType" = "Transparent"  "Queue" = "Overlay+0" "IgnoreProjector" = "True" "IsEmissive" = "true"  }
		Cull Back
		ZWrite On
		ZTest Always
		Blend One OneMinusSrcAlpha
		
		CGINCLUDE
		#include "UnityPBSLighting.cginc"
		#include "UnityShaderVariables.cginc"
		#include "Lighting.cginc"
		#pragma target 3.0
		struct Input
		{
			float2 uv_texcoord;
			float3 worldPos;
		};

		struct SurfaceOutputCustomLightingCustom
		{
			half3 Albedo;
			half3 Normal;
			half3 Emission;
			half Metallic;
			half Smoothness;
			half Occlusion;
			half Alpha;
			Input SurfInput;
			UnityGIInput GIData;
		};

		uniform sampler2D _WavePattern;
		uniform float _WavePatternSize;
		uniform float _WaveAnimSpeed;
		uniform sampler2D _WavesPattern;
		uniform float4 _WavePatternColor;
		uniform float _Speed;
		uniform float _Size;
		uniform float _Height;
		uniform float _Falloff;
		uniform float _Opacity;
		uniform float _WavePatternPower;
		uniform float4 _Color;
		uniform sampler2D _Albedo;
		uniform float4 _Albedo_ST;
		uniform float _EdgeLength;

	

		void vertexDataFunc( inout appdata_full v )
		{
		}

		inline half4 LightingStandardCustomLighting( inout SurfaceOutputCustomLightingCustom s, half3 viewDir, UnityGI gi )
		{
			UnityGIInput data = s.GIData;
			Input i = s.SurfInput;
			half4 c = 0;
			float4 transform7 = mul(unity_ObjectToWorld,float4( 0,0,0,1 ));
			float3 ase_worldPos = i.worldPos;
			float4 temp_output_5_0 = ( transform7 - float4( ase_worldPos , 0.0 ) );
			float mulTime2 = _Time.y * _Speed;
			float4 temp_cast_9 = (( 1.0 - _Height )).xxxx;
			float4 clampResult18 = clamp( ( ( ( ( temp_output_5_0 * sin( mulTime2 ) * _Size ) + (temp_output_5_0).y ) - temp_cast_9 ) / _Falloff ) , float4( 0,0,0,0 ) , float4( 1,1,1,1 ) );
			float4 temp_output_21_0 = ( clampResult18 * _Opacity );
			float2 uv_Albedo = i.uv_texcoord * _Albedo_ST.xy + _Albedo_ST.zw;
			float4 Albedo57 = ( _Color * tex2D( _Albedo, uv_Albedo ) );
			c.rgb = Albedo57.rgb;
			c.a = temp_output_21_0.x;
			c.rgb *= c.a;
			return c;
		}

		inline void LightingStandardCustomLighting_GI( inout SurfaceOutputCustomLightingCustom s, UnityGIInput data, inout UnityGI gi )
		{
			s.GIData = data;
		}

		void surf( Input i , inout SurfaceOutputCustomLightingCustom o )
		{
			o.SurfInput = i;
			float2 appendResult44 = (float2(_WavePatternSize , _WavePatternSize));
			float4 WaveSpeed36 = ( _Time * _WaveAnimSpeed );
			float2 appendResult43 = (float2(1 , WaveSpeed36.x));
			float2 uv_TexCoord47 = i.uv_texcoord * appendResult44 + appendResult43;
			float4 WavePattern49 = tex2D( _WavePattern, uv_TexCoord47 );
			float2 appendResult29 = (float2(1 , ( 1.0 - ( WaveSpeed36 / 5.0 ) ).x));
			float2 uv_TexCoord30 = i.uv_texcoord * float2( 1,1 ) + appendResult29;
			float4 waves32 = tex2D( _WavesPattern, uv_TexCoord30 );
			float4 WavePatternColor45 = _WavePatternColor;
			float4 transform7 = mul(unity_ObjectToWorld,float4( 0,0,0,1 ));
			float3 ase_worldPos = i.worldPos;
			float4 temp_output_5_0 = ( transform7 - float4( ase_worldPos , 0.0 ) );
			float mulTime2 = _Time.y * _Speed;
			float4 temp_cast_4 = (( 1.0 - _Height )).xxxx;
			float4 clampResult18 = clamp( ( ( ( ( temp_output_5_0 * sin( mulTime2 ) * _Size ) + (temp_output_5_0).y ) - temp_cast_4 ) / _Falloff ) , float4( 0,0,0,0 ) , float4( 1,1,1,1 ) );
			float4 temp_output_21_0 = ( clampResult18 * _Opacity );
			float4 Opacity86 = temp_output_21_0;
			float4 lerpResult81 = lerp( float4( 0,0,0,0 ) , ( ( WavePattern49 * waves32 ) * WavePatternColor45 ) , Opacity86);
			float WavePower50 = _WavePatternPower;
			float4 Emission83 = ( lerpResult81 * WavePower50 );
			o.Emission = Emission83.rgb;
		}

		ENDCG
		CGPROGRAM
		#pragma surface surf StandardCustomLighting keepalpha fullforwardshadows vertex:vertexDataFunc 

		ENDCG
		Pass
		{
			Name "ShadowCaster"
			Tags{ "LightMode" = "ShadowCaster" }
			ZWrite On
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#pragma target 4.6
			#pragma multi_compile_shadowcaster
			#pragma multi_compile UNITY_PASS_SHADOWCASTER
			#pragma skip_variants FOG_LINEAR FOG_EXP FOG_EXP2
			#include "HLSLSupport.cginc"
			#if ( SHADER_API_D3D11 || SHADER_API_GLCORE || SHADER_API_GLES || SHADER_API_GLES3 || SHADER_API_METAL || SHADER_API_VULKAN )
				#define CAN_SKIP_VPOS
			#endif
			#include "UnityCG.cginc"
			#include "Lighting.cginc"
			#include "UnityPBSLighting.cginc"
			sampler3D _DitherMaskLOD;
			struct v2f
			{
				V2F_SHADOW_CASTER;
				float2 customPack1 : TEXCOORD1;
				float3 worldPos : TEXCOORD2;
				UNITY_VERTEX_INPUT_INSTANCE_ID
				UNITY_VERTEX_OUTPUT_STEREO
			};
			v2f vert( appdata_full v )
			{
				v2f o;
				UNITY_SETUP_INSTANCE_ID( v );
				UNITY_INITIALIZE_OUTPUT( v2f, o );
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO( o );
				UNITY_TRANSFER_INSTANCE_ID( v, o );
				Input customInputData;
				vertexDataFunc( v );
				float3 worldPos = mul( unity_ObjectToWorld, v.vertex ).xyz;
				half3 worldNormal = UnityObjectToWorldNormal( v.normal );
				o.customPack1.xy = customInputData.uv_texcoord;
				o.customPack1.xy = v.texcoord;
				o.worldPos = worldPos;
				TRANSFER_SHADOW_CASTER_NORMALOFFSET( o )
				return o;
			}
			half4 frag( v2f IN
			#if !defined( CAN_SKIP_VPOS )
			, UNITY_VPOS_TYPE vpos : VPOS
			#endif
			) : SV_Target
			{
				UNITY_SETUP_INSTANCE_ID( IN );
				Input surfIN;
				UNITY_INITIALIZE_OUTPUT( Input, surfIN );
				surfIN.uv_texcoord = IN.customPack1.xy;
				float3 worldPos = IN.worldPos;
				half3 worldViewDir = normalize( UnityWorldSpaceViewDir( worldPos ) );
				surfIN.worldPos = worldPos;
				SurfaceOutputCustomLightingCustom o;
				UNITY_INITIALIZE_OUTPUT( SurfaceOutputCustomLightingCustom, o )
				surf( surfIN, o );
				UnityGI gi;
				UNITY_INITIALIZE_OUTPUT( UnityGI, gi );
				o.Alpha = LightingStandardCustomLighting( o, worldViewDir, gi ).a;
				#if defined( CAN_SKIP_VPOS )
				float2 vpos = IN.pos;
				#endif
				half alphaRef = tex3D( _DitherMaskLOD, float3( vpos.xy * 0.25, o.Alpha * 0.9375 ) ).a;
				clip( alphaRef - 0.01 );
				SHADOW_CASTER_FRAGMENT( IN )
			}
			ENDCG
		}
	}
	Fallback "Diffuse"
	CustomEditor "ASEMaterialInspector"
}
/*ASEBEGIN
Version=18800
-1920;6;1920;1011;-1681.226;-805.3;1;True;False
Node;AmplifyShaderEditor.CommentaryNode;37;1426.546,913.6921;Inherit;False;806.728;359.155;Wave Speed;4;33;34;35;36;Wave Speed;1,1,1,1;0;0
Node;AmplifyShaderEditor.TimeNode;33;1550.138,963.6921;Inherit;False;0;5;FLOAT4;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.RangedFloatNode;34;1476.546,1156.847;Float;False;Property;_WaveAnimSpeed;Wave Anim Speed;19;0;Create;True;0;0;0;False;0;False;3;-4.42;-10;10;0;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;35;1792.675,1087.106;Inherit;False;2;2;0;FLOAT4;0,0,0,0;False;1;FLOAT;0;False;1;FLOAT4;0
Node;AmplifyShaderEditor.CommentaryNode;38;1371.189,310.0364;Inherit;False;1568.628;478.595;Wave Effect;10;24;25;26;27;28;29;30;31;32;23;Wave Effect;1,1,1,1;0;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;36;2009.274,1098.242;Float;False;WaveSpeed;-1;True;1;0;FLOAT4;0,0,0,0;False;1;FLOAT4;0
Node;AmplifyShaderEditor.RangedFloatNode;24;1448.054,672.6315;Float;False;Constant;_Float0;Float 0;7;0;Create;True;0;0;0;False;0;False;5;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode;23;1421.189,586.0344;Inherit;False;36;WaveSpeed;1;0;OBJECT;;False;1;FLOAT4;0
Node;AmplifyShaderEditor.RangedFloatNode;1;-1061.122,-52.18835;Float;False;Property;_Speed;Speed;6;0;Create;True;0;0;0;False;0;False;0;7.88;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.ObjectToWorldTransfNode;7;-1144.93,49.36661;Inherit;False;1;0;FLOAT4;0,0,0,1;False;5;FLOAT4;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.SimpleTimeNode;2;-877,-46;Inherit;False;1;0;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.WorldPosInputsNode;8;-1009.122,236.8116;Float;False;0;4;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3
Node;AmplifyShaderEditor.SimpleDivideOpNode;25;1629.021,599.89;Inherit;False;2;0;FLOAT4;0,0,0,0;False;1;FLOAT;0;False;1;FLOAT4;0
Node;AmplifyShaderEditor.OneMinusNode;26;1762.381,589.4984;Inherit;False;1;0;FLOAT4;0,0,0,0;False;1;FLOAT4;0
Node;AmplifyShaderEditor.SimpleSubtractOpNode;5;-756.1223,189.8116;Inherit;False;2;0;FLOAT4;0,0,0,0;False;1;FLOAT3;0,0,0;False;1;FLOAT4;0
Node;AmplifyShaderEditor.Vector2Node;27;1769.309,431.8918;Float;False;Constant;_Vector2;Vector 2;7;0;Create;True;0;0;0;False;0;False;1,0;0,0;0;3;FLOAT2;0;FLOAT;1;FLOAT;2
Node;AmplifyShaderEditor.SinOpNode;3;-692.1223,-45.18835;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;6;-777.4153,46.94485;Float;False;Property;_Size;Size;8;0;Create;True;0;0;0;False;0;False;0.1;0.5;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.CommentaryNode;51;2395.401,910.6849;Inherit;False;1462.2;689;Wave Main;12;39;40;41;42;43;44;45;46;47;48;49;50;Wave Main;1,1,1,1;0;0
Node;AmplifyShaderEditor.RangedFloatNode;13;-289.2947,357.9797;Float;False;Property;_Height;Height;11;0;Create;True;0;0;0;False;0;False;0;1.06;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.Vector2Node;28;1961.709,360.0364;Float;False;Constant;_Vector3;Vector 3;7;0;Create;True;0;0;0;False;0;False;1,1;0,0;0;3;FLOAT2;0;FLOAT;1;FLOAT;2
Node;AmplifyShaderEditor.DynamicAppendNode;29;1965.018,483.85;Inherit;False;FLOAT2;4;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;4;-525.1223,-58.18835;Inherit;True;3;3;0;FLOAT4;0,0,0,0;False;1;FLOAT;0;False;2;FLOAT;0;False;1;FLOAT4;0
Node;AmplifyShaderEditor.GetLocalVarNode;42;2557.401,1488.685;Inherit;False;36;WaveSpeed;1;0;OBJECT;;False;1;FLOAT4;0
Node;AmplifyShaderEditor.ComponentMaskNode;10;-514.0292,243.2977;Inherit;False;False;True;False;False;1;0;FLOAT4;0,0,0,0;False;1;FLOAT;0
Node;AmplifyShaderEditor.Vector2Node;41;2525.401,1280.685;Float;False;Constant;_Vector0;Vector 0;6;0;Create;True;0;0;0;False;0;False;1,0;0,0;0;3;FLOAT2;0;FLOAT;1;FLOAT;2
Node;AmplifyShaderEditor.RangedFloatNode;40;2445.401,1168.685;Float;False;Property;_WavePatternSize;Wave Pattern Size;17;1;[IntRange];Create;True;0;0;0;False;0;False;5;3;1;20;0;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleAddOpNode;9;-238.1219,31.81161;Inherit;True;2;2;0;FLOAT4;0,0,0,0;False;1;FLOAT;0;False;1;FLOAT4;0
Node;AmplifyShaderEditor.TextureCoordinatesNode;30;2160.728,475.1903;Inherit;False;0;-1;2;3;2;SAMPLER2D;;False;0;FLOAT2;1,1;False;1;FLOAT2;0,0;False;5;FLOAT2;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.OneMinusNode;12;-116.7756,290.3243;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.DynamicAppendNode;43;2797.401,1392.685;Inherit;False;FLOAT2;4;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.DynamicAppendNode;44;2781.401,1184.685;Inherit;False;FLOAT2;4;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.TextureCoordinatesNode;47;2989.401,1328.685;Inherit;False;0;-1;2;3;2;SAMPLER2D;;False;0;FLOAT2;1,1;False;1;FLOAT2;0,0;False;5;FLOAT2;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.SimpleSubtractOpNode;11;51.78445,172.7243;Inherit;True;2;0;FLOAT4;0,0,0,0;False;1;FLOAT;0;False;1;FLOAT4;0
Node;AmplifyShaderEditor.SamplerNode;31;2402.628,459.8181;Inherit;True;Property;_WavesPattern;WavesPattern;20;0;Create;True;0;0;0;False;0;False;-1;None;937c2d88ef9b4e889d5a0a6a952c1a4b;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;1;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.RangedFloatNode;16;221.9577,414.4579;Float;False;Property;_Falloff;Falloff;13;0;Create;True;0;0;0;False;0;False;0.02;0.26;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;32;2715.817,451.2737;Float;False;waves;-1;True;1;0;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.CommentaryNode;84;1881.667,1885.356;Inherit;False;1633.998;666.7612;;11;67;68;71;72;75;79;80;81;82;83;87;Emission;1,1,1,1;0;0
Node;AmplifyShaderEditor.SimpleDivideOpNode;14;403.2778,188.4044;Inherit;True;2;0;FLOAT4;0,0,0,0;False;1;FLOAT;0;False;1;FLOAT4;0
Node;AmplifyShaderEditor.SamplerNode;48;3245.401,1311.085;Inherit;True;Property;_WavePattern;Wave Pattern;16;0;Create;True;0;0;0;False;0;False;-1;None;9fbef4b79ca3b784ba023cb1331520d5;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;1;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.RangedFloatNode;20;1134.606,122.7493;Float;False;Property;_Opacity;Opacity;14;0;Create;True;0;0;0;False;0;False;0.76;0.95;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.ColorNode;39;2445.401,960.6849;Float;False;Property;_WavePatternColor;Wave Pattern Color;15;0;Create;True;0;0;0;False;0;False;0.2470588,0.7764706,0.9098039,1;0.06950871,0.1812541,0.2075472,1;False;0;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.GetLocalVarNode;67;1950.543,2252.246;Inherit;False;32;waves;1;0;OBJECT;;False;1;COLOR;0
Node;AmplifyShaderEditor.ClampOpNode;18;730.8206,188.7693;Inherit;True;3;0;FLOAT4;0,0,0,0;False;1;FLOAT4;0,0,0,0;False;2;FLOAT4;1,1,1,1;False;1;FLOAT4;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;49;3633.601,1311.585;Float;False;WavePattern;-1;True;1;0;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;21;1358.402,-21.4934;Inherit;False;2;2;0;FLOAT4;0,0,0,0;False;1;FLOAT;0;False;1;FLOAT4;0
Node;AmplifyShaderEditor.GetLocalVarNode;68;1948.667,2162.959;Inherit;False;49;WavePattern;1;0;OBJECT;;False;1;COLOR;0
Node;AmplifyShaderEditor.WireNode;71;2148.963,2231.426;Inherit;False;1;0;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;45;2749.401,960.6849;Float;False;WavePatternColor;-1;True;1;0;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;86;1428.886,114.9137;Inherit;False;Opacity;-1;True;1;0;FLOAT4;0,0,0,0;False;1;FLOAT4;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;75;2345.127,2137.731;Inherit;False;2;2;0;COLOR;0,0,0,0;False;1;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.RangedFloatNode;46;3149.401,1007.385;Float;False;Property;_WavePatternPower;Wave Pattern Power;18;0;Create;True;0;0;0;False;0;False;5;75.4;0;100;0;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode;72;2196.281,2289.847;Inherit;False;45;WavePatternColor;1;0;OBJECT;;False;1;COLOR;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;50;3462.202,1018.485;Float;False;WavePower;-1;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.CommentaryNode;58;-375.7596,617.9486;Inherit;False;820.959;696;ALBEDO;6;52;53;54;55;56;57;Albedo;1,1,1,1;0;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;79;2587.475,2132.55;Inherit;True;2;2;0;COLOR;0,0,0,0;False;1;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.GetLocalVarNode;87;2777.886,2213.914;Inherit;False;86;Opacity;1;0;OBJECT;;False;1;FLOAT4;0
Node;AmplifyShaderEditor.GetLocalVarNode;80;2903.766,2298.86;Inherit;False;50;WavePower;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.SamplerNode;53;-309.7596,859.9486;Inherit;True;Property;_Albedo;Albedo;10;0;Create;True;0;0;0;False;0;False;-1;None;None;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;1;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.ColorNode;52;-293.7596,667.9486;Float;False;Property;_Color;Color;7;0;Create;True;0;0;0;False;0;False;1,0,0,0;0.1407084,0.1686383,0.5849056,0;False;0;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.LerpOp;81;2926.767,2068.456;Inherit;False;3;0;COLOR;0,0,0,0;False;1;COLOR;0,0,0,0;False;2;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;56;57.99939,828.6747;Inherit;False;2;2;0;COLOR;0,0,0,0;False;1;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;82;3108.365,2173.459;Inherit;False;2;2;0;COLOR;0,0,0,0;False;1;FLOAT;0;False;1;COLOR;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;57;221.1993,822.2747;Float;False;Albedo;-1;True;1;0;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.CommentaryNode;65;1209.446,1394.17;Inherit;False;1011.896;389.9557;;5;60;61;62;63;64;Wave Rim Power;0.4103774,0.9154238,1,1;0;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;83;3291.666,2148.457;Float;False;Emission;-1;True;1;0;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;64;1997.342,1573.057;Float;False;WaveRim;-1;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode;61;1259.446,1575.526;Inherit;False;32;waves;1;0;OBJECT;;False;1;COLOR;0
Node;AmplifyShaderEditor.RangedFloatNode;60;1263.197,1444.17;Float;False;Property;_WaveRimPower;Wave Rim Power;9;0;Create;True;0;0;0;False;0;False;7;0;0;10;0;1;FLOAT;0
Node;AmplifyShaderEditor.TFHCRemapNode;62;1554.905,1575.426;Inherit;False;5;0;COLOR;0,0,0,0;False;1;COLOR;0,0,0,0;False;2;COLOR;10,0,0,0;False;3;COLOR;10,0,0,0;False;4;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.GetLocalVarNode;85;1535.651,-93.44305;Inherit;False;83;Emission;1;0;OBJECT;;False;1;COLOR;0
Node;AmplifyShaderEditor.SamplerNode;54;-325.7596,1083.949;Inherit;True;Property;_Normal;Normal;12;0;Create;True;0;0;0;False;0;False;-1;None;None;True;0;False;bump;Auto;True;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;1;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;5;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.GetLocalVarNode;59;1544.631,-197.2814;Inherit;False;57;Albedo;1;0;OBJECT;;False;1;COLOR;0
Node;AmplifyShaderEditor.FresnelNode;63;1766.504,1577.126;Inherit;False;Standard;WorldNormal;ViewDir;False;False;5;0;FLOAT3;0,0,0;False;4;FLOAT3;0,0,0;False;1;FLOAT;0;False;2;FLOAT;1;False;3;FLOAT;5;False;1;FLOAT;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;55;64.39941,1087.875;Float;False;Normal;-1;True;1;0;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.StandardSurfaceOutputNode;0;1784.594,-164.4324;Float;False;True;-1;6;ASEMaterialInspector;0;0;CustomLighting;Rwizen/SimplePotionLiquid;False;False;False;False;False;False;False;False;False;False;False;False;False;False;True;False;False;False;False;False;False;Front;1;False;-1;7;False;-1;False;0;False;-1;0;False;-1;False;0;Custom;0.5;True;True;0;True;Transparent;;Overlay;All;14;all;True;True;True;True;0;False;-1;False;0;False;-1;255;False;-1;255;False;-1;0;False;-1;0;False;-1;0;False;-1;0;False;-1;0;False;-1;0;False;-1;0;False;-1;0;False;-1;True;2;2;10;25;False;0.5;True;3;1;False;-1;10;False;-1;0;0;False;-1;0;False;-1;0;False;-1;0;False;-1;0;False;0;0,0,0,0;VertexOffset;True;False;Cylindrical;False;Relative;0;;0;-1;-1;1;0;False;0;0;False;-1;-1;0;False;-1;0;0;0;False;0.1;False;-1;0;False;-1;False;15;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;2;FLOAT3;0,0,0;False;3;FLOAT3;0,0,0;False;4;FLOAT;0;False;6;FLOAT3;0,0,0;False;7;FLOAT3;0,0,0;False;8;FLOAT;0;False;9;FLOAT;0;False;10;FLOAT;0;False;13;FLOAT3;0,0,0;False;11;FLOAT3;0,0,0;False;12;FLOAT3;0,0,0;False;14;FLOAT4;0,0,0,0;False;15;FLOAT3;0,0,0;False;0
WireConnection;35;0;33;0
WireConnection;35;1;34;0
WireConnection;36;0;35;0
WireConnection;2;0;1;0
WireConnection;25;0;23;0
WireConnection;25;1;24;0
WireConnection;26;0;25;0
WireConnection;5;0;7;0
WireConnection;5;1;8;0
WireConnection;3;0;2;0
WireConnection;29;0;27;1
WireConnection;29;1;26;0
WireConnection;4;0;5;0
WireConnection;4;1;3;0
WireConnection;4;2;6;0
WireConnection;10;0;5;0
WireConnection;9;0;4;0
WireConnection;9;1;10;0
WireConnection;30;0;28;0
WireConnection;30;1;29;0
WireConnection;12;0;13;0
WireConnection;43;0;41;1
WireConnection;43;1;42;0
WireConnection;44;0;40;0
WireConnection;44;1;40;0
WireConnection;47;0;44;0
WireConnection;47;1;43;0
WireConnection;11;0;9;0
WireConnection;11;1;12;0
WireConnection;31;1;30;0
WireConnection;32;0;31;0
WireConnection;14;0;11;0
WireConnection;14;1;16;0
WireConnection;48;1;47;0
WireConnection;18;0;14;0
WireConnection;49;0;48;0
WireConnection;21;0;18;0
WireConnection;21;1;20;0
WireConnection;71;0;67;0
WireConnection;45;0;39;0
WireConnection;86;0;21;0
WireConnection;75;0;68;0
WireConnection;75;1;71;0
WireConnection;50;0;46;0
WireConnection;79;0;75;0
WireConnection;79;1;72;0
WireConnection;81;1;79;0
WireConnection;81;2;87;0
WireConnection;56;0;52;0
WireConnection;56;1;53;0
WireConnection;82;0;81;0
WireConnection;82;1;80;0
WireConnection;57;0;56;0
WireConnection;83;0;82;0
WireConnection;64;0;63;0
WireConnection;62;0;61;0
WireConnection;63;3;62;0
WireConnection;55;0;54;0
WireConnection;0;2;85;0
WireConnection;0;9;21;0
WireConnection;0;13;59;0
ASEEND*/
//CHKSM=CBC8A853DF98C5620A2EB7F67F13B1BE223FD380