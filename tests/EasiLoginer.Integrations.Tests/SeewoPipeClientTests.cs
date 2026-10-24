using System.IO.Pipes;
using System.Text;
using EasiLoginer.Integrations.EasiNote.Pipe;
using FluentAssertions;

namespace EasiLoginer.Integrations.Tests;

/// <summary>希沃命名管道协议（文档 §8.4）。</summary>
public sealed class SeewoPipeClientTests
{
    /// <summary>令牌投递：请求 JSON 字段与上游一致，回执被正确解析。</summary>
    [Fact]
    public async Task 令牌投递往返()
    {
        var pipeName = UniquePipeName();
        var server = StartServer(pipeName, """{"Success":true,"Message":"OK","ErrorDetail":""}""");
        var client = CreateClient(pipeName);

        var response = await client.SendTokenAsync(
            new TokenPayload("tok", "uid-1", "user", "nick", "13800000000"),
            CancellationToken.None);

        response.Success.Should().BeTrue();
        response.Message.Should().Be("OK");

        var request = await server.Request;
        request.Should().Contain("\"statusCode\":202");
        request.Should().Contain("\"token\":\"tok\"");
        request.Should().Contain("\"userId\":\"uid-1\"");
        request.Should().Contain("\"result\":\"https://e.seewo.com\"");
        request.Should().Contain("客户端已扫码并确认登录");

        await server.Completion;
    }

    /// <summary>错误码 4000（或消息里含 4000）判定为令牌失效。</summary>
    [Theory]
    [InlineData("""{"Success":false,"Message":"令牌已失效","Code":"4000"}""", true)]
    [InlineData("""{"Success":false,"Message":"err","ErrorCode":"4000"}""", true)]
    [InlineData("""{"Success":false,"Message":"code 4000 expired"}""", true)]
    [InlineData("""{"Success":false,"Message":"其它错误","Code":"5000"}""", false)]
    public void 令牌失效判定(string response, bool expired)
    {
        TokenResponse.Parse(response).TokenExpired.Should().Be(expired);
    }

    /// <summary>登录态查询：写入 <c>false</c>，解析 <c>statusCode=202</c>。</summary>
    [Fact]
    public async Task 登录态查询往返()
    {
        var pipeName = UniquePipeName();
        var server = StartServer(
            pipeName,
            """{"statusCode":202,"userId":"u1","token":"t1","userName":"user","nickName":"nick","phone":"1","message":"ok"}""");
        var client = CreateClient(pipeName);

        var info = await client.ReadCurrentLoginInfoAsync(addToLoggedTokens: false, CancellationToken.None);

        info.Should().NotBeNull();
        info!.IsLoggedIn.Should().BeTrue();
        info.UserId.Should().Be("u1");
        info.Token.Should().Be("t1");

        (await server.Request).Should().Be("false");
        await server.Completion;
    }

    /// <summary>非法回执按"管道不可用"处理（可重试），不抛 JSON 异常。</summary>
    [Fact]
    public async Task 非法回执视为管道不可用()
    {
        var pipeName = UniquePipeName();
        var server = StartServer(pipeName, "not-json");
        var client = CreateClient(pipeName);

        var action = () => client.SendTokenAsync(new TokenPayload("t", "u", "u", "n", ""), CancellationToken.None);

        await action.Should().ThrowAsync<PipeUnavailableException>();
        await server.Completion;
    }

    /// <summary>管道不存在时抛可重试异常，且不会长时间阻塞。</summary>
    [Fact]
    public async Task 管道不存在时快速失败()
    {
        var client = new SeewoPipeClient(
            TimeSpan.FromMilliseconds(200),
            TimeSpan.FromMilliseconds(200),
            UniquePipeName(),
            UniquePipeName());

        var action = () => client.SendTokenAsync(new TokenPayload("t", "u", "u", "n", ""), CancellationToken.None);

        await action.Should().ThrowAsync<PipeUnavailableException>();
    }

    /// <summary>登录态查询遇到不可用管道时返回 null（上游同样吞掉异常）。</summary>
    [Fact]
    public async Task 登录态查询失败返回空()
    {
        var client = new SeewoPipeClient(
            TimeSpan.FromMilliseconds(200),
            TimeSpan.FromMilliseconds(200),
            UniquePipeName(),
            UniquePipeName());

        var info = await client.ReadCurrentLoginInfoAsync(cancellationToken: CancellationToken.None);

        info.Should().BeNull();
    }

    /// <summary>响应结构非法（statusCode 不是数字）时返回 null，而不是抛异常。</summary>
    [Theory]
    [InlineData("""{"statusCode":"abc"}""")]
    [InlineData("""[1,2,3]""")]
    [InlineData("not-json")]
    public void 非法登录态返回空(string line)
    {
        CurrentLoginInfo.TryParse(line).Should().BeNull();
    }

    /// <summary>缺省 statusCode 视为 0（未登录）。</summary>
    [Fact]
    public void 缺省状态码视为未登录()
    {
        var info = CurrentLoginInfo.TryParse("""{"userId":"u"}""");

        info.Should().NotBeNull();
        info!.StatusCode.Should().Be(0);
        info.IsLoggedIn.Should().BeFalse();
    }

    private static SeewoPipeClient CreateClient(string pipeName)
        => new(TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5), pipeName, pipeName);

    private static string UniquePipeName() => "SeewoTest_" + Guid.NewGuid().ToString("N");

    private static (Task<string> Request, Task Completion) StartServer(string pipeName, string response)
    {
        var requestSource = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);

        var completion = Task.Run(async () =>
        {
            await using var server = new NamedPipeServerStream(
                pipeName,
                PipeDirection.InOut,
                1,
                PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous);

            await server.WaitForConnectionAsync();

            using var reader = new StreamReader(server, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, bufferSize: 1024, leaveOpen: true);
            var line = await reader.ReadLineAsync();
            requestSource.TrySetResult(line ?? string.Empty);

            var writer = new StreamWriter(server, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), bufferSize: 1024, leaveOpen: true)
            {
                AutoFlush = true,
                NewLine = "\n",
            };

            await writer.WriteLineAsync(response);
            await writer.FlushAsync();
        });

        return (requestSource.Task, completion);
    }
}
