using MacroDeckHost.Application.Integrations;
using MacroDeckHost.Application.Plugins.Capabilities.Adapters.Actions;
using MacroDeckHost.Application.Ui.Handlers;
using MacroDeckHost.Application.Ui.Transport;
using MacroDeckHost.Application.Ui.Transport.Messages.Actions;
using MacroDeckHost.Auth;
using MacroDeck.Sdk.Actions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MacroDeckHost.Api.Controllers;

[ApiController]
[Route("api/actions")]
public class ActionsController : ControllerBase
{
	private readonly IUiTransportMessageHandler<GetActionsRequest, GetActionsResponse> _getActions;

	private readonly IUiTransportMessageHandler<ExecuteActionButtonTriggerRequest, ExecuteActionButtonTriggerResponse>
		_executeActionButtonTrigger;

	private readonly IUiTransportMessageHandler<ExecuteActionRequest, ExecuteActionResponse> _executeAction;

	private readonly IUiTransportMessageHandler<RunActionFlowRequest, RunActionFlowResponse> _runActionFlow;

	private readonly IUiTransportMessageHandler<GetActionParameterOptionsRequest, GetActionParameterOptionsResponse>
		_getParameterOptions;

	private readonly IUiTransportMessageHandler<GetActionProviderStatesRequest, GetActionProviderStatesResponse>
		_getProviderStates;

	private readonly IUiTransportMessageHandler<GetActionProviderIconRequest, GetActionProviderIconResponse>
		_getProviderIcon;

	private readonly IIntegrationRegistry _integrations;

	private readonly RemoteIconProviderActionRegistry _remoteIconProviders;

	public ActionsController(
		IUiTransportMessageHandler<GetActionsRequest, GetActionsResponse> getActions,
		IUiTransportMessageHandler<ExecuteActionButtonTriggerRequest, ExecuteActionButtonTriggerResponse>
			executeActionButtonTrigger,
		IUiTransportMessageHandler<ExecuteActionRequest, ExecuteActionResponse> executeAction,
		IUiTransportMessageHandler<RunActionFlowRequest, RunActionFlowResponse> runActionFlow,
		IUiTransportMessageHandler<GetActionParameterOptionsRequest, GetActionParameterOptionsResponse>
			getParameterOptions,
		IUiTransportMessageHandler<GetActionProviderStatesRequest, GetActionProviderStatesResponse>
			getProviderStates,
		IUiTransportMessageHandler<GetActionProviderIconRequest, GetActionProviderIconResponse> getProviderIcon,
		IIntegrationRegistry integrations,
		RemoteIconProviderActionRegistry remoteIconProviders)
	{
		_getActions = getActions;
		_executeActionButtonTrigger = executeActionButtonTrigger;
		_executeAction = executeAction;
		_runActionFlow = runActionFlow;
		_getParameterOptions = getParameterOptions;
		_getProviderStates = getProviderStates;
		_getProviderIcon = getProviderIcon;
		_integrations = integrations;
		_remoteIconProviders = remoteIconProviders;
	}

	[HttpGet]
	public Task<GetActionsResponse> GetAll(CancellationToken ct)
	{
		return _getActions.Handle(new GetActionsRequest(), ct).AsTask();
	}

	[HttpPost("execute")]
	[Authorize(Policy = AuthPolicies.ClientAccess)]
	public Task<ExecuteActionButtonTriggerResponse> Execute(
		ExecuteActionButtonTriggerRequest body,
		CancellationToken ct)
	{
		ArgumentNullException.ThrowIfNull(body);

		return _executeActionButtonTrigger.Handle(body, ct).AsTask();
	}

	[HttpPost("run")]
	public Task<ExecuteActionResponse> Run(
		ExecuteActionRequest body,
		CancellationToken ct)
	{
		return _executeAction.Handle(body, ct).AsTask();
	}

	[HttpPost("run-flow")]
	public Task<RunActionFlowResponse> RunFlow(
		RunActionFlowRequest body,
		CancellationToken ct)
	{
		return _runActionFlow.Handle(body, ct).AsTask();
	}

	[HttpPost("options")]
	public Task<GetActionParameterOptionsResponse> GetParameterOptions(
		GetActionParameterOptionsRequest body,
		CancellationToken ct)
	{
		return _getParameterOptions.Handle(body, ct).AsTask();
	}

	[HttpPost("provider-states")]
	public Task<GetActionProviderStatesResponse> GetProviderStates(
		GetActionProviderStatesRequest body,
		CancellationToken ct)
	{
		return _getProviderStates.Handle(body, ct).AsTask();
	}

	[HttpPost("provider-icon")]
	public Task<GetActionProviderIconResponse> GetProviderIcon(
		GetActionProviderIconRequest body,
		CancellationToken ct)
	{
		return _getProviderIcon.Handle(body, ct).AsTask();
	}

	[HttpPost("provider-icon-content")]
	public async Task<IActionResult> GetProviderIconContent(
		GetActionProviderIconContentRequest body,
		CancellationToken ct)
	{
		ArgumentNullException.ThrowIfNull(body);

		if (string.IsNullOrWhiteSpace(body.Version))
		{
			return NotFound();
		}

		var provider = ResolveIconProvider(body.IntegrationId, body.ActionId);
		if (provider is null)
		{
			return NotFound();
		}

		ActionIconContent? content;
		try
		{
			content = await provider
				.GetActionIconContentAsync(ActionParameterConverter.ToNullable(body.Parameters ?? []),
					body.Version,
					ct)
				.ConfigureAwait(false);
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			return NotFound();
		}

		if (content is null || content.Data.Length == 0 || string.IsNullOrWhiteSpace(content.MediaType))
		{
			return NotFound();
		}

		return File(content.Data, content.MediaType);
	}

	private IIconProviderActionDefinition? ResolveIconProvider(string integrationId, string actionId)
	{
		if (!_integrations.IsEnabled(integrationId))
		{
			return null;
		}

		return _integrations.FindAction(integrationId, actionId) switch
		{
			IIconProviderActionDefinition direct => direct,
			RemoteActionDefinition { ProvidesIcon: true } => _remoteIconProviders.Resolve(integrationId, actionId),
			_ => null
		};
	}
}
