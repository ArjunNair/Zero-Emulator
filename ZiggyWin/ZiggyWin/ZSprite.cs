using System;
using Microsoft.DirectX;
using Microsoft.DirectX.Direct3D;

namespace ZeroWin
{
    public abstract class ISprite
    {
        protected Texture texture;
        protected VertexBuffer vertices;
        protected Surface surface;

        public virtual void Init(Device dxDevice, Texture tex, System.Drawing.Rectangle rect, Matrix xform, float uTiling = 1.0f, float vTiling = 1.0f)
        {
            texture = tex;
            surface = texture.GetSurfaceLevel(0);
            vertices = new VertexBuffer(typeof(CustomVertex.TransformedTextured), 4, dxDevice, 0, CustomVertex.TransformedTextured.Format, Pool.Default);
            GraphicsStream str = vertices.Lock(0, 0, 0);
            CustomVertex.TransformedTextured[] quad = new CustomVertex.TransformedTextured[4];
            
            Vector4 topRight = Vector4.Transform(new Vector4(rect.X + rect.Width, rect.Y, 0.0f, 1.0f), xform);
            Vector4 topLeft = Vector4.Transform(new Vector4(rect.X, rect.Y, 0.0f, 1.0f), xform);
            Vector4 bottomRight = Vector4.Transform(new Vector4(rect.X + rect.Width, rect.Y + rect.Height, 0.0f, 1.0f), xform);
            Vector4 bottomLeft = Vector4.Transform(new Vector4(rect.X, rect.Y + rect.Height, 0.0f, 1.0f), xform);

            Console.WriteLine(topRight);
            quad[0] = new CustomVertex.TransformedTextured(topLeft.X, topLeft.Y, topLeft.Z, 1.0f, 0.0f, 0.0f);
            quad[1] = new CustomVertex.TransformedTextured(topRight.X, topRight.Y, topRight.Z, 1.0f, uTiling, 0.0f);
            quad[2] = new CustomVertex.TransformedTextured(bottomLeft.X, bottomLeft.Y, bottomLeft.Z, 1.0f, 0, vTiling);
            quad[3] = new CustomVertex.TransformedTextured(bottomRight.X, bottomRight.Y, bottomRight.Z, 1.0f, uTiling, vTiling);

            str.Write(quad);
            vertices.Unlock();
        }
         
        public virtual void Render(Device dxDevice, TextureFilter texFilter, TextureAddress texAddressMode)
        {
            dxDevice.VertexFormat = CustomVertex.TransformedTextured.Format;
            dxDevice.SetStreamSource(0, vertices, 0);

            dxDevice.SamplerState[0].MinFilter = texFilter;
            dxDevice.SamplerState[0].MagFilter = texFilter;
            
            dxDevice.SamplerState[0].AddressU = texAddressMode;
            dxDevice.SamplerState[0].AddressV = texAddressMode;

            dxDevice.SetTexture(0, texture);
            dxDevice.DrawPrimitives(PrimitiveType.TriangleStrip, 0, 2);
        }
        
        public virtual void CopySurface(System.Drawing.Rectangle target, int[] source)
        {
            //Sometimes throws an exception when closing the window if we try to lock surface and it has been disposed.
            if (!surface.Disposed) {
                GraphicsStream gs = surface.LockRectangle(target, LockFlags.None);
                gs.Write(source);
                surface.UnlockRectangle();
            }
        }

        public Surface GetSurface()
        {
            return surface;
        }

        public virtual void Destroy()
        {
            if (surface != null)
                surface.Dispose();

            if (texture != null)
                texture.Dispose();

            if (vertices != null)
                vertices.Dispose();
        }
    }

    public class DisplaySprite: ISprite
    {
    }

    public class InterlaceSprite: ISprite
    {
        public override void Render(Device dxDevice, TextureFilter texFilter, TextureAddress texAddressMode)
        {
            dxDevice.RenderState.AlphaBlendEnable = true;
            dxDevice.RenderState.SourceBlend = Blend.SourceAlpha;
            dxDevice.RenderState.DestinationBlend = Blend.InvSourceAlpha;
            dxDevice.RenderState.BlendOperation = BlendOperation.Add;
            base.Render(dxDevice, texFilter, texAddressMode);
        }
    }
}
