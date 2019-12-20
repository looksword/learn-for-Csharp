using System;
using System.Collections.Generic;
using System.Text;
using System.Configuration;
using System.Data;
using System.Data.Common;
using System.Data.SQLite;
using MySql.Data.MySqlClient;

namespace BusinessPortal
{
    [Serializable]
    public class TransactionID
    {
        private readonly string id = Guid.NewGuid().ToString();
        internal string ID
        {
            get { return id; }
        }
    }
    
    internal static class DataAccessHelper
    {
        private static readonly IDictionary<string, DbTransaction> transactions = new SortedDictionary<string, DbTransaction>();
        private static readonly DataTable providers = DbProviderFactories.GetFactoryClasses();
        private static ConnectionStringsSection csSection = null;
        /// <summary>
        /// System.Configuration.Configuration.ConnectionStrings
        /// </summary>
        private static ConnectionStringsSection CSSection
        {
            get
            {
                if (csSection != null) return csSection;

                string privateDS = "private.db";
                if (!System.IO.File.Exists(privateDS))
                {
                    SQLiteConnection.CreateFile(privateDS);
                }
                //将当前应用程序的配置文件作为 System.Configuration.Configuration 对象打开。(应用于所有用户)
                csSection = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None).ConnectionStrings;
                foreach (ConnectionStringSettings css in csSection.ConnectionStrings)
                {
                    if (string.IsNullOrEmpty(css.ProviderName))
                    {
                        continue;
                    }
                    if (css.ProviderName == "MySql.Data.MySqlClient")
                    {
                        continue;
                    }
                    if (providers.Select("InvariantName = '" + css.ProviderName + "'").Length == 0)
                    {
                        throw new Exception("DB provider '" + css.ProviderName + "' does not exist in this computer.");
                    }
                }
                return csSection;
            }
        }

        public static void AddConnectionString(string name, string connectionString, string providerName)
        {
            ConnectionStringSettings css = new ConnectionStringSettings(name, connectionString, providerName);
            CSSection.ConnectionStrings.Add(css);
        }

        private static ConnectionStringSettings privateSqliteCSS = new ConnectionStringSettings("private", "data source=private.db", "");
        private static ConnectionStringSettings GetConnStrSetting(string settingName)
        {
            if (string.IsNullOrEmpty(settingName))
            {
                return privateSqliteCSS;
            }
            ConnectionStringSettings css = CSSection.ConnectionStrings[settingName];
            if (css == null)
            {
                throw new Exception("Can not find database connection setting by name '" + settingName + "' in the config file.");
            }
            return css;
        }

        /// <summary>
        /// 异常预处理
        /// </summary>
        /// <param name="ex"></param>
        /// <returns></returns>
        private static Exception PreprocessException(Exception ex)
        {
            //string msg = ex.Message;
            //if (msg.Contains("ORA-"))
            //{
            //    if (msg.Contains("ORA-20001"))
            //    {
            //        msg = msg.Split(new char[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)[0];
            //        //int indexOfFirstAt = msg.IndexOf("@");
            //        //int indexOfFirstAt = msg.IndexOf("NG");
            //        //if (indexOfFirstAt <= 0) return new Exception("@WIP-E023@", ex);
            //        //if (indexOfFirstAt <= 0) return new Exception("NG!System exception", ex);
            //        //msg = msg.Remove(0, indexOfFirstAt + 1);
            //        //int indexOfSecondAt = msg.IndexOf("@");
            //        //int indexOfSecondAt = msg.IndexOf("NG");
            //        //if (indexOfSecondAt <= 0) return new Exception("@WIP-E023@", ex);
            //        //if (indexOfSecondAt <= 0) return new Exception("NG!System exception", ex);
            //        //return new Exception("@" + msg, ex);
            //        msg = msg.Remove(0, 11);
            //        return new Exception(msg, ex);
            //    }
            //    //return new Exception("@WIP-E023@", ex);
            //    return new Exception("NG!System_exception", ex);
            //}
            return ex;
        }

        /// <summary>
        /// 获取 DBFactory 工厂类
        /// </summary>
        /// <param name="providerName"></param>
        /// <returns></returns>
        private static DbProviderFactory GetDbFactory(string providerName)
        {
            if (string.IsNullOrEmpty(providerName))
            {
                return SQLiteFactory.Instance;
            }
            if (providerName == "MySql.Data.MySqlClient")
            {
                return MySqlClientFactory.Instance;
            }
            return DbProviderFactories.GetFactory(providerName);
        }

