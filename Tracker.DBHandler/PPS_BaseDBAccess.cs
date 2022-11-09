using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace Tracker.DBHandler
{
 #region CarsDB Access Base classes

  public class TPPSdbConnectionInfo
  {
    public string ServerName { get; set; }
    public string DBName { get; set; }
    public string UserLogin { get; set; }
    public string UserPwd { get; set; }
    public bool IsAsync { get; set; }

    public TPPSdbConnectionInfo()
    { }

    public TPPSdbConnectionInfo(string DBName, string ServerName) :
      this(DBName, ServerName, null, null, false)
    { }

    public TPPSdbConnectionInfo(string DBName, string ServerName, string UserLogin, string UserPwd, bool IsAsync)
    {
      this.DBName = DBName;
      this.ServerName = ServerName;
      this.UserLogin = UserLogin;
      this.UserPwd = UserPwd;
      this.IsAsync = IsAsync;
    }
  }

  public class TPPSdbConnection
  {
    public static TPPSdbConnection DefaultConnection;
    /// <summary>
    /// variable that defines if the conenction is opened to the SQL server
    /// </summary>
    private bool FisConnected = false;

    /// <summary>
    /// variable that defines the type of connection (NT or SQL)
    /// </summary>
    private bool FisNTCnt = false;

    /// <summary>
    /// Globalisation used to convert Decimal values to proper SQL strings
    /// </summary>
    private static System.Globalization.CultureInfo _ci = new System.Globalization.CultureInfo("en-US");

    public static TPPSdbConnection NewConnection(TPPSdbConnection cnt)
    { 
      TPPSdbConnection newCnt = null;
      if ((cnt != null) && (cnt.isConnected))
      {
        if (cnt.isNTConnection)
          newCnt = new TPPSdbConnection(cnt.SeverName, cnt.DBName, true, true);
        else
          newCnt = new TPPSdbConnection(cnt.SeverName, cnt.DBName, cnt.UserLogin, cnt.UserPwd, true);
      }
      return newCnt;
    }
    
    public String GetServerVersion()
    {
      String res = String.Empty;
      SqlDataReader dr = OpenSQL("sp_MSgetversion");

      if (dr != null) 
      {
        if (dr.Read())
          res = TPPSdbConnection.GetString(dr, 0);
        dr.Close();
        dr = null;
      }
      return res;
    }

    /// <summary>
    /// Function that determines if the SQL connection is on a SQL2005 server or not.
    /// This is typically used to activate 2005 specific functions in the application
    /// </summary>
    /// <returns></returns>
    public bool isSQL2005()
    {
      String res = GetServerVersion();
      int ver;
      bool _isSQL2005 = false;
      if (int.TryParse(res.Remove(res.IndexOf('.')), out ver))
        _isSQL2005 = (ver > 8);
      return _isSQL2005;
    }

    public static bool ContainsWhere(string qry)
    {
      bool res = false;
      if (qry != null)
      {
        while (qry.IndexOf('(') > -1)
        {

          int p = qry.IndexOf('(');
          int cnt = 1;
          int ndx = 1;
          while (cnt > 0)
          {
            if (qry[p + ndx] == '(')
              cnt++;
            else
              if (qry[p + ndx] == ')')
                cnt--;
            ndx++;

          }
          qry = qry.Remove(p, ndx);

        }

        if (qry.ToUpper().Contains("WHERE "))
          res = true;
      }
      return res;
    }
    
    public static System.Globalization.CultureInfo ci
    {
      get
      {
        return _ci;
      }
    }
    /// <summary>
    /// Global SQL connection
    /// </summary>
    private SqlConnection cnt = null;
    /// <summary>
    /// Global SQL Command used in OpenSQL and ExecSQL
    /// </summary>
    private SqlCommand cmd = null;
    private SqlTransaction transaction = null;

    /// <summary>
    /// Name of Database on which the Query are run
    /// </summary>
    public string DBName = "CARSGLOB";
    public string SeverName = "SQL2005";
    public string UserLogin = String.Empty;
    public string UserPwd = String.Empty;

    /// <summary>
    /// Determines if the SQL connection is active
    /// </summary>
    public bool isConnected
    {
      get
      {
        return FisConnected;
      }
    }

    /// <summary>
    /// Determines if the NT connection is NT or SQL based
    /// </summary>
    public bool isNTConnection
    {
      get
      {
        return FisNTCnt;
      }
    }

    /// <summary>
    /// Determines the command time out for the SQL connection
    /// </summary>
    public int TimeOut
    {
      get
      {
        return cmd.CommandTimeout;
      }
      set 
      {
        if (cmd != null)
          if (cmd.CommandTimeout != value)
            cmd.CommandTimeout = value;
      }
    }

    
    /// <summary>
    /// Function that closes the SQL connection (if opened) 
    /// and deallocates all SQL resources
    /// </summary>
    /// <returns>true if the close was successful</returns>
    public bool CloseSQL()
    {
      bool retVal = false;
      if (FisConnected)
      {
        try
        {
          cnt.Close();
          cnt.Dispose();
          cmd.Dispose();
          retVal = true;
        }
        catch (Exception exc)
        {
          //PPSLib.PPS_Log.LogText(null, exc.Message, 0, TraceLevel.Error);
        }
        finally
        {
          FisConnected = false;
          cnt = null;
          cmd = null;
        }
      }
      return retVal;
    }

    public bool OpenConnection(TPPSdbConnectionInfo connInfo)
    {
      return connInfo.UserLogin == null && connInfo.UserPwd == null ?  OpenConnection(connInfo.ServerName, connInfo.DBName) :
                                                                       OpenConnection(connInfo.ServerName, connInfo.DBName, connInfo.UserLogin, connInfo.UserPwd, connInfo.IsAsync);
    }

    public bool OpenConnection(string Srv, string dbName)
    {
      return OpenConnection(Srv, dbName, false);
    }
    
    public bool OpenConnection(string Srv, string dbName, string login, string pwd)
    {
      return OpenConnection(Srv, dbName, "", "", false);
    }

    public bool OpenConnection(string Srv, string dbName, bool asynch)
    {
      return OpenConnection(Srv, dbName, "", "", asynch);
    }

    public bool OpenConnection(string Srv, string dbName, string login, string pwd, bool asynch)
    {
      if (!FisConnected)
      {
        FisNTCnt = false;
        string DefaultSQLCntNoWind = "Password={3};" +
                                     "Persist Security Info=True;" +
                                     "User ID={2};Initial Catalog={1};" +
                                     "Data Source={0}";
        if (asynch)
          DefaultSQLCntNoWind = "Password={3};" +
                                "Persist Security Info=True;" +
                                "Asynchronous Processing=true;" +
                                "User ID={2};Initial Catalog={1};" +
                                "Data Source={0}";

        if (login == String.Empty)
        {
            DefaultSQLCntNoWind = "Asynchronous Processing=true;Data Source={0};Initial Catalog={1};Integrated Security=True";
        }

        DBName = dbName;
        SeverName = Srv;
        UserLogin = login;
        UserPwd = pwd;

        DefaultSQLCntNoWind = String.Format(DefaultSQLCntNoWind, SeverName, DBName, login, pwd);
        try
        {
          cnt = new SqlConnection(DefaultSQLCntNoWind);
          cnt.Open();
          
          cmd = new SqlCommand("SET NOCOUNT ON", cnt);
          cmd.ExecuteNonQuery();
          
          FisConnected = true;
        }
        catch (Exception exc)
        {
          //PPSLib.PPS_Log.LogText(null, exc.Message, 0, TraceLevel.Error);
        }
      }
      return FisConnected;
    }

    public bool OpenConnection(string Srv, string dbName, string login, string pwd, bool asynch, bool useNT)
    {
      if (!FisConnected)
      {
        string DefaultSQLCntNoWind = "Password={3};" +
                                     "Persist Security Info=True;" +
                                     "User ID={2};Initial Catalog={1};" +
                                     "Data Source={0}";
        if (asynch)
          DefaultSQLCntNoWind = "Password={3};" +
                                "Persist Security Info=True;" +
                                "Asynchronous Processing=true;" +
                                "User ID={2};Initial Catalog={1};" +
                                "Data Source={0}";
        DBName = dbName;
        SeverName = Srv;
        FisNTCnt = false;
        UserLogin = login;
        UserPwd = pwd;
        DefaultSQLCntNoWind = String.Format(DefaultSQLCntNoWind, SeverName, DBName, login, pwd);
        try
        {
          cnt = new SqlConnection(DefaultSQLCntNoWind);
          cnt.Open();
          
          cmd = new SqlCommand("SET NOCOUNT ON", cnt);
          cmd.ExecuteNonQuery();
          
          FisConnected = true;
        }
        catch (Exception exc)
        {
          //PPSLib.PPS_Log.LogText(null, exc.Message, 0, TraceLevel.Error);
        }
      }
      return FisConnected;
    }

    public bool BeginTransaction()
    {
      bool retVal = false;

      if (isConnected && cmd != null)
      {
        transaction = cnt.BeginTransaction("Start Transaction");
        cmd.Transaction = transaction;
        retVal = true;
      }

      return retVal;
    }

    public bool EndTransaction()
    {
      bool retVal = false;
        
      if (isConnected && cmd != null && transaction != null)
      {
        transaction.Commit();
        cmd.Transaction = null;
        transaction = null;
        retVal = true;
      }

      return retVal;
    }

    public bool RollBackTransaction()
    {
      bool retVal = false;

      if (isConnected && cmd != null && transaction != null)
      {
        try
        {
          transaction.Rollback();
          retVal = true;
        }
        finally
        {
          transaction = null;
          cmd.Transaction = null;
        }        
      }

      return retVal;
    }

    public bool OpenConnection(string Srv, string dbName, bool asynch, bool useNT)
    {
      if (!useNT)
        return OpenConnection(Srv, dbName, "saPPS", "PPSPPSMaster", asynch);
      else
        if (!FisConnected)
        {
          FisNTCnt = true;

          string DefaultSQLCntNoWind = "Integrated Security=SSPI;" +
                                       "Persist Security Info=False;" +
                                       "Initial Catalog={1};" +
                                       "Data Source={0}";
          if (asynch)
            DefaultSQLCntNoWind = "Integrated Security=SSPI;" +
                                  "Persist Security Info=False;" +
                                  "Asynchronous Processing=true;" +
                                  "Initial Catalog={1};" +
                                  "Data Source={0}";
          DBName = dbName;
          SeverName = Srv;
          DefaultSQLCntNoWind = String.Format(DefaultSQLCntNoWind, SeverName, DBName);
          try
          {
            cnt = new SqlConnection(DefaultSQLCntNoWind);
            cnt.Open();

            cmd = new SqlCommand("SET NOCOUNT ON", cnt);
            cmd.ExecuteNonQuery();

            FisConnected = true;
          }
          catch (Exception exc)
          {
            //PPSLib.PPS_Log.LogText(null, exc.Message, 0, TraceLevel.Error);
          }
        }
      return FisConnected;
    }
    
    public TPPSdbConnection(string SrvName, string dbName)
    {
      OpenConnection(SrvName, dbName);
    }

    public TPPSdbConnection(string SrvName, string dbName, bool async)
    {
      OpenConnection(SrvName, dbName, async);
    }

    public TPPSdbConnection(string SrvName, string dbName, bool async, bool useNT)
    {
      OpenConnection(SrvName, dbName, async, useNT);
    }

    public TPPSdbConnection(string SrvName, string dbName, string login, string pwd, bool async)
    {
      OpenConnection(SrvName, dbName, login, pwd, async);
    }

    public TPPSdbConnection(TPPSdbConnectionInfo connInfo)
    {
      OpenConnection(connInfo);
    }

    public bool ExecSQLAsynch(string Query, AsyncCallback cb)
    {
      if (isConnected)
      {
        //PPSLib.PPS_Log.LogText(null, Query, 0, TraceLevel.Verbose);
        //changeDB
        if (cnt.Database != DBName)
          cnt.ChangeDatabase(DBName);
        
        cmd.CommandText = Query;
        AsyncCallback callback = new AsyncCallback(cb);
        try
        {
          cmd.BeginExecuteNonQuery(callback, cmd);
        }

        catch (Exception exc)
        {
          //MessageBox.Show("Exec Async Error : " + exc.Message);
          //PPSLib.PPS_Log.LogText(null, exc.Message, 0, TraceLevel.Error);
        }
        return true;
      }
      else
        return false;
    }

    public bool OpenSQLAsynch(string Query, AsyncCallback cb)
    {
      if (isConnected)
      {
        //PPSLib.PPS_Log.LogText(null, Query, 0, TraceLevel.Verbose);
        //changeDB
        if (cnt.Database != DBName)
          cnt.ChangeDatabase(DBName);

        cmd.CommandText = Query;
        AsyncCallback callback = new AsyncCallback(cb);
        try
        {
          cmd.BeginExecuteReader(callback, cmd);
        }

        catch (Exception exc)
        {
          //PPSLib.PPS_Log.LogText(null, exc.Message, 0, TraceLevel.Error);
        }
        return true;

      }
      else
        return false;
    }

    public XmlReader OpenXML(string Query, object[] argv)
    {
      return OpenXML(string.Format(Query, argv));
    }

    public XmlReader OpenXML(string Query)
    {
      if (isConnected)
      {
        //PPSLib.PPS_Log.LogText(null, Query, 0, TraceLevel.Verbose);
        //changeDB
        if (cnt.Database != DBName)
          cnt.ChangeDatabase(DBName);

        cmd.CommandText = Query;
        return cmd.ExecuteXmlReader();
      }
      else
        return null;
    }

    /// <summary>
    /// Execute a SQL statement that returns a dataset
    /// </summary>
    /// <param name="Query">SQL statement to execute</param>
    /// <returns>Dataset returned by the statement</returns>
    public SqlDataReader OpenSQL(string Query)
    {
      SqlDataReader ds = null;
      try
      {
        ds = OpenSQL(Query, false);
      }
      catch (Exception exc)
      {
        //PPSLib.PPS_Log.LogText(null, exc.Message, 0, TraceLevel.Error);
        throw exc;
      }
      return ds;
    }

    /// <summary>
    /// Execute a SQL statement that returns a dataset
    /// </summary>
    /// <param name="Query">SQL statement format string to execute</param>
    /// <param name="argv">Parameters</param>
    /// <returns>Dataset returned by the statement</returns>
    public SqlDataReader OpenSQL(string Query, params object[] argv)
    {
      SqlDataReader res = OpenSQL(string.Format(Query, argv));
      return res;
    }

    /// <summary>
    /// Execute a SQL statement that returns no dataset
    /// </summary>
    /// <param name="Query">SQL statement format string to execute</param>
    /// <param name="argv">Parameters</param>
    /// <returns>True if the query was executed properly</returns>
    public bool ExecSQL(string Query, params object[] argv)
    {
      bool res = ExecSQL(string.Format(Query, argv));
      return res;
    }

    /// <summary>
    /// Execute a SQL statement that returns no dataset
    /// </summary>
    /// <param name="Query">SQL statement to execute</param>
    /// <returns></returns>
    public bool ExecSQL(string Query)
    {
      bool res = false;
      try
      {
        OpenSQL(Query, true);
        res = true;
      }
      catch (Exception exc)
      {
        //PPSLib.PPS_Log.LogText(null, exc.Message, 0, TraceLevel.Error);
        throw exc;
      }
      return res;
    }

    /// <summary>
    /// SQL Access function that returns a Data Reader or not depending on noReturn
    /// </summary>
    /// <param name="Query">SQL statement to execute</param>
    /// <param name="noReturn">true if the query returns no dataset</param>
    /// <returns>null or the dataset</returns>
    public SqlDataReader OpenSQL(string Query, bool noReturn)
    {
      if (isConnected)
      {
        //PPSLib.PPS_Log.LogText(null, Query, 0, TraceLevel.Verbose);

        //changeDB
        switch (cnt.State)
        {
          case System.Data.ConnectionState.Broken:
          case System.Data.ConnectionState.Closed:
            cnt.Open();
            break;
        }                
        
        if (cnt.Database != DBName)
          cnt.ChangeDatabase(DBName);

        cmd.CommandText = Query;
        if (noReturn)
        {
          cmd.ExecuteNonQuery();
          return null;
        }
        else
          return cmd.ExecuteReader();
      }
      else
        return null;
    }

    public SqlParameter AddParameter(String parameterName, String typeName, DataTable parameterData)
    {
      if (!parameterName.StartsWith("@"))
        parameterName = "@" + parameterName;

      SqlParameter param = cmd.Parameters.AddWithValue(parameterName, parameterData);
      param.SqlDbType = SqlDbType.Structured;
      param.TypeName = typeName;
      return param; 
    }

    public void RemoveParameter(SqlParameter param)
    {
      if (cmd.Parameters.Count > 0)
        cmd.Parameters.Remove(param);
     
    }

    public void ClearAllParmaters()
    {
      if (cmd.Parameters.Count > 0)
        cmd.Parameters.Clear();
    }

    public Boolean HasParameters()
    {
      return cmd.Parameters.Count > 0;
    }

    public SqlDataReader OpenSQL(string Query, SqlParameter parameter, bool addParameter, bool noReturn)
    {
      if (isConnected)
      {
        //PPSLib.PPS_Log.LogText(null, Query, 0, TraceLevel.Verbose);

        //changeDB
        switch (cnt.State)
        {
          case System.Data.ConnectionState.Broken:
          case System.Data.ConnectionState.Closed:
            cnt.Open();
            break;
        }

        if (cnt.Database != DBName)
          cnt.ChangeDatabase(DBName);

        cmd.CommandType = CommandType.StoredProcedure;

        if (addParameter)
          cmd.Parameters.Add(parameter);

        cmd.CommandText = Query;

        if (noReturn)
        {
          cmd.ExecuteNonQuery();
          return null;
        }
        else
          return cmd.ExecuteReader();
      }
      else
        return null;
    }

    /// <summary>
    /// Fetch a string value field from the data set, returning field ndx, with or without trim
    /// </summary>
    /// <param name="res">Result set</param>
    /// <param name="ndx">Index of the field to retreive</param>
    /// <param name="withTrunc">Trim or not the field value</param>
    /// <returns>String value of the field</returns>
    public static string GetString(SqlDataReader res, int ndx, bool withTrunc)
    {
      String Res = null;
      if (!res.IsDBNull(ndx))
      {
        Res = res.GetString(ndx);
        if (withTrunc)
          Res = Res.Trim();
      }
      return (Res);
    }

    /// <summary>
    /// Fetch a string value field from the data set, returning field ndx, trimmed
    /// </summary>
    /// <param name="res">Result set</param>
    /// <param name="ndx">Index of the field to retreive</param>
    /// <returns>String value of the field, trimmed</returns>
    public static string GetString(SqlDataReader res, int ndx)
    {
      return (GetString(res, ndx, true));
    }

    /// <summary>
    /// Fetch the string value of fldName field from the data set, trimmed or not
    /// </summary>
    /// <param name="res">Result set</param>
    /// <param name="fldName">Field name</param>
    /// <param name="withTrunc">Trim or not the field value</param>
    /// <returns>String value of the field</returns>
    public static string GetString(SqlDataReader res, string fldName, bool withTrunc)
    {
      int ndx = res.GetOrdinal(fldName);
      return GetString(res, ndx, withTrunc);
    }


    /// <summary>
    /// Fetch the string value of fldName field from the data set, trimmed
    /// </summary>
    /// <param name="res">Result set</param>
    /// <param name="fldName">Field name</param>
   /// <returns>String value of the field</returns>
    public static string GetString(SqlDataReader res, string fldName)
    {
      return (GetString(res, fldName, true));
    }

    /// <summary>
    /// returns the value of a bool field(must be cast as INT in the SQL statement)
    /// </summary>
    /// <param name="res">Result set</param>
    /// <param name="fldName">Field name</param>
    /// <returns>bool value of the field</returns>
    public static bool? GetBool(SqlDataReader res, string fldName)
    {
      bool? Res = null;
      int ndx = res.GetOrdinal(fldName);
      if (!res.IsDBNull(ndx))
        Res = (res.GetInt32(ndx) != 0);

      return (Res);
    }

    //Function that returns the value of a decimal (udFix, money, ...) field
    public static Decimal? GetDecimal(SqlDataReader res, string fldName)
    {
      Decimal? Res = null;
      int ndx = res.GetOrdinal(fldName);
      if (!res.IsDBNull(ndx))
        Res = res.GetDecimal(ndx);

      return (Res);
    }

    //Function that returns the value of a double (udFix, money, ...) field
    public static Double? GetDouble(SqlDataReader res, string fldName)
    {
      Double? Res = null;
      int ndx = res.GetOrdinal(fldName);
      if (!res.IsDBNull(ndx))
        Res = res.GetDouble(ndx);

      return (Res);
    }

    //Function that returns the value of a INT field
    public static int? GetInt32(SqlDataReader res, string fldName)
    {
      int? Res = null;
      int ndx = res.GetOrdinal(fldName);
      if (!res.IsDBNull(ndx))
        Res = res.GetInt32(ndx);

      return (Res);
    }
    //Function that returns the value of a INT field
    public static Int64? GetInt64(SqlDataReader res, string fldName)
    {
      Int64? Res = null;
      int ndx = res.GetOrdinal(fldName);
      if (!res.IsDBNull(ndx))
        Res = res.GetInt64(ndx);
      return (Res);
    }

    //Function that returns the value of a Bit field
    public static bool GetBit(SqlDataReader res, string fldName)
    {
      bool Res = false;
      int ndx = res.GetOrdinal(fldName);
      if (!res.IsDBNull(ndx))
        Res = res.GetSqlBoolean(ndx).IsTrue;

      return (Res);
    }

    public static int? GetInt16(SqlDataReader res, string fldName)
    {
      int? Res = null;
      int ndx = res.GetOrdinal(fldName);
      if (!res.IsDBNull(ndx))
        Res = res.GetInt16(ndx);

      return (Res);
    }

    //Function that returns the value of a INT field
    public static int? GetInt(SqlDataReader res, string fldName)
    {
      int? Res = null;
      int ndx = res.GetOrdinal(fldName);
      if (!res.IsDBNull(ndx))
      {
        if (res.GetDataTypeName(ndx) == "smallint")
          Res = res.GetInt16(ndx);
        else
          Res = res.GetInt32(ndx);
      }
      return (Res);
    }


    public static byte? GetByte(SqlDataReader res, string fldName)
    {
      byte? Res = null;
      int ndx = res.GetOrdinal(fldName);
      if (!res.IsDBNull(ndx))
        Res = res.GetByte(ndx);

      return (Res);
    }

    //Function that returns the value of a DateTime field
    public static DateTime? GetDateTime(SqlDataReader res, string fldName)
    {
      DateTime? Res = null;
      int ndx = res.GetOrdinal(fldName);
      if (!res.IsDBNull(ndx))
        Res = res.GetDateTime(ndx);

      return (Res);
    }

    /// <summary>
    /// Returns if a given field of the current record is NULL
    /// </summary>
    /// <param name="res">Data Reader </param>
    /// <param name="fldName">Field to test</param>
    /// <returns>true if the field is NULL</returns>
    public static bool IsDBNull(SqlDataReader res, string fldName)
    {
      int ndx = res.GetOrdinal(fldName);
      return (res.IsDBNull(ndx));
    }

    /// <summary>
    /// Generates a string of nb NULL separated by commas
    /// </summary>
    /// <param name="nb">Number of NULL to generate</param>
    /// <param name="withComma">if true a comma is added to the string (to build SQL statement)</param>
    /// <returns></returns>
    public static string SQLNULLParams(int nb, bool withComma)
    {
      string Res = "";
      for (int ctr = 0; ctr < nb; ctr++)
        Res = Res + "NULL, ";

      if ((!withComma) && (nb > 0))
        Res = Res.Substring(0, Res.Length - 2);
      return Res;
    }

    /// <summary>
    /// Converts a date to a SQL string, supports NULL values, NO comma added at the end
    /// </summary>
    /// <param name="val">Date to convert</param>
    /// <param name="withComma">if true a comma is added to the string (to build SQL statement)</param>
    /// <returns>SQL formated string</returns>
    public static string SQLDateStr(DateTime? val, bool withComma)
    {
      string Res;

      if (!val.HasValue)
        Res = "NULL";
      else
        Res = "'" + val.Value.ToString("yyyyMMdd") + "'";
      if (withComma)
        Res = Res + ", ";
      return Res;
    }

    /// <summary>
    /// Converts a date to a SQL string, supports NULL values, NO comma added at the end
    /// </summary>
    /// <param name="val">Date to convert</param>
    /// <returns>SQL formated string</returns>
    public static string SQLDateStr(DateTime? val)
    {
      return SQLDateStr(val, false);
    }

    /// <summary>
    /// Converts a string to a quoted SQL String, supports NULL values, NO comma added at the end
    /// </summary>
    /// <param name="val">String to convert</param>
    /// <param name="withComma">if true a comma is added to the string (to build SQL statement</param>
    /// <returns>SQL formated string</returns>
    public static string SQLStr(string val, bool withComma)
    {
      string Res;
      if (val == null)
        Res = "NULL";
      else
      {
        val = val.Replace("'", "''");
        Res = "'" + val.Trim() + "'";
      }
      if (withComma)
        Res = Res + ", ";
      return Res;
    }

    /// <summary>
    /// Converts a string to a quoted SQL String, supports NULL values, NO comma added at the end
    /// </summary>
    /// <param name="val">String to convert</param>
    /// <returns>SQL formated string</returns>
    public static string SQLStr(string val)
    {
      return SQLStr(val, false);
    }

    /// <summary>
    /// Converts a string to a quoted SQL String, supports NULL values
    /// </summary>
    /// <param name="val">String to convert</param>
    /// <param name="withComma">if true a comma is added to the string (to build SQL statement</param>
    /// <returns>SQL formated string</returns>
    public static string UnquotedSQLStr(string val, bool withComma)
    {
      string Res;

      if (val == null)
        Res = "NULL";
      else
        Res = val.Trim();
      if (withComma)
        Res = Res + ", ";
      return Res;
    }

    /// <summary>
    /// Converts a string to a unquoted SQL String, supports NULL values
    /// </summary>
    /// <param name="val">String to convert</param>
    /// <returns>SQL formatted string</returns>
    public static string UnquotedSQLStr(string val)
    {
      return SQLStr(val, false);
    }

    /// <summary>
    /// Convert a bool value to a SQL String (true = 1, false = 0, supports null value
    /// </summary>
    /// <param name="val">Boolean value to convert</param>
    /// <param name="withComma">if true a comma is added to the string (to build SQL statement</param>
    /// <returns>SQL formated string</returns>
    public static string SQLStr(bool? val, bool withComma)
    {
      string Res = "NULL";
      if (val.HasValue)
        Res = (val.Value) ? "1" : "0";

      if (withComma)
        Res += ", ";
      return Res;
    }

    /// <summary>
    /// Convert a bool value to a SQL String (true = 1, false = 0, supports null value
    /// </summary>
    /// <param name="val">Boolean value to convert</param>
    /// <returns>SQL formated string</returns>
    public static string SQLStr(bool? val)
    {
      return SQLStr(val, false);
    }

    /// <summary>
    /// Convert a int value to a SQL String (true = 1, false = 0, supports null value
    /// </summary>
    /// <param name="val">int value to convert</param>
    /// <param name="withComma">if true a comma is added to the string (to build SQL statement</param>
    /// <returns>SQL formated string</returns>
    public static string SQLStr(int? val, bool withComma)
    {
      string Res = "NULL";
      if (val.HasValue)
        Res = val.Value.ToString();

      if (withComma)
        Res += ", ";
      return Res;
    }

    /// <summary>
    /// Convert a int value to a SQL String (true = 1, false = 0, supports null value
    /// </summary>
    /// <param name="val">Int value to convert</param>
    /// <returns>SQL formated string</returns>
    public static string SQLStr(int? val)
    {
      return SQLStr(val, false);
    }

    /// <summary>
    /// Convert a int value to a SQL String (true = 1, false = 0, supports null value
    /// </summary>
    /// <param name="val">Int64 value to convert</param>
    /// <param name="withComma">if true a comma is added to the string (to build SQL statement</param>
    /// <returns>SQL formated string</returns>
    public static string SQLStr(Int64? val, bool withComma)
    {
      string Res = "NULL";
      if (val.HasValue)
        Res = val.Value.ToString();

      if (withComma)
        Res += ", ";
      return Res;
    }

    /// <summary>
    /// Convert a int value to a SQL String (true = 1, false = 0, supports null value
    /// </summary>
    /// <param name="val">Int64 value to convert</param>
    /// <returns>SQL formated string</returns>
    public static string SQLStr(Int64? val)
    {
      return SQLStr(val, false);
    }
    /// <summary>
    /// Convert a Decimal value to a SQL String (true = 1, false = 0, supports null value
    /// </summary>
    /// <param name="val">Decimal value to convert</param>
    /// <param name="withComma">if true a comma is added to the string (to build SQL statement</param>
    /// <returns>SQL formated string</returns>
    public static string SQLStr(Decimal? val, bool withComma)
    {
      string Res = "NULL";
      if (val.HasValue)
        Res = val.Value.ToString(ci);

      if (withComma)
        Res += ", ";
      return Res;
    }

    /// <summary>
    /// Convert a Decimal value to a SQL String (true = 1, false = 0, supports null value
    /// </summary>
    /// <param name="val">Decimal value to convert</param>
    /// <returns>SQL formated string</returns>
    public static string SQLStr(Decimal? val)
    {
      return SQLStr(val, false);
    }

    /// <summary>
    /// Convert a Decimal value to a SQL String (true = 1, false = 0, supports null value
    /// </summary>
    /// <param name="val">Decimal value to convert</param>
    /// <param name="withComma">if true a comma is added to the string (to build SQL statement</param>
    /// <returns>SQL formated string</returns>
    public static string SQLStr(Double? val, bool withComma)
    {
      string Res = "NULL";
      if (val.HasValue)
        Res = val.Value.ToString(ci);

      if (withComma)
        Res += ", ";
      return Res;
    }

    /// <summary>
    /// Convert a Decimal value to a SQL String (true = 1, false = 0, supports null value
    /// </summary>
    /// <param name="val">Decimal value to convert</param>
    /// <returns>SQL formated string</returns>
    public static string SQLStr(Double? val)
    {
      return SQLStr(val, false);
    }

    /// <summary>
    /// Converts a DateTime to a SQL string, supports NULL values, NO comma added at the end
    /// </summary>
    /// <param name="val">Date to convert</param>
    /// <param name="withComma">if true a comma is added to the string (to build SQL statement)</param>
    /// <returns>SQL formated string</returns>
    public static string SQLStr(DateTime? val, bool withComma)
    {
      string Res;

      if (!val.HasValue)
        Res = "NULL";
      else
        Res = "'" + val.Value.ToString("yyyyMMdd HH:mm:ss") + "'"; //ERP ERP 01.144.979 FT 05/03/2012 change hh: to HH:
      if (withComma)
        Res = Res + ", ";
      return Res;
    }

    /// <summary>
    /// Converts a date to a SQL string, supports NULL values, NO comma added at the end
    /// </summary>
    /// <param name="val">Date to convert</param>
    /// <returns>SQL formated string</returns>
    public static string SQLStr(DateTime? val)
    {
      return SQLStr(val, false);
    }

    /// <summary>
    /// Log function, using the Debug object of System.Diagnostics
    /// Can be rerooted to a file by adding info to Web.Config
    /// </summary>
    /// <param name="mess"></param>
    public static void Log(string mess)
    { Debug.WriteLine(mess); }
  }

  
  public class TPPSdbObjectBase
  {
    public static TPPSdbConnection dbCnt = null;// new TPPSdbConnection("SQL2005", "CARSGLOB");

    /// <summary>
    /// SQL Access function that returns a Data Reader or not depending on noReturn
    /// </summary>
    /// <param name="Query">SQL statement to execute</param>
    /// <param name="noReturn">true if the query returns no dataset</param>
    /// <returns>null or the dataset</returns>
    public static SqlDataReader OpenSQL(string Query, bool noReturn)
    {
      if (dbCnt == null)
        dbCnt = TPPSdbConnection.DefaultConnection;

      return dbCnt.OpenSQL(Query, noReturn);
    }

    /// <summary>
    /// Execute a SQL statement that returns a dataset
    /// </summary>
    /// <param name="Query">SQL statement format string to execute</param>
    /// <param name="argv">Parameters</param>
    /// <returns>Dataset returned by the statement</returns>
    public static SqlDataReader OpenSQL(string Query, params object[] argv)
    {
      if (dbCnt == null)
        dbCnt = TPPSdbConnection.DefaultConnection;

      return dbCnt.OpenSQL(Query, argv);
    }

    /// <summary>
    /// Execute a SQL statement that returns a dataset
    /// </summary>
    /// <param name="Query">SQL statement format string to execute</param>
    /// <returns>Dataset returned by the statement</returns>
    public static SqlDataReader OpenSQL(string Query)
    {
      if (dbCnt == null)
        dbCnt = TPPSdbConnection.DefaultConnection;

      return dbCnt.OpenSQL(Query);
    }

    /// <summary>
    /// Execute a SQL statement that returns no dataset
    /// </summary>
    /// <param name="Query">SQL statement to execute</param>
    /// <returns></returns>
    public static bool ExecSQL(string Query)
    {
      if (dbCnt == null)
        dbCnt = TPPSdbConnection.DefaultConnection;
      
      return dbCnt.ExecSQL(Query);
    }

    /// <summary>
    /// Execute a SQL statement that returns no dataset
    /// </summary>
    /// <param name="Query">SQL statement format string to execute</param>
    /// <param name="argv">Parameters</param>
    /// <returns>True if the query was executed properly</returns>
    public static bool ExecSQL(string Query, params object[] argv)
    {
      if (dbCnt == null)
        dbCnt = TPPSdbConnection.DefaultConnection;

      return dbCnt.ExecSQL(Query, argv);
    }
    /// <summary>
    /// Fetch a string value field from the data set, returning field ndx, trimmed
    /// </summary>
    /// <param name="res">Result set</param>
    /// <param name="ndx">Index of the field to retreive</param>
    /// <returns>String value of the field, trimmed</returns>
    public static string GetString(SqlDataReader res, int ndx)
    {
      return (TPPSdbConnection.GetString(res, ndx));
    }

    /// <summary>
    /// Fetch the string value of fldName field from the data set, trimmed or not
    /// </summary>
    /// <param name="res">Result set</param>
    /// <param name="fldName">Field name</param>
    /// <param name="withTrunc">Trim or not the field value</param>
    /// <returns>String value of the field</returns>
    public static string GetString(SqlDataReader res, string fldName, bool withTrunc)
    {
      return (TPPSdbConnection.GetString(res, fldName, withTrunc));
    }

    /// <summary>
    /// Fetch the string value of fldName field from the data set, trimmed
    /// </summary>
    /// <param name="res">Result set</param>
    /// <param name="fldName">Field name</param>
    /// <returns>String value of the field</returns>
    public static string GetString(SqlDataReader res, string fldName)
    {
      return (TPPSdbConnection.GetString(res, fldName));
    }

    /// <summary>
    /// returns the value of a bool field(must be cast as INT in the SQL statement) 0 = false
    /// </summary>
    /// <param name="res">Result set</param>
    /// <param name="fldName">Field name</param>
    /// <returns>bool value of the field</returns>
    public static bool? GetBool(SqlDataReader res, string fldName)
    {
      return (TPPSdbConnection.GetBool(res, fldName));
    }

    /// <summary>
    /// returns the value of a decimal field
    /// </summary>
    /// <param name="res">Result set</param>
    /// <param name="fldName">Field name</param>
    /// <returns>Decimal value of the field</returns>
    public static Decimal? GetDecimal(SqlDataReader res, string fldName)
    {
      return (TPPSdbConnection.GetDecimal(res, fldName));
    }

    /// <summary>
    /// returns the value of a decimal field
    /// </summary>
    /// <param name="res">Result set</param>
    /// <param name="fldName">Field name</param>
    /// <returns>Decimal value of the field</returns>
    public static Double? GetDouble(SqlDataReader res, string fldName)
    {
      return (TPPSdbConnection.GetDouble(res, fldName));
    }

    /// <summary>
    /// returns the value of a Int32 field
    /// </summary>
    /// <param name="res">Result set</param>
    /// <param name="fldName">Field name</param>
    /// <returns>Int32 value of the field</returns>
    public static int? GetInt32(SqlDataReader res, string fldName)
    {
      return (TPPSdbConnection.GetInt32(res, fldName));
    }

    /// <summary>
    /// returns the value of a Int32 field
    /// </summary>
    /// <param name="res">Result set</param>
    /// <param name="fldName">Field name</param>
    /// <returns>Int32 value of the field</returns>
    public static bool GetBit(SqlDataReader res, string fldName)
    {
      return (TPPSdbConnection.GetBit(res, fldName));
    }

    /// <summary>
    /// returns the value of a Int16 (smallint) field
    /// </summary>
    /// <param name="res">Result set</param>
    /// <param name="fldName">Field name</param>
    /// <returns>Int32 value of the field</returns>
    public static int? GetInt16(SqlDataReader res, string fldName)
    {
      return (TPPSdbConnection.GetInt16(res, fldName));
    }

    /// <summary>
    /// returns the value of a Int16 (smallint) or Int32  field
    /// </summary>
    /// <param name="res">Result set</param>
    /// <param name="fldName">Field name</param>
    /// <returns>Int32 value of the field</returns>
    public static int? GetInt(SqlDataReader res, string fldName)
    {
      return (TPPSdbConnection.GetInt(res, fldName));
    }

    /// <summary>
    /// returns the value of a Int64 (BIGINT) field
    /// </summary>
    /// <param name="res">Result set</param>
    /// <param name="fldName">Field name</param>
    /// <returns>Int64 value of the field</returns>
    public static Int64? GetInt64(SqlDataReader res, string fldName)
    {
      return (TPPSdbConnection.GetInt64(res, fldName));
    }

    /// <summary>
    /// returns the value of a Byte (tinyint) field
    /// </summary>
    /// <param name="res">Result set</param>
    /// <param name="fldName">Field name</param>
    /// <returns>Byte value of the field</returns>
    public static byte? GetByte(SqlDataReader res, string fldName)
    {
      return (TPPSdbConnection.GetByte(res, fldName));
    }

    /// <summary>
    /// returns the value of a DateTime field
    /// </summary>
    /// <param name="res">Result set</param>
    /// <param name="fldName">Field name</param>
    /// <returns>DateTime value of the field</returns>
    public static DateTime? GetDateTime(SqlDataReader res, string fldName)
    {
      return (TPPSdbConnection.GetDateTime(res, fldName));
    }

    /// <summary>
    /// Returns if a given field of the current record is NULL
    /// </summary>
    /// <param name="res">Data Reader </param>
    /// <param name="fldName">Field to test</param>
    /// <returns>true if the field is NULL</returns>
    public static bool IsDBNull(SqlDataReader res, string fldName)
    {
      return (TPPSdbConnection.IsDBNull(res, fldName));
    }

    /// <summary>
    /// Generates a string of nb NULL separated by commas
    /// </summary>
    /// <param name="nb">Number of NULL to generate</param>
    /// <param name="withComma">if true a comma is added to the string (to build SQL statement)</param>
    /// <returns></returns>
    public static string SQLNULLParams(int nb, bool withComma)
    {
      return (TPPSdbConnection.SQLNULLParams(nb, withComma));
    }

    /// <summary>
    /// Converts a date to a SQL string, supports NULL values, NO comma added at the end
    /// </summary>
    /// <param name="val">Date to convert</param>
    /// <param name="withComma">if true a comma is added to the string (to build SQL statement)</param>
    /// <returns>SQL formated string</returns>
    public static string SQLDateStr(DateTime? val, bool withComma)
    {
      return (TPPSdbConnection.SQLDateStr(val, withComma));
    }

    /// <summary>
    /// Converts a date to a SQL string, supports NULL values, NO comma added at the end
    /// </summary>
    /// <param name="val">Date to convert</param>
    /// <returns>SQL formated string</returns>
    public static string SQLDateStr(DateTime? val)
    {
      return (TPPSdbConnection.SQLDateStr(val));
    }

    /// <summary>
    /// Converts a string to a quoted SQL String, supports NULL values, NO comma added at the end
    /// </summary>
    /// <param name="val">String to convert</param>
    /// <param name="withComma">if true a comma is added to the string (to build SQL statement</param>
    /// <returns>SQL formated string</returns>
    public static string SQLStr(string val, bool withComma)
    {
      return (TPPSdbConnection.SQLStr(val, withComma));
    }

    /// <summary>
    /// Converts a string to a quoted SQL String, supports NULL values, NO comma added at the end
    /// </summary>
    /// <param name="val">String to convert</param>
    /// <returns>SQL formated string</returns>
    public static string SQLStr(string val)
    {
      return (TPPSdbConnection.SQLStr(val));
    }

    /// <summary>
    /// Converts a string to a quoted SQL String, supports NULL values
    /// </summary>
    /// <param name="val">String to convert</param>
    /// <param name="withComma">if true a comma is added to the string (to build SQL statement</param>
    /// <returns>SQL formated string</returns>
    public static string UnquotedSQLStr(string val, bool withComma)
    {
      return (TPPSdbConnection.UnquotedSQLStr(val, withComma));
    }

    /// <summary>
    /// Converts a string to a unquoted SQL String, supports NULL values
    /// </summary>
    /// <param name="val">String to convert</param>
    /// <returns>SQL formatted string</returns>
    public static string UnquotedSQLStr(string val)
    {
      return (TPPSdbConnection.UnquotedSQLStr(val));
    }

    /// <summary>
    /// Convert a bool value to a SQL String (true = 1, false = 0, supports null value
    /// </summary>
    /// <param name="val">Boolean value to convert</param>
    /// <param name="withComma">if true a comma is added to the string (to build SQL statement</param>
    /// <returns>SQL formated string</returns>
    public static string SQLStr(bool? val, bool withComma)
    {
      return (TPPSdbConnection.SQLStr(val, withComma));
    }

    /// <summary>
    /// Convert a bool value to a SQL String (true = 1, false = 0, supports null value
    /// </summary>
    /// <param name="val">Boolean value to convert</param>
    /// <returns>SQL formated string</returns>
    public static string SQLStr(bool? val)
    {
      return (TPPSdbConnection.SQLStr(val));
    }

    /// <summary>
    /// Convert a int value to a SQL String (true = 1, false = 0, supports null value
    /// </summary>
    /// <param name="val">int value to convert</param>
    /// <param name="withComma">if true a comma is added to the string (to build SQL statement</param>
    /// <returns>SQL formated string</returns>
    public static string SQLStr(int? val, bool withComma)
    {
      return (TPPSdbConnection.SQLStr(val, withComma));
    }

    /// <summary>
    /// Convert a int value to a SQL String (true = 1, false = 0, supports null value
    /// </summary>
    /// <param name="val">Int value to convert</param>
    /// <returns>SQL formated string</returns>
    public static string SQLStr(int? val)
    {
      return (TPPSdbConnection.SQLStr(val));
    }

    /// <summary>
    /// Convert a int value to a SQL String (true = 1, false = 0, supports null value
    /// </summary>
    /// <param name="val">Int value to convert</param>
    /// <returns>SQL formated string</returns>
    public static string SQLStr(Int64? val)
    {
      return (TPPSdbConnection.SQLStr(val));
    }
    /// <summary>
    /// Convert a int value to a SQL String (true = 1, false = 0, supports null value
    /// </summary>
    /// <param name="val">int value to convert</param>
    /// <param name="withComma">if true a comma is added to the string (to build SQL statement</param>
    /// <returns>SQL formated string</returns>
    public static string SQLStr(Int64? val, bool withComma)
    {
      return (TPPSdbConnection.SQLStr(val, withComma));
    }

    /// <summary>
    /// Convert a Decimal value to a SQL String (true = 1, false = 0, supports null value
    /// </summary>
    /// <param name="val">Decimal value to convert</param>
    /// <param name="withComma">if true a comma is added to the string (to build SQL statement</param>
    /// <returns>SQL formated string</returns>
    public static string SQLStr(Decimal? val, bool withComma)
    {
      return (TPPSdbConnection.SQLStr(val, withComma));
    }

    /// <summary>
    /// Convert a Decimal value to a SQL String (true = 1, false = 0, supports null value
    /// </summary>
    /// <param name="val">Decimal value to convert</param>
    /// <returns>SQL formated string</returns>
    public static string SQLStr(Decimal? val)
    {
      return (TPPSdbConnection.SQLStr(val));
    }

    /// <summary>
    /// Convert a Decimal value to a SQL String (true = 1, false = 0, supports null value
    /// </summary>
    /// <param name="val">Decimal value to convert</param>
    /// <param name="withComma">if true a comma is added to the string (to build SQL statement</param>
    /// <returns>SQL formated string</returns>
    public static string SQLStr(Double? val, bool withComma)
    {
      return (TPPSdbConnection.SQLStr(val, withComma));
    }

    /// <summary>
    /// Convert a Decimal value to a SQL String (true = 1, false = 0, supports null value
    /// </summary>
    /// <param name="val">Decimal value to convert</param>
    /// <returns>SQL formated string</returns>
    public static string SQLStr(Double? val)
    {
      return (TPPSdbConnection.SQLStr(val));
    }

    /// <summary>
    /// Converts a DateTime to a SQL string, supports NULL values, NO comma added at the end
    /// </summary>
    /// <param name="val">Date to convert</param>
    /// <param name="withComma">if true a comma is added to the string (to build SQL statement)</param>
    /// <returns>SQL formated string</returns>
    public static string SQLStr(DateTime? val, bool withComma)
    {
      return (TPPSdbConnection.SQLStr(val, withComma));
    }

    /// <summary>
    /// Converts a date to a SQL string, supports NULL values, NO comma added at the end
    /// </summary>
    /// <param name="val">Date to convert</param>
    /// <returns>SQL formated string</returns>
    public static string SQLStr(DateTime? val)
    {
      return (TPPSdbConnection.SQLStr(val));
    }

    /// <summary>
    /// Fetch a string value field from the data set, returning field ndx, with or without trim
    /// </summary>
    /// <param name="res">Result set</param>
    /// <param name="ndx">Index of the field to retreive</param>
    /// <param name="withTrunc">Trim or not the field value</param>
    /// <returns>String value of the field</returns>
    public static string GetString(SqlDataReader res, int ndx, bool withTrunc)
    {
      return (TPPSdbConnection.GetString(res, ndx, withTrunc));
    }
    /// <summary>
    /// Log function, using the Debug object of System.Diagnostics
    /// Can be rerooted to a file by adding info to Web.Config
    /// </summary>
    /// <param name="mess"></param>
    public static void Log(string mess)
    { Debug.WriteLine(mess); }
  }

  
  [global::System.Serializable]
  public class PPSInvalidFieldNameException : Exception
  {
    //
    // For guidelines regarding the creation of new exception types, see
    //    http://msdn.microsoft.com/library/default.asp?url=/library/en-us/cpgenref/html/cpconerrorraisinghandlingguidelines.asp
    // and
    //    http://msdn.microsoft.com/library/default.asp?url=/library/en-us/dncscol/html/csharp07192001.asp
    //

    public PPSInvalidFieldNameException() { }
    public PPSInvalidFieldNameException(string fldName)
      : base(string.Format("Invalid Field name : {0}", fldName))
    { }
    public PPSInvalidFieldNameException(string message, Exception inner) : base(message, inner) { }
    protected PPSInvalidFieldNameException(
    System.Runtime.Serialization.SerializationInfo info,
    System.Runtime.Serialization.StreamingContext context)
      : base(info, context) { }
  }

  #endregion
}

