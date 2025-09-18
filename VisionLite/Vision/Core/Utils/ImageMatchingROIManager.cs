using System;
using System.Collections.Generic;
using VisionLite.Vision.Core.Enums;
using VisionLite.Vision.Core.Models;
using static VisionLite.Vision.Core.Models.CaliperData;
using HalconDotNet;

namespace VisionLite.Vision.Core.Utils
{
    /// <summary>
    /// 图像匹配ROI交互管理器（使用可编辑的HDrawingObject）
    /// </summary>
    public class ImageMatchingROIManager : IDisposable
    {
        #region 属性和字段

        public ROIInteractionMode CurrentMode { get; private set; } = ROIInteractionMode.None;

        public ROIGeometry TemplateROI { get; private set; }
        public ROIGeometry SearchROI { get; private set; }

        // HDrawingObject可编辑ROI对象
        private HDrawingObject _templateDrawingObject;
        private HDrawingObject _searchDrawingObject;
        private HDrawingObject _currentDrawingObject;

        private HWindow _halconWindow;
        private VisionImage _currentImage;
        private bool _disposed = false;

        // 防止重复触发事件的标志
        private bool _isProcessingROIEvent = false;


        #endregion

        #region 事件定义

        /// <summary>
        /// 模板ROI完成事件
        /// </summary>
        public event Action<ROIGeometry> TemplateROICompleted;

        /// <summary>
        /// 搜索ROI完成事件
        /// </summary>
        public event Action<ROIGeometry> SearchROICompleted;

        /// <summary>
        /// ROI更新事件（用于实时显示）
        /// </summary>
        public event Action ROIUpdated;

        #endregion

        #region 公共方法

        /// <summary>
        /// 初始化ROI管理器
        /// </summary>
        /// <param name="halconWindow">Halcon窗口对象</param>
        public void Initialize(HWindow halconWindow)
        {
            _halconWindow = halconWindow;
        }


        /// <summary>
        /// 设置当前图像
        /// </summary>
        public void SetCurrentImage(VisionImage image)
        {
            _currentImage = image;
        }

        /// <summary>
        /// 设置ROI交互模式
        /// </summary>
        public void SetROIMode(ROIInteractionMode mode)
        {
            if (CurrentMode == mode) return;

            var previousMode = CurrentMode;

            // 切换模式时，将当前编辑的ROI转换为对应的固定ROI
            if (_currentDrawingObject != null)
            {
                var geometry = GetROIGeometryFromDrawingObject(_currentDrawingObject);
                if (geometry != null)
                {
                    if (previousMode == ROIInteractionMode.TemplateROI)
                    {
                        // 从模板ROI切换出去时，保存为固定模板ROI
                        DisposeTemplateROI();
                        TemplateROI = geometry;
                        _templateDrawingObject = _currentDrawingObject;
                        _currentDrawingObject = null;

                        // 订阅编辑事件
                        _templateDrawingObject.OnDrag(OnTemplateROIDrag);
                        _templateDrawingObject.OnResize(OnTemplateROIResize);
                        TemplateROICompleted?.Invoke(TemplateROI);
                    }
                    else if (previousMode == ROIInteractionMode.SearchROI)
                    {
                        // 从搜索ROI切换出去时，保存为固定搜索ROI
                        DisposeSearchROI();
                        SearchROI = geometry;
                        _searchDrawingObject = _currentDrawingObject;
                        _currentDrawingObject = null;

                        // 订阅编辑事件
                        _searchDrawingObject.OnDrag(OnSearchROIDrag);
                        _searchDrawingObject.OnResize(OnSearchROIResize);
                        SearchROICompleted?.Invoke(SearchROI);
                    }
                }
            }

            CurrentMode = mode;

            // 根据目标模式，激活对应的ROI或创建新的ROI
            if (mode == ROIInteractionMode.TemplateROI)
            {
                if (_templateDrawingObject != null)
                {
                    // 模板ROI已存在，激活为当前编辑对象
                    _currentDrawingObject = _templateDrawingObject;
                    _templateDrawingObject = null;

                    // ROI已激活，可以直接编辑

                    System.Diagnostics.Debug.WriteLine("激活现有模板ROI");
                }
                else if (_halconWindow != null)
                {
                    // 模板ROI不存在，创建新的
                    CreateInteractiveROI(mode);
                    System.Diagnostics.Debug.WriteLine("创建新的模板ROI");
                }
            }
            else if (mode == ROIInteractionMode.SearchROI)
            {
                if (_searchDrawingObject != null)
                {
                    // 搜索ROI已存在，激活为当前编辑对象
                    _currentDrawingObject = _searchDrawingObject;
                    _searchDrawingObject = null;

                    // ROI已激活，可以直接编辑

                    System.Diagnostics.Debug.WriteLine("激活现有搜索ROI");
                }
                else if (_halconWindow != null)
                {
                    // 搜索ROI不存在，创建新的
                    CreateInteractiveROI(mode);
                    System.Diagnostics.Debug.WriteLine("创建新的搜索ROI");
                }
            }
        }

