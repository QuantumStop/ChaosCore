using System;

namespace Core;

#if FMOD
using FMODSbox;
#endif

public static class SurfaceExtensions
{
	public static string FindInParents( this Surface surface, Func<Surface, string> selector )
	{
		for ( var current = surface; current.IsValid(); current = current.GetBaseSurface() )
		{
			var value = selector( current );

			if ( !string.IsNullOrWhiteSpace( value ) ) return value;
		}
		return string.Empty;
	}

	public static List<DecalDefinition> FindInParents( this Surface surface, Func<Surface, List<DecalDefinition>> selector )
	{
		for ( var current = surface; current.IsValid(); current = current.GetBaseSurface() )
		{
			var value = selector( current );

			if ( value is not null && value.Count > 0 ) return value;
		}

		return null;
	}

#if FMOD
	public static FMODEventResource FindInParents( this Surface surface, Func<Surface, FMODEventResource> selector )
	{
		for ( var current = surface; current.IsValid(); current = current.GetBaseSurface() )
		{
			var value = selector( current );

			if ( value.IsValid() ) return value;
		}

		return null;
	}
#endif
}