namespace PPS_DBAccess
{
  public abstract class TPPSroDBObj : Tracker.DBHandler.TPPSdbObjectBase
    {
    /// <summary>
    /// Flag that determines if the current record is read from he database
    /// </summary>
    protected bool FisRecordRead = false;
    protected bool _IsInInitialize = false;
    /// <summary>
    /// Flag that determines if the current record is read from he database
    /// </summary>
    public bool isRecordRead
    {
      get
      {
        return FisRecordRead;
      }
    }

    public abstract void Clear();

    public abstract bool Read(SqlDataReader dr);

    public bool Initialize(object obj)
    {
      bool retVal = false;
      _IsInInitialize = true;
      try
      {
        foreach (PropertyDescriptor prop in TypeDescriptor.GetProperties(this))
        {
          PropertyDescriptor propSrc = TypeDescriptor.GetProperties(obj).Find(prop.Name, false);
          if (propSrc != null)
            if ((prop.PropertyType == propSrc.PropertyType) && (!prop.IsReadOnly))
              prop.SetValue(this, propSrc.GetValue(obj));
        }
        retVal = true;
      }
      catch (Exception exc)
      {
        //PPSLib.PPS_Log.LogText(null, exc.Message, 0, TraceLevel.Error);
      }
      finally
      {
        _IsInInitialize = false;
      }
      return retVal;
    }


