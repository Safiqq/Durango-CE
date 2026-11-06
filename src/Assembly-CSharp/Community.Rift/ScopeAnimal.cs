using System.Globalization;
using Yaml;

namespace Community.Rift;

public sealed class ScopeAnimal
{
	public string Id;

	public int Type;

	public string Name;

	public int Level;

	public float Life;

	public float MaxLife;

	public string Role;

	public bool Boss;

	public bool Elite;

	public int Dist;

	public int Tx;

	public int Tz;

	public float Px;

	public float Pz;

	public bool Sleeping;

	public bool Fighting;

	public string[] Corpse;

	public double? Attack;

	public double? AttackRating;

	public double? Defense;

	internal AnimalBehavior Observed;

	internal PlayerBehavior Observer;

	public string DisplayName
	{
		get
		{
			string name = AnimalYaml.GetName(Type);
			if (!string.IsNullOrEmpty(name) && !(name == Type.ToString(CultureInfo.InvariantCulture)))
			{
				return name;
			}
			return Name ?? name;
		}
	}
}
