using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

namespace EasiLoginer.Architecture.Tests;

/// <summary>
/// 只读 PE 元数据的程序集依赖探针。
/// </summary>
/// <remarks>
/// 架构测试<b>不加载</b>被测程序集，而是直接读元数据的 AssemblyRef / TypeRef 表。
/// 原因：本机开启了 Smart App Control，<c>Assembly.Load</c> 会按哈希口碑拦截
/// 新编译的无签名程序集（0x800711C7），导致测试随机失败；
/// 元数据只读方式不执行任何代码，结果确定且更快。
/// </remarks>
internal static class MetadataProbe
{
    /// <summary>读取一个程序集的引用事实。</summary>
    public static AssemblyFacts Read(string assemblyPath)
    {
        using var stream = File.OpenRead(assemblyPath);
        using var peReader = new PEReader(stream);
        var reader = peReader.GetMetadataReader();

        var assemblies = new List<string>();
        foreach (var handle in reader.AssemblyReferences)
        {
            var reference = reader.GetAssemblyReference(handle);
            assemblies.Add(reader.GetString(reference.Name));
        }

        var namespaces = new HashSet<string>(StringComparer.Ordinal);
        foreach (var handle in reader.TypeReferences)
        {
            var reference = reader.GetTypeReference(handle);

            // 嵌套类型的 Namespace 为空，其外层类型会单独出现在 TypeRef 表中
            if (reference.ResolutionScope.Kind is not HandleKind.AssemblyReference)
            {
                continue;
            }

            var nameSpace = reader.GetString(reference.Namespace);
            if (!string.IsNullOrEmpty(nameSpace))
            {
                namespaces.Add(nameSpace);
            }
        }

        namespaces.UnionWith(assemblies);
        return new AssemblyFacts(assemblies, namespaces);
    }
}

/// <summary>一个程序集的外部引用事实。</summary>
/// <param name="AssemblyReferences">AssemblyRef 表中的程序集名。</param>
/// <param name="Namespaces">TypeRef 表中出现的命名空间（并入程序集名便于统一匹配）。</param>
internal sealed record AssemblyFacts(
    IReadOnlyList<string> AssemblyReferences,
    IReadOnlyCollection<string> Namespaces);