    /// <summary>
    /// Set Components with data from the object
    /// The List contains the match between components and field names
    /// </summary>
    /// <param name="GUIFields">List of fields and corresponding components</param>
    /// <returns></returns>
    public bool SetGUI(SortedList<String, Component> GUIFields)
    {
      bool retVal = false;
      bool _inSetGUI = false;
      _inSetGUI = !_inSetGUI; // to remove compiler warning
      try
      {
        for (int ctr = 0; ctr < GUIFields.Count; ctr++)
        {
          Component cmp = GUIFields.Values[ctr];
          PropertyDescriptor uiFld = null;
          uiFld = TypeDescriptor.GetProperties(cmp).Find("Items", false);
          if (uiFld != null)
          {
            //ObjectCollection  coll = uiFld.GetValue(uiFld);

          }
          uiFld = TypeDescriptor.GetProperties(cmp).Find("Value", false);
          if (uiFld == null)
            uiFld = TypeDescriptor.GetProperties(cmp).Find("KeyValue", false);
          if (uiFld == null)
            uiFld = TypeDescriptor.GetProperties(cmp).Find("Text", false);
          PropertyDescriptor obj = TypeDescriptor.GetProperties(this).Find(GUIFields.Keys[ctr], false);
          try
          {
            if (uiFld != null)
            {
              uiFld.SetValue(cmp, obj.GetValue(this));
              uiFld = TypeDescriptor.GetProperties(cmp).Find("Modified", false);
              if (uiFld != null)
                uiFld.SetValue(cmp, false);
            }
          }
          catch (Exception exc)
          {
            //PPSLib.PPS_Log.LogText(null, exc.Message, 0, TraceLevel.Error);
          }
        }
        retVal = true;
      }
      finally
      {
        _inSetGUI = false;
      }
      return retVal;
    }

