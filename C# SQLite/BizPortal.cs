using System;
using System.Collections.Generic;
using System.Text;
using System.Data;
using System.Data.Common;
using System.Reflection;
using System.Runtime.Remoting;
using System.Runtime.Remoting.Lifetime;
using System.Runtime.Remoting.Channels;
using System.Runtime.Remoting.Channels.Tcp;

namespace BusinessPortal
{
    [Serializable]
    public class NameObjectDictionary : SortedDictionary<string, object>
    {
        private DataSet outputDataSet = null;
        /// <summary>
        /// 输出数据集
        /// </summary>
        public DataSet OutputDataSet
        {
            get { return outputDataSet; }
            set { outputDataSet = value; }
        }
    }

    public class BizPortal : MarshalByRefObject
    {
        private DataTable bizInfoTable = new DataTable();

        private bool isRemotePortal = false;
        public bool IsRemotePortal
        {
            get { return isRemotePortal; }
            set { isRemotePortal = value; }
        }

        private string remotePortalUrl = "";
        /// <summary>
        /// 远程门户网址
        /// </summary>
        public string RemotePortalUrl
        {
            get { return remotePortalUrl; }
            set { remotePortalUrl = value; }
        }

        private BizPortal()
        {

        }

        private static readonly BizPortal instance = new BizPortal();
        /// <summary>
        /// 实例
        /// </summary>
        public static BizPortal Instance
        {
            get { return instance; }
        }

        public static void AddConnectionString(string name, string connectionString, string providerName)
        {
            DataAccessHelper.AddConnectionString(name, connectionString, providerName);
        }

        private class CustomSponsor : MarshalByRefObject, ISponsor
        {
            public TimeSpan Renewal(ILease lease)
            {
                return new TimeSpan(60000, 0, 0, 0);
            }
        }

        public void SetupTcpServer(int listeningPort)
        {
            BizPortal bizPortal = BizPortal.Instance;
            bizPortal.IsRemotePortal = false;
            bizPortal.Init();

            ChannelServices.RegisterChannel(new TcpServerChannel(listeningPort), false);
            RemotingConfiguration.CustomErrorsMode = CustomErrorsModes.Off;
            RemotingServices.Marshal((MarshalByRefObject)bizPortal, "BizPortal");
            ILease lease = (ILease)RemotingServices.GetLifetimeService((MarshalByRefObject)bizPortal);
            lease.Register(new CustomSponsor());
        }

        /// <summary>
        /// 获取远程门户
        /// </summary>
        /// <returns></returns>
        private BizPortal GetRemotePortal()
        {
            BizPortal bizPortal = null;
            bizPortal = (BizPortal)Activator.GetObject(typeof(BizPortal), RemotePortalUrl + "/BizPortal");
            if (bizPortal == null) throw new Exception("Can not get proxy for the specified remote portal.");
            return bizPortal;
        }

        public object DoBiz(string bizClass, string bizMethod, NameObjectDictionary parameters)
        {
            if (IsRemotePortal)
            {
                return GetRemotePortal().DoBiz(bizClass, bizMethod, parameters);
            }
            else
            {
                DataRow[] dataRows = bizInfoTable.Select("BizName = '" + bizClass + "." + bizMethod + "'");
                if (dataRows.Length == 0) throw new Exception("Can not find the biz by this biz name [" + bizClass + "." + bizMethod + "]");
                DataRow dataRow = dataRows[0];
                Type type = (Type)dataRow["BizClassType"];
                object target = null;
                if (!type.IsAbstract) target = Activator.CreateInstance(type);
                return type.InvokeMember(bizMethod,
                                         BindingFlags.InvokeMethod,
                                         null,
                                         target,
                                         new object[] { parameters });
            }
        }

        public DataSet DoBizAndGetData(string bizClass, string bizMethod, NameObjectDictionary parameters)
        {
            if (IsRemotePortal)
            {
                return GetRemotePortal().DoBizAndGetData(bizClass, bizMethod, parameters);
            }
            else
            {
                DataRow[] dataRows = bizInfoTable.Select("BizName = '" + bizClass + "." + bizMethod + "'");
                if (dataRows.Length == 0) throw new Exception("Can not find the biz by this biz name [" + bizClass + "." + bizMethod + "]");
                DataRow dataRow = dataRows[0];
                Type type = (Type)dataRow["BizClassType"];
                object target = null;
                if (!type.IsAbstract) target = Activator.CreateInstance(type);
                return (DataSet)type.InvokeMember(bizMethod,
                                                  BindingFlags.InvokeMethod,
                                                  null,
                                                  target,
                                                  new object[] { parameters });
            }
        }

