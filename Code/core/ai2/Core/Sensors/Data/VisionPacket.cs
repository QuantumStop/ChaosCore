using Core.AI;
using System;
using System.Collections.Generic;
using System.Text;

namespace Core.AI;

public struct VisionPacket : ISensorPacket
{
	public AIController Owner { get; set; }
	public bool Alert { get; set; }
	public bool HasLKP { get; set; }
	public bool HasEnemy { get; set; }
	public bool Tracking { get; set; }
	public float LostTime { get; set; }
	public float Distance { get; set; }
	public float HealthPercentage { get; set; }

}
