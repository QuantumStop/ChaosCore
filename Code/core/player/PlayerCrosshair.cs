using System;
namespace Core;

public class Crosshair : Component
{
	public static Crosshair PlayerCrosshair { get; set; }
	public CrosshairData WeaponCrosshair { get; set; }
	private float _crosshairScale;
	private float _crosshairWidth;
	private float _crosshairGapX;
	private float _crosshairGapY;

	private Color _crosshairColor { get; set; } = Color.White;
	private Color _crosshairDotColor { get; set; } = Color.White;
	private Color _crosshairOutlineColor { get; set; } = Color.Black;

	private bool _hasDot { get; set; } = false;
	private bool _dotHasOutline { get; set; } = false;

	private bool _crosshairHasOutline { get; set; } = true;
	private float _crosshairLength;
	private float _crosshairOutlineThickness;

	private float _smoothedSpeed = 0f;

	private static bool _hideCrosshair => BasePlayer.Local.IsHUDElementHidden( BasePlayer.HIDEHUD_FLAGS.HIDEHUD_CROSSHAIR | BasePlayer.HIDEHUD_FLAGS.HIDEHUD_PLAYERDEAD );

	protected override void OnStart() => PlayerCrosshair = this;

	protected virtual Vector2 CalculateCenter( Vector2 screen ) => screen * 0.5f;

