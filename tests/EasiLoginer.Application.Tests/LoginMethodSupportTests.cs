using EasiLoginer.Application.Automation;
using EasiLoginer.Domain.Configuration;
using FluentAssertions;

namespace EasiLoginer.Application.Tests;

/// <summary>登录方式能力声明：四种方式均已实现，未知值明确拒绝。</summary>
public sealed class LoginMethodSupportTests
{
    /// <summary>四种策略都放行。</summary>
    [Theory]
    [InlineData(LoginMethod.Fixed)]
    [InlineData(LoginMethod.Cv)]
    [InlineData(LoginMethod.Uia)]
    [InlineData(LoginMethod.Token)]
    public void 已实现的方式放行(LoginMethod method)
    {
        LoginMethodSupport.IsImplemented(method).Should().BeTrue();
        var action = () => LoginMethodSupport.EnsureImplemented(method);
        action.Should().NotThrow();
    }

    /// <summary>未知枚举值明确拒绝，并说明配置可能被改坏。</summary>
    [Fact]
    public void 未知值被拒绝()
    {
        var unknown = (LoginMethod)99;

        LoginMethodSupport.IsImplemented(unknown).Should().BeFalse();

        var action = () => LoginMethodSupport.EnsureImplemented(unknown);

        var exception = action.Should().Throw<NotSupportedException>();
        exception.Which.Message.Should().Contain("未知的登录方式");
    }
}
