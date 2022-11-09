using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Tracker.DBHandler
{
    public class SqlDBHandler
    {
        public DBHandler dbHandler = new DBHandler();

        public void Init()
        {
            dbHandler.initConnection();
        }

        public String GetSqlStatment(String statmentName)
        {
            String retVal = null;

            if (dbHandler != null && dbHandler.IsConnectedToDB())
                retVal = dbHandler.GetSqlStatment(statmentName);

            return retVal;
        }

    }
}
