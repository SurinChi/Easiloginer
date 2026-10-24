using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using EasiLoginer.Integrations.EasiNote.Auth;
using FluentAssertions;

namespace EasiLoginer.Integrations.Tests;

/// <summary>希沃 HTTP 登录协议（文档 §8.3），用桩 handler 验证，不触网。</summary>
public sealed class SeewoAuthClientTests
{
    private const string LoginOkJson = """
        {
            "error_code": 0,
            "data": {
                "token": "token-abc",
                "isRegister": 0,
                "user": {
                    "uid": "uid-1",
                    "username": "user@example.com",
                    "nickName": "小明",
                    "phone": "13800000000",
                    "photoUrl": "https://cdn/a.png"
                }
            }
        }
        """;

    /// <summary>MD5 与 Python <c>hashlib.md5</c> 一致（"password" 的已知摘要）。</summary>
    [Fact]
    public void 密码哈希与协议一致()
    {
        SeewoAuthClient.HashPassword("password").Should().Be("5f4dcc3b5aa765d61d8327deb882cf99");
    }

    /// <summary>登录请求的 URL、Cookie、UA 与请求体必须与上游一致。</summary>
    [Fact]
    public async Task 登录请求符合协议()
    {
        var handler = new StubHandler(_ => Json(LoginOkJson));
        var client = new SeewoAuthClient(new HttpClient(handler), SeewoAuthClient.DefaultBaseUrl);

        await client.LoginAsync("user@example.com", "password");

        handler.LastRequest!.Method.Should().Be(HttpMethod.Post);
        handler.LastRequest.RequestUri!.ToString().Should().Be("https://edu.seewo.com/api/v1/auth/login");
        handler.LastRequest.Headers.GetValues("Cookie").Single().Should().Be("x-auth-app=EasiNote5");
        handler.LastRequest.Headers.GetValues("User-Agent").Single().Should().Be("EasiNote/5.2.4");

        var body = JsonNode.Parse(handler.LastBody!)!.AsObject();
        body["username"]!.GetValue<string>().Should().Be("user@example.com");
        body["password"]!.GetValue<string>().Should().Be("5f4dcc3b5aa765d61d8327deb882cf99");
        body.ContainsKey("captcha").Should().BeFalse();
    }

    /// <summary>验证码与手机区号按需附加。</summary>
    [Fact]
    public async Task 验证码与区号按需附加()
    {
        var handler = new StubHandler(_ => Json(LoginOkJson));
        var client = new SeewoAuthClient(new HttpClient(handler));

        await client.LoginAsync("u", "p", captchaKey: "k", captchaContent: "1234", phoneCountryCode: "+86");

        var body = JsonNode.Parse(handler.LastBody!)!.AsObject();
        body["captcha"]!["key"]!.GetValue<string>().Should().Be("k");
        body["captcha"]!["captcha"]!.GetValue<string>().Should().Be("1234");
        body["phoneCountryCode"]!.GetValue<string>().Should().Be("+86");
    }

    /// <summary>登录成功时解析 token 与用户信息（昵称优先 nickName）。</summary>
    [Fact]
    public async Task 解析登录结果()
    {
        var handler = new StubHandler(_ => Json(LoginOkJson));
        var client = new SeewoAuthClient(new HttpClient(handler));

        var result = await client.LoginAsync("u", "p");

        result.Token.Should().Be("token-abc");
        result.User.Uid.Should().Be("uid-1");
        result.User.NickName.Should().Be("小明");
        result.User.PhotoUrl.Should().Be("https://cdn/a.png");
    }

    /// <summary>昵称回退到 <c>nickname</c>；平铺结构也能解析。</summary>
    [Fact]
    public async Task 兼容平铺响应与昵称回退()
    {
        var handler = new StubHandler(_ => Json("""
            { "error_code": 0, "token": "t", "user": { "uid": "1", "nickname": "别名" } }
            """));
        var client = new SeewoAuthClient(new HttpClient(handler));

        var result = await client.LoginAsync("u", "p");

        result.Token.Should().Be("t");
        result.User.NickName.Should().Be("别名");
    }

    /// <summary>4906 → 需要验证码。</summary>
    [Fact]
    public async Task 风控错误映射为验证码异常()
    {
        var handler = new StubHandler(_ => Json("""{"error_code":4906,"message":"请完成验证"}"""));
        var client = new SeewoAuthClient(new HttpClient(handler));

        var action = () => client.LoginAsync("u", "p");

        var exception = await action.Should().ThrowAsync<SeewoCaptchaException>();
        exception.Which.Code.Should().Be(4906);
    }

