namespace EasiLoginer.Presentation.Profiles;

/// <summary>
/// 判断密码输入框的一次「变空」是不是页面导航导致的程序性清空。
/// </summary>
/// <remarks>
/// <para>
/// WPF 在页面离开导航宿主时会清空 <c>PasswordBox</c>；如果不加区分地把这个空值写回
/// 视图模型，用户只是切换了一下页面，档案里的密码就会被抹掉，随后点「运行登录」会
/// 提示“未填写密码”。
/// </para>
/// <para>
/// 区分依据：卸载时焦点已经离开输入框；用户主动删空时焦点仍在框内。只有
/// “空值 + 无焦点 + 模型原本有密码”才判定为程序性清空。
/// </para>
/// </remarks>
public static class ProfilePasswordSync
{
    /// <summary>是否是页面卸载/导航引起的清空，而不是用户编辑。</summary>
    /// <param name="newValue">输入框的新值。</param>
    /// <param name="hasKeyboardFocus">输入框当前是否持有键盘焦点。</param>
    /// <param name="currentValue">视图模型中现有的密码。</param>
    public static bool IsProgrammaticClear(string newValue, bool hasKeyboardFocus, string currentValue)
    {
        ArgumentNullException.ThrowIfNull(newValue);
        ArgumentNullException.ThrowIfNull(currentValue);

        return newValue.Length == 0 &&
               !hasKeyboardFocus &&
               !string.IsNullOrEmpty(currentValue);
    }
}
