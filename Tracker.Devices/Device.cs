using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Tracker.Devices
{
    public class Device
    {
        #region Pramters
        private int m_id;

        public int ID
        {
            get { return m_id; }
            set { m_id = value; }
        }

        private bool m_status;
        public bool Status
        {
            get { return m_status; }
            set { m_status = value; }
        }

        private string m_owner;
        public string Owner
        {
            get { return m_owner; }
            set { m_owner = value; }
        }

        #endregion

        public Device(int p_id, bool p_status, string p_owner)
        {
            ID = p_id;
            Status = p_status;
            Owner = p_owner;
        }
    }
}