        /// <summary>
        /// 开始事务
        /// </summary>
        /// <param name="dbDesc"></param>
        /// <returns></returns>
        public static TransactionID BeginTrans(string dbDesc)
        {
            ConnectionStringSettings css = GetConnStrSetting(dbDesc);
            DbProviderFactory dbFactory = GetDbFactory(css.ProviderName);
            DbConnection dbConn = dbFactory.CreateConnection();
            dbConn.ConnectionString = css.ConnectionString;
            dbConn.Open();
            TransactionID txID = new TransactionID();
            transactions.Add(txID.ID, dbConn.BeginTransaction());
            return txID;
        }

        /// <summary>
        /// 提交事务
        /// </summary>
        /// <param name="txID"></param>
        public static void CommitTrans(TransactionID txID)
        {
            if (txID == null) return;
            DbTransaction tx = null;
            lock (transactions)
            {
                tx = transactions[txID.ID];
                transactions.Remove(txID.ID);
            }
            DbConnection conn = tx.Connection;
            try { tx.Commit(); }
            finally
            {
                try { if (conn != null) conn.Close(); }
                finally
                {
                    try { if (conn != null) conn.Dispose(); }
                    catch (Exception) { }
                    try { tx.Dispose(); }
                    catch (Exception) { }
                }
            }
        }

        /// <summary>
        /// 回滚事务
        /// </summary>
        /// <param name="txID"></param>
        public static void RollbackTrans(TransactionID txID)
        {
            if (txID == null) return;
            DbTransaction tx = null;
            lock (transactions)
            {
                tx = transactions[txID.ID];
                transactions.Remove(txID.ID);
            }
            DbConnection conn = tx.Connection;
            try { tx.Rollback(); }
            finally
            {
                try { if (conn != null) conn.Close(); }
                finally
                {
                    try { if (conn != null) conn.Dispose(); }
                    catch (Exception) { }
                    try { tx.Dispose(); }
                    catch (Exception) { }
                }
            }
        }

        /// <summary>
        /// 执行存储过程
        /// </summary>
        /// <param name="dbDesc"></param>
        /// <param name="spName"></param>
        /// <param name="parameters"></param>
        /// <param name="txID"></param>
        /// <returns></returns>
        public static NameObjectDictionary ExecStoredProc(string dbDesc, string spName, NameObjectDictionary parameters, TransactionID txID)
        {
            ConnectionStringSettings css = GetConnStrSetting(dbDesc);
            DbProviderFactory dbFactory = GetDbFactory(css.ProviderName);

            DbCommand dbCmd = dbFactory.CreateCommand();
            DbCommandBuilder dbCmdBuilder = dbFactory.CreateCommandBuilder();
            DbTransaction dbTrans = null;

            Type dbCmdBuilderType = dbCmdBuilder.GetType();
            dbCmdBuilder.Dispose();
            System.Reflection.MethodInfo mi = dbCmdBuilderType.GetMethod("DeriveParameters");

            DbConnection dbConn = null;
            if (txID != null)
            {
                dbTrans = transactions[txID.ID];
                dbConn = dbTrans.Connection;
                dbCmd.Transaction = dbTrans;
            }
            else
            {
                dbConn = dbFactory.CreateConnection();
                dbConn.ConnectionString = css.ConnectionString;
                dbConn.Open();
            }
            dbCmd.Connection = dbConn;

            try
            {
                try
                {
                    dbCmd.CommandType = CommandType.StoredProcedure;
                    dbCmd.CommandText = spName;
                    if (mi != null) mi.Invoke(null, new object[] { dbCmd });

                    foreach (DbParameter param in dbCmd.Parameters)
                        if ((param.Direction == ParameterDirection.Input) || (param.Direction == ParameterDirection.InputOutput))
                        {
                            param.Value = null;
                            try { if (parameters != null) param.Value = parameters[param.ParameterName]; }
                            catch (Exception) { }
                            //if (param.Value == null)
                            //    throw new Exception("Need a value to fill parameter '" + param.ParameterName +
                            //                        "' of stored procedure '" + spName + "'.");
                        }
                    if ((parameters != null) && (parameters.OutputDataSet != null)) parameters.OutputDataSet.Dispose();

                    if (txID == null)
                    {
                        dbTrans = dbConn.BeginTransaction();
                        try
                        {
                            dbCmd.Transaction = dbTrans;
                            dbCmd.ExecuteNonQuery();
                            dbTrans.Commit();
                        }
                        catch (Exception)
                        {
                            dbTrans.Rollback();
                            throw;
                        }
                    }
                    else dbCmd.ExecuteNonQuery();

                    parameters = new NameObjectDictionary();
                    foreach (DbParameter param in dbCmd.Parameters)
                        parameters.Add(param.ParameterName, param.Value);
                    return parameters;
                }
                catch (Exception ex) { throw PreprocessException(ex); }
            }
            finally
            {
                if (txID == null)
                {
                    dbConn.Close();
                    dbConn.Dispose();
                }
                dbCmd.Dispose();
            }
        }

