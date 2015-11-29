using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UniFiler10.Utilz
{
	public class Navigator
	{
		private static Navigator _instance = null;
		private static object _instanceLocker = new object();
		public static Navigator GetInstance()
		{
			lock (_instanceLocker)
			{
				if (_instance == null) _instance = new Navigator();
				return _instance;
			}
		}
		private Navigator() { }
	}
}