        /// <summary>
        /// 开始事务
        /// </summary>
        /// <param name="dbDesc"></param>
        /// <returns></returns>
        public TransactionID BeginTrans(string dbDesc)
        {
            if (IsRemotePortal)
            {
                return GetRemotePortal().BeginTrans(dbDesc);
            }
            else return DataAccessHelper.BeginTrans(dbDesc);
        }

        /// <summary>
        /// 提交事务
        /// </summary>
        /// <param name="txID"></param>
        public void CommitTrans(TransactionID txID)
        {
            if (IsRemotePortal)
            {
                GetRemotePortal().CommitTrans(txID);
            }
            else DataAccessHelper.CommitTrans(txID);
        }

        /// <summary>
        /// 回滚事务
        /// </summary>
        /// <param name="txID"></param>
        public void RollbackTrans(TransactionID txID)
        {
            if (IsRemotePortal)
            {
                GetRemotePortal().RollbackTrans(txID);
            }
            else DataAccessHelper.RollbackTrans(txID);
        }

        /// <summary>
        /// 执行存储过程
        /// </summary>
        /// <param name="dbDesc"></param>
        /// <param name="spName"></param>
        /// <param name="parameters"></param>
        /// <returns></returns>
        public NameObjectDictionary ExecStoredProc(string dbDesc, string spName, NameObjectDictionary parameters)
        {
            if (IsRemotePortal)
            {
                return GetRemotePortal().ExecStoredProc(dbDesc, spName, parameters);
            }
            else return DataAccessHelper.ExecStoredProc(dbDesc, spName, parameters, null);
        }

        /// <summary>
        /// 执行存储过程
        /// </summary>
        /// <param name="dbDesc"></param>
        /// <param name="spName"></param>
        /// <param name="parameters"></param>
        /// <param name="txID"></param>
        /// <returns></returns>
        public NameObjectDictionary ExecStoredProc(string dbDesc, string spName, NameObjectDictionary parameters, TransactionID txID)
        {
            if (IsRemotePortal)
            {
                return GetRemotePortal().ExecStoredProc(dbDesc, spName, parameters, txID);
            }
            else return DataAccessHelper.ExecStoredProc(dbDesc, spName, parameters, txID);
        }

        /// <summary>
        /// 打开存储过程
        /// </summary>
        /// <param name="dbDesc"></param>
        /// <param name="spName"></param>
        /// <param name="parameters"></param>
        /// <returns></returns>
        public NameObjectDictionary OpenStoredProc(string dbDesc, string spName, NameObjectDictionary parameters)
        {
            if (IsRemotePortal)
            {
                return GetRemotePortal().OpenStoredProc(dbDesc, spName, parameters);
            }
            else return DataAccessHelper.OpenStoredProc(dbDesc, spName, parameters, null);
        }

        /// <summary>
        /// 打开存储过程
        /// </summary>
        /// <param name="dbDesc"></param>
        /// <param name="spName"></param>
        /// <param name="parameters"></param>
        /// <param name="txID"></param>
        /// <returns></returns>
        public NameObjectDictionary OpenStoredProc(string dbDesc, string spName, NameObjectDictionary parameters, TransactionID txID)
        {
            if (IsRemotePortal)
            {
                return GetRemotePortal().OpenStoredProc(dbDesc, spName, parameters, txID);
            }
            else return DataAccessHelper.OpenStoredProc(dbDesc, spName, parameters, txID);
        }

        /// <summary>
        /// 查询SQL语句
        /// </summary>
        /// <param name="dbDesc"></param>
        /// <param name="sql"></param>
        /// <param name="parameters"></param>
        /// <returns></returns>
        public DataTable QuerySQL(string dbDesc, string sql, NameObjectDictionary parameters)
        {
            if (IsRemotePortal)
            {
                return GetRemotePortal().QuerySQL(dbDesc, sql, parameters);
            }
            else return DataAccessHelper.QuerySQL(dbDesc, sql, parameters, null);
        }

