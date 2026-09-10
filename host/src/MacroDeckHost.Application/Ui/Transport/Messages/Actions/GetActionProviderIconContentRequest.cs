using System.Text.Json;

namespace MacroDeckHost.Application.Ui.Transport.Messages.Actions;

/// <summary>Asks for the bytes behind an icon-provider snapshot version, for the editor preview of a
/// draft that has adopted the provider but is not saved yet - the deck resolves saved widgets through
/// the asset pipeline instead and never calls this.</summary>
public class GetActionProviderIconContentRequest
{
	public string IntegrationId { get; set; } = string.Empty;

	public string ActionId { get; set; } = string.Empty;

	public Dictionary<string, JsonElement>? Parameters { get; set; }

	public string Version { get; set; } = string.Empty;
}
