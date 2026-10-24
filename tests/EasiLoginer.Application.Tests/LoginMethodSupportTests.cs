using EasiLoginer.Application.Automation;
using EasiLoginer.Domain.Configuration;
using FluentAssertions;

namespace EasiLoginer.Application.Tests;

/// <summary>登录方式能力声明：Cv 明确拒绝，其余放行。</summary>
public sealed class LoginMethodSupportTests
{
    /// <summary>三种已实现的策略都放行。</summary>
    [Theory]
    [InlineData(LoginMethod.Fixed)]
    [InlineData(LoginMethod.Uia)]
    [InlineData(LoginMethod.Token)]
    public void 已实现的方式放行(LoginMethod method)
    {
        LoginMethodSupport.IsImplemented(method).Should().BeTrue();
        var action = () => LoginMethodSupport.EnsureImplemented(method);
        action.Should().NotThrow();
    }

    /// <summary>图像识别明确拒绝，并说明原因。</summary>
    [Fact]
    public void 图像识别明确拒绝()
    {
        LoginMethodSupport.IsImplemented(LoginMethod.Cv).Should().BeFalse();

        var action = () => LoginMethodSupport.EnsureImplemented(LoginMethod.Cv);

        var exception = action.Should().Throw<NotSupportedException>();
        exception.Which.Message.Should().Contain("图像识别");
        exception.Which.Message.Should().Contain("resources/EasiNoteUI");
    }
}
