using System;
using System.Collections.Generic;

using Microsoft.DirectX;
using Microsoft.DirectX.Direct3D;

namespace ZeroRenderer
{
    public class DirectXRenderer: IRenderer
    {
        private Device dxDevice;
        private PresentParameters currentParams;

        private DisplaySprite displaySprite = new DisplaySprite();
        private InterlaceSprite scanlineSprite = new InterlaceSprite();

        private Rectangle screenRect;
        private Rectangle displayRect;

        void Shutdown();
        void SetSpeccyScreenSize(int width, int height);
        void SetSize(int width, int height);
        bool Init(int width, int height, bool is16bit = false);
        void Paint();
    }
}
