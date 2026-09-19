namespace Core;

using System;

[AttributeUsage( AttributeTargets.Property | AttributeTargets.Field )]
public sealed class EnumOptionsAttribute : Attribute
{
	public bool Exclude { get; set; }

	public long[] Values { get; }

	public EnumOptionsAttribute( params object[] values )
	{
		Values = new long[values.Length];

		for ( int i = 0; i < values.Length; i++ )
			Values[i] = Convert.ToInt64( values[i] );
	}

	public bool Allows( long value )
	{
		bool contains = Contains( value );

		return Exclude ? !contains: contains;
	}

	private bool Contains( long value )
	{
		for ( int i = 0; i < Values.Length; i++ )
		{
			if ( Values[i] == value )
				return true;
		}

		return false;
	}
}
