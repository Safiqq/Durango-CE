using System.Collections.Generic;

namespace OffServer.Rift;

public sealed class ScopeResult
{
	public double At;

	public float Ttl;

	public int Radius;

	public int FromTx;

	public int FromTz;

	public readonly List<ScopeAnimal> Animals = new List<ScopeAnimal>();
}
