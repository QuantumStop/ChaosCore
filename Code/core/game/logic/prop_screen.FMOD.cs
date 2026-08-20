#if FMOD
using System.IO;
using FMODSbox;

namespace Core;

public partial class PropScreen
{
	/// <summary>
	/// Play the video sound separately through FMOD
	/// </summary>
	[Property, Title( "Use FMOD Sound" )] public bool UseFMODSound { get; set; } = false;

	private void PlayFMODSound()
	{
		if ( UseFMODSound && !string.IsNullOrEmpty( _videoPath ) && !ShouldBeMuted )
			FMODSound.Play( $"event:/Videos/{Path.GetFileNameWithoutExtension( _videoPath ).ToLowerInvariant()}", WorldPosition );
	}
}
#endif
