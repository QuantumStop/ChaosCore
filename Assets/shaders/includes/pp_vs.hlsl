PixelInput MainVs( VertexInput i )
{
	PixelInput o;
	o.vPositionPs = float4(i.vPositionOs.xyz, 1.0f);
	o.vTexCoord = i.vTexCoord;
	return o;
}