    public bool GetGUI(SortedList<String, Component> GUIFields)
    {
      bool retVal = false;
      bool _inGetGUI = true;
      _inGetGUI = !_inGetGUI; // to remove compiler warning
      try
      {
        for (int ctr = 0; ctr < GUIFields.Count; ctr++)
        {
          Component cmp = GUIFields.Values[ctr];
          PropertyDescriptor uiFld = null;
          uiFld = TypeDescriptor.GetProperties(cmp).Find("Items", false);
          if (uiFld != null)
          {
            //ObjectCollection  coll = uiFld.GetValue(uiFld);

          }
          uiFld = TypeDescriptor.GetProperties(cmp).Find("Value", false);
          if (uiFld == null)
            uiFld = TypeDescriptor.GetProperties(cmp).Find("KeyValue", false);
          if (uiFld == null)
            uiFld = TypeDescriptor.GetProperties(cmp).Find("Text", false);

          PropertyDescriptor obj = TypeDescriptor.GetProperties(this).Find(GUIFields.Keys[ctr], false);
          try
          {
            if (uiFld != null)
              obj.SetValue(this, uiFld.GetValue(cmp));
          }
          catch (Exception exc)
          {
            //PPSLib.PPS_Log.LogText(null, exc.Message, 0, TraceLevel.Error);
          }
        }
        retVal = true;
      }
      finally
      {
        _inGetGUI = false;
      }
      return retVal;
    }

