using EasiLoginer.Application.Automation;
using EasiLoginer.Application.Profiles;
using EasiLoginer.Integrations.EasiNote.Auth;
using EasiLoginer.Integrations.EasiNote.Pipe;

namespace EasiLoginer.Host.Automation;

/// <summary>把 HTTP 认证客户端适配成 <see cref="ISeewoAuthenticator"/>。</summary>
internal sealed class SeewoAuthenticatorAdapter : ISeewoAuthenticator
{
    private readonly SeewoAuthClient _client;

    public SeewoAuthenticatorAdapter(SeewoAuthClient client) => _client = client;

    public async Task<SeewoAuthResult> LoginAsync(string account, string password, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _client.LoginAsync(account, password, cancellationToken: cancellationToken).ConfigureAwait(false);

            return SeewoAuthResult.Success(new Domain.Automation.SeewoSession(
                result.Token,
                new Domain.Automation.SeewoUser(result.User.Uid, result.User.Username, result.User.NickName, result.User.Phone),
                result.IsRegister));
        }
        catch (SeewoCaptchaException exception)
        {
            return SeewoAuthResult.Failed(SeewoAuthFailure.Captcha, exception.Message);
        }
        catch (SeewoAuthException exception)
        {
            return SeewoAuthResult.Failed(SeewoAuthFailure.Credentials, exception.Message);
        }
        catch (SeewoNetworkException exception)
        {
            return SeewoAuthResult.Failed(SeewoAuthFailure.Network, exception.Message);
        }
        catch (SeewoLoginException exception)
        {
            return SeewoAuthResult.Failed(SeewoAuthFailure.Unknown, exception.Message);
        }
    }
}

/// <summary>把命名管道客户端适配成 <see cref="ISeewoTokenPipe"/>。</summary>
internal sealed class SeewoTokenPipeAdapter : ISeewoTokenPipe
{
    private readonly SeewoPipeClient _client;

    public SeewoTokenPipeAdapter(SeewoPipeClient client) => _client = client;

    public async Task<TokenDeliveryResponse> SendTokenAsync(TokenDeliveryRequest request, CancellationToken cancellationToken)
    {
        var payload = new TokenPayload(
            request.Token,
            request.UserId,
            request.UserName,
            request.NickName,
            request.Phone);

        try
        {
            var response = await _client.SendTokenAsync(payload, cancellationToken).ConfigureAwait(false);
            return new TokenDeliveryResponse(response.Success, response.Message, response.ErrorDetail, response.TokenExpired);
        }
        catch (PipeDeniedException exception)
        {
            throw new TokenPipeDeniedException(exception.Message, exception);
        }
        catch (PipeUnavailableException exception)
        {
            throw new TokenPipeUnavailableException(exception.Message, exception);
        }
    }
}

/// <summary>把档案仓储适配成 <see cref="ILoginUidCache"/>（写回 login_uid 供本地比对）。</summary>
internal sealed class ProfileLoginUidCache : ILoginUidCache
{
    private readonly IProfileRepository _profiles;

    public ProfileLoginUidCache(IProfileRepository profiles) => _profiles = profiles;

    public string? GetLoginUid(string account)
        => _profiles.Current.Automations
            .FirstOrDefault(item => string.Equals(item.Account, account, StringComparison.Ordinal))
            ?.LoginUid;

    public void SetLoginUid(string account, string uid)
    {
        if (string.IsNullOrEmpty(account) || string.IsNullOrEmpty(uid))
        {
            return;
        }

        var automation = _profiles.Current.Automations
            .FirstOrDefault(item => string.Equals(item.Account, account, StringComparison.Ordinal));

        if (automation is null || string.Equals(automation.LoginUid, uid, StringComparison.Ordinal))
        {
            return;
        }

        automation.LoginUid = uid;

        try
        {
            _profiles.Save(ProfileChangeReason.AutomationSaved);
        }
        catch (Domain.Profiles.ProfileLockedException)
        {
            // 锁定态无法写回，下次解锁后再缓存；不影响本次登录
        }
    }
}
