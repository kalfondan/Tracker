using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Tracker.DBHandler
{
    public class DBHandler
    {
        public TPPSdbConnection cnt = null;

        /// <summary>
        /// Initialize the database connection
        /// </summary>
        public void initConnection()
        {
            String serveName = ConfigurationSettings.AppSettings["ServerName"];
            String dbName = ConfigurationSettings.AppSettings["dbName"];
            String user = ConfigurationSettings.AppSettings["UserName"];
            String pwd = ConfigurationSettings.AppSettings["Password"];

            cnt = new TPPSdbConnection(serveName, dbName, user, pwd, false);
            cnt.TimeOut = 90;
        }

        public void initConnection(string serveName, string dbName)
        {
            cnt = new TPPSdbConnection(serveName, dbName);
            cnt.TimeOut = 90;
        }

        /// <summary>
        /// initi connection with option to start transaction
        /// </summary>
        public void initConnection(Boolean withTransaction)
        {
            initConnection();

            if (withTransaction)
                cnt.BeginTransaction();
        }

        /// <summary>
        /// Return true if there is a database connection, else returns false
        /// </summary>
        public Boolean IsConnectedToDB()
        {
            Boolean res = cnt != null && cnt.isConnected;

            if (!res)
            {
                //   MessageBox.Show("לא ניתן להתחבר לבסיס הנתונים.");
            }

            return res;
        }

        public String GetSqlStatment(String statmentName)
        {
            return ConfigurationSettings.AppSettings[statmentName];
        }

        public void ExecSql(String sql, params object[] argv)
        {
            cnt.ExecSQL(sql, argv);
        }

        public bool BeginTransaction()
        {
            return cnt.BeginTransaction();
        }

        public bool EndTransaction()
        {
            return cnt.EndTransaction();
        }

        public bool RollBackTransaction()
        {
            return cnt.RollBackTransaction();
        }

        public void CloseConnection()
        {
            if (IsConnectedToDB())
            {
                cnt.CloseSQL();
                cnt = null;
            }
        }

        /// <summary>
        /// Open query and retrun DataReader
        /// </summary>
        public SqlDataReader GetDataReader(String sql, params object[] argv)
        {
            SqlDataReader rd = null;
            try
            {
                rd = cnt.OpenSQL(sql, argv);
            }
            catch (Exception exc)
            {
                CloseDataReader(rd);
                rd = null;
                ///MessageBox.Show(exc.Message);
            }

            return rd;
        }

        public SqlDataReader GetDataReader(String query, SqlParameter parameter, bool noReturn = false)
        {
            SqlDataReader rd = null;
            try
            {
                rd = cnt.OpenSQL(query, parameter, false, noReturn);
            }
            catch (Exception exc)
            {
                CloseDataReader(rd);
                rd = null;
                //MessageBox.Show(exc.Message);
            }

            return rd;
        }

        public SqlDataReader GetDataReader(String query, String parameterName, String typeName, DataTable parameterData, bool noReturn = false)
        {
            SqlDataReader rd = null;
            try
            {
                SqlParameter parameter = cnt.AddParameter(parameterName, typeName, parameterData);
                rd = GetDataReader(query, parameter, noReturn);
            }
            catch (Exception exc)
            {
                CloseDataReader(rd);
                rd = null;
                // MessageBox.Show(exc.Message);
            }

            return rd;
        }

        /// <summary>
        /// Close the given DataReader if it is not null and open
        /// </summary>
        public void CloseDataReader(SqlDataReader rd)
        {
            if (cnt.HasParameters())
                cnt.ClearAllParmaters();

            if (rd != null && !rd.IsClosed)
                rd.Close();
        }

        public void Disconect()
        {
            cnt.CloseSQL();
        }
    }
}