        /// <summary>
        /// 切换到搜索ROI模式（简化流程）
        /// </summary>
        public void SwitchToSearchROI()
        {
            try
            {
                // 如果当前是模板ROI模式且有当前绘制对象，需要保存模板ROI
                if (CurrentMode == ROIInteractionMode.TemplateROI && _currentDrawingObject != null)
                {
                    // 获取当前模板ROI参数并保存
                    var templateGeometry = GetROIGeometryFromDrawingObject(_currentDrawingObject);
                    if (templateGeometry != null)
                    {
                        // 清理之前的模板ROI
                        DisposeTemplateROI();

                        // 保存模板ROI，保持红色且可编辑
                        TemplateROI = templateGeometry;
                        _templateDrawingObject = _currentDrawingObject;
                        _currentDrawingObject = null; // 转移所有权

                        // 订阅模板ROI的编辑事件
                        _templateDrawingObject.OnDrag(OnTemplateROIDrag);
                        _templateDrawingObject.OnResize(OnTemplateROIResize);

                        // 触发模板ROI完成事件
                        TemplateROICompleted?.Invoke(TemplateROI);
                    }
                }

                // 切换到搜索ROI模式
                CurrentMode = ROIInteractionMode.SearchROI;

                // 如果搜索ROI已存在，激活现有的；否则创建新的
                if (_searchDrawingObject != null)
                {
                    // 搜索ROI已存在，激活为当前编辑对象
                    _currentDrawingObject = _searchDrawingObject;
                    _searchDrawingObject = null; // 转移所有权

                    System.Diagnostics.Debug.WriteLine("激活现有搜索ROI进入编辑状态");
                }
                else
                {
                    // 搜索ROI不存在，创建新的
                    CreateInteractiveROI(ROIInteractionMode.SearchROI);
                    System.Diagnostics.Debug.WriteLine("创建新的搜索ROI");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"切换到搜索ROI失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 切换到模板ROI模式
        /// </summary>
        public void SwitchToTemplateROI()
        {
            try
            {
                // 如果当前是搜索ROI模式且有当前绘制对象，需要保存搜索ROI
                if (CurrentMode == ROIInteractionMode.SearchROI && _currentDrawingObject != null)
                {
                    // 获取当前搜索ROI参数并保存
                    var searchGeometry = GetROIGeometryFromDrawingObject(_currentDrawingObject);
                    if (searchGeometry != null)
                    {
                        // 清理之前的搜索ROI
                        DisposeSearchROI();

                        // 保存搜索ROI，保持蓝色且可编辑
                        SearchROI = searchGeometry;
                        _searchDrawingObject = _currentDrawingObject;
                        _currentDrawingObject = null; // 转移所有权

                        // 设置搜索ROI样式（蓝色）
                        _searchDrawingObject.SetDrawingObjectParams("color", "blue");
                        _searchDrawingObject.SetDrawingObjectParams("line_width", 3);

                        // 订阅搜索ROI的编辑事件
                        _searchDrawingObject.OnDrag(OnSearchROIDrag);
                        _searchDrawingObject.OnResize(OnSearchROIResize);

                        // 触发搜索ROI完成事件
                        SearchROICompleted?.Invoke(SearchROI);
                    }
                }

                // 切换到模板ROI模式
                CurrentMode = ROIInteractionMode.TemplateROI;

                // 如果模板ROI已存在，激活现有的；否则创建新的
                if (_templateDrawingObject != null)
                {
                    // 模板ROI已存在，激活为当前编辑对象
                    _currentDrawingObject = _templateDrawingObject;
                    _templateDrawingObject = null; // 转移所有权

                    System.Diagnostics.Debug.WriteLine("激活现有模板ROI进入编辑状态");
                }
                else
                {
                    // 模板ROI不存在，创建新的
                    CreateInteractiveROI(ROIInteractionMode.TemplateROI);
                    System.Diagnostics.Debug.WriteLine("创建新的模板ROI");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"切换到模板ROI失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 应用所有ROI设置
        /// </summary>
        public void ApplyAllROI()
        {
            if (_isProcessingROIEvent)
            {
                System.Diagnostics.Debug.WriteLine("正在处理ROI事件，跳过重复应用");
                return;
            }

            try
            {
                _isProcessingROIEvent = true;
                // 如果当前有正在编辑的ROI，需要先保存
                if (_currentDrawingObject != null)
                {
                    var geometry = GetROIGeometryFromDrawingObject(_currentDrawingObject);
                    if (geometry != null)
                    {
                        if (CurrentMode == ROIInteractionMode.TemplateROI)
                        {
                            // 保存当前编辑的模板ROI
                            DisposeTemplateROI();
                            TemplateROI = geometry;
                            _templateDrawingObject = _currentDrawingObject;
                            _currentDrawingObject = null;

                            // 订阅编辑事件
                            _templateDrawingObject.OnDrag(OnTemplateROIDrag);
                            _templateDrawingObject.OnResize(OnTemplateROIResize);
                            TemplateROICompleted?.Invoke(TemplateROI);
                        }
                        else if (CurrentMode == ROIInteractionMode.SearchROI)
                        {
                            // 保存当前编辑的搜索ROI
                            DisposeSearchROI();
                            SearchROI = geometry;
                            _searchDrawingObject = _currentDrawingObject;
                            _currentDrawingObject = null;

                            // 设置搜索ROI样式（蓝色）
                            _searchDrawingObject.SetDrawingObjectParams("color", "blue");
                            _searchDrawingObject.SetDrawingObjectParams("line_width", 2);

                            // 订阅编辑事件
                            _searchDrawingObject.OnDrag(OnSearchROIDrag);
                            _searchDrawingObject.OnResize(OnSearchROIResize);
                            SearchROICompleted?.Invoke(SearchROI);
                        }
                    }
                }

                // 更新并确认模板ROI
                if (_templateDrawingObject != null)
                {
                    var templateGeometry = GetROIGeometryFromDrawingObject(_templateDrawingObject);
                    if (templateGeometry != null)
                    {
                        TemplateROI = templateGeometry;
                        System.Diagnostics.Debug.WriteLine($"应用模板ROI: Row1={templateGeometry.Parameters["Row1"]}, Col1={templateGeometry.Parameters["Col1"]}, Row2={templateGeometry.Parameters["Row2"]}, Col2={templateGeometry.Parameters["Col2"]}");
                        TemplateROICompleted?.Invoke(TemplateROI);
                    }
                }

                // 更新并确认搜索ROI
                if (_searchDrawingObject != null)
                {
                    var searchGeometry = GetROIGeometryFromDrawingObject(_searchDrawingObject);
                    if (searchGeometry != null)
                    {
                        SearchROI = searchGeometry;
                        SearchROICompleted?.Invoke(SearchROI);
                    }
                }

                // 退出ROI编辑模式
                CurrentMode = ROIInteractionMode.None;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"应用ROI设置失败: {ex.Message}");
            }
            finally
            {
                _isProcessingROIEvent = false;
            }
        }

        /// <summary>
        /// 清空所有ROI
        /// </summary>
        public void ClearAllROI()
        {
            DisposeTemplateROI();
            DisposeSearchROI();
            DisposeCurrentDrawingObject();

            TemplateROI = null;
            SearchROI = null;
            CurrentMode = ROIInteractionMode.None;
        }

        /// <summary>
        /// 获取指定ROI的几何信息
        /// </summary>
        public ROIGeometry GetROIGeometry(ROIInteractionMode mode)
        {
            return mode == ROIInteractionMode.TemplateROI ? TemplateROI : SearchROI;
        }

        /// <summary>
        /// 获取指定ROI的绘图对象
        /// </summary>
        public HDrawingObject GetDrawingObject(ROIInteractionMode mode)
        {
            return mode == ROIInteractionMode.TemplateROI ? _templateDrawingObject : _searchDrawingObject;
        }

        /// <summary>
        /// 获取当前正在绘制的ROI对象
        /// </summary>
        public HDrawingObject GetCurrentDrawingObject()
        {
            return _currentDrawingObject;
        }

        #endregion

        #region 私有方法

        /// <summary>
        /// 创建交互式ROI
        /// </summary>
        private void CreateInteractiveROI(ROIInteractionMode mode)
        {
            if (_halconWindow == null || _currentImage == null)
            {
                System.Diagnostics.Debug.WriteLine($"无法创建ROI: halconWindow={_halconWindow != null}, currentImage={_currentImage != null}");
                return;
            }

            // 检查图像对象是否有效
            if (_currentImage.HImage == null || !_currentImage.HImage.IsInitialized())
            {
                System.Diagnostics.Debug.WriteLine("无法创建ROI: 图像对象无效或未初始化");
                return;
            }

            try
            {
                // 获取图像尺寸用于默认ROI大小
                HOperatorSet.GetImageSize(_currentImage.HImage, out HTuple width, out HTuple height);

                // 根据ROI类型计算默认位置和大小
                double centerRow, centerCol, roiWidth, roiHeight;

                if (mode == ROIInteractionMode.TemplateROI)
                {
                    // 模板ROI：较小，位于图像中心
                    centerRow = height.D / 2;
                    centerCol = width.D / 2;
                    roiWidth = Math.Min(width.D, height.D) / 4;
                    roiHeight = roiWidth;
                }
                else // SearchROI
                {
                    // 搜索ROI：覆盖整个图像，符合大部分应用场景
                    centerRow = height.D / 2;
                    centerCol = width.D / 2;
                    roiWidth = width.D;
                    roiHeight = height.D;
                }

                // 创建矩形ROI绘图对象
                _currentDrawingObject = HDrawingObject.CreateDrawingObject(
                    HDrawingObject.HDrawingObjectType.RECTANGLE1,
                    centerRow - roiHeight / 2,  // row1
                    centerCol - roiWidth / 2,   // col1
                    centerRow + roiHeight / 2,  // row2
                    centerCol + roiWidth / 2    // col2
                );

                // 设置绘图对象样式
                var color = mode == ROIInteractionMode.TemplateROI ? "red" : "blue";
                _currentDrawingObject.SetDrawingObjectParams("color", color);
                _currentDrawingObject.SetDrawingObjectParams("line_width", 3);
                _currentDrawingObject.SetDrawingObjectParams("marker_size", 10);

                // 附加到窗口
                _halconWindow.AttachDrawingObjectToWindow(_currentDrawingObject);

                // 订阅事件
                _currentDrawingObject.OnDrag(OnROIDrag);
                _currentDrawingObject.OnResize(OnROIResize);
                _currentDrawingObject.OnSelect(OnROISelect);

                // 新创建的ROI自动处于可编辑状态

                // 触发更新事件
                ROIUpdated?.Invoke();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"创建交互式ROI失败: {ex.Message}");
                DisposeCurrentDrawingObject();
            }
        }

        /// <summary>
        /// 从绘图对象获取ROI几何信息
        /// </summary>
        private ROIGeometry GetROIGeometryFromDrawingObject(HDrawingObject drawingObject)
        {
            if (drawingObject == null) return null;

            try
            {
                // 获取矩形参数
                var row1 = drawingObject.GetDrawingObjectParams("row1").D;
                var col1 = drawingObject.GetDrawingObjectParams("column1").D;
                var row2 = drawingObject.GetDrawingObjectParams("row2").D;
                var col2 = drawingObject.GetDrawingObjectParams("column2").D;

                // 确保坐标顺序正确
                var minRow = Math.Min(row1, row2);
                var maxRow = Math.Max(row1, row2);
                var minCol = Math.Min(col1, col2);
                var maxCol = Math.Max(col1, col2);

                return new ROIGeometry
                {
                    RoiType = "rectangle1",
                    Parameters = new Dictionary<string, double>
                    {
                        ["Row1"] = minRow,
                        ["Col1"] = minCol,
                        ["Row2"] = maxRow,
                        ["Col2"] = maxCol
                    }
                };
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"获取ROI几何信息失败: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// ROI拖拽事件处理
        /// </summary>
        private void OnROIDrag(HDrawingObject dobj, HWindow hwin, string type)
        {
            ROIUpdated?.Invoke();
        }

        /// <summary>
        /// ROI缩放事件处理
        /// </summary>
        private void OnROIResize(HDrawingObject dobj, HWindow hwin, string type)
        {
            ROIUpdated?.Invoke();
        }

        /// <summary>
        /// ROI选择事件处理
        /// </summary>
        private void OnROISelect(HDrawingObject dobj, HWindow hwin, string type)
        {
            ROIUpdated?.Invoke();
        }

        /// <summary>
        /// 模板ROI拖拽事件处理
        /// </summary>
        private void OnTemplateROIDrag(HDrawingObject dobj, HWindow hwin, string type)
        {
            if (_isProcessingROIEvent) return;

            // 更新模板ROI参数
            TemplateROI = GetROIGeometryFromDrawingObject(dobj);
            ROIUpdated?.Invoke();
        }

        /// <summary>
        /// 模板ROI缩放事件处理
        /// </summary>
        private void OnTemplateROIResize(HDrawingObject dobj, HWindow hwin, string type)
        {
            if (_isProcessingROIEvent) return;

            // 更新模板ROI参数
            TemplateROI = GetROIGeometryFromDrawingObject(dobj);
            ROIUpdated?.Invoke();
        }

        /// <summary>
        /// 搜索ROI拖拽事件处理
        /// </summary>
        private void OnSearchROIDrag(HDrawingObject dobj, HWindow hwin, string type)
        {
            if (_isProcessingROIEvent) return;

            // 更新搜索ROI参数
            SearchROI = GetROIGeometryFromDrawingObject(dobj);
            ROIUpdated?.Invoke();
        }

        /// <summary>
        /// 搜索ROI缩放事件处理
        /// </summary>
        private void OnSearchROIResize(HDrawingObject dobj, HWindow hwin, string type)
        {
            if (_isProcessingROIEvent) return;

            // 更新搜索ROI参数
            SearchROI = GetROIGeometryFromDrawingObject(dobj);
            ROIUpdated?.Invoke();
        }

        /// <summary>
        /// 释放模板ROI
        /// </summary>
        private void DisposeTemplateROI()
        {
            if (_templateDrawingObject != null)
            {
                try
                {
                    _halconWindow?.DetachDrawingObjectFromWindow(_templateDrawingObject);
                    _templateDrawingObject.Dispose();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"释放模板ROI失败: {ex.Message}");
                }
                finally
                {
                    _templateDrawingObject = null;
                }
            }
        }

        /// <summary>
        /// 释放搜索ROI
        /// </summary>
        private void DisposeSearchROI()
        {
            if (_searchDrawingObject != null)
            {
                try
                {
                    _halconWindow?.DetachDrawingObjectFromWindow(_searchDrawingObject);
                    _searchDrawingObject.Dispose();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"释放搜索ROI失败: {ex.Message}");
                }
                finally
                {
                    _searchDrawingObject = null;
                }
            }
        }

        /// <summary>
        /// 释放当前绘制对象
        /// </summary>
        private void DisposeCurrentDrawingObject()
        {
            if (_currentDrawingObject != null)
            {
                try
                {
                    _halconWindow?.DetachDrawingObjectFromWindow(_currentDrawingObject);
                    _currentDrawingObject.Dispose();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"释放当前绘制对象失败: {ex.Message}");
                }
                finally
                {
                    _currentDrawingObject = null;
                }
            }
        }

        #endregion


        #region IDisposable实现

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    ClearAllROI();
                }
                _disposed = true;
            }
        }

        #endregion
    }
}