	protected override void OnUpdate()
	{
		if ( !BasePlayer.Local.IsValid() ) return;

		if ( _hideCrosshair ) return;

		Vector2 center = CalculateCenter( Screen.Size );

		float alphaTarget = 1f;
		float linesTarget = 1f;
		float mainAlpha = 1f;
		float linesAlpha = 1f;

		BasePlayer player = BasePlayer.Local;
		BaseCombatWeapon weapon = player?.CurrentWeapon;

		if ( !weapon.IsValid() || weapon?.WeaponData?.WeaponCrosshair is null )
		{
			DrawSimpleCrosshair(); // Meant for times when you don't have a weapon/setup crosshair (fallback)
			return;
		}


		WeaponCrosshairType CrosshairType;
		bool hasAmmo = true;

		if ( player.IsValid() && player.Controller.Camera.IsValid() )
		{
			//-- WeaponData Read Block --//

			WeaponCrosshair = weapon?.WeaponData?.WeaponCrosshair;
			CrosshairType = WeaponCrosshair.WeaponCrosshairType;

			_crosshairScale = WeaponCrosshair.CrosshairScale;
			_crosshairWidth = WeaponCrosshair.CrosshairWidth;
			_crosshairGapX = WeaponCrosshair.CrosshairGapX;
			_crosshairGapY = WeaponCrosshair.CrosshairGapY;
			_crosshairLength = WeaponCrosshair.CrosshairLength;
			_crosshairColor = WeaponCrosshair.CrosshairColor;
			_crosshairDotColor = WeaponCrosshair.CrosshairDotColor;
			_crosshairOutlineColor = WeaponCrosshair.CrosshairOutlineColor;

			_hasDot = WeaponCrosshair.CrosshairHasDot;
			_dotHasOutline = WeaponCrosshair.CrosshairDotOutline;

			_crosshairHasOutline = WeaponCrosshair.CrosshairHasOutline;
			_crosshairOutlineThickness = WeaponCrosshair.CrosshairOutlineThickness;

			//-- end block --//

			if ( !WeaponCrosshair.IsValid() ) return;

			// --- Reference baseline ( Doing 16:9 here as a base )
			const float ReferenceWidth = 1920f;
			const float ReferenceHeight = 1080f;
			const float ReferenceAspect = ReferenceWidth / ReferenceHeight;


			// --- Actual screen info
			float screenWidth = Screen.Width;
			float screenHeight = Screen.Height;
			float screenAspect = screenWidth / screenHeight;


			// --- Diagonal resolution scale ( maintains consistent size across resolutions )
			float referenceDiagonal = MathF.Sqrt( ReferenceWidth * ReferenceWidth + ReferenceHeight * ReferenceHeight );
			float currentDiagonal = MathF.Sqrt( screenWidth * screenWidth + screenHeight * screenHeight );
			float resolutionScale = currentDiagonal / referenceDiagonal;
			resolutionScale = Math.Clamp( resolutionScale, 0.5f, 1.5f ); // Optional safety clamp


			// --- Aspect scale ( corrects horizontal stretching/squashing )
			float aspectScale = ReferenceAspect / screenAspect;
			aspectScale = Math.Clamp( aspectScale, 0.8f, 1.2f );


			// --- Final scale applied to all crosshair metrics
			float userScale;

			if ( CrosshairType == WeaponCrosshairType.CROSSHAIR_CROSS_A || CrosshairType == WeaponCrosshairType.CROSSHAIR_CROSS_B || CrosshairType == WeaponCrosshairType.CROSSHAIR_DOT )
				userScale = _crosshairScale / 2;
			else
				userScale = _crosshairScale;

			float finalScale = resolutionScale * userScale;


			// Apply this scale factor to crosshair elements to maintain consistency across aspect ratios
			float scaledGapX = _crosshairGapX;
			float scaledGapY = _crosshairGapY;


			// TODO: Need to handle no ammo somehow eventually
			// hasAmmo = !player.HasweaponTag( "no_ammo" );

			float rawSpeed = BasePlayer.Local.Movement.Velocity.Length;
			_smoothedSpeed = _smoothedSpeed.LerpTo( rawSpeed, Time.Delta * 5f );

			float basefreq = 0f;
			float freq;

			float gapX = _crosshairGapX * finalScale * aspectScale;  // Might come in handy


			// --- Customization appliance
			float length = _crosshairLength * finalScale * 0.5f;
			float width = MathF.Max( WeaponCrosshair.CrosshairWidth * finalScale, 1f );
			float outlineThickness = MathF.Max( _crosshairOutlineThickness * finalScale, 1f );


			// --- Recoil modifier
			float recoilOffset = GetRecoilOffset( WorldTime.Now - BasePlayer.Local.CurrentWeapon.LastAttackTime, WeaponCrosshair.CrosshairRecoilScale, finalScale );

			scaledGapX += recoilOffset;
			scaledGapY += recoilOffset;


			if ( !hasAmmo )
			{
				_crosshairColor = Color.Red;
				linesTarget *= 0.25f;
			}

			if ( CrosshairType == WeaponCrosshairType.CROSSHAIR_CROSS_A || CrosshairType == WeaponCrosshairType.CROSSHAIR_CROSS_B )
			{
				basefreq = 0.025f;

				// Aspect-corrected unit vectors
				Vector2 normRight = new( 1f, 0f );
				Vector2 normUp = new( 0f, 1f );

				// Bar sizes
				Vector2 horizSize = new( length, width );
				Vector2 vertSize = new( width, length );

				Vector2 pixelCenter = new( MathF.Floor( center.x ) + 0.5f, MathF.Floor( center.y ) + 0.5f ); // Pixel perfect centering

				//-- Crosshair --//
				{

					// LEFT
					DrawBar(
						pixelCenter - normRight * (scaledGapX + length * 0.5f),
						horizSize,
						_crosshairColor,
						_crosshairOutlineColor,
						_crosshairHasOutline,
						outlineThickness
					);


					// RIGHT
					DrawBar(
						pixelCenter + normRight * (scaledGapX + length * 0.5f),
						horizSize,
						_crosshairColor,
						_crosshairOutlineColor,
						_crosshairHasOutline,
						outlineThickness
					);

					// TOP
					DrawBar(
						pixelCenter - normUp * (scaledGapY + length * 0.5f),
						vertSize,
						_crosshairColor,
						_crosshairOutlineColor,
						_crosshairHasOutline,
						outlineThickness
					);

					// BOTTOM
					DrawBar(
						pixelCenter + normUp * (scaledGapY + length * 0.5f),
						vertSize,
						_crosshairColor,
						_crosshairOutlineColor,
						_crosshairHasOutline,
						outlineThickness
					);

				}

				if ( _hasDot )
				{
					DrawDotWithOutline(
						center,
						width * 1f,
						_crosshairDotColor,
						_crosshairOutlineColor,
						_dotHasOutline,
						outlineThickness
					);
				}
			}

			if ( CrosshairType == WeaponCrosshairType.CROSSHAIR_CIRCLE )
			{
				Vector2 pixelCenter = new(
					MathF.Floor( center.x ) + 0.5f,
					MathF.Floor( center.y ) + 0.5f
				);

				var RecoilScale = MathF.Exp( -(WorldTime.Now - BasePlayer.Local.CurrentWeapon.LastAttackTime) * WeaponCrosshair.CrosshairRecoilScale );
				var ringSize = WeaponCrosshair.CrosshairRecoilScale * finalScale / 2 + RecoilScale * 12f * finalScale;

				float sineBounce = MathF.Sin( (WorldTime.Now - BasePlayer.Local.CurrentWeapon.LastAttackTime) * MathF.PI * 4f );
				float decay = MathF.Exp( -(WorldTime.Now - BasePlayer.Local.CurrentWeapon.LastAttackTime) * 8f );
				float bounce = MathF.Max( 0f, sineBounce * decay );

				float baseRadius = (30f * gapX / 10f) + recoilOffset * (1f + 0.4f * bounce);
				float innerThickness = 2f * finalScale;
				float outerThickness = 6f * finalScale;

				basefreq = 0.005f;

				foreach ( var segment in WeaponCrosshair.CrosshairCircleSegment )
				{
					// Compute radius and other properties for each segment
					float radius = baseRadius + segment.CrosshairCirclePosition + recoilOffset * (1f + 0.4f * bounce);
					float thickness = segment.CrosshairCircleThickness; // Single thickness for both inner and outer parts
					int segments = segment.CrosshairCircleSegments;
					Color mainColor = segment.CrosshairCircleColor;
					Color outlineColor = _crosshairHasOutline ? _crosshairOutlineColor : mainColor;

					// Apply animation and angle adjustments
					float timeOffsetRadians = WorldTime.Now * segment.AnimationSpeed;

					// Convert start and end angles to radians
					float startRad = segment.StartAngle * MathF.PI / 180f;
					float endRad = segment.EndAngle * MathF.PI / 180f;

					// Flip the arc by reversing the start and end angles
					float flippedStartRad = endRad;  // Flip by swapping start and end angles
					float flippedEndRad = startRad;

					// Call the DrawCircleCrosshairArc with flipped angles
					DrawCircleCrosshairArc(
						center: center,
						radius: radius,
						thickness: thickness, // Use a single thickness parameter for the arc
						mainColor: mainColor,
						outlineColor: outlineColor,
						segments: segments,
						alphaMultiplier: segment.AlphaFade, // Applying the fade effect
						startAngle: flippedStartRad, // Use the flipped start angle
						endAngle: flippedEndRad, // Use the flipped end angle
						angleOffset: timeOffsetRadians, // Adding the time-based offset for animation
						gradientColor: segment.GradientToColor // Gradient effect for the circle
					);
				}

				if ( _hasDot )
				{
					DrawDotWithOutline(
						pixelCenter,
						ringSize,
						_crosshairColor,
						_crosshairOutlineColor,
						_crosshairHasOutline,
						_crosshairOutlineThickness
					);
				}
			}

			if ( CrosshairType == WeaponCrosshairType.CROSSHAIR_DOT )
			{
				Vector2 pixelCenter = new(
					MathF.Floor( center.x ) + 0.5f,
					MathF.Floor( center.y ) + 0.5f
				);

				var RecoilScale = MathF.Exp( -(WorldTime.Now - BasePlayer.Local.CurrentWeapon.LastAttackTime) * WeaponCrosshair.CrosshairRecoilScale );
				var ringSize = WeaponCrosshair.CrosshairRecoilScale * finalScale / 2 + RecoilScale * 12f * finalScale;

				DrawDotWithOutline(
					pixelCenter,
					ringSize,
					_crosshairColor,
					_crosshairOutlineColor,
					_crosshairHasOutline,
					_crosshairOutlineThickness
				);
			}

			freq = _smoothedSpeed * basefreq * 2;
			freq.Clamp( 0, 12.5f );

			mainAlpha.LerpTo( alphaTarget, Time.Delta * 30f );
			linesAlpha.LerpTo( linesTarget, Time.Delta * 3f );
		}
	}

