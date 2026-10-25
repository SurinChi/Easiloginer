using EasiLoginer.Domain.Geometry;

namespace EasiLoginer.Automation.Cv;

/// <summary>
/// 模板定位端口：把“模板文件名”解析成屏幕上的中心点。
/// </summary>
/// <remarks>
/// 抽成接口是为了让 <see cref="Strategies.CvLoginStrategy"/> 的流程可以在没有真机、
/// 甚至没有屏幕的环境下单测。
/// </remarks>
public interface ITemplateLocator
{
    /// <summary>按模板名（不含扩展名）查找；未找到返回 <see langword="null"/>。</summary>
    PixelPoint? Locate(string templateName);
}
