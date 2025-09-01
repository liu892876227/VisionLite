using LaserPanelLibrary.Models;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TwinCAT.Ads;
using TwinCAT.Ads.TypeSystem;

namespace LaserPanelLibrary.Services
{
    public class AdsClientService
    {
        private TcAdsClient _client;
        private readonly ConcurrentDictionary<string, int> _handleCache = new ConcurrentDictionary<string, int>();

        private string NetID;
        private int PlcPort;

        private int _handle;
        private const string PlcLink = ""; // PLC连接变量

        public bool IsConnected => _client?.IsConnected ?? false;

        public AdsClientService(string netID = "169.254.162.172.1.1", int plcPort = 801)
        {
            try
            {
                //连接PLC
                NetID = netID;
                PlcPort = plcPort;
                _client = new TcAdsClient();
                _client.Connect(netID, PlcPort);
                //_client.Timeout = 30;
                Console.WriteLine($"[INFO] Connected to {netID} : {plcPort}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Connect failed: {ex.Message}");
            }
        }

        public bool CheckIsConnected()
        {
            try
            {
                bool value = false;
                return Read<bool>(PlcLink, ref value);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] PLC DisConnected: {ex.Message}");
                return false;
            }
        }

        private int GetHandle(string variableName)
        {
            return _handleCache.GetOrAdd(variableName, name => _client.CreateVariableHandle(name));
        }

