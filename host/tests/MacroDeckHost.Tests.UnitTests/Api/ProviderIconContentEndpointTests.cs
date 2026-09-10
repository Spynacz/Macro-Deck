using System.Text.Json;
using MacroDeck.Localization;
using MacroDeck.Sdk;
using MacroDeck.Sdk.Actions;
using MacroDeckHost.Api.Controllers;
using MacroDeckHost.Application.Plugins.Capabilities.Adapters.Actions;
using MacroDeckHost.Application.Ui.Transport.Messages.Actions;
using Microsoft.AspNetCore.Mvc;

namespace MacroDeckHost.Tests.UnitTests.Api;

[TestFixture]
public class ProviderIconContentEndpointTests
{
	private sealed class ContentAction : IActionDefinition, IIconProviderActionDefinition
	{
		public string Id => "avatar";

		public LocalizedText Name => "Avatar";

		public LocalizedText Description => string.Empty;

		public IReadOnlyList<ActionParameter> Parameters => [];

		public Func<string, ActionIconContent?> ContentByVersion { get; set; } = _ => null;

		public Func<Exception>? ThrowInstead { get; set; }

		public IActionExecutor CreateExecutor() => throw new NotSupportedException();

		public Task<ActionIconSnapshot?> GetActionIconAsync(
			IReadOnlyDictionary<string, object?> parameters,
			CancellationToken cancellationToken)
			=> Task.FromResult<ActionIconSnapshot?>(null);

		public Task<ActionIconContent?> GetActionIconContentAsync(
			IReadOnlyDictionary<string, object?> parameters,
			string version,
			CancellationToken cancellationToken)
			=> ThrowInstead is not null
				? Task.FromException<ActionIconContent?>(ThrowInstead())
				: Task.FromResult(ContentByVersion(version));
	}

	private sealed class ContentIntegration : IIntegration
	{
		public string Id => "test.integration";

		public LocalizedText Name => "Content Integration";

		public string Version => "1.0.0";

		public required ContentAction Action { get; init; }

		public IReadOnlyList<IActionDefinition> Actions => [Action];

		public bool IsInitialized => true;

		public Task InitializeAsync(IIntegrationContext context) => Task.CompletedTask;

		public Task ShutdownAsync() => Task.CompletedTask;
	}

	private static ActionsController CreateController(ContentAction action)
	{
		var integrations = new FakeIntegrationRegistry();
		integrations.Add(new ContentIntegration { Action = action });

		return new ActionsController(null!,
			null!,
			null!,
			null!,
			null!,
			null!,
			null!,
			integrations,
			null!);
	}

	private static GetActionProviderIconContentRequest Request(string version) => new()
	{
		IntegrationId = "test.integration",
		ActionId = "avatar",
		Parameters = new Dictionary<string, JsonElement>(),
		Version = version,
	};

	[Test]
	public async Task Reported_bytes_are_served_with_their_media_type()
	{
		var action = new ContentAction
		{
			ContentByVersion = _ => new ActionIconContent([1, 2, 3, 4], "image/png"),
		};

		var result = await CreateController(action).GetProviderIconContent(Request("v1"), CancellationToken.None);

		Assert.That(result, Is.InstanceOf<FileContentResult>());
		Assert.Multiple(() =>
		{
			Assert.That(((FileContentResult)result).FileContents, Is.EqualTo(new byte[] { 1, 2, 3, 4 }));
			Assert.That(((FileContentResult)result).ContentType, Is.EqualTo("image/png"));
		});
	}

	[Test]
	public async Task An_unknown_action_reports_not_found()
	{
		var action = new ContentAction();
		var controller = CreateController(action);
		var request = Request("v1");
		request.ActionId = "nope";

		var result = await controller.GetProviderIconContent(request, CancellationToken.None);

		Assert.That(result, Is.InstanceOf<NotFoundResult>());
	}

	[Test]
	public async Task A_moved_on_version_reports_not_found()
	{
		var action = new ContentAction { ContentByVersion = _ => null };

		var result = await CreateController(action).GetProviderIconContent(Request("stale"), CancellationToken.None);

		Assert.That(result, Is.InstanceOf<NotFoundResult>());
	}

	[Test]
	public async Task A_failing_provider_reports_not_found()
	{
		var action = new ContentAction { ThrowInstead = () => new InvalidOperationException("down") };

		var result = await CreateController(action).GetProviderIconContent(Request("v1"), CancellationToken.None);

		Assert.That(result, Is.InstanceOf<NotFoundResult>());
	}
}