        /// <summary>
        /// 打开存储过程
        /// </summary>
        /// <param name="dbDesc"></param>
        /// <param name="spName"></param>
        /// <param name="parameters"></param>
        /// <param name="txID"></param>
        /// <returns></returns>
        public static NameObjectDictionary OpenStoredProc(string dbDesc, string spName, NameObjectDictionary parameters, TransactionID txID)
        {
            ConnectionStringSettings css = GetConnStrSetting(dbDesc);
            DbProviderFactory dbFactory = GetDbFactory(css.ProviderName);

            DbCommand dbCmd = dbFactory.CreateCommand();
            DbDataAdapter dbDataAdapter = dbFactory.CreateDataAdapter();
            DbCommandBuilder dbCmdBuilder = dbFactory.CreateCommandBuilder();
            DbTransaction dbTrans = null;

            Type dbCmdBuilderType = dbCmdBuilder.GetType();
            dbCmdBuilder.Dispose();
            System.Reflection.MethodInfo mi = dbCmdBuilderType.GetMethod("DeriveParameters");

            DbConnection dbConn = null;
            if (txID != null)
            {
                dbTrans = transactions[txID.ID];
                dbConn = dbTrans.Connection;
                dbCmd.Transaction = dbTrans;
            }
            else
            {
                dbConn = dbFactory.CreateConnection();
                dbConn.ConnectionString = css.ConnectionString;
                dbConn.Open();
            }
            dbCmd.Connection = dbConn;

            try
            {
                try
                {
                    dbCmd.CommandType = CommandType.StoredProcedure;
                    dbCmd.CommandText = spName;
                    if (mi != null) mi.Invoke(null, new object[] { dbCmd });

                    foreach (DbParameter param in dbCmd.Parameters)
                        if ((param.Direction == ParameterDirection.Input) || (param.Direction == ParameterDirection.InputOutput))
                        {
                            param.Value = null;
                            try { if (parameters != null) param.Value = parameters[param.ParameterName]; }
                            catch (Exception) { }
                            //if (param.Value == null)
                            //    throw new Exception("Need a value to fill parameter '" + param.ParameterName +
                            //                        "' of stored procedure '" + spName + "'.");
                        }
                    if ((parameters != null) && (parameters.OutputDataSet != null)) parameters.OutputDataSet.Dispose();

                    dbDataAdapter.SelectCommand = dbCmd;
                    parameters = new NameObjectDictionary();
                    parameters.OutputDataSet = new DataSet();

                    if (txID == null)
                    {
                        dbTrans = dbConn.BeginTransaction();
                        try
                        {
                            dbCmd.Transaction = dbTrans;
                            dbDataAdapter.Fill(parameters.OutputDataSet);
                            dbTrans.Commit();
                        }
                        catch (Exception)
                        {
                            dbTrans.Rollback();
                            throw;
                        }
                    }
                    else dbDataAdapter.Fill(parameters.OutputDataSet);

                    foreach (DbParameter param in dbCmd.Parameters)
                        parameters.Add(param.ParameterName, param.Value);
                    return parameters;
                }
                catch (Exception ex) { throw PreprocessException(ex); }
            }
            finally
            {
                if (txID == null)
                {
                    dbConn.Close();
                    dbConn.Dispose();
                }
                dbDataAdapter.Dispose();
                dbCmd.Dispose();
            }
        }

