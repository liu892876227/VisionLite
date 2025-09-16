namespace VisionLite.Vision.Calibration.NinePoint.Core
{
    /// <summary>
    /// 物理单位辅助类
    /// 提供物理单位的中文显示支持
    /// </summary>
    public static class PhysicalUnitHelper
    {
        /// <summary>
        /// 获取物理单位的中文描述
        /// 由于只支持毫米，固定返回"毫米"
        /// </summary>
        public static string GetChineseDescription(PhysicalUnit unit)
        {
            return "毫米";
        }
    }
}