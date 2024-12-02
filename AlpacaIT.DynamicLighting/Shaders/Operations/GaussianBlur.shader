Shader "Hidden/Dynamic Lighting/GaussianBlur"
{
    // contains source code from https://github.com/daniel-ilett/smo-shaders (see Licenses/SmoShaders.txt).
    // shoutouts to Daniel Ilett!

    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}

        CGINCLUDE
        #include "UnityCG.cginc"

        static const float TWO_PI = 6.28319;
        static const float E = 2.71828;

        sampler2D _MainTex;
        float4 _MainTex_ST;
        float2 _MainTex_TexelSize;
        #define _KernelSize 3
        #define _Spread 1.0
        //int	_KernelSize;
        //float _Spread;

        // gaussian function in one dimension.
	    float gaussian(int x)
	    {
		    float sigmaSqu = _Spread * _Spread;
		    return (1 / sqrt(TWO_PI * sigmaSqu)) * pow(E, -(x * x) / (2 * sigmaSqu));
	    }

        ENDCG
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

        Pass
        {
            Name "HorizontalPass"

            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag

            fixed4 frag (v2f_img i) : SV_Target
            {
				float3 col = float3(0.0, 0.0, 0.0);
				float kernelSum = 0.0;

				int upper = ((_KernelSize - 1) / 2);
				int lower = -upper;

				for (int x = lower; x <= upper; ++x)
				{
					float gauss = gaussian(x);
					kernelSum += gauss;
					col += gauss * tex2D(_MainTex, i.uv + fixed2(_MainTex_TexelSize.x * x, 0.0));
				}

				col /= kernelSum;
				return float4(col, 1.0);
            }
            ENDCG
        }

        Pass
        {
            Name "VerticalPass"

            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag

            fixed4 frag (v2f_img i) : SV_Target
            {
				float3 col = float3(0.0, 0.0, 0.0);
				float kernelSum = 0.0;

				int upper = ((_KernelSize - 1) / 2);
				int lower = -upper;

				for (int y = lower; y <= upper; ++y)
				{
					float gauss = gaussian(y);
					kernelSum += gauss;
					col += gauss * tex2D(_MainTex, i.uv + fixed2(0.0, _MainTex_TexelSize.y * y));
				}

				col /= kernelSum;
				return float4(col, 1.0);
            }
            ENDCG
        }
    }
}