        /// <summary>
        /// 查询SQL语句
        /// </summary>
        /// <param name="dbDesc"></param>
        /// <param name="sql"></param>
        /// <param name="parameters"></param>
        /// <param name="txID"></param>
        /// <returns></returns>
        public static DataTable QuerySQL(string dbDesc, string sql, NameObjectDictionary parameters, TransactionID txID)
        {
            ConnectionStringSettings css = GetConnStrSetting(dbDesc);
            DbProviderFactory dbFactory = GetDbFactory(css.ProviderName);

            DbCommand dbCmd = dbFactory.CreateCommand();
            DbDataAdapter dbDataAdapter = dbFactory.CreateDataAdapter();

            DbConnection dbConn = null;
            if (txID != null)
            {
                DbTransaction tx = transactions[txID.ID];
                dbConn = tx.Connection;
                dbCmd.Transaction = tx;
            }
            else
            {
                dbConn = dbFactory.CreateConnection();
                dbConn.ConnectionString = css.ConnectionString;
                dbConn.Open();
            }
            dbCmd.Connection = dbConn;

            try
            {
                try
                {
                    dbCmd.CommandType = CommandType.Text;
                    dbCmd.CommandText = sql;
                    if (parameters != null)
                        foreach (string paramName in parameters.Keys)
                        {
                            DbParameter param = dbCmd.CreateParameter();
                            param.ParameterName = paramName;
                            param.Value = parameters[paramName];
                            dbCmd.Parameters.Add(param);
                        }
                    dbDataAdapter.SelectCommand = dbCmd;
                    DataTable dataTable = new DataTable();
                    dbDataAdapter.Fill(dataTable);
                    return dataTable;
                }
                catch (Exception ex) { throw PreprocessException(ex); }
            }
            finally
            {
                if (txID == null)
                {
                    dbConn.Close();
                    dbConn.Dispose();
                }
                dbDataAdapter.Dispose();
                dbCmd.Dispose();
            }
        }

        /// <summary>
        /// 执行SQL语句
        /// </summary>
        /// <param name="dbDesc"></param>
        /// <param name="sql"></param>
        /// <param name="parameters"></param>
        /// <param name="txID"></param>
        public static void ExecSQL(string dbDesc, string sql, NameObjectDictionary parameters, TransactionID txID)
        {
            ConnectionStringSettings css = GetConnStrSetting(dbDesc);
            DbProviderFactory dbFactory = GetDbFactory(css.ProviderName);

            DbCommand dbCmd = dbFactory.CreateCommand();

            DbConnection dbConn = null;
            if (txID != null)
            {
                DbTransaction tx = transactions[txID.ID];
                dbConn = tx.Connection;
                dbCmd.Transaction = tx;
            }
            else
            {
                dbConn = dbFactory.CreateConnection();
                dbConn.ConnectionString = css.ConnectionString;
                dbConn.Open();
            }
            dbCmd.Connection = dbConn;

            try
            {
                try
                {
                    dbCmd.CommandType = CommandType.Text;
                    dbCmd.CommandText = sql;
                    if (parameters != null)
                        foreach (string paramName in parameters.Keys)
                        {
                            DbParameter param = dbCmd.CreateParameter();
                            param.ParameterName = paramName;
                            param.Value = parameters[paramName];
                            dbCmd.Parameters.Add(param);
                        }
                    dbCmd.ExecuteNonQuery();
                }
                catch (Exception ex) { throw PreprocessException(ex); }
            }
            finally
            {
                if (txID == null)
                {
                    dbConn.Close();
                    dbConn.Dispose();
                }
                dbCmd.Dispose();
            }
        }

        /// <summary>
        /// 更新 返回DataTable
        /// </summary>
        /// <param name="dbDesc"></param>
        /// <param name="sql"></param>
        /// <param name="dataTable"></param>
        /// <param name="txID"></param>
        /// <returns></returns>
        public static DataTable UpdateWithCmdBuilder(string dbDesc, string sql, DataTable dataTable, TransactionID txID)
        {
            ConnectionStringSettings css = GetConnStrSetting(dbDesc);
            DbProviderFactory dbFactory = GetDbFactory(css.ProviderName);

            DbCommand dbCmd = dbFactory.CreateCommand();
            DbDataAdapter dbDataAdapter = dbFactory.CreateDataAdapter();
            DbCommandBuilder dbCmdBuilder = dbFactory.CreateCommandBuilder();

            DbConnection dbConn = null;
            if (txID != null)
            {
                DbTransaction tx = transactions[txID.ID];
                dbConn = tx.Connection;
                dbCmd.Transaction = tx;
            }
            else
            {
                dbConn = dbFactory.CreateConnection();
                dbConn.ConnectionString = css.ConnectionString;
                dbConn.Open();
            }
            dbCmd.Connection = dbConn;

            try
            {
                try
                {
                    dbCmd.CommandType = CommandType.Text;
                    dbCmd.CommandText = sql;
                    dbDataAdapter.SelectCommand = dbCmd;
                    dbCmdBuilder.DataAdapter = dbDataAdapter;
                    dbDataAdapter.ContinueUpdateOnError = true;
                    dbDataAdapter.Update(dataTable);
                    return dataTable;
                }
                catch (Exception ex) { throw PreprocessException(ex); }
            }
            finally
            {
                if (txID == null)
                {
                    dbConn.Close();
                    dbConn.Dispose();
                }
                dbDataAdapter.Dispose();
                dbCmd.Dispose();
            }
        }
    }
}
