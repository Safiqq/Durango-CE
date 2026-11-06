using BestHTTP;

namespace Durango.Logic.Clusters;

/// <summary>
/// SANITIZED: the original implementation built a stable hardware fingerprint from
/// the Unity device ID, the Windows MachineGuid registry value and the SMBIOS UUID,
/// then attached it to gateway HTTP requests.
///
/// That code has been removed. Nothing here reads hardware identifiers, the registry
/// or firmware tables, and no identifier is sent to any server. The class and the
/// <see cref="AddTo"/> signature are kept only so existing call sites still compile.
/// </summary>
public static class CommunityHardwareId
{
	/// <summary>Always null: no hardware identifier is collected.</summary>
	public static string Value
	{
		get
		{
			return null;
		}
	}

	/// <summary>No-op: does not add a hardware ID header or field.</summary>
	public static void AddTo(HTTPRequest request)
	{
	}
}
