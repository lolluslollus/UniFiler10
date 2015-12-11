using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Utilz
{
    public interface IAnimationStarter
    {
		void StartAnimation(int whichAnimation);
        void EndAnimation(int whichAnimation);
    }
}
