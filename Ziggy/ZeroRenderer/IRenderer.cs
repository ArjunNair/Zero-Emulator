using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ZeroRenderer
{
    public interface IRenderer
    {
        void Shutdown();
        void SetSpeccyScreenSize(int width, int height);
        void SetSize(int width, int height);
        bool Init(int width, int height, bool is16bit = false);
        void Paint();
    }
}
