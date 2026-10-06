float2 vTexCoord : TEXCOORD0;

// VS only
#if ( PROGRAM == VFX_PROGRAM_VS )
	float4 vPositionPs		: SV_Position;
#endif

// PS only
#if ( ( PROGRAM == VFX_PROGRAM_PS ) )
	float4 vPositionSs		: SV_Position;
#endif