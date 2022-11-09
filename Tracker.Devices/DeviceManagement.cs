using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Tracker.Devices
{
    class DeviceManagement:Common.ICommon
    {
        private List<Device> m_Devices;

        public List<Device> Devices
        {
            get { return m_Devices; }
            set { m_Devices = value; }
        }



        public DeviceManagement()
        {

        }

        public void Add()
        {
            DataBase.DataBase.Create_Device();
        }

        public void Remove()
        {
            DataBase.DataBase.Remove_Device();
        }

        public object Information(int p_id)
        {
            foreach (var item in Devices)
            {
                if (p_id == item.ID)
                {
                    return item;
                }
            }
            return null;
        }

     
    }
}
