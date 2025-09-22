using System;
using System.Collections.Generic;
using System.Linq;
using HalconDotNet;

namespace VisionLite.Vision.Calibration.NinePoint.Core
{
    /// <summary>
    /// 坐标变换计算器
    /// </summary>
    public static class TransformCalculator
    {
        /// <summary>
        /// 计算单应性变换矩阵
        /// </summary>
        public static bool ComputeHomographyMatrix(
            List<CalibrationPointPair> pointPairs, 
            out double[,] transformMatrix, 
            out double[,] inverseMatrix)
        {
            transformMatrix = new double[3, 3];
            inverseMatrix = new double[3, 3];
            
            try
            {
                var validPairs = pointPairs.Where(p => p.IsSet).ToList();
                if (validPairs.Count < 4)
                {
                    return false;
                }

                // 检查点位分布
                if (!ValidatePointDistribution(validPairs))
                {
                    return false;
                }

                // 准备Halcon输入数据
                var imageRows = validPairs.Select(p => p.ImagePoint.Y).ToArray();
                var imageCols = validPairs.Select(p => p.ImagePoint.X).ToArray();
                var worldRows = validPairs.Select(p => p.WorldPoint.Y).ToArray();
                var worldCols = validPairs.Select(p => p.WorldPoint.X).ToArray();


                // 使用Halcon计算单应性矩阵
                HTuple homMat2D = null;
                bool halconSuccess = false;
                
                try
                {
                    var imageColsTuple = new HTuple(imageCols);
                    var imageRowsTuple = new HTuple(imageRows);
                    var worldColsTuple = new HTuple(worldCols);
                    var worldRowsTuple = new HTuple(worldRows);

                    HOperatorSet.VectorToHomMat2d(
                        imageColsTuple, imageRowsTuple,
                        worldColsTuple, worldRowsTuple,
                        out homMat2D);

                    // 检查矩阵有效性
                    if (homMat2D != null && (homMat2D.Length == 9 || homMat2D.Length == 6))
                    {
                        // 检查是否有NaN或无穷大值
                        var matrixArray = homMat2D.DArr;
                        bool hasInvalidValues = false;
                        for (int i = 0; i < matrixArray.Length; i++)
                        {
                            if (double.IsNaN(matrixArray[i]) || double.IsInfinity(matrixArray[i]))
                            {
                                hasInvalidValues = true;
                            }
                        }
                        
                        if (!hasInvalidValues)
                        {
                            // 根据Halcon返回的矩阵类型进行处理
                            if (homMat2D.Length == 6)
                            {
                                // Halcon返回的是仿射变换矩阵(2x3)，这对于规则网格数据是合适的
                                ConvertAffineToHomography(homMat2D, transformMatrix);
                            }
                            else if (homMat2D.Length == 9)
                            {
                                // 完整的3x3单应性矩阵
                                ConvertHtupleToMatrix(homMat2D, transformMatrix);
                            }
                            else
                            {
                                return false;
                            }
                            
                            // 检查矩阵的行列式
                            double det = CalculateDeterminant3x3(transformMatrix);

                            if (Math.Abs(det) > 1e-10)
                            {
                                halconSuccess = true;
                            }
                        }
                    }
                }
                catch (Exception halconEx)
                {
                    // Halcon矩阵计算失败（重要错误）
                    Console.WriteLine($"[VisionLite] Halcon矩阵计算失败: {halconEx.Message}");
                }
                
                // 如果Halcon失败，返回错误
                if (!halconSuccess)
                {
                    return false;
                }

                // 计算逆矩阵
                try
                {
                    HOperatorSet.HomMat2dInvert(homMat2D, out HTuple invHomMat2D);
                    ConvertHtupleToMatrix(invHomMat2D, inverseMatrix);
                }
                catch
                {
                    // Halcon逆矩阵计算失败，手动计算
                    if (!InvertMatrix3x3(transformMatrix, inverseMatrix))
                    {
                        return false;
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                // 单应性矩阵计算失败（重要错误）
                Console.WriteLine($"[VisionLite] 单应性矩阵计算失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 计算仿射变换矩阵
        /// </summary>
        public static bool ComputeAffineMatrix(
            List<CalibrationPointPair> pointPairs,
            out double[,] transformMatrix,
            out double[,] inverseMatrix)
        {
            transformMatrix = new double[3, 3];
            inverseMatrix = new double[3, 3];
            
            try
            {
                var validPairs = pointPairs.Where(p => p.IsSet).ToList();
                if (validPairs.Count < 3)
                {
                    return false;
                }

                // 使用最小二乘法求解仿射变换参数
                // [a b tx] [x]   [X]
                // [c d ty] [y] = [Y]
                // [0 0 1 ] [1]   [1]
                
                var A = new double[validPairs.Count * 2, 6];
                var B = new double[validPairs.Count * 2];
                
                for (int i = 0; i < validPairs.Count; i++)
                {
                    var pair = validPairs[i];
                    int row1 = i * 2;
                    int row2 = i * 2 + 1;
                    
                    // X = a*x + b*y + tx
                    A[row1, 0] = pair.ImagePoint.X;
                    A[row1, 1] = pair.ImagePoint.Y;
                    A[row1, 2] = 1;
                    A[row1, 3] = 0;
                    A[row1, 4] = 0;
                    A[row1, 5] = 0;
                    B[row1] = pair.WorldPoint.X;
                    
                    // Y = c*x + d*y + ty
                    A[row2, 0] = 0;
                    A[row2, 1] = 0;
                    A[row2, 2] = 0;
                    A[row2, 3] = pair.ImagePoint.X;
                    A[row2, 4] = pair.ImagePoint.Y;
                    A[row2, 5] = 1;
                    B[row2] = pair.WorldPoint.Y;
                }
                
                // 使用SVD求解最小二乘问题
                var parameters = SolveLeastSquares(A, B);
                if (parameters == null) return false;
                
                // 构建变换矩阵
                transformMatrix[0, 0] = parameters[0]; // a
                transformMatrix[0, 1] = parameters[1]; // b
                transformMatrix[0, 2] = parameters[2]; // tx
                transformMatrix[1, 0] = parameters[3]; // c
                transformMatrix[1, 1] = parameters[4]; // d
                transformMatrix[1, 2] = parameters[5]; // ty
                transformMatrix[2, 0] = 0;
                transformMatrix[2, 1] = 0;
                transformMatrix[2, 2] = 1;
                
                // 计算逆矩阵
                InvertMatrix3x3(transformMatrix, inverseMatrix);
                
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[VisionLite] 仿射变换计算失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 图像坐标转世界坐标
        /// </summary>
        public static Point2D ImageToWorld(Point2D imagePoint, double[,] transformMatrix)
        {
            try
            {
                // 齐次坐标变换
                double x = transformMatrix[0, 0] * imagePoint.X + transformMatrix[0, 1] * imagePoint.Y + transformMatrix[0, 2];
                double y = transformMatrix[1, 0] * imagePoint.X + transformMatrix[1, 1] * imagePoint.Y + transformMatrix[1, 2];
                double w = transformMatrix[2, 0] * imagePoint.X + transformMatrix[2, 1] * imagePoint.Y + transformMatrix[2, 2];
                
                if (Math.Abs(w) < 1e-10) return new Point2D();
                
                return new Point2D(x / w, y / w);
            }
            catch
            {
                return new Point2D();
            }
        }

        /// <summary>
        /// 世界坐标转图像坐标
        /// </summary>
        public static Point2D WorldToImage(Point2D worldPoint, double[,] inverseMatrix)
        {
            return ImageToWorld(worldPoint, inverseMatrix);
        }

        /// <summary>
        /// 计算标定误差
        /// </summary>
        public static double CalculateCalibrationError(
            List<CalibrationPointPair> pointPairs,
            double[,] transformMatrix)
        {
            if (pointPairs == null || pointPairs.Count == 0) return double.MaxValue;
            
            double totalError = 0;
            int validCount = 0;
            
            foreach (var pair in pointPairs.Where(p => p.IsSet))
            {
                try
                {
                    // 将图像坐标转换为世界坐标
                    var predictedWorld = ImageToWorld(pair.ImagePoint, transformMatrix);
                    
                    // 计算与实际世界坐标的距离误差
                    var error = predictedWorld.DistanceTo(pair.WorldPoint);
                    
                    pair.FitError = error;
                    totalError += error;
                    validCount++;
                }
                catch
                {
                    pair.FitError = double.MaxValue;
                }
            }
            
            return validCount > 0 ? totalError / validCount : double.MaxValue;
        }
        
        /// <summary>
        /// 评估标定质量
        /// </summary>
        public static CalibrationQuality EvaluateQuality(double averageError, double maxError)
        {
            if (averageError <= 0.1 && maxError <= 0.2)
                return CalibrationQuality.Excellent;
            else if (averageError <= 0.5 && maxError <= 1.0)
                return CalibrationQuality.Good;
            else if (averageError <= 1.0 && maxError <= 2.0)
                return CalibrationQuality.Acceptable;
            else if (averageError <= 2.0 && maxError <= 4.0)
                return CalibrationQuality.Poor;
            else
                return CalibrationQuality.Unusable;
        }

        #region 私有辅助方法

        /// <summary>
        /// 验证点位分布是否合理
        /// </summary>
        private static bool ValidatePointDistribution(List<CalibrationPointPair> validPairs)
        {
            if (validPairs.Count < 4) return false;

            // 检查重复点
            for (int i = 0; i < validPairs.Count - 1; i++)
            {
                for (int j = i + 1; j < validPairs.Count; j++)
                {
                    var dist1 = validPairs[i].ImagePoint.DistanceTo(validPairs[j].ImagePoint);
                    var dist2 = validPairs[i].WorldPoint.DistanceTo(validPairs[j].WorldPoint);
                    
                    if (dist1 < 1.0 || dist2 < 0.001) // 图像坐标相差小于1像素或世界坐标相差小于0.001mm
                    {
                        return false;
                    }
                }
            }

            // 检查是否有足够的非共线点
            if (validPairs.Count >= 3)
            {
                int nonCollinearCount = 0;
                for (int i = 0; i < validPairs.Count - 2; i++)
                {
                    for (int j = i + 1; j < validPairs.Count - 1; j++)
                    {
                        for (int k = j + 1; k < validPairs.Count; k++)
                        {
                            if (!AreCollinear(validPairs[i].ImagePoint, validPairs[j].ImagePoint, validPairs[k].ImagePoint))
                            {
                                nonCollinearCount++;
                            }
                        }
                    }
                }
                
                if (nonCollinearCount == 0)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// 检查三点是否共线
        /// </summary>
        private static bool AreCollinear(Point2D p1, Point2D p2, Point2D p3)
        {
            // 使用叉积判断共线性
            double cross = (p2.X - p1.X) * (p3.Y - p1.Y) - (p2.Y - p1.Y) * (p3.X - p1.X);
            return Math.Abs(cross) < 1e-6; // 阈值可以调整
        }

        /// <summary>
        /// 计算3x3矩阵的行列式
        /// </summary>
        private static double CalculateDeterminant3x3(double[,] matrix)
        {
            return matrix[0, 0] * (matrix[1, 1] * matrix[2, 2] - matrix[1, 2] * matrix[2, 1]) -
                   matrix[0, 1] * (matrix[1, 0] * matrix[2, 2] - matrix[1, 2] * matrix[2, 0]) +
                   matrix[0, 2] * (matrix[1, 0] * matrix[2, 1] - matrix[1, 1] * matrix[2, 0]);
        }

        private static void ConvertHtupleToMatrix(HTuple htuple, double[,] matrix)
        {
            var array = htuple.DArr;
            for (int i = 0; i < 3; i++)
            {
                for (int j = 0; j < 3; j++)
                {
                    matrix[i, j] = array[i * 3 + j];
                }
            }
        }
        
        /// <summary>
        /// 将Halcon返回的仿射变换矩阵(6个元素)转换为3x3单应性矩阵
        /// </summary>
        private static void ConvertAffineToHomography(HTuple affineMatrix, double[,] homographyMatrix)
        {
            var array = affineMatrix.DArr;
            
            // Halcon仿射矩阵格式: [a, b, tx, c, d, ty]
            // 对应的变换: 
            // X = a*x + b*y + tx
            // Y = c*x + d*y + ty
            
            // 转换为3x3单应性矩阵:
            // [a  b  tx]
            // [c  d  ty]
            // [0  0  1 ]
            
            homographyMatrix[0, 0] = array[0]; // a
            homographyMatrix[0, 1] = array[1]; // b  
            homographyMatrix[0, 2] = array[2]; // tx
            homographyMatrix[1, 0] = array[3]; // c
            homographyMatrix[1, 1] = array[4]; // d
            homographyMatrix[1, 2] = array[5]; // ty
            homographyMatrix[2, 0] = 0.0;      // 0
            homographyMatrix[2, 1] = 0.0;      // 0
            homographyMatrix[2, 2] = 1.0;      // 1
        }

        private static double[] SolveLeastSquares(double[,] A, double[] B)
        {
            // 简化实现，使用正规方程：(A^T * A) * x = A^T * B
            try
            {
                int m = A.GetLength(0);
                int n = A.GetLength(1);
                
                // 计算 A^T * A
                var AtA = new double[n, n];
                for (int i = 0; i < n; i++)
                {
                    for (int j = 0; j < n; j++)
                    {
                        double sum = 0;
                        for (int k = 0; k < m; k++)
                        {
                            sum += A[k, i] * A[k, j];
                        }
                        AtA[i, j] = sum;
                    }
                }
                
                // 计算 A^T * B
                var AtB = new double[n];
                for (int i = 0; i < n; i++)
                {
                    double sum = 0;
                    for (int k = 0; k < m; k++)
                    {
                        sum += A[k, i] * B[k];
                    }
                    AtB[i] = sum;
                }
                
                // 求解线性方程组
                return GaussianElimination(AtA, AtB);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[VisionLite] 最小二乘求解失败: {ex.Message}");
                return null;
            }
        }

        private static double[] GaussianElimination(double[,] A, double[] B)
        {
            int n = B.Length;
            var augmented = new double[n, n + 1];
            
            // 构建增广矩阵
            for (int i = 0; i < n; i++)
            {
                for (int j = 0; j < n; j++)
                {
                    augmented[i, j] = A[i, j];
                }
                augmented[i, n] = B[i];
            }
            
            // 高斯消元
            for (int i = 0; i < n; i++)
            {
                // 寻找主元
                int maxRow = i;
                for (int k = i + 1; k < n; k++)
                {
                    if (Math.Abs(augmented[k, i]) > Math.Abs(augmented[maxRow, i]))
                    {
                        maxRow = k;
                    }
                }
                
                // 交换行
                if (maxRow != i)
                {
                    for (int j = 0; j <= n; j++)
                    {
                        var temp = augmented[i, j];
                        augmented[i, j] = augmented[maxRow, j];
                        augmented[maxRow, j] = temp;
                    }
                }
                
                // 消元
                for (int k = i + 1; k < n; k++)
                {
                    if (Math.Abs(augmented[i, i]) < 1e-10) continue;
                    
                    double factor = augmented[k, i] / augmented[i, i];
                    for (int j = i; j <= n; j++)
                    {
                        augmented[k, j] -= factor * augmented[i, j];
                    }
                }
            }
            
            // 回代求解
            var x = new double[n];
            for (int i = n - 1; i >= 0; i--)
            {
                x[i] = augmented[i, n];
                for (int j = i + 1; j < n; j++)
                {
                    x[i] -= augmented[i, j] * x[j];
                }
                if (Math.Abs(augmented[i, i]) < 1e-10) return null;
                x[i] /= augmented[i, i];
            }
            
            return x;
        }

        private static bool InvertMatrix3x3(double[,] matrix, double[,] inverse)
        {
            try
            {
                double det = matrix[0, 0] * (matrix[1, 1] * matrix[2, 2] - matrix[1, 2] * matrix[2, 1]) -
                           matrix[0, 1] * (matrix[1, 0] * matrix[2, 2] - matrix[1, 2] * matrix[2, 0]) +
                           matrix[0, 2] * (matrix[1, 0] * matrix[2, 1] - matrix[1, 1] * matrix[2, 0]);
                
                if (Math.Abs(det) < 1e-10) return false;
                
                inverse[0, 0] = (matrix[1, 1] * matrix[2, 2] - matrix[1, 2] * matrix[2, 1]) / det;
                inverse[0, 1] = (matrix[0, 2] * matrix[2, 1] - matrix[0, 1] * matrix[2, 2]) / det;
                inverse[0, 2] = (matrix[0, 1] * matrix[1, 2] - matrix[0, 2] * matrix[1, 1]) / det;
                inverse[1, 0] = (matrix[1, 2] * matrix[2, 0] - matrix[1, 0] * matrix[2, 2]) / det;
                inverse[1, 1] = (matrix[0, 0] * matrix[2, 2] - matrix[0, 2] * matrix[2, 0]) / det;
                inverse[1, 2] = (matrix[0, 2] * matrix[1, 0] - matrix[0, 0] * matrix[1, 2]) / det;
                inverse[2, 0] = (matrix[1, 0] * matrix[2, 1] - matrix[1, 1] * matrix[2, 0]) / det;
                inverse[2, 1] = (matrix[0, 1] * matrix[2, 0] - matrix[0, 0] * matrix[2, 1]) / det;
                inverse[2, 2] = (matrix[0, 0] * matrix[1, 1] - matrix[0, 1] * matrix[1, 0]) / det;
                
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[VisionLite] 3x3矩阵求逆失败: {ex.Message}");
                return false;
            }
        }


        #endregion
    }
}