	private static float GetRecoilOffset( float timeSinceAttacked, float recoilScale, float finalScale )
	{
		// Normalized attack time (0-1 over 0.15s)
		float recoilAlpha = 1f - timeSinceAttacked.LerpInverse( 0f, 0.15f );
		float eased = EasingPlus.EaseOutQuad( recoilAlpha );

		// Small sine wobble with exponential decay
		float sine = MathF.Sin( timeSinceAttacked * MathF.PI * 6f );
		float decay = MathF.Exp( -timeSinceAttacked * 10f );
		float bounce = sine * decay * 0.2f;

		// Final recoil offset with scale
		return (eased + bounce) * recoilScale * finalScale * 2f;
	}


	private static void DrawBar( Vector2 barCenter, Vector2 barSize, Color mainColor, Color outlineColor, bool hasOutline, float outlineThickness )
	{
		using var playerhud = BasePlayer.Local.Controller.Camera.BeginHud();

		// Pixel-align the center
		Vector2 pixelCenter = new( MathF.Floor( barCenter.x ) + 0.5f, MathF.Floor( barCenter.y ) + 0.5f );

		// Outline goes first for correct order
		if ( hasOutline )
		{
			Vector2 outlineSize = barSize + new Vector2( outlineThickness * 2f );
			Vector2 outlinePos = pixelCenter - outlineSize * 0.5f;

			playerhud.Fill = outlineColor;
			playerhud.Stroke = Stroke.None;
			playerhud.Rect( new Rect( outlinePos, outlineSize ) );
		}

		// Inner bar
		Vector2 barPos = pixelCenter - barSize * 0.5f;
		playerhud.Fill = mainColor;
		playerhud.Stroke = Stroke.None;
		playerhud.Rect( new Rect( barPos, barSize ) );
	}

