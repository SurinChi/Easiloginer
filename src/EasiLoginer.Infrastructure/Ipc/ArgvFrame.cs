using System.Text.Json;
using System.Text.Json.Serialization;

namespace EasiLoginer.Infrastructure.Ipc;

/// <summary>
/// 次实例转发给主实例的 argv 帧，对应上游 <c>runtime/ipc.py</c> 的 JSON 结构。
/// </summary>
/// <remarks>
/// 线上格式固定为 <c>{"argv": ["EasiLoginer.exe", ...]}</c>，字段名显式声明，
/// 不依赖全局命名策略（文档 §4.4）。
/// </remarks>
/// <param name="Argv">完整命令行，第 0 项为可执行文件名。</param>
public sealed record ArgvFrame([property: JsonPropertyName("argv")] string[] Argv);

/// <summary>IPC 帧的 System.Text.Json 源生成上下文（AOT 友好）。</summary>
[JsonSerializable(typeof(ArgvFrame))]
internal sealed partial class IpcJsonContext : JsonSerializerContext;