        #region 从PLC读值
        /// <summary>
        /// 读取ADS通讯结构体
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="variableName"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public bool Read<T>(string variableName, ref T value)
        {
            try
            {
                var handle = GetHandle(variableName);
                value = (T)_client.ReadAny(handle, typeof(T));
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Read {variableName} failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 读取ADS通讯结构体一维数组
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="variableName"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public bool Read<T>(string variableName, ref T[] value)
        {
            try
            {
                var handle = GetHandle(variableName);
                value = (T[])_client.ReadAny(handle, typeof(T[]), new int[] { value.GetLength(0) });
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Read failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 读取ADS通讯结构体二维数组
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="variableName"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public bool Read<T>(string variableName, ref T[,] value)
        {
            try
            {
                var handle = GetHandle(variableName);
                T[] value0 = (T[])_client.ReadAny(handle, typeof(T[]), new int[] { value.GetLength(0) * value.GetLength(1) });
                for (int i = 0; i < value0.Length; i++)
                {
                    value[i / value.GetLength(1), i % value.GetLength(1)] = value0[i];
                }
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Read failed: {ex.Message}");
                return false;
            }
        }
        #endregion

        #region 向PLC写值
        /// <summary>
        /// 写入与ADS通讯结构体
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="variableName"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public bool Write<T>(string variableName, T value)
        {
            if (!IsConnected)
            {
                Console.WriteLine($"[ERROR] PLC not connected!");
                return false;
            }

            try
            {
                int handle = GetHandle(variableName);
                if (value != null)
                {
                    _client.WriteAny(handle, value);
                }
                //Console.WriteLine($"[INFO] Write {variableName} value : {value} to PLC successed");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Write to PLC failed {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 写入与ADS通讯结构体一维
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="variableName"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public bool Write<T>(string variableName, T[] value)
        {
            if (!IsConnected)
            {
                Console.WriteLine($"[ERROR] PLC not connected!");
                return false;
            }

            try
            {
                int handle = GetHandle(variableName);
                if (value != null)
                {
                    _client.WriteAny(handle, value);
                }
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Write to PLC failed {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 写入与ADS通讯结构体二维
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="variableName"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public bool Write<T>(string variableName, T[,] value)
        {
            if (!IsConnected)
            {
                Console.WriteLine($"[ERROR] PLC not connected!");
                return false;
            }

            try
            {
                int handle = GetHandle(variableName);
                if (value != null)
                {
                    _client.WriteAny(handle, value);
                }
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Write to PLC failed {ex.Message}");
                return false;
            }
        }
        #endregion

        public void Dispose()
        {
            foreach (var handle in _handleCache.Values)
            {
                try
                {
                    _client.DeleteVariableHandle(handle);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                    Console.WriteLine($"[INFO] Disconnected");
                }
            }

            _client.Dispose();
        }

        public bool WriteToPlc(string variableName, string valueStr, string plcType)
        {
            try
            {
                object parsedValue = PLCData.ParseValueByType(valueStr, plcType);
                Type targetType = PLCData.GetType(plcType).ClrType;

                // 如果你 PLC SDK 写入方法需要泛型，可以用 switch
                switch (Type.GetTypeCode(targetType))
                {
                    case TypeCode.Boolean:
                        Write(variableName, (bool)parsedValue);
                        var tt=typeof(int);
                        break;
                    case TypeCode.Byte:
                        Write(variableName, (byte)parsedValue);
                        break;
                    case TypeCode.SByte:
                        Write(variableName, (sbyte)parsedValue);
                        break;
                    case TypeCode.Int16:
                        Write(variableName, (short)parsedValue);
                        break;
                    case TypeCode.UInt16:
                        Write(variableName, (ushort)parsedValue);
                        break;
                    case TypeCode.Int32:
                        Write(variableName, (int)parsedValue);
                        break;
                    case TypeCode.UInt32:
                        Write(variableName, (uint)parsedValue);
                        break;
                    case TypeCode.Int64:
                        Write(variableName, (long)parsedValue);
                        break;
                    case TypeCode.UInt64:
                        Write(variableName, (ulong)parsedValue);
                        break;
                    case TypeCode.Single:
                        Write(variableName, (float)parsedValue);
                        break;
                    case TypeCode.Double:
                        Write(variableName, (double)parsedValue);
                        break;
                    case TypeCode.String:
                        Write(variableName, (string)parsedValue);
                        break;
                    default:
                        throw new NotSupportedException($"类型 {plcType} 不支持写入 PLC");
                }

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Write {variableName} To Plc failed: {ex.Message}");
                return false;
            }
        }

        public bool ReadFromPLC(string variableName, string plcType ,ref string res)
        {
            try
            {
                Type targetType = PLCData.GetType(plcType).ClrType;

                // 如果你 PLC SDK 写入方法需要泛型，可以用 switch
                switch (Type.GetTypeCode(targetType))
                {
                    case TypeCode.Boolean:
                        bool b = default(bool);
                        Read(variableName, ref b);
                        res =  b.ToString();
                        break;
                    case TypeCode.Byte:
                        byte by = default(byte);
                        Read(variableName, ref by);
                        res = by.ToString();    
                        break;
                    case TypeCode.SByte:
                        sbyte sby = default(sbyte);
                        Read(variableName, ref sby);
                        res = sby.ToString();    
                        break;
                    case TypeCode.Int16:
                        short s = default(short);
                        Read(variableName, ref s);
                        res = s.ToString();    
                        break;
                    case TypeCode.UInt16:
                        ushort us = default(ushort);
                        Read(variableName, ref us);
                        res = us.ToString();
                        break;
                    case TypeCode.Int32:
                        int i = default(int);
                        Read(variableName, ref i);
                        res = i.ToString();
                        break;
                    case TypeCode.UInt32:
                        uint ui = default(uint);
                        Read(variableName, ref ui);
                        res = ui.ToString();
                        break;
                    case TypeCode.Int64:
                        long l = default(long);
                        Read(variableName, ref l);
                        res = l.ToString();
                        break;
                    case TypeCode.UInt64:
                        ulong ul = default(ulong);
                        Read(variableName, ref ul);
                        res = ul.ToString();
                        break;
                    case TypeCode.Single:
                        float f = default(float);
                        Read(variableName, ref f);
                        res = f.ToString();
                        break;
                    case TypeCode.Double:
                        double d = default(double);
                        Read(variableName, ref d);
                        res = d.ToString();
                        break;
                    case TypeCode.String:
                        string str = default(string);
                        Read(variableName, ref str);
                        res = str;
                        break;
                    default:
                        throw new NotSupportedException($"类型 {plcType} 从 PLC 读不到");
                }

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] ReadFromPLC failed: {ex.Message}");
                return false;
            }
        }

    }
}
