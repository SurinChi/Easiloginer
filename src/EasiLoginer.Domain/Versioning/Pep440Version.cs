using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace EasiLoginer.Domain.Versioning;

/// <summary>
/// PEP 440 版本号，对应 Python <c>packaging.version.Version</c>。
/// </summary>
/// <remarks>
/// <para>
/// 更新清单与公告的版本范围都按 PEP 440 比较，<b>不能</b>用 <see cref="Version"/>
/// 或 SemVer 代替：<c>1.2.2b6</c> 不是合法 SemVer（文档 §5.4）。
/// </para>
/// <para>
/// 支持 epoch、release、pre（a/b/rc/alpha/beta/pre/preview）、
/// post（post/rev/r）、dev、local 六类段。PEP 440 的隐式 post-release 写法
/// （如 <c>1.2.0-1</c>）本项目不使用，遇到时判定解析失败，而不是猜测语义。
/// </para>
/// <para>
/// 相等性按 PEP 440 归一化：<c>1.2</c>、<c>1.2.0</c>、<c>v1.2</c> 视为同一版本。
/// </para>
/// </remarks>
public sealed class Pep440Version : IComparable<Pep440Version>, IEquatable<Pep440Version>
{
    /// <summary>只有 dev 段（<c>1.2.0.dev1</c>）时的 pre 排序键，低于任何 pre 段。</summary>
    private const int PreRankDevOnly = -1;

    /// <summary>正式版（无 pre、无 dev）的 pre 排序键，高于任何 pre 段。</summary>
    private const int PreRankFinal = 3;