        /// <summary>
        /// 查询SQL语句
        /// </summary>
        /// <param name="dbDesc"></param>
        /// <param name="sql"></param>
        /// <param name="parameters"></param>
        /// <param name="txID"></param>
        /// <returns></returns>
        public DataTable QuerySQL(string dbDesc, string sql, NameObjectDictionary parameters, TransactionID txID)
        {
            if (IsRemotePortal)
            {
                return GetRemotePortal().QuerySQL(dbDesc, sql, parameters, txID);
            }
            else return DataAccessHelper.QuerySQL(dbDesc, sql, parameters, txID);
        }

        /// <summary>
        /// 执行SQL语句
        /// </summary>
        /// <param name="dbDesc"></param>
        /// <param name="sql"></param>
        /// <param name="parameters"></param>
        public void ExecSQL(string dbDesc, string sql, NameObjectDictionary parameters)
        {
            if (IsRemotePortal)
            {
                GetRemotePortal().ExecSQL(dbDesc, sql, parameters);
            }
            else DataAccessHelper.ExecSQL(dbDesc, sql, parameters, null);
        }

        /// <summary>
        /// 执行SQL语句
        /// </summary>
        /// <param name="dbDesc"></param>
        /// <param name="sql"></param>
        /// <param name="parameters"></param>
        /// <param name="txID"></param>
        public void ExecSQL(string dbDesc, string sql, NameObjectDictionary parameters, TransactionID txID)
        {
            if (IsRemotePortal)
            {
                GetRemotePortal().ExecSQL(dbDesc, sql, parameters, txID);
            }
            else DataAccessHelper.ExecSQL(dbDesc, sql, parameters, txID);
        }

        /// <summary>
        /// 更新 返回DataTable
        /// </summary>
        /// <param name="dbDesc"></param>
        /// <param name="sql"></param>
        /// <param name="dataTable"></param>
        /// <returns></returns>
        public DataTable UpdateWithCmdBuilder(string dbDesc, string sql, DataTable dataTable)
        {
            if (IsRemotePortal)
            {
                return GetRemotePortal().UpdateWithCmdBuilder(dbDesc, sql, dataTable);
            }
            else return DataAccessHelper.UpdateWithCmdBuilder(dbDesc, sql, dataTable, null);
        }

        /// <summary>
        /// 更新 带ID 返回DataTable
        /// </summary>
        /// <param name="dbDesc"></param>
        /// <param name="sql"></param>
        /// <param name="dataTable"></param>
        /// <param name="txID"></param>
        /// <returns></returns>
        public DataTable UpdateWithCmdBuilder(string dbDesc, string sql, DataTable dataTable, TransactionID txID)
        {
            if (IsRemotePortal)
            {
                return GetRemotePortal().UpdateWithCmdBuilder(dbDesc, sql, dataTable, txID);
            }
            else return DataAccessHelper.UpdateWithCmdBuilder(dbDesc, sql, dataTable, txID);
        }

        public void Init()
        {
            if (IsRemotePortal)
            {
                //System.Runtime.Remoting
            }
            else
            {
                bizInfoTable.Clear();
                bizInfoTable.Columns.Clear();
                bizInfoTable.Columns.Add("BizName", typeof(string));
                bizInfoTable.Columns.Add("BizClassType", typeof(Type));

                Assembly asm = Assembly.GetExecutingAssembly();
                string scanAsmPath = System.IO.Path.GetDirectoryName(asm.Location);
                string[] fileList = System.IO.Directory.GetFiles(scanAsmPath, "*.dll");
                foreach (string filePath in fileList)
                {
                    try
                    {
                        asm = Assembly.LoadFile(filePath);
                        foreach (Type type in asm.GetTypes())
                            foreach (MethodInfo bizMethod in type.GetMethods())
                            {
                                DataRow dataRow = bizInfoTable.NewRow();
                                dataRow["BizName"] = type.Name + "." + bizMethod.Name;
                                dataRow["BizClassType"] = type;
                                bizInfoTable.Rows.Add(dataRow);
                            }
                    }
                    catch (Exception) { }
                }
            }
        }
    }
}