    /// <summary>4908 / 4909 → 账号或密码错误。</summary>
    [Theory]
    [InlineData(4908)]
    [InlineData(4909)]
    public async Task 凭据错误映射为认证异常(int errorCode)
    {
        var handler = new StubHandler(_ => Json($$"""{"error_code":{{errorCode}},"message":"bad"}"""));
        var client = new SeewoAuthClient(new HttpClient(handler));

        var action = () => client.LoginAsync("u", "p");

        var exception = await action.Should().ThrowAsync<SeewoAuthException>();
        exception.Which.Code.Should().Be(errorCode);
    }

    /// <summary>其它错误码 → 通用登录失败，且消息带错误码。</summary>
    [Fact]
    public async Task 其它错误映射为通用失败()
    {
        var handler = new StubHandler(_ => Json("""{"error_code":5000,"message":"服务器开小差"}"""));
        var client = new SeewoAuthClient(new HttpClient(handler));

        var action = () => client.LoginAsync("u", "p");

        var exception = await action.Should().ThrowAsync<SeewoLoginException>();
        exception.Which.Code.Should().Be(5000);
        exception.Which.Message.Should().Contain("服务器开小差");
    }

    /// <summary>网络异常映射为可重试的网络错误。</summary>
    [Fact]
    public async Task 网络异常映射为网络错误()
    {
        var handler = new StubHandler(_ => throw new HttpRequestException("boom"));
        var client = new SeewoAuthClient(new HttpClient(handler));

        var action = () => client.LoginAsync("u", "p");

        await action.Should().ThrowAsync<SeewoNetworkException>();
    }

    /// <summary>用户信息请求带 token Cookie，头像优先 animatedUserPhoto。</summary>
    [Fact]
    public async Task 用户信息与头像优先级()
    {
        var handler = new StubHandler(_ => Json("""
            {
                "data": {
                    "photoUrl": "https://cdn/static.png",
                    "nickName": "小明",
                    "userInfoExtendVo": { "animatedUserPhoto": "https://cdn/anim.gif" }
                }
            }
            """));
        var client = new SeewoAuthClient(new HttpClient(handler));

        var profile = await client.GetUserProfileAsync("token-abc");

        profile.Should().NotBeNull();
        profile!.Avatar.Should().Be("https://cdn/anim.gif");
        handler.LastRequest!.RequestUri!.ToString().Should().Be("https://edu.seewo.com/api/v2/user/info");
        handler.LastRequest.Headers.GetValues("Cookie").Single().Should().Contain("x-auth-token=token-abc");
    }

    /// <summary>没有动态头像时回退静态头像；都没有则为 null。</summary>
    [Fact]
    public async Task 头像回退()
    {
        var withStatic = new StubHandler(_ => Json("""{"data":{"photoUrl":"https://cdn/s.png"}}"""));
        (await new SeewoAuthClient(new HttpClient(withStatic)).GetUserProfileAsync("t"))!.Avatar
            .Should().Be("https://cdn/s.png");

        var empty = new StubHandler(_ => Json("""{"data":{}}"""));
        (await new SeewoAuthClient(new HttpClient(empty)).GetUserProfileAsync("t"))!.Avatar
            .Should().BeNull();
    }

    /// <summary>用户信息接口非 200 时返回 null，不抛异常。</summary>
    [Fact]
    public async Task 用户信息失败返回空()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized));
        var client = new SeewoAuthClient(new HttpClient(handler));

        var profile = await client.GetUserProfileAsync("t");

        profile.Should().BeNull();
    }

    /// <summary>配置了 <c>x-auth-brand</c> 时 Cookie 一并带上。</summary>
    [Fact]
    public async Task 品牌Cookie可选()
    {
        var handler = new StubHandler(_ => Json(LoginOkJson));
        var client = new SeewoAuthClient(new HttpClient(handler), appBrand: "brand-x");

        await client.LoginAsync("u", "p");

        handler.LastRequest!.Headers.GetValues("Cookie").Single()
            .Should().Be("x-auth-app=EasiNote5; x-auth-brand=brand-x");
    }

    private static HttpResponseMessage Json(string json)
        => new(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        };

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }

        public string? LastBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            if (request.Content is not null)
            {
                LastBody = await request.Content.ReadAsStringAsync(cancellationToken);
            }

            return responder(request);
        }
    }
}