    private static readonly Regex Pattern = new(
        @"^v?(?:(?<epoch>[0-9]+)!)?(?<release>[0-9]+(?:\.[0-9]+)*)" +
        @"(?:[-_.]?(?<pre>a|b|c|rc|alpha|beta|pre|preview)[-_.]?(?<preN>[0-9]+)?)?" +
        @"(?:[-_.]?(?<post>post|rev|r)[-_.]?(?<postN>[0-9]+)?)?" +
        @"(?:[-_.]?(?<dev>dev)[-_.]?(?<devN>[0-9]+)?)?" +
        @"(?:\+(?<local>[a-z0-9]+(?:[-_.][a-z0-9]+)*))?$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private readonly int[] _release;
    private readonly PreReleaseKind? _preKind;
    private readonly int _preNumber;
    private readonly int? _postNumber;
    private readonly int? _devNumber;
    private readonly string? _local;

    private Pep440Version(
        string text,
        int epoch,
        int[] release,
        PreReleaseKind? preKind,
        int preNumber,
        int? postNumber,
        int? devNumber,
        string? local)
    {
        Text = text;
        Epoch = epoch;
        _release = release;
        _preKind = preKind;
        _preNumber = preKind is null ? 0 : preNumber;
        _postNumber = postNumber;
        _devNumber = devNumber;
        _local = local;
    }

    /// <summary>解析时的原始文本（已去除首尾空白）。</summary>
    public string Text { get; }

    /// <summary>版本纪元（<c>1!</c> 中的 1），默认 0。</summary>
    public int Epoch { get; }

    /// <summary>pre 段的排序键：dev-only &lt; alpha &lt; beta &lt; rc &lt; 正式版。</summary>
    private int PreRank
    {
        get
        {
            if (_preKind is not null)
            {
                return (int)_preKind.Value;
            }

            return _devNumber is not null && _postNumber is null ? PreRankDevOnly : PreRankFinal;
        }
    }

    private enum PreReleaseKind
    {
        /// <summary>a / alpha。</summary>
        Alpha = 0,

        /// <summary>b / beta。</summary>
        Beta = 1,

        /// <summary>rc / c / pre / preview。</summary>
        ReleaseCandidate = 2,
    }

    /// <summary>解析版本号，失败时抛出 <see cref="FormatException"/>。</summary>
    public static Pep440Version Parse(string text)
        => TryParse(text, out var version)
            ? version
            : throw new FormatException($"无法解析为 PEP 440 版本号：'{text}'。");

    /// <summary>尝试解析版本号。</summary>
    public static bool TryParse([NotNullWhen(true)] string? text, [NotNullWhen(true)] out Pep440Version? version)
    {
        version = null;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var trimmed = text.Trim();
        var match = Pattern.Match(trimmed);
        if (!match.Success)
        {
            return false;
        }

        var epoch = 0;
        if (match.Groups["epoch"].Success &&
            !int.TryParse(match.Groups["epoch"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out epoch))
        {
            return false;
        }

        var releaseParts = match.Groups["release"].Value.Split('.');
        var release = new int[releaseParts.Length];
        for (var i = 0; i < releaseParts.Length; i++)
        {
            if (!int.TryParse(releaseParts[i], NumberStyles.None, CultureInfo.InvariantCulture, out release[i]))
            {
                return false;
            }
        }

        PreReleaseKind? preKind = null;
        var preNumber = 0;
        if (match.Groups["pre"].Success)
        {
            var pre = match.Groups["pre"].Value;
            if (pre.Equals("a", StringComparison.OrdinalIgnoreCase) ||
                pre.Equals("alpha", StringComparison.OrdinalIgnoreCase))
            {
                preKind = PreReleaseKind.Alpha;
            }
            else if (pre.Equals("b", StringComparison.OrdinalIgnoreCase) ||
                     pre.Equals("beta", StringComparison.OrdinalIgnoreCase))
            {
                preKind = PreReleaseKind.Beta;
            }
            else
            {
                preKind = PreReleaseKind.ReleaseCandidate;
            }

            if (match.Groups["preN"].Success &&
                !int.TryParse(match.Groups["preN"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out preNumber))
            {
                return false;
            }
        }

        int? postNumber = null;
        if (match.Groups["post"].Success)
        {
            var post = 0;
            if (match.Groups["postN"].Success &&
                !int.TryParse(match.Groups["postN"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out post))
            {
                return false;
            }

            postNumber = post;
        }

        int? devNumber = null;
        if (match.Groups["dev"].Success)
        {
            var dev = 0;
            if (match.Groups["devN"].Success &&
                !int.TryParse(match.Groups["devN"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out dev))
            {
                return false;
            }

            devNumber = dev;
        }

        var local = match.Groups["local"].Success ? match.Groups["local"].Value : null;

        version = new Pep440Version(trimmed, epoch, release, preKind, preNumber, postNumber, devNumber, local);
        return true;
    }

    /// <inheritdoc />
    public int CompareTo(Pep440Version? other)
    {
        if (other is null)
        {
            return 1;
        }

        var result = Epoch.CompareTo(other.Epoch);
        if (result != 0)
        {
            return result;
        }

        result = CompareRelease(_release, other._release);
        if (result != 0)
        {
            return result;
        }

        result = PreRank.CompareTo(other.PreRank);
        if (result != 0)
        {
            return result;
        }

        result = _preNumber.CompareTo(other._preNumber);
        if (result != 0)
        {
            return result;
        }

        result = (_postNumber ?? -1).CompareTo(other._postNumber ?? -1);
        if (result != 0)
        {
            return result;
        }

        result = (_devNumber ?? int.MaxValue).CompareTo(other._devNumber ?? int.MaxValue);
        if (result != 0)
        {
            return result;
        }

        return CompareLocal(_local, other._local);
    }

    /// <inheritdoc />
    public bool Equals(Pep440Version? other) => other is not null && CompareTo(other) == 0;

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is Pep440Version other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Epoch);

        // 1.2 与 1.2.0 必须同哈希，因此去掉尾部零
        var last = _release.Length - 1;
        while (last > 0 && _release[last] == 0)
        {
            last--;
        }

        for (var i = 0; i <= last; i++)
        {
            hash.Add(_release[i]);
        }

        hash.Add(PreRank);
        hash.Add(_preNumber);
        hash.Add(_postNumber ?? -1);
        hash.Add(_devNumber ?? int.MaxValue);
        hash.Add(_local, StringComparer.OrdinalIgnoreCase);
        return hash.ToHashCode();
    }

    /// <summary>输出 PEP 440 归一化形式，如 <c>1.2.0b1</c>、<c>1.2.0.post1</c>。</summary>
    public override string ToString()
    {
        var builder = new StringBuilder();
        if (Epoch > 0)
        {
            builder.Append(Epoch).Append('!');
        }

        builder.Append(string.Join('.', _release));

        switch (_preKind)
        {
            case PreReleaseKind.Alpha:
                builder.Append('a').Append(_preNumber);
                break;
            case PreReleaseKind.Beta:
                builder.Append('b').Append(_preNumber);
                break;
            case PreReleaseKind.ReleaseCandidate:
                builder.Append("rc").Append(_preNumber);
                break;
            case null:
                break;
        }

        if (_postNumber is not null)
        {
            builder.Append(".post").Append(_postNumber.Value);
        }

        if (_devNumber is not null)
        {
            builder.Append(".dev").Append(_devNumber.Value);
        }

        if (_local is not null)
        {
            builder.Append('+').Append(_local);
        }

        return builder.ToString();
    }

    private static int CompareRelease(int[] left, int[] right)
    {
        var length = Math.Max(left.Length, right.Length);
        for (var i = 0; i < length; i++)
        {
            var l = i < left.Length ? left[i] : 0;
            var r = i < right.Length ? right[i] : 0;
            if (l != r)
            {
                return l.CompareTo(r);
            }
        }

        return 0;
    }

    private static int CompareLocal(string? left, string? right)
    {
        if (left is null)
        {
            return right is null ? 0 : -1;
        }

        return right is null ? 1 : string.Compare(left, right, StringComparison.OrdinalIgnoreCase);
    }
}