    public bool isGUIChanged(SortedList<String, Component> GUIFields)
    {
      bool res = false;
      foreach (Component cmp in GUIFields.Values)
      {
        PropertyDescriptor uiFld = TypeDescriptor.GetProperties(cmp).Find("Modified", false);

        if ((uiFld != null) && ((bool)uiFld.GetValue(cmp)))
        {
          res = true;
          break;
        }
      }
      return res;
    }

    public bool SetValues(object obj)
    {
      bool retVal = false;
      _IsInInitialize = true;
      try
      {
        foreach (PropertyDescriptor prop in TypeDescriptor.GetProperties(this))
        {
          PropertyDescriptor propSrc = TypeDescriptor.GetProperties(obj).Find(prop.Name, false);
          if (propSrc != null)
            if ((prop.PropertyType == propSrc.PropertyType) && (!propSrc.IsReadOnly))
              propSrc.SetValue(obj, prop.GetValue(this));
        }
        retVal = true;
      }
      catch (Exception exc)
      {
        Log(exc.Message);
      }
      finally
      {
        _IsInInitialize = false;
      }
      return retVal;
    }

  }


  public abstract class TPPSrwDBObj : TPPSroDBObj
  {
    /// <summary>
    /// Flag that determines if the current record has changed since it was read
    /// </summary>
    protected bool FisChangedFromDB = false;

    protected abstract void SetMandatoryFieldList();
    public abstract bool isMandatoryField(string fieldName);
    public TPPSrwDBObj()
    {
      SetMandatoryFieldList();
    }

    /// <summary>
    /// Flag that determines if the current record has changed since it was read
    /// </summary>
    public bool isChangedFromDB
    {
      get
      {
        return FisChangedFromDB;
      }
    }

    public abstract bool Write();

    public abstract bool Delete();

    public virtual bool Insert()
      {
          return false;
      } 
  }
}