	private static void DrawDotWithOutline( Vector2 center, float dotRadius, Color mainColor, Color outlineColor, bool hasOutline, float outlineThickness )
	{
		using var playerhud = BasePlayer.Local.Controller.Camera.BeginHud();

		// Pixel-align for sharp rendering
		Vector2 pixelCenter = new( MathF.Floor( center.x ) + 0.5f, MathF.Floor( center.y ) + 0.5f );

		if ( hasOutline )
		{
			playerhud.Fill = outlineColor;
			playerhud.Stroke = Stroke.None;
			playerhud.Circle( pixelCenter, dotRadius + outlineThickness );
		}

		playerhud.Fill = mainColor;
		playerhud.Stroke = Stroke.None;
		playerhud.Circle( pixelCenter, dotRadius );
	}

	private static void DrawCircleCrosshairArc( Vector2 center, float radius, float thickness, Color mainColor, Color outlineColor, int segments = 64,
	float alphaMultiplier = 1f, float startAngle = 0f, float endAngle = MathF.Tau, float angleOffset = 0f, Color? gradientColor = null )
	{
		using var hud = BasePlayer.Local.Controller.Camera.BeginHud();

		startAngle += angleOffset;
		endAngle += angleOffset;

		// Normalize angles
		if ( endAngle <= startAngle ) endAngle = startAngle + 0.01f;

		float step = (endAngle - startAngle) / segments;
		float overlapFactor = 1.01f; // Slight overlap for cleaner joins

		// Outline Pass
		if ( outlineColor.a > 0f && thickness > 0f )
		{
			float outerThickness = thickness + 1.5f; // Outer is slightly wider
			float outerRadius = radius;

			Vector2 last = center + new Vector2( MathF.Cos( startAngle ), MathF.Sin( startAngle ) ) * outerRadius;

			for ( int i = 1; i <= segments; i++ )
			{
				float angle = startAngle + step * i * overlapFactor;
				Vector2 next = center + new Vector2( MathF.Cos( angle ), MathF.Sin( angle ) ) * outerRadius;

				Color col = outlineColor.WithAlpha( outlineColor.a * alphaMultiplier );
				hud.Stroke = Stroke.Solid( col, outerThickness );
				hud.Line( last, next );

				last = next;
			}
		}

		// Main Arc
		{
			Vector2 last = center + new Vector2( MathF.Cos( startAngle ), MathF.Sin( startAngle ) ) * radius;

			for ( int i = 1; i <= segments; i++ )
			{
				float t = i / (float)segments;
				float angle = startAngle + step * i * overlapFactor;
				Vector2 next = center + new Vector2( MathF.Cos( angle ), MathF.Sin( angle ) ) * radius;

				Color col = gradientColor.HasValue
					? Color.Lerp( mainColor, gradientColor.Value, t ).WithAlpha( mainColor.a * alphaMultiplier )
					: mainColor.WithAlpha( mainColor.a * alphaMultiplier );

				hud.Stroke = Stroke.Solid( col, thickness );
				hud.Line( last, next );
				last = next;
			}
		}
	}


	private static void DrawSimpleCrosshair()
	{
		using var playerhud = BasePlayer.Local.Controller.Camera.BeginHud();

		Vector2 center = Screen.Size * 0.5f;
		Vector2 pixelCenter = new(
			MathF.Floor( center.x ) + 0.5f,
			MathF.Floor( center.y ) + 0.5f
		);

		// Actual 1x1 pixel sized "dots"
		Vector2 dotSize = new( 0.75f, 0.75f );
		Color dotColor = Color.White;

		float spacing = 10f;

		playerhud.Fill = dotColor;
		playerhud.Stroke = Stroke.None;

		playerhud.Rect( new Rect( pixelCenter, dotSize ) );

		playerhud.Rect( new Rect( pixelCenter + Vector2.Up * spacing, dotSize ) );
		playerhud.Rect( new Rect( pixelCenter + Vector2.Down * spacing, dotSize ) );
		playerhud.Rect( new Rect( pixelCenter + Vector2.Left * spacing, dotSize ) );
		playerhud.Rect( new Rect( pixelCenter + Vector2.Right * spacing, dotSize ) );
	}
}
